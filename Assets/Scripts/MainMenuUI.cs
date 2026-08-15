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

        bool startTriggered = false;

        // 1. Keyboard (Space, Enter, or any key)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                Keyboard.current.anyKey.wasPressedThisFrame)
            {
                startTriggered = true;
            }
        }

        // 2. Gamepad (A/Cross, Start, or any button)
        if (!startTriggered && Gamepad.current != null)
        {
            if (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                Gamepad.current.startButton.wasPressedThisFrame ||
                Gamepad.current.allControls.Count > 0 && Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                startTriggered = true;
            }
        }

        // 3. Mouse / Touch (Click/tap anywhere)
        if (!startTriggered && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            startTriggered = true;
        }

        if (!startTriggered && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            startTriggered = true;
        }

        if (startTriggered)
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
