using System;
using UnityEngine;
using Code.Scripts.Audio;

public class TimerManager : MonoBehaviour
{
    [SerializeField] private float startTimer = 40f;
    [SerializeField] private int lowTimeWarningThreshold = 10;
    private float currentTime = 0f;
    private int lastWarningTick = -1;
    public float CurrentTime => currentTime;
    public int LowTimeWarningThreshold => lowTimeWarningThreshold;

    // Fired once per second tick while time is low, same moment the warning SFX plays -
    // lets the HUD punch/shake the clock in sync with the beep instead of just recoloring it.
    public event Action<int> OnLowTimeTick;

    private void Start()
    {
        currentTime = startTimer;
        OrderManager.OnTimeBonusAwarded += AddTime;
    }

    private void OnDestroy()
    {
        OrderManager.OnTimeBonusAwarded -= AddTime;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.isGameOver) return;

        startTimer -= Time.deltaTime;
        currentTime = Mathf.FloorToInt(startTimer);
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
                AudioManager.Instance.PlaySFX("low_time_warning");
                OnLowTimeTick?.Invoke(tick);
            }
        }
    }
    public void AddTime(float amount)
    {
        startTimer += amount;
        lastWarningTick = -1;
    }

}
