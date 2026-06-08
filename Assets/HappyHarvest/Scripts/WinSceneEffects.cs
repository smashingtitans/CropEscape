using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyHarvest
{
    /// <summary>
    /// Handles ambient effects in WinScene:
    ///   - Rain particle system (world-space, starts 1.5s after load)
    ///   - Falling crop/coin sprites rendered as UI VisualElements — always in front of text
    ///   - Music fade-in (starts 0.5s after load)
    /// Sprites are object-pooled: toggled display:none / display:flex, never destroyed.
    /// Say "undo" in chat to revert all changes made by this feature.
    /// </summary>
    public class WinSceneEffects : MonoBehaviour
    {
        [Header("Sprites (assign in Inspector)")]
        public Sprite CarrotSprite;
        public Sprite CornSprite;
        public Sprite WheatSprite;
        public Sprite CoinSprite;

        [Header("Music")]
        public AudioClip WinMusic;

        // ── tuneable constants ──────────────────────────────────────────────

        // music
        private const float MusicStartDelay   = 0.5f;
        private const float MusicFadeDuration = 2.0f;

        // particle rain (world-space)
        private const float ParticleStartDelay = 1.5f;
        private const float ParticleEmitRate   = 40f;
        private const float ParticleSizeMin    = 0.08f;
        private const float ParticleSizeMax    = 0.15f;
        private const float ParticleSpeed      = 0.5f;
        private const float ParticleLifeMin    = 3.0f;
        private const float ParticleLifeMax    = 4.0f;
        private const float ParticleSpawnWidth = 11f;
        private const float ParticleSpawnY     = 7f;

        // sprite shower (UI Toolkit VisualElements)
        private const float SpriteSpawnInterval = 0.25f;   // seconds between spawns
        private const float SpriteFallPx        = 120f;    // pixels per second
        private const float SpriteSizeMin       = 55f;     // px
        private const float SpriteSizeMax       = 85f;     // px
        private const float SpriteMinSpacingPx  = 85f;     // min px gap between active sprites
        private const float MaxRotation         = 10f;     // degrees clamp
        private const float RotationSpeed       = 8f;      // degrees per second max
        private const int   MaxSpawnAttempts    = 10;
        private const int   PoolSize            = 60;      // pre-allocated VisualElements

        // ── runtime ────────────────────────────────────────────────────────

        private ParticleSystem      m_Rain;
        private AudioSource         m_Audio;
        private Sprite[]            m_Sprites;
        private int                 m_SpriteIndex;

        private VisualElement       m_Container;           // SpriteContainer in UXML
        private List<VisualElement> m_Pool    = new List<VisualElement>();
        private List<float>         m_ActiveX = new List<float>(); // pixel-X of every active sprite

        // ── lifecycle ──────────────────────────────────────────────────────

        void Start()
        {
            m_Sprites = new[] { CarrotSprite, CornSprite, WheatSprite, CoinSprite };

            var doc = FindObjectOfType<UIDocument>();
            if (doc != null)
                m_Container = doc.rootVisualElement.Q<VisualElement>("SpriteContainer");

            SetupAudio();
            StartCoroutine(DelayedStart());
        }

        // ── setup ──────────────────────────────────────────────────────────

        void BuildPool()
        {
            if (m_Container == null) return;
            for (int i = 0; i < PoolSize; i++)
            {
                var el = new VisualElement();
                el.style.position = Position.Absolute;
                el.style.display  = DisplayStyle.None;
                m_Container.Add(el);
                m_Pool.Add(el);
            }
        }

        void SetupAudio()
        {
            m_Audio = gameObject.AddComponent<AudioSource>();
            m_Audio.clip         = WinMusic;
            m_Audio.loop         = true;
            m_Audio.volume       = 0f;
            m_Audio.playOnAwake  = false;
            m_Audio.spatialBlend = 0f;
        }

        void SetupRain()
        {
            var go = new GameObject("WinRain");
            go.transform.SetParent(transform);

            m_Rain = go.AddComponent<ParticleSystem>();
            m_Rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = m_Rain.main;
            main.loop            = true;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(ParticleLifeMin, ParticleLifeMax);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(ParticleSpeed);
            main.startSize       = new ParticleSystem.MinMaxCurve(ParticleSizeMin, ParticleSizeMax);
            main.startColor      = new Color(0.7f, 0.85f, 1f, 0.55f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 400;

            var vel = m_Rain.velocityOverLifetime;
            vel.enabled = true;
            vel.space   = ParticleSystemSimulationSpace.World;
            vel.x = 0f;
            vel.y = new ParticleSystem.MinMaxCurve(-ParticleSpeed);
            vel.z = 0f;

            var emission = m_Rain.emission;
            emission.rateOverTime = ParticleEmitRate;

            var shape = m_Rain.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale     = new Vector3(ParticleSpawnWidth * 2f, 0.1f, 1f);
            go.transform.position = new Vector3(0f, ParticleSpawnY, 0f);

            var rend = m_Rain.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.material   = new Material(Shader.Find("Sprites/Default"));

            m_Rain.Play();
        }

        // ── coroutines ─────────────────────────────────────────────────────

        IEnumerator DelayedStart()
        {
            // music
            yield return new WaitForSeconds(MusicStartDelay);
            m_Audio.Play();
            StartCoroutine(FadeMusic());

            // particles
            yield return new WaitForSeconds(ParticleStartDelay - MusicStartDelay);
            SetupRain();

            // wait one frame so UI layout resolves before we read panel dimensions
            yield return null;
            BuildPool();
            StartCoroutine(SpawnSpriteShower());
        }

        IEnumerator FadeMusic()
        {
            float elapsed = 0f;
            while (elapsed < MusicFadeDuration)
            {
                elapsed += Time.deltaTime;
                m_Audio.volume = Mathf.Clamp01(elapsed / MusicFadeDuration) * 0.65f;
                yield return null;
            }
        }

        IEnumerator SpawnSpriteShower()
        {
            while (true)
            {
                ActivateSprite();
                yield return new WaitForSeconds(SpriteSpawnInterval);
            }
        }

        // ── pool logic ─────────────────────────────────────────────────────

        VisualElement GetInactive()
        {
            foreach (var el in m_Pool)
                if (el.style.display == DisplayStyle.None) return el;
            return null;
        }

        void ActivateSprite()
        {
            if (m_Container == null || m_Sprites == null) return;

            float panelW = m_Container.resolvedStyle.width;
            float panelH = m_Container.resolvedStyle.height;
            if (panelW <= 0 || panelH <= 0) return;

            float size   = Random.Range(SpriteSizeMin, SpriteSizeMax);
            float margin = size;

            // find a non-overlapping X position
            float x      = 0f;
            bool placed  = false;
            for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
            {
                float candidate = Random.Range(margin, panelW - margin);
                bool tooClose   = false;
                foreach (float existing in m_ActiveX)
                {
                    if (Mathf.Abs(candidate - existing) < SpriteMinSpacingPx)
                    { tooClose = true; break; }
                }
                if (!tooClose) { x = candidate; placed = true; break; }
            }
            if (!placed) return;

            var el = GetInactive();
            if (el == null) return;

            Sprite sprite = m_Sprites[m_SpriteIndex % m_Sprites.Length];
            m_SpriteIndex++;
            if (sprite == null) return;

            // configure element
            el.style.width           = size;
            el.style.height          = size;
            el.style.left            = x - size * 0.5f;
            el.style.top             = -size;
            el.style.backgroundImage = new StyleBackground(sprite);
            el.style.display         = DisplayStyle.Flex;
            el.transform.rotation    = Quaternion.identity;

            // guarantee a minimum speed so no sprite ever looks static
            float minRot   = RotationSpeed * 0.5f;
            float rotSpeed = Random.Range(minRot, RotationSpeed) * (Random.value > 0.5f ? 1f : -1f);
            m_ActiveX.Add(x);
            StartCoroutine(FallAndRecycle(el, x, panelH, size, rotSpeed));
        }

        IEnumerator FallAndRecycle(VisualElement el, float spawnX, float panelH, float size, float rotSpeed)
        {
            float top   = -size;
            float angle = 0f;

            while (top < panelH + size)
            {
                float dt = Time.deltaTime;

                top   += SpriteFallPx * dt;
                angle += rotSpeed * dt;
                if (angle >  MaxRotation) { angle =  MaxRotation; rotSpeed = -Mathf.Abs(rotSpeed); }
                if (angle < -MaxRotation) { angle = -MaxRotation; rotSpeed =  Mathf.Abs(rotSpeed); }

                el.style.top          = top;
                el.transform.rotation = Quaternion.Euler(0f, 0f, angle);

                yield return null;
            }

            // recycle
            el.style.display = DisplayStyle.None;
            m_ActiveX.Remove(spawnX);
        }
    }
}
