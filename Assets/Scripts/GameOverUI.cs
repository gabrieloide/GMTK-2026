using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Code.Scripts.Audio;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI finalScoreText;

    // The player is usually still on the keys when the clock runs out, so hold off
    // briefly or the score flashes past before it can be read.
    [SerializeField] private float restartInputDelay = 1f;
    private float shownAt;

    private void OnEnable()
    {
        GameManager.OnGameOver += ShowGameOver;
    }

    private void OnDisable()
    {
        GameManager.OnGameOver -= ShowGameOver;
    }

    private void Update()
    {
        if (gameOverPanel == null || !gameOverPanel.activeSelf) return;
        if (Time.unscaledTime - shownAt < restartInputDelay) return;

        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            Restart();
        }
    }

    private void ShowGameOver()
    {
        shownAt = Time.unscaledTime;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (finalScoreText != null && GameManager.Instance != null) finalScoreText.text = $"Final Score: {GameManager.Instance.Score}";
        AudioManager.Instance?.PlaySFX("game_over");
    }

    public void Restart()
    {
        AudioManager.Instance?.PlaySFX("ui_restart");
        TransitionManager.Instance.PlayTransition(() => 
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        });
    }
}
