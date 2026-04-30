using UnityEngine;
using UnityEngine.Events;

/*
 * OVERVIEW
 *   Pressure plate that activates when the player steps on it and deactivates when
 *   they leave.  Detection uses Physics2D.OverlapBox each frame — no trigger collider
 *   or Rigidbody2D on the player is required.
 *
 * PLACEMENT — TILEMAP SYSTEM
 *   This prefab is placed via the Props tilemap using a PropTile asset, not by dragging
 *   it into the scene directly.  Because it is spawned at runtime by PropTilemapSpawner,
 *   the Inspector callbacks (On Activated / On Deactivated) cannot be wired up in the
 *   Editor.  Use one of the two code-based approaches below instead.
 *
 * HOOKING UP EVENTS — recommended (EventManager)
 *   Any script in the scene can subscribe to the global event bus:
 *       private void OnEnable()  => EventManager.OnPressurePlateActivated   += HandleActivated;
 *       private void OnDisable() => EventManager.OnPressurePlateActivated   -= HandleActivated;
 *       private void HandleActivated(string id) { if (id == "plate_01") DoSomething(); }
 *   Use the plate's Plate Id string to filter — set a stable, human-readable id on the
 *   prefab (e.g. "factory_crusher_01") so listeners can reference it by name.
 *   CrusherTrap and AutoCrusherTrap already use this pattern via their Trigger Plate Id field.
 *
 * HOOKING UP EVENTS — alternative (Inspector callbacks at runtime)
 *   If you need to wire a specific scene object to the plate without writing a new script,
 *   add a small connector MonoBehaviour to that object:
 *       void OnEnable()  => EventManager.OnPressurePlateActivated += id => { if (id == "plate_01") target.Open(); };
 *       void OnDisable() => EventManager.OnPressurePlateActivated -= ...;
 *   This is equivalent to the On Activated Unity Event but works with runtime-spawned plates.
 *
 * PLATE ID
 *   Set a fixed, human-readable Plate Id directly on the prefab in the Project window
 *   (e.g. "factory_crusher_01").  Any listener that needs to respond to this plate
 *   references the same string.  Ids only need to be unique per scene.
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
 *   IMPORTANT: set the SpriteRenderer's Order in Layer higher than the tilemaps so the
 *   animation is visible when the prefab is spawned on top of a tilemap.
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
 *   The zone is cached at Start from the collider's world bounds — it will not shift
 *   even if the Animator moves or resizes the collider during an animation.
 */

public class PressurePlate : MonoBehaviour, IPropConnectable
{
    [Tooltip("Stable, human-readable id used by EventManager listeners. Must match the Trigger Plate Id on any linked CrusherTrap.")]
    [SerializeField] private string plateId;

    [Tooltip("If true, the plate stays activated after the player leaves and cannot be re-triggered.")]
    [SerializeField] private bool oneShot = false;

    [Tooltip("Height of the detection zone above the plate. Increase if activation flickers.")]
    [SerializeField] private float detectionHeight = 2f;

    [Tooltip("Sprite shown while the plate is pressed. Leave empty to rely solely on animations.")]
    [SerializeField] private Sprite pressedSprite;

    [Header("Inspector Callbacks")]
    [Tooltip("Called the moment the player steps on the plate. Not usable when spawned via tilemap — use EventManager instead.")]
    [SerializeField] private UnityEvent onActivated;

    [Tooltip("Called when the player steps off (ignored if One Shot is true). Not usable when spawned via tilemap — use EventManager instead.")]
    [SerializeField] private UnityEvent onDeactivated;

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private Sprite _normalSprite;
    private bool _playerOver;
    private bool _lockedPressed; // set on first exit when oneShot=true — plate stays pressed permanently

    // Detection zone cached at Start so Animator-driven transform/collider changes can't affect it.
    private Vector2 _detectionCenter;
    private Vector2 _detectionSize;

    private static readonly int IsPressedHash = Animator.StringToHash("IsPressed");

    // Called by PropTilemapSpawner when spawned from a PropTile with a connectionId.
    // Overrides the prefab's default plateId so no separate prefab is needed per connection.
    public void SetConnectionId(string id) => plateId = id;

    // Assigns a unique id the moment this component is added in the editor
    private void Reset() => plateId = System.Guid.NewGuid().ToString();

    private void Awake()
    {
        _animator       = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
            _normalSprite = _spriteRenderer.sprite;
    }

    private void Start()
    {
        // Cache detection zone from the collider's initial world bounds so animations
        // that move or disable the collider cannot shift or break the detection area.
        var col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogError("[PressurePlate] No Collider2D found — detection will not work.", this);
            enabled = false;
            return;
        }
        var bounds = col.bounds;
        _detectionCenter = new Vector2(bounds.center.x, bounds.center.y + detectionHeight * 0.5f);
        _detectionSize   = new Vector2(bounds.size.x, detectionHeight);
    }

    // Polls for player overlap each frame using a fixed box zone cached at Start
    private void Update()
    {
        bool playerPresent = Physics2D.OverlapBox(
            _detectionCenter,
            _detectionSize,
            0f,
            LayerMask.GetMask("Player", "SoftBodyPoint")
        );

        if (playerPresent && !_playerOver)
            OnPlayerEnter();
        else if (!playerPresent && _playerOver)
            OnPlayerExit();
    }

    private void OnPlayerEnter()
    {
        _playerOver = true;
        if (_lockedPressed) return; // one-shot already fired, nothing left to do

        SetPressedVisual(true);
        EventManager.PressurePlateActivated(plateId);
        onActivated?.Invoke();
        Debug.Log($"[PressurePlate] '{name}' ({plateId}) activated.");
    }

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
