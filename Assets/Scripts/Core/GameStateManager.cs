using System;
using UnityEngine;

public enum GameState
{
    Playing,
    GameOver
}

/*
 *  Scene-scoped singleton that owns the current GameState.
 *  Any script can subscribe to OnStateChanged to react to state transitions
 *  (e.g. GameOverUI showing its panel, PlayerMovement freezing input).
 *
 *  Place ONE GameStateManager GameObject in each gameplay scene.
 *  Resets to Playing automatically on scene load because the instance is new.
 */
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.Playing;

    public static event Action<GameState> OnStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Set(GameState next)
    {
        if (State == next) return;
        State = next;
        Time.timeScale = (next == GameState.Playing) ? 1f : 0f;
        OnStateChanged?.Invoke(next);
    }
}
