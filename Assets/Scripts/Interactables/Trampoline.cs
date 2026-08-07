using System.Collections.Generic;
using UnityEngine;

// A tilemap-placed prop that launches a soft-body player upward when one of its
// ring points enters the narrow contact zone above the surface collider.
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class Trampoline : MonoBehaviour
{
    [Header("Bounce")]
    [Tooltip("Upward speed applied to the entire soft body when it lands on the trampoline.")]
    [SerializeField, Min(0f)] private float bounceStrength = 16f;

    [Header("Top Contact")]
    [Tooltip("Height of the detection zone immediately above the surface collider.")]
    [SerializeField, Min(0.01f)] private float contactHeight = 0.15f;

    private readonly HashSet<SoftBodyPlayer> _playersOnTop = new();
    private readonly HashSet<SoftBodyPlayer> _playersDetectedThisStep = new();

    private Collider2D _surfaceCollider;
    private Vector2 _contactCenter;
    private Vector2 _contactSize;
    private int _softBodyPointMask;

    private void Start()
    {
        _surfaceCollider = GetComponent<Collider2D>();
        _softBodyPointMask = LayerMask.GetMask("SoftBodyPoint");

        if (_softBodyPointMask == 0)
        {
            Debug.LogError("[Trampoline] The SoftBodyPoint layer is missing, so player contact cannot be detected.", this);
            enabled = false;
            return;
        }

        CacheContactZone();
    }

    private void FixedUpdate()
    {
        _playersDetectedThisStep.Clear();

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            _contactCenter,
            _contactSize,
            0f,
            _softBodyPointMask);

        foreach (Collider2D hit in hits)
        {
            SoftBodyPointRef pointRef = hit.GetComponent<SoftBodyPointRef>();
            SoftBodyPlayer player = pointRef != null ? pointRef.owner : null;
            if (player == null || !_playersDetectedThisStep.Add(player)) continue;

            bool justTouchedTop = !_playersOnTop.Contains(player);
            bool isFallingOrResting = player.CalculateAverageVelocity().y <= 0f;
            if (justTouchedTop && isFallingOrResting)
                player.BounceUpward(bounceStrength);
        }

        _playersOnTop.Clear();
        _playersOnTop.UnionWith(_playersDetectedThisStep);
    }

    private void CacheContactZone()
    {
        Bounds bounds = _surfaceCollider.bounds;
        _contactCenter = new Vector2(bounds.center.x, bounds.max.y + contactHeight * 0.5f);
        _contactSize = new Vector2(bounds.size.x, contactHeight);
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D surface = _surfaceCollider != null ? _surfaceCollider : GetComponent<Collider2D>();
        if (surface == null) return;

        Bounds bounds = surface.bounds;
        Vector2 center = new Vector2(bounds.center.x, bounds.max.y + contactHeight * 0.5f);
        Vector2 size = new Vector2(bounds.size.x, contactHeight);

        Gizmos.color = new Color(0.2f, 1f, 0.35f, 0.9f);
        Gizmos.DrawWireCube(center, size);
    }
}
