using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static Action OnGameOver;
    public bool isGameOver = false;

    [SerializeField] private int scorePerDelivery = 100;
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

    private void AddScore()
    {
        Score += scorePerDelivery;
    }
}