using UnityEngine;

// Attach to the same GameObject as SoftBodyPlayer.
// Freezes all ring-point physics while dialogue is open, then restores it.
[RequireComponent(typeof(SoftBodyPlayer))]
public class PlayerDialogueHandler : MonoBehaviour
{
    private SoftBodyPlayer _body;

    private void Awake()
    {
        _body = GetComponent<SoftBodyPlayer>();
    }

    private void OnEnable()
    {
        EventManager.OnDialogueStart += OnDialogueStart;
        EventManager.OnDialogueEnd   += OnDialogueEnd;
    }

    private void OnDisable()
    {
        EventManager.OnDialogueStart -= OnDialogueStart;
        EventManager.OnDialogueEnd   -= OnDialogueEnd;
    }

    private void OnDialogueStart(DialogueData _) => _body.Freeze();
    private void OnDialogueEnd()                 => _body.Unfreeze();
}
