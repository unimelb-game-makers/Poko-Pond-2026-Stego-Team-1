using UnityEngine;

// Stationary, tile-painted electrified surface. Uses the same contact rule as
// MovingPlatform: a shallow kill zone at the collider top, affecting all states.
[RequireComponent(typeof(BoxCollider2D))]
public class ElectricPlatform : MonoBehaviour, IPropConnectable, IPropPowered
{
    [SerializeField, Min(0.02f)] private float contactHeight = 0.25f;
    private BoxCollider2D surface;
    private string _connectionId = "";
    private bool _electricEffectEnabled = true;

    public bool IsElectrifying => isActiveAndEnabled && _electricEffectEnabled;
    public bool IsPowered => IsElectrifying;

    public void SetConnectionId(string id) => _connectionId = id ?? "";

    private void Awake() => surface = GetComponent<BoxCollider2D>();

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

    private void FixedUpdate()
    {
        if (!IsElectrifying) return;

        // Dropping through temporarily disables a platform's rigidbody. Keep
        // the electric zone in place even while its physical body is disabled.
        Vector2 top = transform.TransformPoint(surface.offset + Vector2.up * surface.size.y * 0.5f);
        PlayerLife.KillAllInBox(top, new Vector2(surface.size.x * Mathf.Abs(transform.lossyScale.x), contactHeight));
    }

    private void OnTriggerActivated(string id)
    {
        if (string.IsNullOrEmpty(_connectionId) || id != _connectionId) return;
        _electricEffectEnabled = false;
    }

    private void OnTriggerDeactivated(string id)
    {
        if (string.IsNullOrEmpty(_connectionId) || id != _connectionId) return;
        _electricEffectEnabled = true;
    }
}
