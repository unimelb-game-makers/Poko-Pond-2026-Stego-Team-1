using UnityEngine;

/*
 * OVERVIEW
 *   Condenser prop — currently logs when the player enters/exits the left-side detection zone.
 *   Gas-cloud transformation is not implemented; this is a placeholder for the future mechanic.
 *   Placed via the Props tilemap using a PropTile asset; spawned at runtime by PropTilemapSpawner.
 *
 * ENTRY DIRECTION
 *   The entrance is on the left side of the condenser sprite. The detection zone is
 *   positioned at the left edge of the BoxCollider2D so only a gas cloud approaching
 *   from the left triggers condensation. Tune Entry Zone Width and Entry Zone Height
 *   in the Inspector to align with the opening in the sprite art.
 *
 * ACTIVATION
 *   Initial state and trigger behaviour (Hold / Toggle) are set by PropTilemapSpawner
 *   at spawn time via SetActivationConfig. Configure them in the Props tilemap's
 *   Sync Cell List in the Inspector — no fields need to be set on the prefab directly.
 *
 * ANIMATION (optional)
 *   Attach an Animator with a trigger parameter named "Condense".
 *   The trigger fires the instant condensation occurs. Loop Time must be unchecked
 *   on the Condense clip so it plays once and returns to the idle state.
 *
 * SETUP
 *   1. Add a SpriteRenderer, Animator (optional), and BoxCollider2D to the prefab.
 *   2. Set the GameObject layer to "Props".
 *   3. Size the BoxCollider2D to the full condenser sprite footprint.
 *   4. Tune Entry Zone Width/Height so the blue gizmo aligns with the left-side opening.
 *   5. Assign the prefab to a PropTile asset; paint it on the Props tilemap.
 */
public class Condenser : MonoBehaviour, IPropConnectable, IPropActivatable
{
    [Header("Entry Zone")]
    [Tooltip("Width of the left-side detection zone. Should match the width of the condenser opening in the sprite.")]
    [SerializeField] private float entryZoneWidth  = 0.8f;
    [Tooltip("Height of the left-side detection zone. Should match the height of the condenser opening.")]
    [SerializeField] private float entryZoneHeight = 1.2f;

    // ── Private ─────────────────────────────────────────────────────────────

    private bool             _isActive       = true;  // default until SetActivationConfig is called
    private bool             _initialActive  = true;
    private ConnectionMode   _connectionMode = ConnectionMode.Hold;
    private string           _connectionId   = "";
    private Animator         _animator;
    private Vector2          _entryCenter;
    private Vector2          _entrySize;
    private bool             _playerOver;

    private static readonly int CondenseTriggerHash = Animator.StringToHash("Condense");

    // Called by PropTilemapSpawner — sets the trigger id this condenser listens for.
    public void SetConnectionId(string id) => _connectionId = id;

    // Called by PropTilemapSpawner — sets initial state and how triggers affect this condenser.
    public void SetActivationConfig(ConnectionMode mode, bool initialActive)
    {
        _connectionMode = mode;
        _initialActive  = initialActive;
        _isActive       = initialActive;
    }

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        var col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogError("[Condenser] No Collider2D found — detection will not work. Add a BoxCollider2D to the prefab.", this);
            enabled = false;
            return;
        }

        // Entry zone is placed at the left edge of the collider bounds
        var b        = col.bounds;
        _entryCenter = new Vector2(b.min.x + entryZoneWidth * 0.5f, b.center.y);
        _entrySize   = new Vector2(entryZoneWidth, entryZoneHeight);
    }

    private void OnEnable()
    {
        EventManager.OnPressurePlateActivated   += OnTriggerActivated;
        EventManager.OnPressurePlateDeactivated += OnTriggerDeactivated;
    }

    private void OnDisable()
    {
        EventManager.OnPressurePlateActivated   -= OnTriggerActivated;
        EventManager.OnPressurePlateDeactivated -= OnTriggerDeactivated;
    }

    private void OnTriggerActivated(string id)
    {
        if (string.IsNullOrEmpty(_connectionId) || id != _connectionId) return;
        _isActive = _connectionMode == ConnectionMode.Toggle ? !_isActive : !_initialActive;
    }

    private void OnTriggerDeactivated(string id)
    {
        if (string.IsNullOrEmpty(_connectionId) || id != _connectionId) return;
        // Toggle mode ignores release — state was already flipped on press
        if (_connectionMode == ConnectionMode.Toggle) return;
        _isActive = _initialActive;
    }

    private void Update()
    {
        if (!_isActive) { _playerOver = false; return; }

        bool present = Physics2D.OverlapBox(
            _entryCenter, _entrySize, 0f,
            LayerMask.GetMask("Player", "SoftBodyPoint")) != null;

        if (present && !_playerOver)
        {
            _playerOver = true;
            Debug.Log($"[Condenser] Player entered (id='{_connectionId}')", this);
            if (_animator != null) _animator.SetTrigger(CondenseTriggerHash);
        }
        else if (!present && _playerOver)
        {
            _playerOver = false;
            Debug.Log($"[Condenser] Player exited (id='{_connectionId}')", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isActive
            ? new Color(0f, 0.8f, 1f, 0.8f)
            : new Color(0.5f, 0.5f, 0.5f, 0.4f);
        Gizmos.DrawWireCube(_entryCenter, _entrySize);
    }
}
