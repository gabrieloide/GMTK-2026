using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Handles switching between the Main Menu camera and Gameplay camera using Cinemachine priorities.
/// </summary>
public class GameStateCameraSwitcher : MonoBehaviour
{
    [Tooltip("The camera that should be active during the Main Menu.")]
    [SerializeField] private CinemachineCamera mainMenuCamera;
    
    [Tooltip("The camera that should follow the player during Gameplay.")]
    [SerializeField] private CinemachineCamera gameplayCamera;

    private void Start()
    {
        // Al cargar la escena, el estado inicial es MainMenu.
        // Damos prioridad a la cámara del menú.
        if (mainMenuCamera != null) mainMenuCamera.Priority = 10;
        if (gameplayCamera != null) gameplayCamera.Priority = 0;
    }

    private void OnEnable()
    {
        GameManager.OnGameStarted += SwitchToGameplayCamera;
    }

    private void OnDisable()
    {
        GameManager.OnGameStarted -= SwitchToGameplayCamera;
    }

    private void SwitchToGameplayCamera()
    {
        // Al empezar a jugar, invertimos las prioridades para que Cinemachine haga la transición
        if (mainMenuCamera != null) mainMenuCamera.Priority = 0;
        if (gameplayCamera != null) gameplayCamera.Priority = 10;
    }
}
