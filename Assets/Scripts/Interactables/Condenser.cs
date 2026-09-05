using UnityEngine;

/*
 * OVERVIEW
 *   Condenser/freezer transforms the player at its left-side intake.
 *   Solid output is placed on the floor to the left, clear of the machine,
 *   with no launch velocity. Its prefab footprint is a trigger so ice can
 *   return to a plate and subsequently pass the machine without jumping.
 *   Placed via the Props tilemap using a PropTile asset; spawned at runtime by PropTilemapSpawner.
 *
 * ENTRY DIRECTION
 *   The entrance is on the left side of the condenser sprite. The detection zone is
 *   positioned at the left edge of the Collider2D so a player approaching
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
 *   1. Add a SpriteRenderer, Animator (optional), and Collider2D to the prefab.
 *   2. Keep the footprint out of the Ground/Platform layers.
 *   3. Size the collider to the footprint and enable Is Trigger for a walk-through station.
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

    [Header("Solid Output")]
    [Tooltip("Gap between the released ice body's right edge and the machine's left edge, in world units.")]
    [SerializeField, Min(0.05f)] private float solidExitClearance = 0.15f;

    // Solid construction in SoftBodyPlayer uses a one-unit square of points.
    // Include the point collider radius when clearing the machine and floor.
    public Vector2 GetSolidExitPosition(float pointRadius)
    {
        Bounds footprint = GetComponent<Collider2D>().bounds;
        float halfExtent = 0.5f + pointRadius;
        float exitX = footprint.min.x - halfExtent - solidExitClearance;
        // Thin-platform artwork does not fill a complete tile. Use its actual
        // supporting surface instead of assuming the machine's cell bottom is ground.
        RaycastHit2D floor = Physics2D.Raycast(new Vector2(exitX, footprint.min.y + 0.25f),
            Vector2.down, 2f, LayerMask.GetMask("Ground", "Platform"));
        float floorY = floor.collider != null ? floor.point.y : footprint.min.y;
        return new Vector2(exitX, floorY + halfExtent + 0.05f);
    }

    // ── Private ─────────────────────────────────────────────────────────────

    private bool             _isActive       = true;  // default until SetActivationConfig is called
    private bool             _initialActive  = true;
    private ConnectionMode   _connectionMode = ConnectionMode.Hold;
    private string           _connectionId   = "";
    private Animator         _animator;
    private Vector2          _entryCenter;
    private Vector2          _entrySize;
    private bool             _playerOver;
    private GameObject       _player;
    
    // Public Interface ──────────────────────────────────────────────────
    public LayerMask PlayerSoftBodyLayer;
    public PlayerBodyState valueToChangeTo;

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
        
        _player = GameObject.FindWithTag("Player");
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
            SoftBodyPlayer player = _player != null ? _player.GetComponent<SoftBodyPlayer>() : null;
            if (player == null) return;
            if (valueToChangeTo == PlayerBodyState.Solid)
                player.changeBodyState(valueToChangeTo, GetSolidExitPosition(player.pointRadius), Vector2.zero);
            else
                player.changeBodyState(valueToChangeTo, new Vector2(transform.position.x, transform.position.y + 0.5f));
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
