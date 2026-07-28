using UnityEngine;

public class TimerManager : MonoBehaviour
{
    [SerializeField] private float startTimer = 40f;
    private float currentTime = 0f;
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
        }

    }
    public void AddTime(float amount)
    {
        startTimer += amount;
    }

}
