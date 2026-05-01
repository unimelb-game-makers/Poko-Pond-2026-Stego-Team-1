using System;

// Central event bus.  Subscribe from any script; fire only from the owning system.
// All events are static so no scene reference is needed to subscribe or unsubscribe.
public static class EventManager
{
    // ── Dialogue ──────────────────────────────────────────────────────────

    // Fired when a dialogue sequence begins. Use to disable movement, lock camera, etc.
    public static event Action<DialogueData> OnDialogueStart;
    // Fired when a dialogue sequence ends. Use to re-enable movement, unlock camera, etc.
    public static event Action               OnDialogueEnd;

    internal static void DialogueStart(DialogueData data) => OnDialogueStart?.Invoke(data);
    internal static void DialogueEnd()                    => OnDialogueEnd?.Invoke();

    // ── Pressure plates ───────────────────────────────────────────────────

    // Fired when the player steps on a plate. Parameter is the plate's unique id string.
    public static event Action<string> OnPressurePlateActivated;
    // Fired when the player leaves a plate (skipped in One Shot mode).
    public static event Action<string> OnPressurePlateDeactivated;

    internal static void PressurePlateActivated(string id)   => OnPressurePlateActivated?.Invoke(id);
    internal static void PressurePlateDeactivated(string id) => OnPressurePlateDeactivated?.Invoke(id);

    // ── Player split / merge ──────────────────────────────────────────────

    // Fired by PlayerSplitController at the end of a successful split.
    public static event Action OnPlayerSplit;
    // Fired by PlayerSplitController at the end of a successful merge.
    public static event Action OnPlayerMerge;

    internal static void PlayerSplit() => OnPlayerSplit?.Invoke();
    internal static void PlayerMerge() => OnPlayerMerge?.Invoke();

    // ── Evaporation / Condensation ────────────────────────────────────────

    // Fired by PlayerSplitController when any droplet (main or split) becomes a gas cloud.
    public static event Action OnPlayerEvaporate;
    // Fired by PlayerSplitController when a gas cloud condenses back into a liquid droplet.
    public static event Action OnPlayerCondense;

    internal static void PlayerEvaporate() => OnPlayerEvaporate?.Invoke();
    internal static void PlayerCondense()  => OnPlayerCondense?.Invoke();
}
