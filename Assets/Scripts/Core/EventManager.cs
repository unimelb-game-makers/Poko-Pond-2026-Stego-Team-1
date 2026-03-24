using System;

public static class EventManager
{
    // Fired when a dialogue sequence begins. Use to disable movement, lock camera, etc.
    public static event Action<DialogueData> OnDialogueStart;

    // Fired when a dialogue sequence ends. Use to re-enable movement, unlock camera, etc.
    public static event Action OnDialogueEnd;

    internal static void DialogueStart(DialogueData data) => OnDialogueStart?.Invoke(data);
    internal static void DialogueEnd()                    => OnDialogueEnd?.Invoke();
}
