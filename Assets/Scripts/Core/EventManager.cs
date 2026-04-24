using System;

public static class EventManager
{
    // Fired when a dialogue sequence begins. Use to disable movement, lock camera, etc.
    public static event Action<DialogueData> OnDialogueStart;

    // Fired when a dialogue sequence ends. Use to re-enable movement, unlock camera, etc.
    public static event Action OnDialogueEnd;

    // Fired when a pressure plate is stepped on. Parameter is the plate's unique id.
    public static event Action<string> OnPressurePlateActivated;

    // Fired when a pressure plate is released (player exits). Parameter is the plate's unique id.
    public static event Action<string> OnPressurePlateDeactivated;

    internal static void DialogueStart(DialogueData data) => OnDialogueStart?.Invoke(data);
    internal static void DialogueEnd()                    => OnDialogueEnd?.Invoke();

    internal static void PressurePlateActivated(string id)   => OnPressurePlateActivated?.Invoke(id);
    internal static void PressurePlateDeactivated(string id) => OnPressurePlateDeactivated?.Invoke(id);

    public static event Action OnPlayerSplit;
    public static event Action OnPlayerMerge;

    internal static void PlayerSplit() => OnPlayerSplit?.Invoke();
    internal static void PlayerMerge() => OnPlayerMerge?.Invoke();
}
