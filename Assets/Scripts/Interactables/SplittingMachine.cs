using UnityEngine;

// One-off progression object that enables PlayerSplitController's split input
// when the player steps onto the machine.
[RequireComponent(typeof(Collider2D))]
public class SplittingMachine : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float activationWidth = 1.8f;
    [SerializeField, Min(0.1f)] private float activationHeight = 1.5f;
    [SerializeField] private Vector2 activationOffset = new Vector2(0f, 0.75f);
    [SerializeField] private SpriteRenderer machineRenderer;
    [SerializeField] private Color availableColour = Color.white;
    [SerializeField] private Color activatedColour = new Color(0.72f, 1f, 0.82f, 1f);

    private PlayerSplitController _splitController;
    private bool _activated;

    public bool IsActivated => _activated;

    private void Start()
    {
        _splitController = FindFirstObjectByType<PlayerSplitController>();
        _activated = _splitController != null && _splitController.SplittingUnlocked;
        ApplyVisual();
    }

    private void Update()
    {
        if (_activated) return;
        if (_splitController == null)
            _splitController = FindFirstObjectByType<PlayerSplitController>();
        if (_splitController == null || !PlayerIsOnMachine()) return;

        _splitController.UnlockSplitting();
        _activated = true;
        ApplyVisual();
    }

    private bool PlayerIsOnMachine()
    {
        return Physics2D.OverlapBox(
            (Vector2)transform.position + activationOffset,
            new Vector2(activationWidth, activationHeight),
            0f,
            LayerMask.GetMask("Player", "SoftBodyPoint")) != null;
    }

    private void ApplyVisual()
    {
        if (machineRenderer != null)
            machineRenderer.color = _activated ? activatedColour : availableColour;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 1f, 0.8f);
        Gizmos.DrawWireCube(
            (Vector2)transform.position + activationOffset,
            new Vector3(activationWidth, activationHeight, 0f));
    }
}
