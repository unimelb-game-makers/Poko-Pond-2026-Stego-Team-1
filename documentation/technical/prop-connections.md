# Prop Connections — Linking Triggers to Props

This document covers how triggers (activators: pressure plates, levers, buttons) connect to props that react (activatees: crushers, evaporators, condensers, doors) in the tilemap prop system.

---

## Concepts

| Term | Examples | Role |
|------|---------|------|
| **Activator** | PressurePlate, Lever, Button | Detects player interaction and fires an event |
| **Activatee** | CrusherTrap, Evaporator, Condenser | Listens for events and reacts |
| **Connection ID** | `"crusher_a"`, `"evap_01"` | Shared string that links one activator to one or more activatees |
| **Connection Mode** | Hold, Toggle | How the activatee responds to the trigger |
| **Initial Active** | true / false | Whether the activatee starts on or off before any trigger fires |

Connections are string-based and go through `EventManager`. No direct references between objects — activators and activatees never know about each other.

---

## Connection Mode

Each activatee cell in the Cell Overrides list has a **Connection Mode** field:

| Mode | Behaviour |
|------|-----------|
| **Hold** | Activatee state matches the trigger state — flips when trigger is pressed, reverts when released |
| **Toggle** | Each trigger press flips the activatee state. Trigger release has no effect |

### Examples

- A pressure plate that **temporarily disables** an evaporator while held → **Hold**, `initialActive = true`
- A pressure plate that **permanently turns on** a condenser on first press → **Toggle**, `initialActive = false`
- An always-on evaporator with no trigger → leave Connection ID empty, `initialActive = true`

---

## Initial Active

Each activatee cell also has an **Initial Active** field:

- `true` — the activatee starts on. A linked trigger turns it off (Hold) or flips it (Toggle).
- `false` — the activatee starts off. A linked trigger turns it on (Hold) or flips it (Toggle).

`Initial Active` applies even with no Connection ID — it sets the permanent state of an always-on or always-off prop.

---

## How Connections Work

### 1. Activator fires an event with its ID

```
Player steps on plate
→ PressurePlate fires EventManager.PressurePlateActivated("evap_01")
```

### 2. Activatee listens and filters by ID

```
Evaporator is subscribed to EventManager.OnPressurePlateActivated
→ receives "evap_01"
→ checks: does "evap_01" == my _connectionId?
→ yes → applies Connection Mode logic
```

### 3. Config is set at spawn time

`PropTilemapSpawner` reads the Cell Overrides list and:
- Calls `SetConnectionId(id)` on each spawned prefab that implements `IPropConnectable`
- Calls `SetActivationConfig(mode, initialActive)` on each spawned prefab that implements `IPropActivatable`

---

## Setting Up a Connection in the Editor

1. Paint the activator tile and activatee tile on the Props tilemap.
2. Select the **Props** Tilemap → right-click **PropTilemapSpawner** → **Sync Cell List**.
3. Find both cells in the list.
4. Type the **same string** in both **Connection ID** fields.
5. Set **Connection Mode** and **Initial Active** on the activatee cell.
6. The Scene view labels and colour-codes linked cells — props with the same Connection ID share the same colour.

**Rules:**
- Connection IDs only need to be unique within a scene.
- One activator can link to multiple activatees — give them all the same Connection ID.
- One activatee can only have one Connection ID (one trigger source).

---

## Implementing a New Activator

An activator detects player interaction and fires through `EventManager`.

### Step 1 — Implement `IPropConnectable`

```csharp
public class Lever : MonoBehaviour, IPropConnectable
{
    private string _leverId;
    public void SetConnectionId(string id) => _leverId = id;
}
```

`PropTilemapSpawner` calls `SetConnectionId` automatically at spawn.

### Step 2 — Fire through EventManager when activated

Use the existing pressure plate events (semantics: something activates/deactivates):

```csharp
EventManager.PressurePlateActivated(_leverId);   // on press / interaction
EventManager.PressurePlateDeactivated(_leverId); // on release (if applicable)
```

If the activator has distinct semantics (one-way trigger, timed pulse), add a new event pair to `EventManager.cs`.

### Step 3 — Detection

Use `Physics2D.OverlapBox` on the `"Player"` and `"SoftBodyPoint"` layer mask. Never rely on a trigger collider or Rigidbody on the player root. Cache the detection zone in `Start()` from the collider's initial bounds.

### Step 4 — Full activator template

```csharp
public class Lever : MonoBehaviour, IPropConnectable
{
    [SerializeField] private float detectionHeight = 1f;

    private string  _leverId;
    private Vector2 _detectionCenter;
    private Vector2 _detectionSize;
    private bool    _playerOver;

    public void SetConnectionId(string id) => _leverId = id;

    private void Start()
    {
        var col = GetComponent<Collider2D>();
        var b   = col.bounds;
        _detectionCenter = new Vector2(b.center.x, b.center.y + detectionHeight * 0.5f);
        _detectionSize   = new Vector2(b.size.x, detectionHeight);
    }

    private void Update()
    {
        bool playerPresent = Physics2D.OverlapBox(
            _detectionCenter, _detectionSize, 0f,
            LayerMask.GetMask("Player", "SoftBodyPoint"));

        if (playerPresent && !_playerOver) OnPlayerEnter();
        else if (!playerPresent && _playerOver) OnPlayerExit();
    }

    private void OnPlayerEnter()
    {
        _playerOver = true;
        EventManager.PressurePlateActivated(_leverId);
    }

    private void OnPlayerExit()
    {
        _playerOver = false;
        EventManager.PressurePlateDeactivated(_leverId);
    }
}
```

---

## Implementing a New Activatee

An activatee listens to `EventManager`, applies Connection Mode logic, and reacts.

### Step 1 — Implement both interfaces

```csharp
public class Door : MonoBehaviour, IPropConnectable, IPropActivatable
{
    private string         _connectionId;
    private ConnectionMode _connectionMode = ConnectionMode.Hold;
    private bool           _initialActive  = true;
    private bool           _isActive       = true;

    public void SetConnectionId(string id) => _connectionId = id;

    public void SetActivationConfig(ConnectionMode mode, bool initialActive)
    {
        _connectionMode = mode;
        _initialActive  = initialActive;
        _isActive       = initialActive;
    }
}
```

### Step 2 — Subscribe to EventManager and apply mode logic

```csharp
private void OnEnable()
{
    EventManager.OnPressurePlateActivated   += OnTriggerActivated;
    EventManager.OnPressurePlateDeactivated += OnTriggerDeactivated;
}

private void OnDisable()
{
    EventManager.OnPressurePlateActivated   -= OnTriggerActivated;
    EventManager.OnPressurePlateDeactivated -= OnTriggerDeactivated;
}

private void OnTriggerActivated(string id)
{
    if (string.IsNullOrEmpty(_connectionId) || id != _connectionId) return;
    _isActive = _connectionMode == ConnectionMode.Toggle ? !_isActive : !_initialActive;
    ApplyState();
}

private void OnTriggerDeactivated(string id)
{
    if (string.IsNullOrEmpty(_connectionId) || id != _connectionId) return;
    if (_connectionMode == ConnectionMode.Toggle) return; // Toggle ignores release
    _isActive = _initialActive;
    ApplyState();
}

private void ApplyState() { /* open/close door, play animation, etc. */ }
```

---

## Connection Patterns

### One activator → one activatee
Set the same Connection ID on both cells.

### One activator → multiple activatees
Give all activatee cells the same Connection ID. Every matching activatee reacts simultaneously.

### Multiple activators → one activatee
Give all activator cells the same Connection ID as the activatee. Any one of them firing triggers it.

### Always-on / always-off prop (no trigger)
Leave Connection ID empty. Set `Initial Active` to the desired permanent state.

### Activatee not placed via tilemap
Skip `IPropConnectable`. Call `SetActivationConfig` from your own code or set `_initialActive` directly in a field. Subscribe to `EventManager` normally.

---

## Existing Prop Reference

| Script | Role | Implements | Connection Mode support |
|--------|------|-----------|------------------------|
| `PressurePlate` | Activator | `IPropConnectable` | — (fires events) |
| `CrusherTrap` | Activatee | `IPropConnectable` | Hold only (internal) |
| `AutoCrusherTrap` | Activatee | `IPropConnectable` | Hold only (internal) |
| `Evaporator` | Activatee | `IPropConnectable`, `IPropActivatable` | Hold + Toggle |
| `Condenser` | Activatee | `IPropConnectable`, `IPropActivatable` | Hold + Toggle |
