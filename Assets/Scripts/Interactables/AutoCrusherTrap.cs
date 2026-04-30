using System.Collections;
using UnityEngine;

public class AutoCrusherTrap : MonoBehaviour, IPropConnectable
{
    [Header("Trigger")]
    [Tooltip("Plate Id of the PressurePlate that PAUSES this crusher. Must match exactly.")]
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
    private SoftBodyPlayer _player;
    private PlayerLife _playerLife;
    private bool _paused;

    private void Awake()
    {
        _sprite = GetComponent<SpriteRenderer>();
        if (_sprite != null && frames != null && frames.Length > 0)
            _sprite.sprite = frames[0];
    }

    private void Start()
    {
        _player = Object.FindFirstObjectByType<SoftBodyPlayer>();
        if (_player == null)
        {
            Debug.LogError("[AutoCrusherTrap] No SoftBodyPlayer found in scene — crusher will not kill.", this);
            return;
        }
        _playerLife = _player.GetComponent<PlayerLife>();
        if (_playerLife == null)
            Debug.LogError("[AutoCrusherTrap] SoftBodyPlayer is missing a PlayerLife component.", _player);

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
        if (plateId == triggerPlateId) _paused = true;
    }

    private void HandlePlateDeactivated(string plateId)
    {
        if (plateId == triggerPlateId) _paused = false;
    }

    private IEnumerator CycleLoop()
    {
        while (true)
        {
            yield return WaitWhilePaused();

            // Slam: frames 1→4
            float slamFrameTime = slamDuration / SlamFrameCount;
            for (int i = 0; i < SlamFrameCount && i < frames.Length; i++)
            {
                _sprite.sprite = frames[i];
                yield return new WaitForSeconds(slamFrameTime);
            }

            // Kill check at impact
            Vector2 worldCenter = (Vector2)transform.position + crushCenter;
            if (_player != null && _playerLife != null)
            {
                bool inZone = IsPlayerInCrushZone(worldCenter);
                if (inZone) _playerLife.Kill();
            }

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

    private IEnumerator WaitWhilePaused()
    {
        while (_paused)
            yield return null;
    }

    private bool IsPlayerInCrushZone(Vector2 zoneCenter)
    {
        Vector2 half = crushSize * 0.5f;
        Rect zone = new Rect(zoneCenter - half, crushSize);
        if (zone.Contains(_player.Center)) return true;
        foreach (Rigidbody2D pt in _player.Points)
            if (zone.Contains(pt.position)) return true;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _paused ? Color.cyan : new Color(1f, 0.5f, 0f, 0.8f);
        Vector2 worldCenter = (Vector2)transform.position + crushCenter;
        Gizmos.DrawWireCube(worldCenter, crushSize);
    }
}
