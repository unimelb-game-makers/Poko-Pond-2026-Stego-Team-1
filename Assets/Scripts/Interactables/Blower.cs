using System.Collections.Generic;
using UnityEngine;

// An always-on tilemap prop that applies acceleration on every physics step to each
// soft-body player inside its wind zone. The direction is local to the blower, so
// rotating a painted tile also rotates its arrow and wind.
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class Blower : MonoBehaviour, IPropWindConfigurable
{
    [Header("Wind")]
    [Tooltip("Local direction the blower pushes. Common values: Right (1,0), Up (0,1), Left (-1,0), Down (0,-1).")]
    [SerializeField] private Vector2 blowDirection = Vector2.right;

    [Tooltip("Acceleration applied in the blow direction. Around 30 can counter normal gravity; higher values lift or stop the player faster.")]
    [SerializeField, Min(0f)] private float blowStrength = 30f;

    [Tooltip("How far the wind extends from the front face of the blower.")]
    [SerializeField, Min(0.1f)] private float windRange = 4f;

    [Tooltip("Width of the wind zone perpendicular to the blow direction.")]
    [SerializeField, Min(0.1f)] private float windWidth = 1.5f;

    [Header("Visual")]
    [Tooltip("Child transform containing the arrow SpriteRenderer.")]
    [SerializeField] private Transform directionVisual;

    private readonly HashSet<SoftBodyPlayer> _playersInWind = new();

    private Collider2D _sourceCollider;
    private int _softBodyPointMask;

    private Vector2 LocalDirection => blowDirection.sqrMagnitude > 0.0001f
        ? blowDirection.normalized
        : Vector2.right;

    // Called by PropTilemapSpawner when a painted cell enables its Blower override.
    public void SetWindConfig(Vector2 direction, float strength)
    {
        blowDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        blowStrength = Mathf.Max(0f, strength);
        UpdateDirectionVisual();
    }

    private void Awake()
    {
        _sourceCollider = GetComponent<Collider2D>();
        _softBodyPointMask = LayerMask.GetMask("SoftBodyPoint");

        if (_softBodyPointMask == 0)
        {
            Debug.LogError("[Blower] The SoftBodyPoint layer is missing, so wind cannot detect the player.", this);
            enabled = false;
            return;
        }

        UpdateDirectionVisual();
    }

    private void OnValidate()
    {
        if (blowDirection.sqrMagnitude < 0.0001f)
            blowDirection = Vector2.right;
        else
            blowDirection.Normalize();

        UpdateDirectionVisual();
    }

    private void FixedUpdate()
    {
        GetWindZone(out Vector2 center, out Vector2 size, out float angle, out Vector2 worldDirection);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, angle, _softBodyPointMask);
        _playersInWind.Clear();

        foreach (Collider2D hit in hits)
        {
            SoftBodyPointRef pointRef = hit.GetComponent<SoftBodyPointRef>();
            SoftBodyPlayer player = pointRef != null ? pointRef.owner : null;
            if (player != null && _playersInWind.Add(player))
                player.AddExternalAcceleration(worldDirection * blowStrength);
        }
    }

    private void GetWindZone(
        out Vector2 center,
        out Vector2 size,
        out float angle,
        out Vector2 worldDirection)
    {
        Collider2D source = _sourceCollider != null ? _sourceCollider : GetComponent<Collider2D>();
        worldDirection = transform.TransformDirection((Vector3)LocalDirection).normalized;

        Bounds bounds = source.bounds;
        float sourceHalfDepth = Mathf.Abs(worldDirection.x) * bounds.extents.x
                              + Mathf.Abs(worldDirection.y) * bounds.extents.y;

        center = (Vector2)bounds.center + worldDirection * (sourceHalfDepth + windRange * 0.5f);
        size = new Vector2(windRange, windWidth);
        angle = Mathf.Atan2(worldDirection.y, worldDirection.x) * Mathf.Rad2Deg;
    }

    private void UpdateDirectionVisual()
    {
        if (directionVisual == null) return;

        Vector2 direction = LocalDirection;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        directionVisual.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D source = GetComponent<Collider2D>();
        if (source == null) return;

        GetWindZone(out Vector2 center, out Vector2 size, out float angle, out Vector2 worldDirection);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.85f);
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = previousMatrix;

        Vector2 arrowStart = source.bounds.center;
        Vector2 arrowEnd = arrowStart + worldDirection;
        Vector2 side = new Vector2(-worldDirection.y, worldDirection.x) * 0.18f;
        Gizmos.DrawLine(arrowStart, arrowEnd);
        Gizmos.DrawLine(arrowEnd, arrowEnd - worldDirection * 0.25f + side);
        Gizmos.DrawLine(arrowEnd, arrowEnd - worldDirection * 0.25f - side);
    }
}
