using UnityEngine;
using TMPro;

// Classic arcade score readout: spawn a "+100" at the point it was earned, let it rise with
// an ease-out drift and fade away right there - the way points read in Mario/Sonic-era
// games, rather than flying across the screen to the HUD. Built at runtime as a small
// World Space canvas (UGUI) rather than a 3D mesh TextMeshPro: this project's URP setup
// only renders TMP text correctly through the Canvas/UGUI path (the 3D TextMeshPro
// component relies on the classic Distance Field shader, which URP silently drops).
public class ScorePopup : MonoBehaviour
{
    // TMP Essential Resources' own default font, loaded by its Resources-relative path
    // (Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset).
    private const string DefaultFontResourcePath = "Fonts & Materials/LiberationSans SDF";

    // World Space canvases render their RectTransform at literal pixel scale, so the
    // canvas itself is scaled down by this factor to bring UI-sized text down to
    // world-scale units (matches the in-world nameplate/health-bar convention).
    private const float WorldUnitsPerCanvasPixel = 0.01f;
    private const float FontSize = 100f;

    // Delivery points sit at car-roof height, so a popup spawned right at the delivery
    // position renders directly under the player's own car from this game's steep
    // top-down chase camera - lifting the spawn clears the roofline immediately instead
    // of waiting on the rise animation to climb out from underneath it.
    private static readonly Vector3 SpawnHeightOffset = Vector3.up * 3f;

    [SerializeField] private float baseScale = 2.2f;
    [SerializeField] private float riseDistance = 1.5f;
    [SerializeField] private float duration = 0.9f;
    [SerializeField] private float punchDuration = 0.15f;
    [SerializeField] private float punchScale = 1.35f;

    private TextMeshProUGUI label;
    private Transform cameraTransform;
    private Vector3 basePosition;
    private float timer;

    public static void Spawn(Vector3 worldPosition, string text, Color color)
    {
        var go = new GameObject($"ScorePopup ({text})", typeof(Canvas));
        go.transform.position = worldPosition + SpawnHeightOffset;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        var canvasRect = go.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(400f, 150f);

        var labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(go.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.color = color;
        label.fontSize = FontSize;
        label.alignment = TextAlignmentOptions.Center;

        var font = Resources.Load<TMP_FontAsset>(DefaultFontResourcePath);
        if (font != null) label.font = font;

        go.AddComponent<ScorePopup>();
    }

    private void Awake()
    {
        label = GetComponentInChildren<TextMeshProUGUI>();
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
        transform.localScale = Vector3.one * (WorldUnitsPerCanvasPixel * baseScale * punch);

        // UI shaders render both faces, so matching the camera's own rotation is enough
        // to keep the text facing it correctly (not mirrored) at any viewing angle.
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
