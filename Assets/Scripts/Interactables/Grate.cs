using UnityEngine;

/*
 * OVERVIEW
 *   Grate prop — a horizontal-only barrier. The liquid player (SoftBodyPlayer ring points,
 *   whole body or split droplets) can push through it left<->right with a viscous drag, like
 *   squeezing between bars. Any non-liquid body — the future gas cloud, the future ice/solid
 *   state — is physically blocked; it never gets special-cased in this script, see FUTURE STATES.
 *   Not meant to be placed so the player needs to pass through top<->bottom.
 *   Placeholder art: a plain white square sprite until final bar artwork exists.
 *
 * HOW PASSAGE WORKS
 *   The grate's BoxCollider2D is solid, but ring points on the SoftBodyPoint layer are excluded
 *   from colliding with it via the Physics 2D Layer Collision Matrix (see ONE-TIME UNITY SETUP) —
 *   so they never physically stop against it. Instead, this script polls the passage zone every
 *   FixedUpdate (Physics2D.OverlapBoxNonAlloc, same detection style as Evaporator/PressurePlate)
 *   and clamps the speed of every ring point Rigidbody2D found inside to maxPassSpeed, every
 *   physics step, regardless of which SoftBodyPlayer instance (main body or split droplet) owns
 *   it. A continuous per-step clamp is used instead of a one-shot velocity multiply because
 *   SoftBodyPlayer's restore/spring forces (restoreForce ~60) reaccelerate a slowed point almost
 *   immediately — only a clamp reapplied every step can hold against that. zoneSize.x should
 *   cover the full body width (~1.1 by default) so the whole blob is held back together as it
 *   crosses, not just whichever single point happens to be inside a narrow gap.
 *
 * FUTURE STATES (gas cloud / ice)
 *   Nothing extra is required here to block them later. As long as their layer is left enabled
 *   against the grate's layer in the Layer Collision Matrix (the default), the solid
 *   BoxCollider2D blocks them exactly like a normal wall — only SoftBodyPoint is excluded.
 *
 * ONE-TIME UNITY SETUP
 *   1. Create a layer named "Grate" (Project Settings → Tags and Layers).
 *   2. Physics 2D → Layer Collision Matrix → uncheck Grate × SoftBodyPoint.
 *      Leave every other Grate × X pairing checked so the grate stays solid to everything else,
 *      including any future GasCloud / Ice layer.
 *   3. Assign this prefab's GameObject to the "Grate" layer.
 *
 * SETUP (prefab)
 *   1. Add a SpriteRenderer (placeholder: plain white square) and a BoxCollider2D to the prefab.
 *   2. Size the BoxCollider2D to the full grate footprint — this is both the visual bar geometry
 *      and the solid-block collider for non-liquid bodies.
 *   3. Size/position the Passage Zone (Inspector, gizmo shown when selected) to match the opening
 *      between the bars — this is the region where drag is applied.
 *   4. Assign the prefab to a PropTile asset; paint it on the Props tilemap, one anchor cell,
 *      oriented so the opening runs left<->right (placed between a floor tile and a ceiling tile).
 *
 * Not IPropConnectable/IPropActivatable — the grate has no on/off state, it is always solid.
 */
public class Grate : MonoBehaviour
{
    [Header("Drag")]
    [Tooltip("Max speed (m/s) a ring point is allowed while inside the passage zone, reapplied every physics step. This is what creates the felt resistance — lower = harder to squeeze through.")]
    [SerializeField] private float maxPassSpeed = 1.2f;

    [Header("Passage Zone")]
    [Tooltip("Local-space offset of the passage zone from this transform.")]
    [SerializeField] private Vector2 zoneCenter = Vector2.zero;

    [Tooltip("Size of the passage zone. Width should cover the full player body (~1.1 by default) so the whole blob is caught at once, not just a single leading point.")]
    [SerializeField] private Vector2 zoneSize = new Vector2(1.4f, 1.2f);

    [Header("Placeholder Passage Animation")]
    [Tooltip("How much the sprite pulses in scale while a ring point is passing through. Purely visual feedback until real bar art/animation exists.")]
    [SerializeField] private float passPulseScale = 0.06f;

    [Tooltip("Speed of the pulse cycle while occupied.")]
    [SerializeField] private float passPulseSpeed = 6f;

    private SpriteRenderer _sprite;
    private Vector3 _baseScale;
    private int _softBodyPointMask;

    private static readonly Collider2D[] OverlapBuffer = new Collider2D[16];

    private void Awake()
    {
        _sprite = GetComponent<SpriteRenderer>();
        _baseScale = transform.localScale;
        _softBodyPointMask = LayerMask.GetMask("SoftBodyPoint");
    }

    private void FixedUpdate()
    {
        Vector2 center = (Vector2)transform.position + zoneCenter;
        int count = Physics2D.OverlapBoxNonAlloc(center, zoneSize, 0f, OverlapBuffer, _softBodyPointMask);

        for (int i = 0; i < count; i++)
        {
            Rigidbody2D rb = OverlapBuffer[i].attachedRigidbody;
            if (rb == null) continue;

            Vector2 vel = rb.linearVelocity;
            float speed = vel.magnitude;
            if (speed > maxPassSpeed)
                rb.linearVelocity = vel * (maxPassSpeed / speed);
        }

        UpdatePlaceholderAnimation(count > 0);
    }

    private void UpdatePlaceholderAnimation(bool occupied)
    {
        if (_sprite == null) return;

        if (occupied)
        {
            float pulse = 1f + Mathf.Sin(Time.time * passPulseSpeed) * passPulseScale;
            transform.localScale = new Vector3(_baseScale.x * pulse, _baseScale.y, _baseScale.z);
        }
        else if (transform.localScale != _baseScale)
        {
            transform.localScale = _baseScale;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        Vector2 center = (Vector2)transform.position + zoneCenter;
        Gizmos.DrawWireCube(center, zoneSize);
    }
}
