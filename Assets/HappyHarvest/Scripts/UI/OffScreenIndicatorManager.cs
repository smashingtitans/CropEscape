using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HappyHarvest
{
    /// <summary>
    /// Shows off-screen UI arrows that point toward important targets:
    ///   * a "Shop" arrow that points to the Market (Prefab_Market)
    ///   * a "Harvest" arrow that points to the OLDEST fully grown crop currently in the scene
    ///
    /// The arrows stick to the edges of the screen and slide along them to track the target.
    /// When the target becomes visible on screen the matching arrow hides itself.
    /// Arrows flash when they first appear, and fade with distance (solid when far, faint when close).
    ///
    /// SETUP (in the Unity Editor):
    ///   1. Create an empty GameObject in the scene (name it e.g. "OffScreenIndicators").
    ///   2. Add this script to it.
    ///   3. Drag the 4 shop_* sprites into Shop Sprites (order: Up, Right, Down, Left).
    ///   4. Drag the 4 harvest_* sprites into Harvest Sprites (same order).
    /// The Canvas and arrow images are created automatically at runtime - nothing else to wire up.
    /// </summary>
    public class OffScreenIndicatorManager : MonoBehaviour
    {
        [System.Serializable]
        public class DirectionalSprites
        {
            public Sprite Up;
            public Sprite Right;
            public Sprite Down;
            public Sprite Left;
        }

        [Header("Sprites (drag the PNGs here)")]
        public DirectionalSprites ShopSprites;
        public DirectionalSprites HarvestSprites;

        [Header("Targets")]
        [Tooltip("Object names that count as a fully grown crop to point at.")]
        public string[] HarvestTargetNames = { "Prefab_Carrot_04", "Prefab_Corn_05", "Prefab_wheat_05" };
        [Tooltip("Name of the shop object to point at.")]
        public string ShopTargetName = "Prefab_Market";

        [Header("Edge placement")]
        [Tooltip("How far (in pixels) the arrow stays from the screen edge.")]
        public float EdgeMargin = 60.0f;
        [Tooltip("On-screen pixel size of each arrow.")]
        public float IconSize = 96.0f;

        [Header("Opacity vs distance")]
        [Tooltip("At or beyond this world distance the arrow is at Max Opacity (far = solid).")]
        public float MaxOpacityDistance = 25.0f;
        [Tooltip("At or below this world distance the arrow is at Min Opacity (close = faint).")]
        public float MinOpacityDistance = 4.0f;
        [Range(0f, 1f)] public float MinOpacity = 0.25f;
        [Range(0f, 1f)] public float MaxOpacity = 1.0f;

        [Header("Flash on appear")]
        public float FlashDuration = 1.2f;
        public float FlashesPerSecond = 4.0f;

        [Tooltip("How often (seconds) to rescan the scene for harvest targets.")]
        public float TargetScanInterval = 0.5f;

        // Runtime UI
        private Canvas m_Canvas;
        private Indicator m_ShopIndicator;
        private Indicator m_HarvestIndicator;

        // Harvest target tracking: object -> time it first appeared (to find the "oldest").
        private readonly Dictionary<GameObject, float> m_HarvestFirstSeen = new();
        private float m_NextScanTime;
        private GameObject m_ShopTarget;

        // Holds the live state of a single arrow.
        private class Indicator
        {
            public RectTransform Rect;
            public Image Image;
            public DirectionalSprites Sprites;
            public bool WasActive;       // was it visible last frame? (used to detect "first appear")
            public float FlashEndTime;   // until when the flash effect plays
        }

        private void Start()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("OffScreenIndicatorCanvas");
            canvasGo.transform.SetParent(transform, false);

            m_Canvas = canvasGo.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Canvas.sortingOrder = 100; // draw on top of the world
            canvasGo.AddComponent<GraphicRaycaster>();

            m_ShopIndicator = CreateIndicator("ShopIndicator", ShopSprites);
            m_HarvestIndicator = CreateIndicator("HarvestIndicator", HarvestSprites);
        }

        private Indicator CreateIndicator(string name, DirectionalSprites sprites)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(m_Canvas.transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(IconSize, IconSize);

            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true; // keep each PNG's natural shape, no stretching

            go.SetActive(false);

            return new Indicator { Rect = rect, Image = image, Sprites = sprites };
        }

        private void Update()
        {
            if (GameManager.Instance == null)
                return;

            var player = GameManager.Instance.Player;
            var cam = Camera.main;
            if (player == null || cam == null)
                return;

            if (Time.time >= m_NextScanTime)
            {
                ScanTargets();
                m_NextScanTime = Time.time + TargetScanInterval;
            }

            Vector3 playerPos = player.transform.position;

            // Shop arrow.
            UpdateIndicator(m_ShopIndicator, m_ShopTarget, playerPos, cam);

            // Harvest arrow -> oldest fully grown crop.
            UpdateIndicator(m_HarvestIndicator, GetOldestHarvestTarget(), playerPos, cam);
        }

        private void UpdateIndicator(Indicator indicator, GameObject target, Vector3 playerPos, Camera cam)
        {
            // No target, or the target is visible on screen -> hide the arrow.
            if (target == null || IsOnScreen(target.transform.position, cam))
            {
                if (indicator.Rect.gameObject.activeSelf)
                    indicator.Rect.gameObject.SetActive(false);
                indicator.WasActive = false;
                return;
            }

            if (!indicator.Rect.gameObject.activeSelf)
                indicator.Rect.gameObject.SetActive(true);

            // First time appearing -> start the flash.
            if (!indicator.WasActive)
            {
                indicator.FlashEndTime = Time.time + FlashDuration;
                indicator.WasActive = true;
            }

            // Place on screen edge + pick the directional sprite.
            PlaceOnEdge(indicator, target.transform.position, cam);

            // Opacity from distance (far = solid, close = faint).
            float dist = Vector3.Distance(playerPos, target.transform.position);
            float t = Mathf.InverseLerp(MinOpacityDistance, MaxOpacityDistance, dist);
            float alpha = Mathf.Lerp(MinOpacity, MaxOpacity, t);

            // Apply the flash on top (blink while it lasts).
            if (Time.time < indicator.FlashEndTime)
            {
                float blink = (Mathf.Sin(Time.time * FlashesPerSecond * Mathf.PI * 2.0f) + 1.0f) * 0.5f;
                alpha *= blink;
            }

            var c = indicator.Image.color;
            c.a = alpha;
            indicator.Image.color = c;
        }

        private bool IsOnScreen(Vector3 worldPos, Camera cam)
        {
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            return vp.z > 0 && vp.x > 0 && vp.x < 1 && vp.y > 0 && vp.y < 1;
        }

        private void PlaceOnEdge(Indicator indicator, Vector3 worldPos, Camera cam)
        {
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            // If the target is behind the camera, flip so the math points the right way.
            if (screenPos.z < 0)
            {
                screenPos.x = Screen.width - screenPos.x;
                screenPos.y = Screen.height - screenPos.y;
            }

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = (Vector2)screenPos - center;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.up;

            float halfW = Screen.width * 0.5f - EdgeMargin;
            float halfH = Screen.height * 0.5f - EdgeMargin;

            // Find how far along 'dir' we can go before hitting a vertical or horizontal edge.
            float scaleX = Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
            float scaleY = Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
            float scale = Mathf.Min(scaleX, scaleY);

            Vector2 edgePos = center + dir * scale;
            indicator.Rect.position = edgePos;

            // Which edge did we hit? -> choose the arrow image.
            if (scaleX < scaleY)
                indicator.Image.sprite = dir.x > 0 ? indicator.Sprites.Right : indicator.Sprites.Left;
            else
                indicator.Image.sprite = dir.y > 0 ? indicator.Sprites.Up : indicator.Sprites.Down;
        }

        // ---- Target finding ----------------------------------------------------

        private void ScanTargets()
        {
            // Shop: cache it, refresh if it went away.
            if (m_ShopTarget == null)
                m_ShopTarget = FindByName(ShopTargetName);

            // Harvest: track every matching object and when it first appeared.
            var found = new HashSet<GameObject>();
            var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var tr in all)
            {
                if (!NameMatchesHarvest(tr.gameObject.name))
                    continue;

                found.Add(tr.gameObject);
                if (!m_HarvestFirstSeen.ContainsKey(tr.gameObject))
                    m_HarvestFirstSeen[tr.gameObject] = Time.time;
            }

            // Forget objects that are gone.
            var stale = new List<GameObject>();
            foreach (var kv in m_HarvestFirstSeen)
            {
                if (kv.Key == null || !found.Contains(kv.Key))
                    stale.Add(kv.Key);
            }
            foreach (var go in stale)
                m_HarvestFirstSeen.Remove(go);
        }

        private GameObject GetOldestHarvestTarget()
        {
            GameObject oldest = null;
            float oldestTime = float.MaxValue;
            foreach (var kv in m_HarvestFirstSeen)
            {
                if (kv.Key == null)
                    continue;
                if (kv.Value < oldestTime)
                {
                    oldestTime = kv.Value;
                    oldest = kv.Key;
                }
            }
            return oldest;
        }

        private bool NameMatchesHarvest(string objectName)
        {
            foreach (var n in HarvestTargetNames)
            {
                if (!string.IsNullOrEmpty(n) && objectName.StartsWith(n))
                    return true;
            }
            return false;
        }

        private GameObject FindByName(string targetName)
        {
            var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var tr in all)
            {
                if (tr.gameObject.name.StartsWith(targetName))
                    return tr.gameObject;
            }
            return null;
        }
    }
}
