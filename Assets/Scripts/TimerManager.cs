using UnityEngine;

public class TimerManager : MonoBehaviour
{
    [SerializeField] private float startTimer = 40f;
    [SerializeField] private float timeToAdd = 0f;
    private float currentTime = 0f;

    private void Start()
    {
        currentTime = startTimer;
        OrderManager.OnOrderFinished += AddTime;
    }

    private void Update()
    {
        startTimer -= Time.deltaTime;
        currentTime = Mathf.FloorToInt(startTimer);
        if(currentTime <= 0)
        {
            GameManager.Instance.isGameOver = true;
            GameManager.OnGameOver?.Invoke();
        }

    }
    public void AddTime()
    {
        startTimer += timeToAdd;
    }

}
