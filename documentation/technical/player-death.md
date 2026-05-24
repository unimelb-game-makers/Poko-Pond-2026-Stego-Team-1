# Player Death — Technical Documentation

This document covers how player death works in Poko Pond, how to add new hazards correctly, and why the system is designed the way it is.

---

## Table of Contents
- [Overview](#overview)
- [How Death Works](#how-death-works)
- [Split State Awareness](#split-state-awareness)
- [Adding a New Hazard](#adding-a-new-hazard)
  - [Zone-Based Hazard (area kill)](#zone-based-hazard-area-kill)
  - [Contact-Based Hazard (collision kill)](#contact-based-hazard-collision-kill)
- [Key Rule: Never Cache a Player Reference for Killing](#key-rule-never-cache-a-player-reference-for-killing)
- [Scripts Reference](#scripts-reference)

---

## Overview

Death flows through three layers:

```
Hazard
  → PlayerLife.Kill()  or  PlayerLife.KillAllInBox()
    → EventManager.OnPlayerKilled
      → GameStateManager.HandlePlayerKilled()
        → GameState.GameOver
```

Hazards never talk to `GameStateManager` directly. `PlayerLife` never knows what caused the death. `GameStateManager` never knows which hazard fired — it just reacts to the event.

---

## How Death Works

### `PlayerLife`

Attach to any player body — the main `SoftBodyPlayer` or a split droplet. Both receive one automatically.

```csharp
public void Kill()
```
Marks the body as dead and fires `EventManager.PlayerKilled()`. Calling `Kill()` multiple times on the same body is safe — it no-ops after the first call.

```csharp
public static void KillAllInBox(Vector2 center, Vector2 size)
```
Finds every player body whose softbody ring points overlap the given world-space rectangle and calls `Kill()` on each. Internally uses `Physics2D.OverlapBoxAll` on the `SoftBodyPoint` layer and resolves the owning `SoftBodyPlayer` via `SoftBodyPointRef`. Works correctly regardless of whether the player is merged or split — the caller does not need to know.

### `EventManager.OnPlayerKilled`

Fired once per body that dies. If both split droplets are inside a crusher zone simultaneously, it fires twice — but `GameStateManager` ignores duplicate events once the state is already `GameOver`.

### `GameStateManager`

Subscribes to `OnPlayerKilled` and calls `Set(GameState.GameOver)` when the game is currently `Playing`. No other script should trigger game over from a death — route everything through `PlayerLife`.

---

## Split State Awareness

When the player splits, `PlayerSplitController.SpawnDroplet` adds a `PlayerLife` component to each droplet automatically. This means:

- **Either half dying triggers game over** — no extra code needed in hazards.
- The main player's `PlayerLife` is not removed during a split — it's simply on a frozen, invisible body that hazards cannot physically reach.
- On merge, the droplets (and their `PlayerLife` components) are destroyed. The main player's `PlayerLife` resets to alive because it was never killed.

**You do not need to handle split state in hazards.** As long as a hazard uses `KillAllInBox` or gets a `PlayerLife` reference from a contact, the split case is covered for free.

---

## Adding a New Hazard

### Zone-Based Hazard (area kill)

Use this pattern when you know the kill area in world space (crushers, pits, lava pools, etc.).

```csharp
// At the moment of impact:
Vector2 worldCenter = (Vector2)transform.position + killZoneOffset;
PlayerLife.KillAllInBox(worldCenter, killZoneSize);
```

`KillAllInBox` handles all cases:
- Player merged → finds the main player's ring points
- Player split → finds whichever droplet(s) are in the zone
- Both droplets in zone → kills both (game over fires once)
- No player in zone → no-op

**Do not** cache a `SoftBodyPlayer` or `PlayerLife` reference in `Start()` for this purpose — see [the key rule below](#key-rule-never-cache-a-player-reference-for-killing).

#### Gizmo tip

Draw your kill zone in `OnDrawGizmosSelected` so designers can tune it without running the game:

```csharp
private void OnDrawGizmosSelected()
{
    Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
    Gizmos.DrawWireCube((Vector2)transform.position + killZoneOffset, killZoneSize);
}
```

### Contact-Based Hazard (collision kill)

Use this pattern when the hazard reacts to physical contact (spikes, enemies, instant-death walls).

Because the player has **no collider on the root GameObject** (see [softbody-player.md](softbody-player.md)), you cannot use `OnCollisionEnter2D` or `OnTriggerEnter2D` on the hazard itself to get a `PlayerLife` directly. Instead, react to contact with a `SoftBodyPoint` child and resolve ownership through `SoftBodyPointRef`.

```csharp
private void OnTriggerEnter2D(Collider2D other)
{
    SoftBodyPointRef pointRef = other.GetComponent<SoftBodyPointRef>();
    if (pointRef == null) return;
    pointRef.owner.GetComponent<PlayerLife>()?.Kill();
}
```

Or, if the hazard has a trigger collider that should kill on contact:

1. Set the hazard's collider to **Is Trigger = true**.
2. Ensure the hazard GameObject is on a layer that collides with `SoftBodyPoint` in the Physics 2D matrix.
3. Use the snippet above in `OnTriggerEnter2D`.

`SoftBodyPointRef.owner` is the `SoftBodyPlayer` that owns the point — its `PlayerLife` is valid whether it is the main player or a split droplet.

---

## Key Rule: Never Cache a Player Reference for Killing

Do **not** do this in `Start()`:

```csharp
// BAD — always finds the main player, misses split droplets
_playerLife = FindFirstObjectByType<SoftBodyPlayer>().GetComponent<PlayerLife>();
```

This approach silently fails when the player is split because:
- The cached reference points to the main player, which is frozen and invisible during a split.
- Split droplets are spawned at runtime and are never found by a `Start()`-time lookup.

Use `KillAllInBox` for zone hazards, or resolve via `SoftBodyPointRef` for contact hazards. Both are split-safe by design.

---

## Scripts Reference

| Script | Location | Role |
|--------|----------|------|
| `PlayerLife` | `Assets/Scripts/Player/PlayerLife.cs` | Tracks alive/dead state per body; entry point for all death |
| `EventManager` | `Assets/Scripts/Core/EventManager.cs` | Broadcasts `OnPlayerKilled` to any subscriber |
| `GameStateManager` | `Assets/Scripts/Core/GameStateManager.cs` | Subscribes to `OnPlayerKilled`; owns the `GameOver` transition |
| `SoftBodyPointRef` | `Assets/Scripts/Player/SoftBodyPointRef.cs` | Component on each ring-point child; holds a reference back to its owning `SoftBodyPlayer` |
| `CrusherTrap` | `Assets/Scripts/Interactables/CrusherTrap.cs` | Zone hazard — calls `KillAllInBox` at impact |
| `AutoCrusherTrap` | `Assets/Scripts/Interactables/AutoCrusherTrap.cs` | Zone hazard — calls `KillAllInBox` at impact |
