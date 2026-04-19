using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    [Tooltip("Displayed in the speaker label. Leave empty to hide it.")]
    public string speakerName;

    [TextArea(2, 6)]
    [Tooltip("Dialogue text. Supports TMP rich-text tags (<b>, <color=#...>, etc.) " +
             "and inline pauses: {pause=0.5}")]
    public string text;

    [Tooltip("Portrait shown on the left of the dialogue box. Leave null to hide.")]
    public Sprite portrait;

    [Tooltip("Per-line beep clip. Leave null to use the DialogueManager default.")]
    public AudioClip voiceOverride;

    [Tooltip("Seconds per character for this line. 0 = use the DialogueManager default.")]
    [Min(0f)]
    public float charDelayOverride;
}

// A dialogue sequence asset. Assign lines in the Inspector, then attach to a DialogueTrigger.
// Create via: right-click in Project → Create → Dialogue → Dialogue Data
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Dialogue Data", order = 0)]
public class DialogueData : ScriptableObject
{
    public DialogueLine[] lines;
}
