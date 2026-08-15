using UnityEngine;
using TMPro;

/// <summary>
/// Spawns a floating score popup (e.g. "+100") at the delivery point in world space.
/// Uses a WorldSpace Canvas with TextMeshProUGUI configured with high sorting order
/// and outline so it's clearly readable over the 3D city and never gets clipped by buildings.
/// </summary>
public class ScorePopup : MonoBehaviour
{
    private const string ModernizFontPath = "Fonts & Materials/Moderniz SDF";
    private const string LiberationFontPath = "Fonts & Materials/LiberationSans SDF";

    // Canvas scaling to map UI text size into world units
    private const float WorldUnitsPerPixel = 0.035f;
    private const float FontSize = 42f;

    private static readonly Vector3 SpawnHeightOffset = Vector3.up * 4.5f;

    [SerializeField] private float riseDistance = 3.5f;
    [SerializeField] private float duration = 1.1f;
    [SerializeField] private float punchScale = 1.4f;
    [SerializeField] private float punchDuration = 0.18f;
    [SerializeField] private float spawnTiltAngle = 12f;

    private TextMeshProUGUI label;
    private Transform cameraTransform;
    private Vector3 basePosition;
    private float timer;
    private float spinDirection;

    public static void Spawn(Vector3 worldPosition, string text, Color color)
    {
        var go = new GameObject($"ScorePopup ({text})", typeof(Canvas));
        go.transform.position = worldPosition + SpawnHeightOffset;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 3000; // Always render on top of 3D world geometry

        var canvasRect = go.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(250f, 80f);

        // Pre-set scale immediately so there is no 1-frame giant text flash
        go.transform.localScale = Vector3.one * (WorldUnitsPerPixel * 1.4f);

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
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.fontStyle = FontStyles.Bold;

        // Try loading Moderniz font first (matches HUD), then fallback to LiberationSans
        var font = Resources.Load<TMP_FontAsset>(ModernizFontPath);
        if (font == null) font = Resources.Load<TMP_FontAsset>(LiberationFontPath);
        if (font != null) label.font = font;

        // High contrast outline
        label.outlineWidth = 0.22f;
        label.outlineColor = new Color(0f, 0f, 0f, 0.9f);

        go.AddComponent<ScorePopup>();
    }

    private void Awake()
    {
        label = GetComponentInChildren<TextMeshProUGUI>();
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
        else
        {
            var cam = FindAnyObjectByType<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        basePosition = transform.position;
        spinDirection = Random.value < 0.5f ? -1f : 1f;

        if (cameraTransform != null)
        {
            transform.rotation = cameraTransform.rotation;
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);

        // Ease-out rise animation
        float eased = 1f - (1f - t) * (1f - t);
        transform.position = basePosition + Vector3.up * (riseDistance * eased);

        // Elastic pop/punch on spawn
        float punchProgress = t < punchDuration ? t / punchDuration : 1f;
        float punch = Mathf.Lerp(punchScale, 1f, punchProgress);
        transform.localScale = Vector3.one * (WorldUnitsPerPixel * punch);

        // Gentle tilt that settles
        float tilt = spinDirection * spawnTiltAngle * (1f - punchProgress);

        if (cameraTransform != null)
        {
            transform.rotation = cameraTransform.rotation * Quaternion.Euler(0f, 0f, tilt);
        }

        // Fade out over the last 40% of duration
        const float fadeStart = 0.6f;
        if (t > fadeStart && label != null)
        {
            float alpha = Mathf.Lerp(1f, 0f, (t - fadeStart) / (1f - fadeStart));
            Color c = label.color;
            c.a = alpha;
            label.color = c;

            Color oc = label.outlineColor;
            oc.a = alpha * 0.9f;
            label.outlineColor = oc;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
