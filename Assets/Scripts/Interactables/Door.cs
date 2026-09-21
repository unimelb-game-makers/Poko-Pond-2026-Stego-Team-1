using UnityEngine;

// A colour-coded factory door. Green doors open when the player approaches;
// yellow doors latch green after a toggle trigger; red doors remain green only
// while their hold trigger is active.
[RequireComponent(typeof(Collider2D))]
public class Door : MonoBehaviour, IPropConnectable, IPropActivatable
{
    [Header("Artwork")]
    [SerializeField] private SpriteRenderer doorRenderer;
    [SerializeField] private Sprite greenClosedSprite;
    [SerializeField] private Sprite yellowClosedSprite;
    [SerializeField] private Sprite redClosedSprite;
    [SerializeField] private Sprite[] greenOpeningFrames;
    [SerializeField] private Sprite[] yellowToGreenFrames;
    [SerializeField] private Collider2D blockingCollider;

    [Header("Opening")]
    [SerializeField, Min(0.1f)] private float proximityWidth = 3f;
    [SerializeField, Min(0.1f)] private float proximityHeight = 4f;
    [SerializeField] private Vector2 proximityOffset = new Vector2(0f, 1.5f);
    [SerializeField, Min(0.01f)] private float frameDuration = 0.08f;

    private string _connectionId = "";
    private ConnectionMode _connectionMode = ConnectionMode.Hold;
    private bool _initialUnlocked = true;
    private bool _isUnlocked = true;
    private int _doorFrame;
    private int _unlockTransitionFrame = -1;
    private float _frameTimer;

    public bool IsUnlocked => _isUnlocked;
    public bool IsOpen => greenOpeningFrames != null && greenOpeningFrames.Length > 0
        && _doorFrame >= greenOpeningFrames.Length - 1;

    public void SetConnectionId(string id) => _connectionId = id ?? "";

    public void SetActivationConfig(ConnectionMode mode, bool initialActive)
    {
        _connectionMode = mode;
        _initialUnlocked = initialActive;
        _isUnlocked = initialActive;
        _doorFrame = 0;
        _unlockTransitionFrame = -1;
        ApplyRestingSprite();
        SetBlocked(true);
    }

    private void Awake()
    {
        if (doorRenderer == null) doorRenderer = GetComponentInChildren<SpriteRenderer>();
        if (blockingCollider == null) blockingCollider = GetComponent<Collider2D>();
        ApplyRestingSprite();
        SetBlocked(true);
    }

    private void OnEnable()
    {
        EventManager.OnPressurePlateActivated += OnTriggerActivated;
        EventManager.OnPressurePlateDeactivated += OnTriggerDeactivated;
    }

    private void OnDisable()
    {
        EventManager.OnPressurePlateActivated -= OnTriggerActivated;
        EventManager.OnPressurePlateDeactivated -= OnTriggerDeactivated;
    }

    private void Update()
    {
        if (_unlockTransitionFrame >= 0)
        {
            TickUnlockTransition();
            return;
        }

        if (!_isUnlocked)
        {
            _doorFrame = 0;
            ApplyRestingSprite();
            SetBlocked(true);
            return;
        }

        bool shouldOpen = PlayerIsNearby();
        int lastFrame = greenOpeningFrames == null ? -1 : greenOpeningFrames.Length - 1;
        if (lastFrame < 0)
        {
            if (doorRenderer != null) doorRenderer.sprite = greenClosedSprite;
            SetBlocked(!shouldOpen);
            return;
        }

        int targetFrame = shouldOpen ? lastFrame : 0;
        if (_doorFrame == targetFrame) return;

        // Clear the passage as soon as opening starts. Closing only starts after
        // the player has left the proximity box, and blocks once fully closed.
        if (shouldOpen) SetBlocked(false);
        _frameTimer += Time.deltaTime;
        if (_frameTimer < frameDuration) return;
        _frameTimer = 0f;
        _doorFrame += targetFrame > _doorFrame ? 1 : -1;
        doorRenderer.sprite = greenOpeningFrames[_doorFrame];
        if (_doorFrame == 0) SetBlocked(true);
    }

    private void TickUnlockTransition()
    {
        if (yellowToGreenFrames == null || yellowToGreenFrames.Length == 0)
        {
            _unlockTransitionFrame = -1;
            ApplyRestingSprite();
            return;
        }

        if (doorRenderer != null)
            doorRenderer.sprite = yellowToGreenFrames[_unlockTransitionFrame];

        _frameTimer += Time.deltaTime;
        if (_frameTimer < frameDuration) return;
        _frameTimer = 0f;
        _unlockTransitionFrame++;
        if (_unlockTransitionFrame < yellowToGreenFrames.Length) return;

        _unlockTransitionFrame = -1;
        _doorFrame = 0;
        ApplyRestingSprite();
    }

    private bool PlayerIsNearby()
    {
        Vector2 center = (Vector2)transform.position + proximityOffset;
        return Physics2D.OverlapBox(
            center,
            new Vector2(proximityWidth, proximityHeight),
            0f,
            LayerMask.GetMask("Player", "SoftBodyPoint")) != null;
    }

    private void OnTriggerActivated(string id)
    {
        if (string.IsNullOrEmpty(_connectionId) || id != _connectionId) return;

        bool wasUnlocked = _isUnlocked;
        // A yellow Toggle door is a permanent unlock, not a reversible switch.
        // This also makes multiple one-shot plates on the same connection safe:
        // later activations cannot turn an already-green door yellow again.
        _isUnlocked = _connectionMode == ConnectionMode.Toggle ? true : !_initialUnlocked;
        if (!wasUnlocked && _isUnlocked && _connectionMode == ConnectionMode.Toggle
            && yellowToGreenFrames != null && yellowToGreenFrames.Length > 0)
        {
            _unlockTransitionFrame = 0;
            _frameTimer = 0f;
            SetBlocked(true);
        }
        else
        {
            _unlockTransitionFrame = -1;
            ApplyRestingSprite();
        }
    }

    private void OnTriggerDeactivated(string id)
    {
        if (string.IsNullOrEmpty(_connectionId) || id != _connectionId) return;
        if (_connectionMode == ConnectionMode.Toggle) return;
        _isUnlocked = _initialUnlocked;
        _unlockTransitionFrame = -1;
        _doorFrame = 0;
        ApplyRestingSprite();
        SetBlocked(true);
    }

    private void ApplyRestingSprite()
    {
        if (doorRenderer == null) return;
        if (_isUnlocked)
            doorRenderer.sprite = greenClosedSprite;
        else
            doorRenderer.sprite = _connectionMode == ConnectionMode.Toggle
                ? yellowClosedSprite
                : redClosedSprite;
    }

    private void SetBlocked(bool blocked)
    {
        if (blockingCollider != null) blockingCollider.enabled = blocked;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isUnlocked ? Color.green : Color.red;
        Gizmos.DrawWireCube(
            (Vector2)transform.position + proximityOffset,
            new Vector3(proximityWidth, proximityHeight, 0f));
    }
}
