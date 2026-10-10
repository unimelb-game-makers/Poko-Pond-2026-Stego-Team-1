using System;
using UnityEngine;

// On/off state for a prop driven by an activator (pressure plate, water battery)
// through EventManager's pressure-plate events. Serialized so props placed directly
// in a scene can be linked in the Inspector; tile-spawned props are configured by
// PropTilemapSpawner through IPropConnectable / IPropActivatable instead.
[Serializable]
public class PropActivation
{
    [Tooltip("Id of the activator that switches this prop. Empty = never switched (always uses Initial Active).")]
    public string connectionId = "";

    [Tooltip("Hold: on while the activator is held/running. Toggle: each activation flips the state.")]
    public ConnectionMode mode = ConnectionMode.Hold;

    [Tooltip("State before any activator fires (and the state a Hold prop returns to on release).")]
    public bool initialActive = true;

    public bool Active { get; private set; } = true;

    private Action<bool> _onChanged;

    // Call from OnEnable. onChanged runs whenever the state changes.
    public void Bind(Action<bool> onChanged)
    {
        _onChanged = onChanged;
        Active = initialActive;
        EventManager.OnPressurePlateActivated += HandleActivated;
        EventManager.OnPressurePlateDeactivated += HandleDeactivated;
    }

    // Call from OnDisable.
    public void Unbind()
    {
        EventManager.OnPressurePlateActivated -= HandleActivated;
        EventManager.OnPressurePlateDeactivated -= HandleDeactivated;
        _onChanged = null;
    }

    public void Configure(ConnectionMode connectionMode, bool initiallyActive)
    {
        mode = connectionMode;
        initialActive = initiallyActive;
        Set(initiallyActive);
    }

    private void HandleActivated(string id)
    {
        if (!Matches(id)) return;
        Set(mode == ConnectionMode.Toggle ? !Active : !initialActive);
    }

    private void HandleDeactivated(string id)
    {
        if (Matches(id) && mode == ConnectionMode.Hold)
            Set(initialActive);
    }

    private bool Matches(string id) => !string.IsNullOrEmpty(connectionId) && id == connectionId;

    private void Set(bool value)
    {
        if (Active == value) return;
        Active = value;
        _onChanged?.Invoke(value);
    }
}
