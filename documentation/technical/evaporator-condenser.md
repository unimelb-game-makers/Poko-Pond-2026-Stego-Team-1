# Evaporator & Condenser — Evaporation / Condensation System

This document covers the evaporation mechanic: how the water-droplet player converts to a gas cloud via an Evaporator prop and returns to liquid form via a Condenser prop.

---

## Overview

| State | Form | Physics |
|-------|------|---------|
| **Liquid** | SoftBodyPlayer (spring-based blob) | Normal gravity, full jump, ground movement |
| **Gas** | GasCloud (simple Rigidbody2D) | Reduced gravity (~35%), terminal fall speed cap, high air control, upward drift assist |

The only way to enter gas form is an **Evaporator**. The only way to return to liquid is a **Condenser**. The player has no manual control over the transition.

---

## Evaporator

### What it does
When the liquid player stands on an active Evaporator:
1. The `SoftBodyPlayer` is frozen and hidden.
2. A `GasCloud` spawns at the same position with an upward velocity burst.
3. The player controls the gas cloud with reduced gravity and high air control.

### Art
- Sprite sheet: `evaporator.png`
- Has an idle looping animation that plays while active.
- Animation stops (transitions to Off state) when the evaporator is deactivated by a trigger.

### Animator setup
| Parameter | Type | Meaning |
|-----------|------|---------|
| `IsActive` | Bool | `true` = idle animation playing; `false` = off state |

State machine:
```
Entry → Idle (default)
Off → Idle  : IsActive = true,  Has Exit Time off
Idle → Off  : IsActive = false, Has Exit Time off
```

### Detection
`Physics2D.OverlapBoxAll` on `"Player"` + `"SoftBodyPoint"` layers, polled every frame. Conversion is instant on contact — no hold time. The detection zone is cached from the BoxCollider2D bounds in `Start()` — the zone sits above `b.max.y` (top surface of the collider), not above `b.center.y`.

### Activation config (set in PropTilemapSpawner Cell Overrides)
- **Initial Active `true`** — evaporator is on by default; a linked plate turns it off.
- **Initial Active `false`** — evaporator is off by default; a linked plate turns it on.
- **Hold** — active only while trigger is pressed.
- **Toggle** — each trigger press flips state permanently.

### Script: `Evaporator.cs`
Implements `IPropConnectable` + `IPropActivatable`. Key fields:

| Field | Purpose |
|-------|---------|
| `burstSpeed` | Speed of the upward velocity burst applied to the gas cloud on evaporation |
| `detectionHeight` | Height of the OverlapBox above the collider surface |

---

## Condenser

### What it does
When a gas cloud enters the left side of an active Condenser:
1. The `GasCloud` is destroyed.
2. The original `SoftBodyPlayer` is unfrozen and restored at the condenser's position.
3. A brief fade-in and merge pop animation plays.

### Art
- Sprite sheet: `condenser_animation.png` (5 frames, 64×32px — spans 2 tile cells)
- No idle animation currently. A `Condense` trigger animation plays once on condensation.

### Animator setup
| Parameter | Type | Meaning |
|-----------|------|---------|
| `Condense` | Trigger | Fires when condensation occurs — plays the animation once |

State machine:
```
Entry → Idle (static first frame, looping)
Any State → Condense : Condense triggered, Has Exit Time off
Condense → Idle      : Has Exit Time on (let clip finish)
```
**Important:** Loop Time must be **off** on the Condense clip.

### Entry direction
The condenser entrance is on its **left side**. Detection uses `Physics2D.OverlapBox` on the `"GasCloud"` layer at the left edge of the BoxCollider2D. A gas cloud approaching from the right cannot trigger condensation.

Tune **Entry Zone Width** and **Entry Zone Height** in the Inspector so the blue gizmo (visible when the condenser is selected in Play mode) aligns with the opening in the sprite art.

### Collision
The condenser's BoxCollider2D (on the `Props` layer) **physically blocks** the liquid player (SoftBodyPoint × Props = enabled in Layer Collision Matrix). The gas cloud **passes through** it physically (GasCloud × Props = disabled), so it can enter the detection zone from the left.

### Activation config (set in PropTilemapSpawner Cell Overrides)
Same Hold/Toggle/Initial Active system as Evaporator. See `prop-connections.md`.

### Script: `Condenser.cs`
Implements `IPropConnectable` + `IPropActivatable`. Key fields:

| Field | Purpose |
|-------|---------|
| `entryZoneWidth` | Width of the left-side detection zone |
| `entryZoneHeight` | Height of the left-side detection zone |

---

## Gas Cloud (`GasCloud.cs`)

Spawned exclusively by `PlayerSplitController.SpawnGasCloud()` — never placed in the scene directly.

### Physics
| Property | Value | Notes |
|----------|-------|-------|
| Gravity Scale | ~0.35 | Set on the Rigidbody2D in Awake |
| Max Fall Speed | 2 m/s | Hard cap — cloud sinks slowly |
| Vertical Drift | Up input adds upward force | Counteracts gravity without full anti-gravity |
| Horizontal | Full move force, speed cap | High air control — steerable mid-air |

### Visuals
Procedural fan mesh — N ring vertices with per-vertex sinusoidal noise and a global breathing pulse. Outer ring uses lower-alpha colour to soften the silhouette. The face sprite is a child SpriteRenderer copied from the linked SoftBodyPlayer. Both mesh and face are in world space — no special camera handling needed.

**Material note:** The gas cloud uses the same `bodyMaterial` as `SoftBodyPlayer`. The material's **Surface Type must be Transparent** (URP material Inspector) for the semi-transparent outer edge to render correctly.

### Layer
`GasCloud` layer (must be created in Project Settings → Tags and Layers).

Layer Collision Matrix requirements:

| Pair | Setting | Reason |
|------|---------|--------|
| GasCloud × Ground | Enabled | Cloud collides with terrain |
| GasCloud × Props | Disabled | Cloud passes through condenser collider |
| GasCloud × Player | Disabled | No collision with liquid player |
| GasCloud × SoftBodyPoint | Disabled | No collision with liquid player ring points |
| GasCloud × GasCloud | Disabled | Two clouds don't collide with each other |

### Split droplet behaviour
Each split droplet evaporates independently. Only the droplet touching the Evaporator converts — the other stays liquid. The active/passive slot assignment follows the same Tab-swap logic as liquid droplets.

**Merge rules between split droplets:**

| Droplet 0 | Droplet 1 | Merge result |
|-----------|-----------|-------------|
| Liquid | Liquid | Standard liquid merge (existing behaviour) |
| Gas | Gas | Merge into a single gas cloud — still needs a condenser to return to liquid |
| Liquid | Gas | **Blocked** — brief outward pulse on both entities signals the mismatch |

---

## PlayerSplitController API

| Method | Called by | Purpose |
|--------|-----------|---------|
| `TryEvaporate(SoftBodyPlayer sp, Vector2 burst)` | `Evaporator.Update()` | Converts sp to gas. Returns false if already gas. |
| `TryCondense(GasCloud gc, Vector2 position)` | `Condenser.Update()` | Restores liquid form at position. Returns false if gc not tracked. |

---

## EventManager Events

| Event | Fired when |
|-------|-----------|
| `OnPlayerEvaporate` | Any droplet (main or split) becomes a gas cloud |
| `OnPlayerCondense` | Any gas cloud condenses back to liquid |

Subscribe to these from any script that needs to react to the player's physical state changing (e.g. UI indicators, audio).

---

## Scene Setup Checklist

- [ ] `GasCloud` layer created in Project Settings → Tags and Layers
- [ ] Layer Collision Matrix configured (see table above)
- [ ] `Props` layer created (for condenser physical blocking)
- [ ] Condenser prefab layer set to `Props`
- [ ] Condenser BoxCollider2D sized to full 64×32 sprite footprint
- [ ] Condenser Entry Zone Width/Height tuned to left-side opening (verify with blue gizmo in Play mode)
- [ ] Evaporator BoxCollider2D sized to surface footprint
- [ ] Evaporator Animator configured with `IsActive` Bool parameter
- [ ] Condenser Animator configured with `Condense` Trigger, Loop Time off on Condense clip
- [ ] Body material Surface Type set to Transparent for gas cloud alpha blending
- [ ] Both props added to Tile Palette via PropTile assets
- [ ] PropTilemapSpawner → Sync Cell List → Connection ID / Mode / Initial Active configured per cell
