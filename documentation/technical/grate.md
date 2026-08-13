# Grate — Liquid-Only Passage Barrier

This document covers the grate mechanic: a horizontal-only obstacle that the liquid player can push through with drag, while blocking every other body type.

---

## Overview

| State | Can pass through the grate? |
|-------|------------------------------|
| Liquid (`SoftBodyPlayer`, whole or split) | Yes — with viscous drag, like squeezing between bars |
| Gas cloud *(not yet implemented)* | No — blocked like a solid wall |
| Ice / solid *(planned, not yet implemented)* | No — blocked like a solid wall |

The grate is designed to sit **between a floor and ceiling tile**, with its opening running left↔right. It is not intended to be placed so the player needs to pass through it top↔bottom.

There is currently no gas cloud or ice state implemented in the codebase (`evaporator-condenser.md` documents the intended design, but `Evaporator`/`Condenser` are placeholders that only log detection — see their script headers). The grate requires no extra work to block those states once they exist: its collider is solid to every layer except `SoftBodyPoint`, so any future body that isn't explicitly excluded from the Layer Collision Matrix is blocked automatically.

---

## How It Works

### Solid by default, one exception

The grate's `BoxCollider2D` is a normal, non-trigger, solid collider on the `Grate` layer. In the Physics 2D Layer Collision Matrix, `Grate × SoftBodyPoint` is the **only** pairing unchecked — every other layer keeps the default (collides), so the grate physically blocks anything that isn't a liquid ring point.

### Drag instead of a hard stop

Because `SoftBodyPoint` never physically collides with the grate, ring points would otherwise pass through with zero resistance. `Grate.cs` polls a **Passage Zone** every `FixedUpdate` with `Physics2D.OverlapBoxNonAlloc` on the `SoftBodyPoint` layer (same detection style as `Evaporator`/`PressurePlate` — bypasses the Layer Collision Matrix, so it still finds points inside despite the disabled physical collision) and clamps each found ring point's speed to `maxPassSpeed`. This works identically for the main player and for split droplets — the script never needs to know which `SoftBodyPlayer` owns a point.

**Why a continuous clamp, not a one-shot slowdown:** `SoftBodyPlayer`'s restore/spring forces (`restoreForce ≈ 60`) reaccelerate a slowed point almost immediately — a single velocity multiply gets washed out within a frame or two and is imperceptible. Re-clamping every physics step holds the speed down for as long as the point stays inside the zone, which reads as real resistance.

**Why the zone is wider than the collider:** `zoneSize.x` defaults to `1.4`, wider than the player's ~1.1 body diameter, so the *entire* blob is caught and clamped together as it crosses — not just whichever single leading point happens to be inside a narrow gap. A too-narrow zone only slows one point at a time, which the springs immediately smooth back out and feels like no drag at all.

### Placeholder passage animation

While any ring point is inside the passage zone, the grate's own sprite pulses in scale (`passPulseScale` / `passPulseSpeed`). This is a stand-in for a future "slime squeezing through bars" effect once real bar art exists — it does not touch the player's mesh or physics.

---

## Script: `Grate.cs`

| Field | Purpose |
|-------|---------|
| `maxPassSpeed` | Max speed (m/s) a ring point may have while inside the passage zone — reapplied every physics step. Lower = harder to squeeze through. Default `1.2` |
| `zoneCenter` / `zoneSize` | Local-space passage detection zone (blue gizmo when selected). `zoneSize.x` should exceed the full body width (~1.1) so the whole blob is caught at once |
| `passPulseScale` / `passPulseSpeed` | Placeholder visual feedback while occupied |

Does **not** implement `IPropConnectable` / `IPropActivatable` — the grate has no on/off state, it is always solid. `PropTilemapSpawner` skips both calls automatically for props that don't implement them.

---

## One-Time Unity Setup

1. Create a layer named `Grate` (Project Settings → Tags and Layers).
2. Physics 2D → Layer Collision Matrix → uncheck `Grate × SoftBodyPoint`. Leave every other `Grate × X` pairing checked.
3. Assign the Grate prefab's GameObject to the `Grate` layer.

---

## Creating the Grate GameObject

1. **Create the prefab**
   - Create an empty GameObject, name it `Grate`.
   - Set its Layer to `Grate` (see setup above).
   - Add a `SpriteRenderer` — for now, assign a plain white square sprite (any `Sprite (2D and UI)` texture works; a 1×1 white pixel scaled up is fine as a placeholder).
   - Add the `Grate` script (this repo: `Assets/Scripts/Interactables/Grate.cs`).
   - Add a `BoxCollider2D`, sized to the grate's full visual footprint (the bars). Leave **Is Trigger** unchecked — it must stay solid for everything except `SoftBodyPoint`.
   - In the Inspector, set **Order in Layer** on the SpriteRenderer higher than the tilemaps' order (same rule as every other prop — otherwise it renders behind the tilemap).
   - Tune the **Passage Zone** (`zoneCenter` / `zoneSize`) so the blue gizmo lines up with the opening between the bars.
   - Drag the finished GameObject into `Assets/Prefabs/` to save it as a prefab, matching the project convention (see `add-props.md`).

2. **Create the PropTile asset**
   - In `Assets/Tiles/Props/`, right-click → **Create → Tiles → Prop Tile**.
   - Name it `Grate_PropTile`.
   - **Preview Sprite** — the same placeholder white square.
   - **Prefab** — the `Grate` prefab from step 1.
   - **Spawn Offset** — leave at `(0, 0, 0)` initially.

3. **Add it to a Tile Palette**
   - Open **Window → 2D → Tile Palette**.
   - Pick the palette matching the grate's footprint (`1x1` if it fits one cell).
   - Drag `Grate_PropTile` into the palette.

4. **Paint it in the scene**
   - Select the **Props** Tilemap, set **Active Tilemap** to `Props` in the Tile Palette window.
   - Paint the grate cell **between a floor tile and a ceiling tile** on `SolidPlatforms`, so the only way through is left↔right.

5. **Test in Play mode**
   - Confirm the whole liquid player passes through with visible drag.
   - Split (Left Shift) and confirm both droplets can independently pass through.
   - Confirm nothing currently in the scene can walk over/around it in a way that breaks the intended left↔right-only passage (no floor/ceiling gaps around the grate).

See [`add-props.md`](add-props.md) and [`prefab-to-tile.md`](prefab-to-tile.md) for the general prop workflow this follows.

---

## Troubleshooting

### Sprite is invisible even with Order in Layer set correctly

The `Grate` layer is newly created, and Unity does **not** auto-include new layers in a camera's render output. Select the **Main Camera** → Inspector → Rendering → **Culling Mask** → tick `Grate`. (Same gotcha documented for the `Platform` layer in `tilemaps.md`.)

### No drag felt when passing through

- Confirm `zoneSize.x` is wide enough to cover the whole player body (~1.1 by default) — a zone narrower than the body only catches one ring point at a time, and the softbody's spring/restore forces smooth that out almost instantly, feeling like no drag at all.
- Confirm you're testing with the player actually crossing through the zone, not just standing near it — check the blue gizmo lines up with where the player's ring points actually travel.
- Lower `maxPassSpeed` for a more obvious effect while testing.
- This does **not** require the prop to be placed on the Props tilemap — it works as a standalone prefab in the scene, since detection and drag are handled entirely by `Grate.cs`, independent of `PropTilemapSpawner`.
