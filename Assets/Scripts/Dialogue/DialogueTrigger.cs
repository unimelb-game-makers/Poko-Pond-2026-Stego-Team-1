using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DialogueTrigger : MonoBehaviour
{
    [Tooltip("The dialogue asset this NPC will speak. Create via right-click → Create → Dialogue → Dialogue Data.")]
    [SerializeField] private DialogueData dialogueData;

    [Tooltip("Key the player presses to start the conversation.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Tooltip("Optional GameObject shown when player is in range (e.g. a '!' or 'Press E' sprite). Can be null.")]
    [SerializeField] private GameObject interactPrompt;

    private bool _playerInRange;

    // Ensures the collider is set to trigger and hides the prompt on start
    private void Start()
    {
        var col = GetComponent<Collider2D>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[DialogueTrigger] Collider2D on '{name}' is not a trigger — setting it now.", this);
            col.isTrigger = true;
        }

        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    // Starts dialogue when the player presses the interact key while in range
    private void Update()
    {
        if (!_playerInRange) return;
        if (DialogueManager.Instance == null || DialogueManager.Instance.IsDialogueActive()) return;

        if (Input.GetKeyDown(interactKey))
            DialogueManager.Instance.StartDialogue(dialogueData);
    }

    // Shows the interact prompt when the player enters the trigger
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = true;
        if (interactPrompt != null) interactPrompt.SetActive(true);
    }

    // Hides the interact prompt when the player leaves the trigger
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = false;
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }
}
