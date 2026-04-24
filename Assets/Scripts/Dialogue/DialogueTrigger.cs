using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    [Tooltip("The dialogue asset this NPC will speak. Create via right-click → Create → Dialogue → Dialogue Data.")]
    [SerializeField] private DialogueData dialogueData;

    [Tooltip("Primary key to start the conversation.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Tooltip("Alternate key to start the conversation (useful when Z is also the dialogue advance key).")]
    [SerializeField] private KeyCode interactKeyAlt = KeyCode.Z;

    [Tooltip("Optional GameObject shown when player is in range (e.g. a '!' or 'Press E' sprite). Can be null.")]
    [SerializeField] private GameObject interactPrompt;

    [Tooltip("How close the player must be to interact.")]
    [SerializeField] private float interactRadius = 1.5f;

    private SoftBodyPlayer _softBody;
    private bool _playerInRange;
    // Guards against the Z key simultaneously ending dialogue and re-triggering it
    // on the same frame (DialogueManager and DialogueTrigger both run Update).
    private bool _dialogueEndedThisFrame;

    private void Start()
    {
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void OnEnable()  => EventManager.OnDialogueEnd += OnDialogueEnd;
    private void OnDisable() => EventManager.OnDialogueEnd -= OnDialogueEnd;

    private void OnDialogueEnd() => _dialogueEndedThisFrame = true;

    private void Update()
    {
        if (_softBody == null)
        {
            _softBody = FindFirstObjectByType<SoftBodyPlayer>();
            if (_softBody == null) return;
        }

        bool inRange = Vector2.Distance(transform.position, _softBody.Center) <= interactRadius;

        if (inRange != _playerInRange)
        {
            _playerInRange = inRange;
            if (interactPrompt != null) interactPrompt.SetActive(inRange);
        }

        if (!_playerInRange) return;
        if (DialogueManager.Instance == null || DialogueManager.Instance.IsDialogueActive()) return;

        // Skip interaction for one frame after dialogue ends so the advance key (Z)
        // cannot simultaneously close the final line and re-open the conversation.
        if (_dialogueEndedThisFrame)
        {
            _dialogueEndedThisFrame = false;
            return;
        }

        if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(interactKeyAlt))
            DialogueManager.Instance.StartDialogue(dialogueData);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
