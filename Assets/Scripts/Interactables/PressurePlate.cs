using UnityEngine;
using UnityEngine.Events;

// Pressure plate that activates when the player walks over it and deactivates when they leave.
// Detection is handled via Physics2D.OverlapBox each frame — no Rigidbody2D required on the player.
//
// SETUP
//   1. Add this component to a GameObject with a BoxCollider2D (the visual footprint of the plate).
//   2. Ensure the Player GameObject is on a Unity Layer named exactly "Player".
//   3. Plate Id is auto-generated on placement — no manual work needed. Override it only if a
//      specific listener needs a human-readable name (e.g. "door_01").
//
// HOOKING UP EVENTS (Inspector)
//   - On Activated   → drag any GameObject/component here and choose a method to call when stepped on.
//   - On Deactivated → same, called when the player steps off (skipped if One Shot is enabled).
//   Example: drag a Door GameObject and call Door.Open() on Activated, Door.Close() on Deactivated.
//
// HOOKING UP EVENTS (Code)
//   Subscribe to EventManager.OnPressurePlateActivated / OnPressurePlateDeactivated.
//   Both pass the plate's id string so you can tell plates apart:
//       EventManager.OnPressurePlateActivated += id => { if (id == "door_01") OpenDoor(); };
//
// ONE SHOT MODE
//   Tick One Shot to keep the plate permanently activated after the first step.
//   Deactivated will never fire, and subsequent steps are ignored.
//
// ANIMATIONS
//   Attach an Animator component and assign a controller with one Bool parameter:
//       "IsPressed" — true while the plate is in its pressed state (including locked one-shot).
//   Recommended state machine:
//       Idle → Press anim (IsPressed = true,  Has Exit Time = false) → Pressed Hold (loop)
//       Pressed Hold → Release anim (IsPressed = false, Has Exit Time = false) → Idle
//   Disable Loop Time on the Press and Release clips — the Pressed Hold state provides the hold.
//   The Animator is optional — the plate works without one.
//
// PRESSED SPRITE
//   Assign Pressed Sprite in the inspector for a guaranteed visual even without a full animator setup.
//   The SpriteRenderer sprite is swapped directly, so the plate always looks correct regardless of
//   the current animator state — useful as a fallback and when quickly toggling on/off.
//
// DETECTION TUNING
//   Detection Height controls how tall the overlap zone is above the plate.
//   Increase it if activation flickers (player collider only briefly intersects the thin plate).
//   Decrease it if nearby objects above the plate trigger it unintentionally.

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
    private bool _activated;
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
            LayerMask.GetMask("Player")
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

        _activated = true;
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

        _activated = false;
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
