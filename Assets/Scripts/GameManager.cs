using System;
using UnityEngine;

public enum GameState
{
    MainMenu,
    Playing,
    GameOver
}

public class GameManager : MonoBehaviour
{
    public static Action OnGameStarted;
    public static Action OnGameOver;

    public GameState State { get; private set; } = GameState.MainMenu;
    public bool isGameOver = false;
    public bool IsGameUnfrozen { get; private set; }

    [SerializeField] private int scorePerDelivery = 100;
    [SerializeField] private Color scorePopupColor = new Color(1f, 0.85f, 0.2f);
    public int Score { get; private set; }

    public static GameManager Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
            IsGameUnfrozen = false;
        }
    }

    private void OnEnable()
    {
        OrderManager.OnOrderFinished += AddScore;
    }

    private void OnDisable()
    {
        OrderManager.OnOrderFinished -= AddScore;
    }

    private void AddScore(Vector3 deliveryPosition)
    {
        Score += scorePerDelivery;
        ScorePopup.Spawn(deliveryPosition, $"+{scorePerDelivery}", scorePopupColor);
    }

    public static Action OnGameUnfrozen;

    public void StartGame()
    {
        if (State != GameState.MainMenu) return;

        if (TransitionManager.Instance != null)
        {
            TransitionManager.Instance.PlayTransition(
                onScreenCovered: () =>
                {
                    State = GameState.Playing;
                    OnGameStarted?.Invoke();
                },
                onTransitionFinished: () =>
                {
                    IsGameUnfrozen = true;
                    OnGameUnfrozen?.Invoke();
                }
            );
        }
        else
        {
            State = GameState.Playing;
            OnGameStarted?.Invoke();
            IsGameUnfrozen = true;
            OnGameUnfrozen?.Invoke();
        }
    }
}