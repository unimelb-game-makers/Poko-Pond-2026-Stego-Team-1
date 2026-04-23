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

    private void Start()
    {
        // Find the player via SoftBodyPlayer component — avoids relying on a "Player" tag
        // that may be absent after migrating away from the old PlayerMovement setup.
        _softBody = FindFirstObjectByType<SoftBodyPlayer>();

        if (_softBody == null)
            Debug.LogWarning($"[DialogueTrigger] '{name}' could not find a SoftBodyPlayer in the scene.", this);

        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void Update()
    {
        if (_softBody == null) return;

        bool inRange = Vector2.Distance(transform.position, _softBody.Center) <= interactRadius;

        if (inRange != _playerInRange)
        {
            _playerInRange = inRange;
            if (interactPrompt != null) interactPrompt.SetActive(inRange);
        }

        if (!_playerInRange) return;
        if (DialogueManager.Instance == null || DialogueManager.Instance.IsDialogueActive()) return;

        if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(interactKeyAlt))
            DialogueManager.Instance.StartDialogue(dialogueData);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
