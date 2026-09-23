using System.Collections.Generic;
using UnityEngine;

// One square PropTile per belt block. Adjacent blocks offer surface motion to
// the whole soft body; SoftBodyPlayer applies it once per physics step.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(BoxCollider2D), typeof(Animator))]
public class ConveyorBelt : MonoBehaviour, IPropConnectable, IPropActivatable
{
    [SerializeField] private float surfaceSpeed = -2f;
    [SerializeField, Min(0.02f)] private float contactHeight = 0.16f;
    private BoxCollider2D surface;
    private Animator animator;
    private string connectionId;
    private ConnectionMode mode;
    private bool initialActive = true;
    private bool active = true;
    private readonly HashSet<SoftBodyPlayer> riders = new();

    public bool IsActive => active;
    public float SurfaceSpeed => surfaceSpeed;
    public void SetConnectionId(string id) => connectionId = id;
    public void SetActivationConfig(ConnectionMode connectionMode, bool initiallyActive)
    {
        mode = connectionMode;
        initialActive = initiallyActive;
        SetActive(initiallyActive);
    }

    private void Awake()
    {
        surface = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        SetActive(active);
    }

    private void OnEnable()
    {
        EventManager.OnPressurePlateActivated += OnActivated;
        EventManager.OnPressurePlateDeactivated += OnDeactivated;
    }
    private void OnDisable()
    {
        EventManager.OnPressurePlateActivated -= OnActivated;
        EventManager.OnPressurePlateDeactivated -= OnDeactivated;
    }
    private void OnActivated(string id)
    {
        if (!string.IsNullOrEmpty(connectionId) && id == connectionId)
            SetActive(mode == ConnectionMode.Toggle ? !active : !initialActive);
    }
    private void OnDeactivated(string id)
    {
        if (!string.IsNullOrEmpty(connectionId) && id == connectionId && mode == ConnectionMode.Hold)
            SetActive(initialActive);
    }
    private void SetActive(bool value)
    {
        active = value;
        if (animator != null) animator.speed = value ? 1f : 0f;
    }

    private void FixedUpdate()
    {
        if (!active) return;
        Bounds b = surface.bounds;
        Vector2 centre = new Vector2(b.center.x, b.max.y + contactHeight * 0.25f);
        riders.Clear();
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(centre,
                     new Vector2(b.size.x, contactHeight), 0f, LayerMask.GetMask("SoftBodyPoint", "Player")))
        {
            SoftBodyPlayer player = hit.GetComponent<SoftBodyPointRef>()?.owner;
            if (player == null) player = hit.GetComponentInParent<SoftBodyPlayer>();
            if (player == null || !player.IsGrounded || !riders.Add(player)) continue;
            player.OfferSurfaceVelocity(Vector2.right * surfaceSpeed);
        }
    }
}
