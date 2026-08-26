using System.Collections;
using UnityEngine;

public class AutoCrusherTrap : MonoBehaviour, IPropConnectable, IPropActivatable
{
    [Header("Trigger")]
    [Tooltip("Plate Id of the PressurePlate linked to this crusher. Must match exactly.")]
    [SerializeField] private string triggerPlateId;

    [Header("Animation Frames")]
    [Tooltip("All 26 sprite sheet frames, in order. 1–3 windup, 4 slam, 5–26 retract.")]
    [SerializeField] private Sprite[] frames;

    [Header("Timing")]
    [Tooltip("Total duration of the slam (frames 1→4).")]
    [SerializeField] private float slamDuration = 0.15f;

    [Tooltip("Total duration of the retract (frames 5→26).")]
    [SerializeField] private float retractDuration = 3f;

    [Tooltip("Pause at idle pose before starting the next slam cycle.")]
    [SerializeField] private float idlePause = 0.5f;

    [Header("Crush Zone")]
    [Tooltip("Local-space offset of the zone where the player is crushed.")]
    [SerializeField] private Vector2 crushCenter = new Vector2(0f, -1f);

    [Tooltip("Size of the zone where the player is crushed.")]
    [SerializeField] private Vector2 crushSize = new Vector2(2f, 1f);

    private const int SlamFrameCount = 4;

    // Called by PropTilemapSpawner — sets the plate id this crusher listens for.
    public void SetConnectionId(string id) => triggerPlateId = id;

    private SpriteRenderer _sprite;
    private bool _isActive = true;
    private bool _initialActive = true;
    private ConnectionMode _connectionMode = ConnectionMode.Hold;

    // Called by PropTilemapSpawner — sets initial state and how the linked
    // pressure plate affects this crusher.  The defaults preserve the original
    // behaviour: the crusher starts cycling and a held plate pauses it.
    public void SetActivationConfig(ConnectionMode mode, bool initialActive)
    {
        _connectionMode = mode;
        _initialActive = initialActive;
        _isActive = initialActive;
    }

    private void Awake()
    {
        _sprite = GetComponent<SpriteRenderer>();
        if (_sprite != null && frames != null && frames.Length > 0)
            _sprite.sprite = frames[0];
    }

    private void Start()
    {
        StartCoroutine(CycleLoop());
    }

    private void OnEnable()
    {
        EventManager.OnPressurePlateActivated += HandlePlateActivated;
        EventManager.OnPressurePlateDeactivated += HandlePlateDeactivated;
    }

    private void OnDisable()
    {
        EventManager.OnPressurePlateActivated -= HandlePlateActivated;
        EventManager.OnPressurePlateDeactivated -= HandlePlateDeactivated;
    }

    private void HandlePlateActivated(string plateId)
    {
        if (plateId != triggerPlateId) return;

        // Hold mode matches the existing pause-on-plate behaviour when the
        // default state is active.  Toggle mode also supports an initially
        // inactive crusher that starts on the first plate press.
        _isActive = _connectionMode == ConnectionMode.Toggle
            ? !_isActive
            : !_initialActive;
    }

    private void HandlePlateDeactivated(string plateId)
    {
        if (plateId != triggerPlateId || _connectionMode == ConnectionMode.Toggle)
            return;

        _isActive = _initialActive;
    }

    private IEnumerator CycleLoop()
    {
        while (true)
        {
            yield return WaitWhileInactive();

            if (frames == null || frames.Length == 0)
            {
                yield return null;
                continue;
            }

            // Slam: frames 1→4
            float slamFrameTime = slamDuration / SlamFrameCount;
            for (int i = 0; i < SlamFrameCount && i < frames.Length; i++)
            {
                _sprite.sprite = frames[i];
                yield return new WaitForSeconds(slamFrameTime);
            }

            // Kill check at impact
            Vector2 worldCenter = (Vector2)transform.position + crushCenter;
            PlayerLife.KillAllSolidInBox(worldCenter, crushSize);

            // Retract: frames 5→26
            int retractFrameCount = Mathf.Max(0, frames.Length - SlamFrameCount);
            if (retractFrameCount > 0)
            {
                float retractFrameTime = retractDuration / retractFrameCount;
                for (int i = SlamFrameCount; i < frames.Length; i++)
                {
                    _sprite.sprite = frames[i];
                    yield return new WaitForSeconds(retractFrameTime);
                }
            }

            // Return to idle and pause briefly before next cycle
            if (frames.Length > 0)
                _sprite.sprite = frames[0];

            yield return new WaitForSeconds(idlePause);
        }
    }

    private IEnumerator WaitWhileInactive()
    {
        while (!_isActive)
            yield return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isActive ? new Color(1f, 0.5f, 0f, 0.8f) : Color.cyan;
        Vector2 worldCenter = (Vector2)transform.position + crushCenter;
        Gizmos.DrawWireCube(worldCenter, crushSize);
    }
}
