using UnityEngine;
using TMPro;

// Classic arcade score readout: spawn a "+100" at the point it was earned, let it rise with
// an ease-out drift and fade away right there - the way points read in Mario/Sonic-era
// games, rather than flying across the screen to the HUD. Built at runtime with a plain
// TextMeshPro instead of a prefab: the whole thing is one GameObject with no authored
// assets to keep in sync.
public class ScorePopup : MonoBehaviour
{
    // TMP Essential Resources' own default font, loaded by its Resources-relative path
    // (Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset).
    private const string DefaultFontResourcePath = "Fonts & Materials/LiberationSans SDF";

    [SerializeField] private float baseScale = 1.5f;
    [SerializeField] private float riseDistance = 1.5f;
    [SerializeField] private float duration = 0.9f;
    [SerializeField] private float punchDuration = 0.15f;
    [SerializeField] private float punchScale = 1.35f;

    private TextMeshPro label;
    private Transform cameraTransform;
    private Vector3 basePosition;
    private float timer;

    public static void Spawn(Vector3 worldPosition, string text, Color color)
    {
        var go = new GameObject($"ScorePopup ({text})");
        go.transform.position = worldPosition;

        var label = go.AddComponent<TextMeshPro>();
        label.text = text;
        label.color = color;
        label.fontSize = 10f;
        label.alignment = TextAlignmentOptions.Center;

        var font = Resources.Load<TMP_FontAsset>(DefaultFontResourcePath);
        if (font != null) label.font = font;

        go.AddComponent<ScorePopup>();
    }

    private void Awake()
    {
        label = GetComponent<TextMeshPro>();
        cameraTransform = Camera.main != null ? Camera.main.transform : null;
        basePosition = transform.position;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);

        // Fast off the mark, settling near the top - a straight lerp reads as mechanical.
        float eased = 1f - (1f - t) * (1f - t);
        transform.position = basePosition + Vector3.up * (riseDistance * eased);

        float punch = t < punchDuration ? Mathf.Lerp(punchScale, 1f, t / punchDuration) : 1f;
        transform.localScale = Vector3.one * (baseScale * punch);

        if (cameraTransform != null) transform.rotation = cameraTransform.rotation;

        const float fadeStart = 0.5f;
        if (t > fadeStart)
        {
            Color c = label.color;
            c.a = Mathf.Lerp(1f, 0f, (t - fadeStart) / (1f - fadeStart));
            label.color = c;
        }

        if (t >= 1f) Destroy(gameObject);
    }
}
