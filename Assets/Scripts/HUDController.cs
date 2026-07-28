using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [SerializeField] private Text timerText;
    [SerializeField] private Text scoreText;
    [SerializeField] private TimerManager timerManager;

    private void Update()
    {
        if (timerManager != null && timerText != null) timerText.text = $"Time: {Mathf.Max(0, Mathf.CeilToInt(timerManager.CurrentTime))}";
        if (GameManager.Instance != null && scoreText != null) scoreText.text = $"Score: {GameManager.Instance.Score}";
    }
}
