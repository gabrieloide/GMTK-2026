using UnityEngine;
using Code.Scripts.Audio;

public class TimerManager : MonoBehaviour
{
    [SerializeField] private float startTimer = 40f;
    [SerializeField] private int lowTimeWarningThreshold = 10;
    private float currentTime = 0f;
    private int lastWarningTick = -1;
    public float CurrentTime => currentTime;

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
            }
        }
    }
    public void AddTime(float amount)
    {
        startTimer += amount;
        lastWarningTick = -1;
    }

}
