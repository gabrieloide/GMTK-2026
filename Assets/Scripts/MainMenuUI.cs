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

        // 1. New Input System Keyboard (Space, Enter, NumpadEnter)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                startTriggered = true;
            }
        }

        // 2. New Input System Gamepad (A/Cross or Start)
        if (Gamepad.current != null)
        {
            if (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                Gamepad.current.startButton.wasPressedThisFrame)
            {
                startTriggered = true;
            }
        }

        // 3. New Input System Mouse / Touch (Click/tap anywhere)
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            startTriggered = true;
        }
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            startTriggered = true;
        }

        // 4. Legacy Input Fallback (in case active input handling differs)
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
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
