using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 * SCENE SETUP — build this UI hierarchy, then wire the fields below in the Inspector.
 *
 *  DialogueCanvas          (Canvas — Screen Space Overlay, sort order 10)
 *  └── DialoguePanel       (Image/Panel — anchored bottom-center, ~800×160 px)
 *      ├── SpeakerNameText (TextMeshProUGUI — small label, top-left of panel)
 *      ├── DialogueText    (TextMeshProUGUI — main text area)
 *      └── ContinueIndicator (TextMeshProUGUI or Image — "▼", bottom-right)
 *
 *  DialogueManager GO      (empty GameObject)
 *  ├── DialogueManager     (this script)
 *  └── AudioSource         (Play On Awake = false)
 */
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("The root panel to show/hide (the DialoguePanel GameObject).")]
    [SerializeField] private GameObject dialoguePanel;
    [Tooltip("TextMeshPro label for the speaker's name.")]
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [Tooltip("TextMeshPro component where dialogue is typed out.")]
    [SerializeField] private TextMeshProUGUI dialogueText;
    [Tooltip("The '▼ continue' indicator shown after a line finishes typing.")]
    [SerializeField] private GameObject continueIndicator;
    [Tooltip("Image on the left of the panel that shows the speaker's portrait. Hidden when the line has no portrait.")]
    [SerializeField] private Image portraitImage;

    [Header("Typewriter")]
    [Tooltip("Seconds between each revealed character. Lower = faster.")]
    [SerializeField] private float defaultCharDelay = 0.04f;
    [Tooltip("Key to skip typewriter or advance to the next line.")]
    [SerializeField] private KeyCode advanceKey = KeyCode.Z;

    [Header("Beep")]
    [Tooltip("AudioSource on this GameObject used to play beeps.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Short blip clip (~50 ms sine wave). Can be overridden per DialogueLine.")]
    [SerializeField] private AudioClip defaultBeepClip;
    [Tooltip("Play a beep every N characters. 1 = every character.")]
    [SerializeField] [Min(1)] private int beepFrequency = 1;
    [Tooltip("Volume of the beep (0 = silent, 1 = full).")]
    [SerializeField] [Range(0f, 1f)] private float beepVolume = 0.3f;
    [Tooltip("Base pitch of the beep (1 = normal, lower = deeper).")]
    [SerializeField] [Range(0.5f, 2f)] private float basePitch = 0.6f;
    [Tooltip("Random ± pitch offset per beep for variety.")]
    [SerializeField] [Range(0f, 0.5f)] private float pitchVariance = 0.1f;

    private DialogueLine[] _lines;
    private int            _lineIndex;
    private bool           _isTyping;
    private bool           _skipRequested;
    private Coroutine      _typingCoroutine;
    private AudioClip      _currentBeepClip;

    // Matches inline pause tags: {pause=0.5}
    private static readonly Regex PauseTag =
        new Regex(@"\{pause=(?<dur>[0-9]*\.?[0-9]+)\}", RegexOptions.Compiled);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (continueIndicator != null) continueIndicator.SetActive(false);
    }

    private void Update()
    {
        if (!IsDialogueActive()) return;

        if (Input.GetKeyDown(advanceKey))
        {
            if (_isTyping) _skipRequested = true; // skip to end of line
            else           ShowNextLine();         // advance
        }
    }
    public void StartDialogue(DialogueData data)
    {
            // Opens the dialogue box and starts typing the given DialogueData sequence.
        if (data == null || data.lines == null || data.lines.Length == 0)
        {
            Debug.LogWarning("[DialogueManager] StartDialogue called with empty DialogueData.");
            return;
        }

        if (IsDialogueActive()) StopAllCoroutines();

        _lines     = data.lines;
        _lineIndex = 0;

        dialoguePanel.SetActive(true);
        EventManager.DialogueStart(data);
        ShowNextLine();
    }

    // Returns true while a dialogue sequence is currently open.
    public bool IsDialogueActive() => dialoguePanel != null && dialoguePanel.activeSelf;

    private void ShowNextLine()
    {
        if (_lineIndex >= _lines.Length) { EndDialogue(); return; }

        DialogueLine line = _lines[_lineIndex++];

        if (speakerNameText != null)
        {
            bool hasSpeaker = !string.IsNullOrWhiteSpace(line.speakerName);
            speakerNameText.text = line.speakerName;
            speakerNameText.gameObject.SetActive(hasSpeaker);
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = line.portrait;
            portraitImage.gameObject.SetActive(line.portrait != null);
        }

        _currentBeepClip = line.voiceOverride != null ? line.voiceOverride : defaultBeepClip;
        float charDelay  = line.charDelayOverride > 0f ? line.charDelayOverride : defaultCharDelay;

        if (continueIndicator != null) continueIndicator.SetActive(false);

        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        _typingCoroutine = StartCoroutine(TypeLine(line.text, charDelay));
    }

    private void EndDialogue()
    {
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        dialoguePanel.SetActive(false);
        if (continueIndicator != null) continueIndicator.SetActive(false);
        if (portraitImage != null) portraitImage.gameObject.SetActive(false);
        EventManager.DialogueEnd();
    }

    private IEnumerator TypeLine(string rawText, float charDelay)
    {
            // Reveals text one character at a time using TMP's maxVisibleCharacters.
    // Handles {pause=X} inline tags and plays a beep every <see cref="beepFrequency"/> chars.
        _isTyping      = true;
        _skipRequested = false;

        var    pauses    = new List<(int index, float dur)>();
        string cleanText = StripPauseTags(rawText, pauses);

        dialogueText.text = cleanText;
        dialogueText.ForceMeshUpdate();
        int total = dialogueText.textInfo.characterCount;
        dialogueText.maxVisibleCharacters = 0;

        for (int i = 0; i < total; i++)
        {
            if (_skipRequested) { dialogueText.maxVisibleCharacters = total; break; }

            dialogueText.maxVisibleCharacters = i + 1;

            if ((i + 1) % beepFrequency == 0) PlayBeep();

            float pauseDur = GetPauseDuration(pauses, i);
            if (pauseDur > 0f) yield return new WaitForSecondsRealtime(pauseDur);

            yield return new WaitForSecondsRealtime(charDelay);
        }

        _isTyping = false;
        if (continueIndicator != null) continueIndicator.SetActive(true);
    }

    private void PlayBeep()
    {
        if (audioSource == null || _currentBeepClip == null) return;
        audioSource.pitch = basePitch + Random.Range(-pitchVariance, pitchVariance);
        audioSource.PlayOneShot(_currentBeepClip, beepVolume);
    }


    
    private static string StripPauseTags(string raw, List<(int, float)> outPauses)
    {
        // Removes {pause=X} tags from text, recording each pause's character position.

        var sb         = new StringBuilder();
        int searchFrom = 0;
        int visIndex   = 0;

        foreach (Match m in PauseTag.Matches(raw))
        {
            string before = raw.Substring(searchFrom, m.Index - searchFrom);
            sb.Append(before);
            visIndex  += before.Length;
            outPauses.Add((visIndex - 1, float.Parse(m.Groups["dur"].Value)));
            searchFrom = m.Index + m.Length;
        }

        sb.Append(raw.Substring(searchFrom));
        return sb.ToString();
    }

    private static float GetPauseDuration(List<(int index, float dur)> pauses, int i)
    {
        foreach (var p in pauses)
            if (p.index == i) return p.dur;
        return 0f;
    }
}
