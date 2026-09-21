using UnityEngine;

// Attach to the same GameObject as SoftBodyPlayer.
// Pauses player gameplay while dialogue is open without disabling the ring-point
// Rigidbody2D components.  DialogueManager uses realtime waits, so pausing the
// game time leaves its typewriter and advance input responsive.
[RequireComponent(typeof(SoftBodyPlayer))]
public class PlayerDialogueHandler : MonoBehaviour
{
    private SoftBodyPlayer       _body;
    private PlayerSplitController _splitController;

    private bool  _dialoguePauseActive;
    private bool  _previousInputEnabled;
    private bool  _previousSplitControllerEnabled;
    private float _previousTimeScale;

    // GameStateManager owns the time scale for game-over transitions.  If that
    // state changes while dialogue is open, leave its resulting time scale alone
    // instead of restoring a stale playing value.
    private GameStateManager _savedStateManager;
    private GameState         _previousGameState;
    private bool              _hasPreviousGameState;
    private bool              _gameStateChangedDuringDialogue;

    private void Awake()
    {
        _body = GetComponent<SoftBodyPlayer>();
        _splitController = GetComponent<PlayerSplitController>();
    }

    private void OnEnable()
    {
        EventManager.OnDialogueStart += OnDialogueStart;
        EventManager.OnDialogueEnd   += OnDialogueEnd;
        GameStateManager.OnStateChanged += OnGameStateChanged;
    }

    private void OnDisable()
    {
        EventManager.OnDialogueStart -= OnDialogueStart;
        EventManager.OnDialogueEnd   -= OnDialogueEnd;
        GameStateManager.OnStateChanged -= OnGameStateChanged;
        RestoreGameplayState();
    }

    private void OnDestroy()
    {
        // OnDisable normally runs first, but cleanup is intentionally idempotent
        // so destroying this component cannot strand the global time scale at 0.
        RestoreGameplayState();
    }

    private void OnDialogueStart(DialogueData _)
    {
        // DialogueManager can restart an active conversation without sending an
        // end event first.  Keep the original state from the first start.
        if (_dialoguePauseActive) return;

        _dialoguePauseActive          = true;
        _previousTimeScale            = Time.timeScale;
        _previousInputEnabled         = _body != null && _body.InputEnabled;
        _previousSplitControllerEnabled = _splitController != null && _splitController.enabled;
        _savedStateManager            = GameStateManager.Instance;
        _hasPreviousGameState         = _savedStateManager != null;
        _previousGameState            = _hasPreviousGameState
            ? _savedStateManager.State
            : GameState.Playing;
        _gameStateChangedDuringDialogue = false;

        // Keep all ring colliders and Rigidbody2D.simulated flags intact.  The
        // time-scale pause stops physics, while InputEnabled prevents a queued
        // jump or movement action from being applied when dialogue closes.
        if (_body != null) _body.InputEnabled = false;
        if (_splitController != null) _splitController.enabled = false;
        Time.timeScale = 0f;
    }

    private void OnDialogueEnd() => RestoreGameplayState();

    private void OnGameStateChanged(GameState state)
    {
        if (!_dialoguePauseActive || !_hasPreviousGameState) return;
        if (GameStateManager.Instance != _savedStateManager) return;

        if (state != _previousGameState)
            _gameStateChangedDuringDialogue = true;
    }

    private void RestoreGameplayState()
    {
        if (!_dialoguePauseActive) return;

        if (_body != null)
            _body.InputEnabled = _previousInputEnabled;

        if (_splitController != null)
            _splitController.enabled = _previousSplitControllerEnabled;

        bool stateManagerOwnsCurrentTimeScale =
            _gameStateChangedDuringDialogue &&
            _savedStateManager != null &&
            GameStateManager.Instance == _savedStateManager;

        if (!stateManagerOwnsCurrentTimeScale)
            Time.timeScale = _previousTimeScale;

        _dialoguePauseActive            = false;
        _savedStateManager              = null;
        _hasPreviousGameState           = false;
        _gameStateChangedDuringDialogue = false;
    }
}
