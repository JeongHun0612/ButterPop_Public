using System;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    public GameState CurrentState { get; private set; } = GameState.Ready;
    public event Action<GameState> OnStateChanged;

    [SerializeField] private bool debugLog = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState) return;

        var prev = CurrentState;
        CurrentState = newState;

        if (debugLog)
            Debug.Log($"[StageManager] {prev} → {newState}");

        OnStateChanged?.Invoke(newState);
    }
}
