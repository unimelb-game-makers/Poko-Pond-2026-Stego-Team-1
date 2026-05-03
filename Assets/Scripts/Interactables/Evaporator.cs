using UnityEngine;

/*
 * OVERVIEW
 *   Evaporator prop — converts a SoftBodyPlayer into a GasCloud on contact.
 *   Placed via the Props tilemap using a PropTile asset; spawned at runtime by PropTilemapSpawner.
 *   Burst velocity is always straight up.
 *
 * TOGGLE (optional)
 *   Set a Connection ID on the PropTile that matches a PressurePlate's Connection ID.
 *   When the plate is activated, the evaporator flips from its default state (startsActive).
 *   Leave Connection ID empty for an always-on evaporator.
 *
 * ANIMATION
 *   Attach an Animator with a Bool parameter named "IsActive".
 *   IsActive = true  → idle looping animation plays.
 *   IsActive = false → transitions to an Off state (static / dimmed).
 *   The parameter is driven automatically on Start and whenever the toggle state changes.
 *
 * DETECTION
 *   OverlapBoxAll on "Player" and "SoftBodyPoint" layers each frame.
 *   Conversion is instant. PlayerSplitController.TryEvaporate guards against
 *   double-conversion (returns false if the droplet is already a gas cloud).
 *
 * SETUP
 *   1. Add a SpriteRenderer, Animator, and BoxCollider2D to the prefab.
 *   2. Size the BoxCollider2D to match the evaporator surface footprint.
 *   3. Assign the prefab to a PropTile asset; paint it on the Props tilemap.
 */
public class Evaporator : MonoBehaviour, IPropConnectable, IPropActivatable
{
    [Header("Burst")]
    [Tooltip("Speed of the upward velocity burst applied to the gas cloud at the moment of evaporation.")]
    [SerializeField] private float burstSpeed = 4f;

    [Header("Detection")]
    [Tooltip("Height of the overlap zone above the collider top. Increase if activation flickers on approach.")]
    [SerializeField] private float detectionHeight = 0.6f;

    // ── Private ─────────────────────────────────────────────────────────────

    private bool                  _isActive        = true;  // default until SetActivationConfig is called
    private bool                  _initialActive   = true;
    private ConnectionMode        _connectionMode  = ConnectionMode.Hold;
    private string                _connectionId    = "";
    private PlayerSplitController _controller;
    private Animator              _animator;
    private Vector2               _detectionCenter;
    private Vector2               _detectionSize;

    private static readonly int IsActiveHash = Animator.StringToHash("IsActive");

    // Called by PropTilemapSpawner — sets the trigger id this evaporator listens for.
    public void SetConnectionId(string id) => _connectionId = id;

    // Called by PropTilemapSpawner — sets initial state and how triggers affect this evaporator.
    public void SetActivationConfig(ConnectionMode mode, bool initialActive)
    {
        _connectionMode = mode;
        _initialActive  = initialActive;
        _isActive       = initialActive;
        SetAnimatorState(_isActive);
    }

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        _controller = Object.FindFirstObjectByType<PlayerSplitController>();
        if (_controller == null)
            Debug.LogError("[Evaporator] No PlayerSplitController found in scene.", this);

        // Drive initial animator state — covers the case where SetActivationConfig was not called
        SetAnimatorState(_isActive);

        var col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogError("[Evaporator] No Collider2D found — detection will not work. Add a BoxCollider2D to the prefab.", this);
            enabled = false;
            return;
        }

        // Cache the detection zone above the collider so Animator-driven transforms can't shift it
        var b            = col.bounds;
        _detectionCenter = new Vector2(b.center.x, b.max.y + detectionHeight * 0.5f);
        _detectionSize   = new Vector2(b.size.x,   detectionHeight);
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
        SetAnimatorState(_isActive);
    }

    private void OnTriggerDeactivated(string id)
    {
        if (string.IsNullOrEmpty(_connectionId) || id != _connectionId) return;
        // Toggle mode ignores release — state was already flipped on press
        if (_connectionMode == ConnectionMode.Toggle) return;
        _isActive = _initialActive;
        SetAnimatorState(_isActive);
    }

    private void SetAnimatorState(bool active)
    {
        if (_animator != null)
            _animator.SetBool(IsActiveHash, active);
    }

    private void Update()
    {
        if (!_isActive || _controller == null) return;

        var hits = Physics2D.OverlapBoxAll(
            _detectionCenter,
            _detectionSize,
            0f,
            LayerMask.GetMask("Player", "SoftBodyPoint")
        );

        foreach (var col in hits)
        {
            // Ring-point GOs are not parented to SoftBodyPlayer, so use the ref component instead
            var pointRef = col.GetComponent<SoftBodyPointRef>();
            var sp = pointRef != null ? pointRef.owner : col.GetComponent<SoftBodyPlayer>();
            if (sp == null) continue;
            if (_controller.TryEvaporate(sp, new Vector2(0f, burstSpeed)))
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isActive
            ? new Color(1f, 0.55f, 0f, 0.8f)
            : new Color(0.5f, 0.5f, 0.5f, 0.4f);
        Gizmos.DrawWireCube(_detectionCenter, _detectionSize);
    }
}
