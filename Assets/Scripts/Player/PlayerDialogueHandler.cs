using UnityEngine;

// Add to the Player prefab alongside PlayerMovement.
// Disables PlayerMovement while dialogue is open, then re-enables it when done.
[RequireComponent(typeof(PlayerMovement))]
public class PlayerDialogueHandler : MonoBehaviour
{
    private PlayerMovement _movement;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
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
    private void OnDialogueStart(DialogueData _) => _movement.enabled = false;

    // Unfreezes the player when dialogue ends
    private void OnDialogueEnd() => _movement.enabled = true;
}
