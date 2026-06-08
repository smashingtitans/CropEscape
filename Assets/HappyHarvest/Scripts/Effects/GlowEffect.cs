using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class GlowEffect : MonoBehaviour
{
    [ColorUsage(true, true)]
    public Color glowColor = new Color(1f, 1f, 0f, 1f);

    [Range(0f, 1f)]
    public float glowStrength = 0.6f;

    public bool glowEnabled = true;

    public bool blinkEnabled = true;
    [Range(0.1f, 10f)]
    public float blinkSpeed = 2f;

    private SpriteRenderer _renderer;
    private Color          _originalColor;

    void Start()
    {
        _renderer      = GetComponent<SpriteRenderer>();
        _originalColor = _renderer.color;
    }

    void Update()
    {
        if (_renderer == null) return;

        if (!glowEnabled)
        {
            _renderer.color = _originalColor;
            return;
        }

        float t = blinkEnabled
            ? (Mathf.Sin(Time.time * blinkSpeed) * 0.5f + 0.5f) * glowStrength
            : glowStrength;

        _renderer.color = Color.Lerp(_originalColor, glowColor, t);
    }

    void OnDisable()
    {
        if (_renderer != null)
            _renderer.color = _originalColor;
    }

    public void SetGlowColor(Color color)  => glowColor   = color;
    public void SetGlowEnabled(bool value) => glowEnabled = value;
}
