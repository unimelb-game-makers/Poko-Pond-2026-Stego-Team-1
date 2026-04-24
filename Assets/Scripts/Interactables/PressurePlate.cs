using UnityEngine;
using UnityEngine.Events;

/*
 * OVERVIEW
 *   Pressure plate that activates when the player steps on it and deactivates when
 *   they leave.  Detection uses Physics2D.OverlapBox each frame — no trigger collider
 *   or Rigidbody2D on the player is required.
 *
 * SETUP
 *   1. Add this component to a GameObject that has a BoxCollider2D (the plate footprint).
 *   2. Detection works with both the "Player" layer and the softbody "SoftBodyPoint"
 *      layer, so it responds correctly whether the player is whole or split.
 *   3. Plate Id is auto-generated (GUID) when this component is first added in the
 *      Editor.  Override it only when a listener needs a stable, human-readable name.
 *
 * HOOKING UP EVENTS — Inspector
 *   On Activated   → drag any GameObject/component and choose a method to call.
 *   On Deactivated → same, fires when the player leaves (skipped in One Shot mode).
 *   Example: drag a Door and wire Door.Open() / Door.Close() to the two events.
 *
 * HOOKING UP EVENTS — Code
 *   Subscribe via EventManager:
 *       EventManager.OnPressurePlateActivated   += id => { if (id == "door_01") OpenDoor(); };
 *       EventManager.OnPressurePlateDeactivated += id => { ... };
 *   Both events pass the plate's id so multiple plates can share one handler.
 *
 * ONE SHOT MODE
 *   Tick One Shot to lock the plate in its pressed state permanently after the first
 *   step.  OnDeactivated never fires, and re-treading the plate does nothing.
 *
 * ANIMATIONS
 *   Attach an Animator with a Bool parameter named "IsPressed":
 *       Idle → Press anim  (IsPressed = true,  Has Exit Time = false) → Pressed Hold (loop)
 *       Pressed Hold → Release anim  (IsPressed = false, Has Exit Time = false) → Idle
 *   Disable Loop Time on the Press and Release clips — Pressed Hold provides the hold.
 *   The Animator is optional; the plate functions correctly without one.
 *
 * PRESSED SPRITE
 *   Assign Pressed Sprite for a guaranteed visual without a full Animator.  The sprite
 *   is swapped directly on the SpriteRenderer, so the plate always looks correct
 *   regardless of animator state — handy as a fallback or for simple on/off plates.
 *
 * DETECTION TUNING
 *   Detection Height controls how tall the overlap zone is above the plate surface.
 *   Increase it if activation flickers (the player barely clips the thin plate).
 *   Decrease it if objects above the plate trigger it unintentionally.
 */

public class PressurePlate : MonoBehaviour
{
    [Tooltip("Auto-generated on placement. Override only if you need a human-readable id for a specific listener.")]
    [SerializeField] private string plateId;

    [Tooltip("If true, the plate stays activated after the player leaves and cannot be re-triggered.")]
    [SerializeField] private bool oneShot = false;

    [Tooltip("Height of the detection zone above the plate. Increase if activation flickers.")]
    [SerializeField] private float detectionHeight = 2f;

    [Tooltip("Sprite shown while the plate is pressed. Leave empty to rely solely on animations.")]
    [SerializeField] private Sprite pressedSprite;

    [Header("Inspector Callbacks")]
    [Tooltip("Called the moment the player steps on the plate.")]
    [SerializeField] private UnityEvent onActivated;

    [Tooltip("Called when the player steps off (ignored if One Shot is true).")]
    [SerializeField] private UnityEvent onDeactivated;

    private Collider2D _col;
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private Sprite _normalSprite;
    private bool _playerOver;
    private bool _lockedPressed; // set on first exit when oneShot=true — plate stays pressed permanently

    private static readonly int IsPressedHash = Animator.StringToHash("IsPressed");

    // Assigns a unique id the moment this component is added in the editor
    private void Reset() => plateId = System.Guid.NewGuid().ToString();

    private void Awake()
    {
        _col            = GetComponent<Collider2D>();
        _animator       = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
            _normalSprite = _spriteRenderer.sprite;
    }

    // Polls for player overlap each frame using a box cast above the plate's collider bounds
    private void Update()
    {
        var bounds          = _col.bounds;
        var detectionCenter = new Vector2(bounds.center.x, bounds.center.y + detectionHeight * 0.5f);
        var detectionSize   = new Vector2(bounds.size.x, detectionHeight);

        bool playerPresent = Physics2D.OverlapBox(
            detectionCenter,
            detectionSize,
            0f,
            LayerMask.GetMask("Player", "SoftBodyPoint")
        );

        if (playerPresent && !_playerOver)
            OnPlayerEnter();
        else if (!playerPresent && _playerOver)
            OnPlayerExit();
    }

    // Fires activation events the first frame the player is detected over the plate
    private void OnPlayerEnter()
    {
        _playerOver = true;
        if (_lockedPressed) return; // one-shot already fired, nothing left to do

        SetPressedVisual(true);
        EventManager.PressurePlateActivated(plateId);
        onActivated?.Invoke();
        Debug.Log($"[PressurePlate] '{name}' ({plateId}) activated.");
    }

    // Fires deactivation events the first frame the player is no longer detected
    private void OnPlayerExit()
    {
        _playerOver = false;

        if (oneShot)
        {
            // Lock pressed state permanently — visual stays pressed, no deactivation event
            _lockedPressed = true;
            return;
        }

        SetPressedVisual(false);
        EventManager.PressurePlateDeactivated(plateId);
        onDeactivated?.Invoke();
        Debug.Log($"[PressurePlate] '{name}' ({plateId}) deactivated.");
    }

    // Drives both the Animator bool and the SpriteRenderer sprite so visuals are always correct
    private void SetPressedVisual(bool pressed)
    {
        if (_animator != null)
            _animator.SetBool(IsPressedHash, pressed);

        if (_spriteRenderer != null && pressedSprite != null)
            _spriteRenderer.sprite = pressed ? pressedSprite : _normalSprite;
    }
}
