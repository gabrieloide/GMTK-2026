using UnityEngine;
using TMPro;

public class HUDController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TimerManager timerManager;
    [SerializeField] private GameObject hudContainer;

    [Header("Score Punch")]
    [SerializeField] private float punchScale = 1.3f;
    [SerializeField] private float punchRotation = 12f;
    [SerializeField] private float punchDuration = 0.5f;
    [SerializeField] private Color scoreFlashColor = new Color(1f, 0.95f, 0.5f);

    [Header("Low Time Urgency")]
    [SerializeField] private Color normalTimerColor = Color.white;
    [SerializeField] private Color urgentTimerColor = new Color(1f, 0.2f, 0.15f);
    [SerializeField] private float urgentPulseSpeed = 6f;
    [SerializeField] private float heartbeatScale = 1.3f;
    [SerializeField] private float criticalTimeThreshold = 5f;
    [SerializeField] private float shakeMagnitude = 5f;

    // Decaying-oscillation constants shared by every punch below - tuned once here so the
    // score bounce and the timer heartbeat both read as the same "bouncy" motion language.
    private const float SpringFrequency = 22f;
    private const float SpringDamping = 10f;
    private const float HeartbeatDuration = 0.5f;

    private int lastScore;
    private float scorePunchTimer = -1f;
    private Color scoreBaseColor;

    private RectTransform timerRect;
    private Vector2 timerHomePosition;
    private float heartbeatTimer = -1f;

    private void Awake()
    {
        if (scoreText != null) scoreBaseColor = scoreText.color;
        if (timerText != null)
        {
            timerRect = timerText.rectTransform;
            timerHomePosition = timerRect.anchoredPosition;
        }
    }

    private void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.MainMenu)
        {
            if (hudContainer != null) hudContainer.SetActive(false);
            else 
            {
                if (timerText != null) timerText.gameObject.SetActive(false);
                if (scoreText != null) scoreText.gameObject.SetActive(false);
            }
        }
    }

    private void OnEnable()
    {
        if (timerManager != null) timerManager.OnLowTimeTick += HandleLowTimeTick;
        GameManager.OnGameStarted += ShowHUD;
    }

    private void OnDisable()
    {
        if (timerManager != null) timerManager.OnLowTimeTick -= HandleLowTimeTick;
        GameManager.OnGameStarted -= ShowHUD;
    }

    private void ShowHUD()
    {
        if (hudContainer != null) hudContainer.SetActive(true);
        else 
        {
            if (timerText != null) timerText.gameObject.SetActive(true);
            if (scoreText != null) scoreText.gameObject.SetActive(true);
        }
    }

    private void Update()
    {
        if (timerManager != null && timerText != null)
        {
            float time = timerManager.CurrentTime;
            timerText.text = $"Time: {Mathf.Max(0, Mathf.CeilToInt(time))}";
            UpdateTimerUrgency(time);
            UpdateHeartbeat(time);
        }

        if (GameManager.Instance != null && scoreText != null)
        {
            int currentScore = GameManager.Instance.Score;
            // A plain text swap reads as static - the punch is what sells "you just
            // earned that", in sync with the popup rising from the delivery point.
            if (currentScore != lastScore)
            {
                lastScore = currentScore;
                scorePunchTimer = 0f;
            }
            scoreText.text = $"Score: {currentScore}";
        }

        UpdateScorePunch();
    }

    private void HandleLowTimeTick(int tick)
    {
        heartbeatTimer = 0f;
    }

    private void UpdateScorePunch()
    {
        if (scoreText == null) return;

        if (scorePunchTimer < 0f || scorePunchTimer >= punchDuration)
        {
            scorePunchTimer = -1f;
            scoreText.transform.localScale = Vector3.one;
            scoreText.transform.localRotation = Quaternion.identity;
            scoreText.color = scoreBaseColor;
            return;
        }

        scorePunchTimer += Time.deltaTime;

        // Decaying oscillation so the punch overshoots and settles instead of just
        // shrinking back in a straight line - reads as a bounce, not a snap.
        float spring = Mathf.Exp(-SpringDamping * scorePunchTimer) * Mathf.Cos(SpringFrequency * scorePunchTimer);
        scoreText.transform.localScale = Vector3.one * (1f + (punchScale - 1f) * spring);
        scoreText.transform.localRotation = Quaternion.Euler(0f, 0f, punchRotation * spring);

        float flashT = Mathf.Clamp01(scorePunchTimer / (punchDuration * 0.4f));
        scoreText.color = Color.Lerp(scoreFlashColor, scoreBaseColor, flashT);
    }

    private void UpdateHeartbeat(float time)
    {
        if (timerText == null || timerRect == null) return;

        if (heartbeatTimer < 0f || heartbeatTimer >= HeartbeatDuration)
        {
            heartbeatTimer = -1f;
            timerRect.anchoredPosition = timerHomePosition;
            timerText.transform.localScale = Vector3.one;
            return;
        }

        heartbeatTimer += Time.deltaTime;

        float spring = Mathf.Exp(-SpringDamping * heartbeatTimer) * Mathf.Cos(SpringFrequency * heartbeatTimer);
        timerText.transform.localScale = Vector3.one * (1f + (heartbeatScale - 1f) * spring);

        // Only the last few seconds shake, getting more frantic as time runs out - on top
        // of the once-a-second heartbeat punch every low-time tick already gives it.
        if (time <= criticalTimeThreshold)
        {
            float urgencyT = 1f - Mathf.Clamp01(time / criticalTimeThreshold);
            float shakeX = (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * 2f * shakeMagnitude * urgencyT;
            float shakeY = (Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f) * 2f * shakeMagnitude * urgencyT;
            timerRect.anchoredPosition = timerHomePosition + new Vector2(shakeX, shakeY);
        }
        else
        {
            timerRect.anchoredPosition = timerHomePosition;
        }
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
