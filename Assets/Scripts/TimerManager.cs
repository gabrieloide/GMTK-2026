using System;
using UnityEngine;
using Code.Scripts.Audio;

public class TimerManager : MonoBehaviour
{
    [Tooltip("Time budget for the very first delivery, before OrderManager has picked a destination and set a distance-based limit. Also used as a fallback if that event is ever missed.")]
    [SerializeField] private float startTimer = 40f;
    [SerializeField] private int lowTimeWarningThreshold = 10;

    private float timeRemaining = 0f;
    private float currentTime = 0f;
    private int lastWarningTick = -1;
    public float CurrentTime => currentTime;
    public int LowTimeWarningThreshold => lowTimeWarningThreshold;

    // Fired once per second tick while time is low, same moment the warning SFX plays -
    // lets the HUD punch/shake the clock in sync with the beep instead of just recoloring it.
    public event Action<int> OnLowTimeTick;

    private void Awake()
    {
        // Subscribed in Awake (guaranteed to run for every object before any Start)
        // so OrderManager.Start() -> AddOrder() can never fire this event before we're listening.
        OrderManager.OnDeliveryTimeLimitSet += ResetTimeForNextDelivery;
    }

    private void Start()
    {
        timeRemaining = startTimer;
        currentTime = timeRemaining;
    }

    private void OnDestroy()
    {
        OrderManager.OnDeliveryTimeLimitSet -= ResetTimeForNextDelivery;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing || GameManager.Instance.isGameOver || !GameManager.Instance.IsGameUnfrozen) return;

        timeRemaining -= Time.deltaTime;
        currentTime = Mathf.FloorToInt(timeRemaining);
        if(currentTime <= 0)
        {
            GameManager.Instance.isGameOver = true;
            GameManager.OnGameOver?.Invoke();
            return;
        }

        if (currentTime <= lowTimeWarningThreshold)
        {
            int tick = Mathf.CeilToInt(currentTime);
            if (tick != lastWarningTick)
            {
                lastWarningTick = tick;
                AudioManager.Instance?.PlaySFX("low_time_warning");
                OnLowTimeTick?.Invoke(tick);
            }
        }
    }

    // Each delivery hands the player a fresh window for the next one - the clock measures
    // time between deliveries, not one timer that runs and accumulates for the whole game.
    public void ResetTimeForNextDelivery(float seconds)
    {
        timeRemaining = seconds;
        lastWarningTick = -1;
    }

}
