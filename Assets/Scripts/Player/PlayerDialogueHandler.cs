using UnityEngine;

// Add to the Player prefab alongside PlayerMovement.
// Disables PlayerMovement and zeroes velocity while dialogue is open, then re-enables it when done.
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDialogueHandler : MonoBehaviour
{
    private PlayerMovement _movement;
    private Rigidbody2D    _rb;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        _rb       = GetComponent<Rigidbody2D>();
    }

    // Subscribes to dialogue events
    private void OnEnable()
    {
        EventManager.OnDialogueStart += OnDialogueStart;
        EventManager.OnDialogueEnd   += OnDialogueEnd;
    }

    // Unsubscribes from dialogue events
    private void OnDisable()
    {
        EventManager.OnDialogueStart -= OnDialogueStart;
        EventManager.OnDialogueEnd   -= OnDialogueEnd;
    }

    // Freezes the player when dialogue starts
    private void OnDialogueStart(DialogueData _)
    {
        _movement.enabled = false;
        _rb.linearVelocity      = Vector2.zero;
    }

    // Unfreezes the player when dialogue ends
    private void OnDialogueEnd() => _movement.enabled = true;
}
