using UnityEngine;
using TMPro;

public class HUDController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TimerManager timerManager;

    [Header("Score Punch")]
    [SerializeField] private float punchScale = 1.25f;
    [SerializeField] private float punchDuration = 0.2f;

    [Header("Low Time Urgency")]
    [SerializeField] private Color normalTimerColor = Color.white;
    [SerializeField] private Color urgentTimerColor = new Color(1f, 0.2f, 0.15f);
    [SerializeField] private float urgentPulseSpeed = 6f;

    private int lastScore;
    private float punchTimer = -1f;

    private void Update()
    {
        if (timerManager != null && timerText != null)
        {
            float time = timerManager.CurrentTime;
            timerText.text = $"Time: {Mathf.Max(0, Mathf.CeilToInt(time))}";
            UpdateTimerUrgency(time);
        }

        if (GameManager.Instance != null && scoreText != null)
        {
            int currentScore = GameManager.Instance.Score;
            // A plain text swap reads as static - the punch is what sells "you just
            // earned that", in sync with the popup rising from the delivery point.
            if (currentScore != lastScore)
            {
                lastScore = currentScore;
                punchTimer = 0f;
            }
            scoreText.text = $"Score: {currentScore}";
        }

        UpdateScorePunch();
    }

    private void UpdateScorePunch()
    {
        if (punchTimer < 0f || scoreText == null) return;

        punchTimer += Time.deltaTime;
        float t = Mathf.Clamp01(punchTimer / punchDuration);
        scoreText.transform.localScale = Vector3.one * Mathf.Lerp(punchScale, 1f, t);
        if (t >= 1f) punchTimer = -1f;
    }

    // Shares TimerManager's own threshold rather than a second hardcoded number, so the
    // clock only turns urgent exactly when the low-time SFX starts ticking.
    private void UpdateTimerUrgency(float time)
    {
        if (time > timerManager.LowTimeWarningThreshold)
        {
            timerText.color = normalTimerColor;
            return;
        }

        float pulse = (Mathf.Sin(Time.time * urgentPulseSpeed) + 1f) * 0.5f;
        timerText.color = Color.Lerp(normalTimerColor, urgentTimerColor, pulse);
    }
}
