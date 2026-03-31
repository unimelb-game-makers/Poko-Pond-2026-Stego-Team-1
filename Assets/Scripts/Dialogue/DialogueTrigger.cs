using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    [Tooltip("The dialogue asset this NPC will speak. Create via right-click → Create → Dialogue → Dialogue Data.")]
    [SerializeField] private DialogueData dialogueData;

    [Tooltip("Key the player presses to start the conversation.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Tooltip("Optional GameObject shown when player is in range (e.g. a '!' or 'Press E' sprite). Can be null.")]
    [SerializeField] private GameObject interactPrompt;

    [Tooltip("How close the player must be to interact.")]
    [SerializeField] private float interactRadius = 1.5f;

    private Transform _player;
    private bool _playerInRange;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            _player = playerObj.transform;

        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void Update()
    {
        if (_player == null) return;

        bool inRange = Vector2.Distance(transform.position, _player.position) <= interactRadius;

        if (inRange != _playerInRange)
        {
            _playerInRange = inRange;
            if (interactPrompt != null) interactPrompt.SetActive(inRange);
        }

        if (!_playerInRange) return;
        if (DialogueManager.Instance == null || DialogueManager.Instance.IsDialogueActive()) return;

        if (Input.GetKeyDown(interactKey))
            DialogueManager.Instance.StartDialogue(dialogueData);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
