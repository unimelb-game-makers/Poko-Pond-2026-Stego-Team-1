using UnityEngine;

// Stationary, tile-painted electrified surface. Uses the same contact rule as
// MovingPlatform: a shallow kill zone at the collider top, affecting all states.
[RequireComponent(typeof(BoxCollider2D))]
public class ElectricPlatform : MonoBehaviour, IPropPowered
{
    [SerializeField, Min(0.02f)] private float contactHeight = 0.25f;
    private BoxCollider2D surface;
    public bool IsElectrifying => isActiveAndEnabled;
    public bool IsPowered => IsElectrifying;
    private void Awake() => surface = GetComponent<BoxCollider2D>();
    private void FixedUpdate()
    {
        // Dropping through temporarily disables a platform's rigidbody. Keep
        // the electric zone in place even while its physical body is disabled.
        Vector2 top = transform.TransformPoint(surface.offset + Vector2.up * surface.size.y * 0.5f);
        PlayerLife.KillAllInBox(top, new Vector2(surface.size.x * Mathf.Abs(transform.lossyScale.x), contactHeight));
    }
}
