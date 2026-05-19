using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * OVERVIEW
 *   Lets the softbody player traverse one-way platform tilemaps:
 *     - Press S / Down → fall through any platform directly below.
 *     - Jump from below → automatically pass up through platforms overhead.
 *
 *   Each ring point raycasts in the direction of travel; if a Platform-layer
 *   collider is within probe distance, the platform tilemap's Rigidbody2D is
 *   marked unsimulated for a brief window so every attached collider drops
 *   out of physics at once. After the window ends, simulation resumes.
 *
 *   Toggling Rigidbody2D.simulated is what makes this work with composite
 *   colliders + softbody points — IgnoreLayerCollision and per-collider
 *   enable flags don't cleanly break the merged contacts.
 *
 * SETUP
 *   1. Attach to the SoftBodyPlayer GameObject.
 *   2. Platform tilemap's GameObject layer must match the Platform Layer field.
 *   3. Platform tilemap needs a Static Rigidbody2D + TilemapCollider2D
 *      (Composite Operation = Merge) + CompositeCollider2D + PlatformEffector2D.
 */
public class PlatformDropThrough : MonoBehaviour
{
    [Tooltip("Layer of the one-way platform tilemap.")]
    public string platformLayer = "Platform";

    [Tooltip("How long the platform tilemap stays unsimulated per drop or pass-up.")]
    public float passDuration = 0.3f;

    [Tooltip("Raycast distance (world units) checked above/below each ring point for a platform.")]
    public float passProbeDistance = 0.4f;

    [Tooltip("Minimum upward velocity (units/sec) on any ring point to auto-trigger a pass-up.")]
    public float passUpVelocityThreshold = 3f;

    private int            _platformMask;
    private SoftBodyPlayer _sp;
    private readonly HashSet<Rigidbody2D> _disabledBodies = new HashSet<Rigidbody2D>();

    private void Start()
    {
        _sp = GetComponent<SoftBodyPlayer>();
        int idx = LayerMask.NameToLayer(platformLayer);
        if (idx < 0)
        {
            Debug.LogError($"[PlatformDropThrough] Layer '{platformLayer}' does not exist.", this);
            enabled = false;
            return;
        }
        _platformMask = 1 << idx;
    }

    private void Update()
    {
        // Held, not pressed — so landing on a platform mid-air while S is down still drops through.
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            ProbeAndDisable(Vector2.down);
    }

    private void FixedUpdate()
    {
        bool movingUp = false;
        foreach (var rb in _sp.Points)
        {
            if (rb != null && rb.linearVelocity.y > passUpVelocityThreshold) { movingUp = true; break; }
        }
        if (movingUp) ProbeAndDisable(Vector2.up);
    }

    private void ProbeAndDisable(Vector2 direction)
    {
        foreach (var rb in _sp.Points)
        {
            if (rb == null) continue;
            var hit = Physics2D.Raycast(rb.position, direction, passProbeDistance, _platformMask);
            if (hit.collider == null) continue;
            var body = hit.collider.attachedRigidbody;
            if (body == null || _disabledBodies.Contains(body)) continue;
            StartCoroutine(DisableCoroutine(body));
        }
    }

    private IEnumerator DisableCoroutine(Rigidbody2D body)
    {
        _disabledBodies.Add(body);
        body.simulated = false;
        yield return new WaitForSeconds(passDuration);
        if (body != null) body.simulated = true;
        _disabledBodies.Remove(body);
    }
}
