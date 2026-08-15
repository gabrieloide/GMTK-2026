using UnityEngine;
using UnityEngine.InputSystem;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuPanel;

    private void Start()
    {
        // Ensure the panel is visible on start if we are in MainMenu state
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.MainMenu)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        }
    }

    private void OnEnable()
    {
        GameManager.OnGameStarted += HideMenu;
    }

    private void OnDisable()
    {
        GameManager.OnGameStarted -= HideMenu;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.MainMenu) return;

        // Solo empezar el juego cuando se presiona la tecla Espacio
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            StartGame();
        }
    }

    public void StartGame()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame();
        }
    }

    private void HideMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
    }
}
