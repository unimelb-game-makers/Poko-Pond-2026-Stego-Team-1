# Prop Connections — Linking Activators to Activatees

This document covers how props that trigger things (activators: pressure plates, levers, buttons) connect to props that react (activatees: crushers, doors, platforms) in the tilemap prop system.

---

## Concepts

| Term | Examples | Role |
|------|---------|------|
| **Activator** | PressurePlate, Lever, Button | Detects player interaction and fires an event |
| **Activatee** | CrusherTrap, AutoCrusherTrap, Door | Listens for events and reacts |
| **Connection ID** | `"crusher_a"`, `"door_02"` | Shared string that links one activator to one activatee |

Connections are string-based and go through `EventManager`. No direct references between objects — activators and activatees never know about each other.

---

## How Connections Work

### 1. Activator fires an event with its ID

```
Player steps on plate
→ PressurePlate fires EventManager.PressurePlateActivated("crusher_a")
```

### 2. Activatee listens and filters by ID

```
CrusherTrap is subscribed to EventManager.OnPressurePlateActivated
→ receives "crusher_a"
→ checks: does "crusher_a" == my triggerPlateId?
→ yes → slams
```

### 3. Connection ID is set at spawn time

`PropTilemapSpawner` reads the Cell Overrides list and calls `SetConnectionId(id)` on each spawned prefab that implements `IPropConnectable`. This overrides whatever default ID is baked into the prefab.

---

## Setting Up a Connection in the Editor

1. Paint the activator tile and activatee tile on the Props tilemap.
2. Select the **Props** Tilemap → in **PropTilemapSpawner**, right-click → **Sync Cell List**.
3. Find both cells in the list (the prop name and cell coordinate are shown).
4. Type the **same string** in both Connection ID fields.
5. With PropTilemapSpawner selected, the Scene view labels and colour-codes each cell — linked props share the same colour.

**Rules:**
- Connection IDs only need to be unique within a scene.
- One activator can link to one activatee (one-to-one).
- One activatee can listen to multiple activators by sharing the same ID across several tiles.

---

## Implementing a New Activator

An activator detects player interaction and fires through `EventManager`.

### Step 1 — Implement `IPropConnectable`

```csharp
public class Lever : MonoBehaviour, IPropConnectable
{
    [SerializeField] private string leverId;
    public void SetConnectionId(string id) => leverId = id;
}
```

`PropTilemapSpawner` calls `SetConnectionId` automatically at spawn. Set a sensible default on the prefab as a fallback for any lever not given a cell override.

### Step 2 — Fire through EventManager when activated

Use the existing pressure plate events if the activator semantics match (something activates/deactivates):

```csharp
// When the player activates the lever
EventManager.PressurePlateActivated(leverId);

// When the player deactivates (if it toggles)
EventManager.PressurePlateDeactivated(leverId);
```

If the new activator has meaningfully different semantics (e.g. a one-way trigger, a timed pulse), add a new event pair to `EventManager`:

```csharp
// In EventManager.cs
public static event Action<string> OnLeverPulled;
internal static void LeverPulled(string id) => OnLeverPulled?.Invoke(id);
```

### Step 3 — Detection

Follow the same pattern as `PressurePlate` — use `Physics2D.OverlapBox` with the `"Player"` and `"SoftBodyPoint"` layer mask. Never rely on a trigger collider or Rigidbody on the player root.

Cache the detection zone in `Start()` from the collider's initial world bounds so animations cannot shift it:

```csharp
private void Start()
{
    var col = GetComponent<Collider2D>();
    var bounds = col.bounds;
    _detectionCenter = new Vector2(bounds.center.x, bounds.center.y + detectionHeight * 0.5f);
    _detectionSize   = new Vector2(bounds.size.x, detectionHeight);
}
```

### Step 4 — Full activator template

```csharp
public class Lever : MonoBehaviour, IPropConnectable
{
    [SerializeField] private string leverId;
    [SerializeField] private float detectionHeight = 1f;

    private Vector2 _detectionCenter;
    private Vector2 _detectionSize;
    private bool _playerOver;
    private bool _pulled;

    public void SetConnectionId(string id) => leverId = id;

    private void Start()
    {
        var col    = GetComponent<Collider2D>();
        var bounds = col.bounds;
        _detectionCenter = new Vector2(bounds.center.x, bounds.center.y + detectionHeight * 0.5f);
        _detectionSize   = new Vector2(bounds.size.x, detectionHeight);
    }

    private void Update()
    {
        bool playerPresent = Physics2D.OverlapBox(
            _detectionCenter, _detectionSize, 0f,
            LayerMask.GetMask("Player", "SoftBodyPoint"));

        if (playerPresent && !_playerOver) OnPlayerEnter();
        else if (!playerPresent && _playerOver) _playerOver = false;
    }

    private void OnPlayerEnter()
    {
        _playerOver = true;
        // Example: toggle on first pull only
        if (_pulled) return;
        _pulled = true;
        EventManager.PressurePlateActivated(leverId);
        // play animation, swap sprite, etc.
    }
}
```

---

## Implementing a New Activatee

An activatee listens to `EventManager` and reacts when its linked activator fires.

### Step 1 — Implement `IPropConnectable`

```csharp
public class Door : MonoBehaviour, IPropConnectable
{
    [SerializeField] private string triggerPlateId;
    public void SetConnectionId(string id) => triggerPlateId = id;
}
```

### Step 2 — Subscribe to EventManager

```csharp
private void OnEnable()
{
    EventManager.OnPressurePlateActivated   += HandleActivated;
    EventManager.OnPressurePlateDeactivated += HandleDeactivated;
}

private void OnDisable()
{
    EventManager.OnPressurePlateActivated   -= HandleActivated;
    EventManager.OnPressurePlateDeactivated -= HandleDeactivated;
}

private void HandleActivated(string id)
{
    if (id != triggerPlateId) return;
    Open();
}

private void HandleDeactivated(string id)
{
    if (id != triggerPlateId) return;
    Close();
}
```

### Step 3 — Full activatee template

```csharp
public class Door : MonoBehaviour, IPropConnectable
{
    [SerializeField] private string triggerPlateId;

    public void SetConnectionId(string id) => triggerPlateId = id;

    private void OnEnable()
    {
        EventManager.OnPressurePlateActivated   += HandleActivated;
        EventManager.OnPressurePlateDeactivated += HandleDeactivated;
    }

    private void OnDisable()
    {
        EventManager.OnPressurePlateActivated   -= HandleActivated;
        EventManager.OnPressurePlateDeactivated -= HandleDeactivated;
    }

    private void HandleActivated(string id)
    {
        if (id != triggerPlateId) return;
        // open door, play animation, etc.
    }

    private void HandleDeactivated(string id)
    {
        if (id != triggerPlateId) return;
        // close door, etc.
    }
}
```

---

## Connection Patterns

### One activator → one activatee
Set the same Connection ID on both cells in PropTilemapSpawner.

### One activator → multiple activatees
Give all activatee tiles the same Connection ID as the activator. Every activatee with a matching `triggerPlateId` reacts.

### Multiple activators → one activatee
Give all activator tiles the same Connection ID as the activatee. Any one of them firing triggers it. If you need ALL to be held simultaneously (AND logic), the activatee needs custom state tracking.

### Activatee not placed via tilemap (scene object)
If the activatee is a regular scene GameObject (not tilemap-spawned), skip `IPropConnectable`. Just set `triggerPlateId` directly in the Inspector and subscribe to `EventManager` — no cell override needed.

---

## Existing Prop Reference

| Script | Role | Event fired/heard | IPropConnectable field |
|--------|------|------------------|----------------------|
| `PressurePlate` | Activator | Fires `OnPressurePlateActivated` / `OnPressurePlateDeactivated` | `plateId` |
| `CrusherTrap` | Activatee | Hears `OnPressurePlateActivated` | `triggerPlateId` |
| `AutoCrusherTrap` | Activatee (paused by plate) | Hears both events | `triggerPlateId` |
