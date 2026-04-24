using System.Collections;
using UnityEngine;

// Crusher trap that slams down when a linked pressure plate is activated, then slowly retracts.
// Frames 1–3 are the windup, frame 4 is the impact, frames 5–26 are the slow retract.
// The kill check fires once, the instant frame 4 is on screen — the player is safe during
// windup and retract.
//
// SETUP
//   1. Add this component to a GameObject with a SpriteRenderer.
//   2. Slice the crusher sprite sheet (Sprite Editor → Slice → Grid By Cell Count, 26×1).
//   3. Drag all 26 sliced sprites into the Frames array in order (frame 1 first, frame 26 last).
//   4. Place a PressurePlate in the scene. Copy its Plate Id and paste it into Trigger Plate Id
//      below — or set the plate's id to something readable like "crusher_01" and reuse that here.
//   5. Position the crusher where it should slam. Use the orange gizmo (visible when selected)
//      to size the Crush Zone so it covers exactly the area under the crusher that should kill.
//
// BEHAVIOUR
//   When the linked plate activates, frames 1→4 play over Slam Duration. At the end of the slam a
//   single overlap check fires — any collider on the Player layer inside the crush zone is crushed.
//   Frames 5→26 then play over Retract Duration, during which the crusher is harmless and cannot
//   be retriggered. When retract finishes the crusher returns to frame 1 and is armed again.
//
// DETECTION TUNING
//   The orange gizmo shows the crush zone while idle and turns red while cycling.
//   Crush Zone Center is a local-space offset from the crusher's transform.

public class CrusherTrap : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("Plate Id of the PressurePlate that activates this crusher. Must match exactly.")]
    [SerializeField] private string triggerPlateId;

    [Header("Animation Frames")]
    [Tooltip("All 26 sprite sheet frames, in order. 1–3 windup, 4 slam, 5–26 retract.")]
    [SerializeField] private Sprite[] frames;

    [Header("Timing")]
    [Tooltip("Total duration of the slam (frames 1→4). Frames play evenly across this window.")]
    [SerializeField] private float slamDuration = 0.15f;

    [Tooltip("Total duration of the retract (frames 5→26). The crusher cannot retrigger during this time.")]
    [SerializeField] private float retractDuration = 3f;

    [Header("Crush Zone")]
    [Tooltip("Local-space offset of the zone where the player is crushed.")]
    [SerializeField] private Vector2 crushCenter = new Vector2(0f, -1f);

    [Tooltip("Size of the zone where the player is crushed.")]
    [SerializeField] private Vector2 crushSize = new Vector2(2f, 1f);

    private const int SlamFrameCount = 4; // frames 1–4 belong to the slam phase

    private SpriteRenderer _sprite;
    private bool _cycling;
    private SoftBodyPlayer _player;
    private PlayerLife _playerLife;

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
            Debug.LogError("[CrusherTrap] No SoftBodyPlayer found in scene — crusher will not kill.", this);
            return;
        }
        _playerLife = _player.GetComponent<PlayerLife>();
        if (_playerLife == null)
            Debug.LogError("[CrusherTrap] SoftBodyPlayer GameObject is missing a PlayerLife component — crusher will not kill. Add PlayerLife to the Player prefab.", _player);
    }

    private void OnEnable()  => EventManager.OnPressurePlateActivated += HandlePlateActivated;
    private void OnDisable() => EventManager.OnPressurePlateActivated -= HandlePlateActivated;

    // Starts one slam cycle if the activated plate is our trigger and we aren't already cycling
    private void HandlePlateActivated(string plateId)
    {
        if (plateId != triggerPlateId || _cycling) return;
        StartCoroutine(SlamCycle());
    }

    // Plays slam → kill check → retract, then arms the crusher again
    private IEnumerator SlamCycle()
    {
        _cycling = true;

        // Windup + slam: play frames 1→4 evenly across slamDuration
        float slamFrameTime = slamDuration / SlamFrameCount;
        for (int i = 0; i < SlamFrameCount && i < frames.Length; i++)
        {
            _sprite.sprite = frames[i];
            yield return new WaitForSeconds(slamFrameTime);
        }

        // Kill check fires the instant frame 4 is on screen — the moment of impact.
        // The softbody player has no collider on the Player layer, so we check geometry
        // directly against the player centroid and all ring-point positions.
        Vector2 worldCenter = (Vector2)transform.position + crushCenter;
        if (_player != null && _playerLife != null)
        {
            bool inZone = IsPlayerInCrushZone(worldCenter);
            Debug.Log($"[CrusherTrap] Impact check — player center: {_player.Center}, zone center: {worldCenter}, size: {crushSize}, in zone: {inZone}");
            if (inZone) _playerLife.Kill();
        }

        // Retract: play frames 5→26 evenly across retractDuration (harmless during this phase)
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

        // Return to idle pose
        if (frames.Length > 0)
            _sprite.sprite = frames[0];

        _cycling = false;
    }

    // Returns true if the player centroid or any ring point lies inside the crush zone.
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
        Gizmos.color = _cycling ? Color.red : new Color(1f, 0.5f, 0f, 0.8f);
        Vector2 worldCenter = (Vector2)transform.position + crushCenter;
        Gizmos.DrawWireCube(worldCenter, crushSize);
    }
}
