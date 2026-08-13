# Prefab → Tile → Tilemap Quick Start

A focused walkthrough for taking an existing prefab and getting it placeable as a tile on the Props tilemap. For the full reference (animations, sorting, activator wiring) see [`add-props.md`](add-props.md). For tilemap layers and tile palette sizing, see [`tilemaps.md`](tilemaps.md).

---

## Where everything lives

| Asset | Path |
|---|---|
| Prefab | `Assets/Prefabs/<PropName>.prefab` |
| Prop sprite (PNG) | `Assets/Art/Environment/<theme>/<PropName>.png` |
| **PropTile asset** (`.asset`) | `Assets/Tiles/Props/<PropName>_PropTile.asset` |
| Tile Palettes | `Assets/Tilemaps/{1x1,2x1,2x2}.prefab` |
| Scene tilemap GameObject | Hierarchy: `Grid → Props` |

Three palettes exist, sized by tile footprint — you pick one based on how many cells your prop covers visually:

| Palette | When to use |
|---|---|
| `1x1.prefab` | Prop sprite fits in one cell (PressurePlate, Evaporator) |
| `2x1.prefab` | Prop sprite spans two horizontal cells (Condenser) |
| `2x2.prefab` | Prop sprite spans 2×2 cells (Crusher) |

Multi-cell props still occupy only **one anchor cell** at runtime — the oversized sprite just overflows visually. The palette choice is purely so the palette preview renders cleanly.

---

## Step 1 — Make sure the prefab is ready

Open the prefab in **Prefab Mode** (double-click the `.prefab` asset) and verify:

- The behaviour script is on the prefab root (e.g. `PressurePlate`, `Evaporator`).
- All Inspector fields are configured **on the prefab asset itself**, not on a scene instance — `PropTilemapSpawner` instantiates from the asset, so scene-level overrides are lost.
- A `BoxCollider2D` (or whatever the script needs for detection) is set up on the prefab.
- If the prop has an Animator, the controller is assigned on the prefab and animation clips were recorded in Prefab Mode (not on a scene instance).
- The SpriteRenderer's **Order in Layer** is higher than the tilemaps' (otherwise the spawned prefab renders behind the tilemap and looks like it's not animating). See `add-props.md` Step 3 for full sorting guidance.

If the prop responds to a trigger (pressure plate, lever), implement `IPropConnectable` and (optionally) `IPropActivatable` — see [`prop-connections.md`](prop-connections.md).

Save the prefab to `Assets/Prefabs/`.

---

## Step 2 — Prepare the sprite

The sprite the palette uses for previews is separate from the prefab's runtime sprite. You can use the same PNG for both.

1. Select the sprite PNG in the Project window.
2. Click **Open Sprite Editor**.
3. Set the **Pivot** to match the prop's anchor:
   - Floor props (PressurePlate, Evaporator) → **Bottom**.
   - Ceiling props → **Top Center** or **Top Left** (whichever cell will be painted).
   - Wide multi-cell props anchored on the left → **Left**.
4. **Apply** and close.
5. In the sprite Inspector, set **Pixels Per Unit = 32** for a single-cell tile, or 32 per cell for multi-cell sprites (PPU is per-cell — a 64×32 two-cell sprite still uses PPU 32). See [`tilemaps.md`](tilemaps.md#1-import-the-sprite) for full import settings.

PropTiles do **not** need a Custom Physics Shape — the prefab provides its own collider.

---

## Step 3 — Create the PropTile asset

The PropTile is the bridge between a tilemap cell and a runtime prefab.

1. In the Project window, navigate to `Assets/Tiles/Props/`.
2. Right-click → **Create → Tiles → Prop Tile**.
3. Name it `<PropName>_PropTile` (e.g. `Lever_PropTile`).
4. Select the new asset and fill its Inspector fields:

| Field | Value |
|---|---|
| **Preview Sprite** | The prop sprite (full size) — used as the palette/scene preview at edit time. Hidden at runtime. |
| **Prefab** | The prefab from Step 1 (drag from `Assets/Prefabs/`). |
| **Spawn Offset** | Leave at `(0, 0, 0)` initially. Tune later if the spawned prefab appears offset from the painted cell. |

One PropTile asset per prop type — connection IDs are configured per-cell on the `PropTilemapSpawner`, not on the tile asset.

---

## Step 4 — Add the PropTile to the right palette

Pick the palette whose footprint matches your prop's visual size:

1. Open **Window → 2D → Tile Palette**.
2. From the palette dropdown at the top of the panel, select `1x1`, `2x1`, or `2x2` (whichever matches).
3. Drag the PropTile asset from `Assets/Tiles/Props/` into the palette window.

The PropTile is now ready to paint. The preview shows the full sprite; for multi-cell props it visually overflows — that's correct.

> If the prop sprite distorts the palette cell sizes when dropped in, you used the wrong palette. Drag it into the palette matching its footprint.

---

## Step 5 — Paint it onto the Props tilemap

If the scene doesn't already have a Props tilemap, create one:
1. Hierarchy → right-click **Grid** → **2D Object → Tilemap → Rectangular**.
2. Rename to `Props`.
3. Add the **PropTilemapSpawner** component.
4. Keep its Layer on a non-physics layer.

To paint:

1. In the **Tile Palette** window, set **Active Tilemap** to `Props`.
2. Pick the brush tool (`B`).
3. Click your PropTile in the palette, then click the cell in the Scene where the prop should anchor.

For multi-cell props, paint **only the one anchor cell** (left cell for 2×1, bottom-left for 2×2) — the oversized sprite handles the rest visually.

If a directional prop supports rotation (for example, a blower), rotate the painted tile with the Tile Palette rotation tool. `PropTilemapSpawner` applies the cell rotation to the runtime prefab. For a painted blower that needs unique direction or strength values, run **Sync Cell List** and enable **Override Blower Settings** for that cell in `PropTilemapSpawner`.

---

## Step 6 — Wire up connections (if applicable)

If the prop is an activatee (reacts to a trigger):

1. Select the **Props** Tilemap GameObject.
2. Right-click `PropTilemapSpawner` in the Inspector → **Sync Cell List**.
3. Find your cell in the list and set:
   - **Connection ID** — same string as the trigger that controls it.
   - **Connection Mode** — `Hold` or `Toggle`.
   - **Initial Active** — whether the prop starts on or off.

See [`prop-connections.md`](prop-connections.md) for the full connection model.

---

## Step 7 — Test in Play mode

Press Play and verify:

- The prefab appears in the Hierarchy (under Grid at runtime, spawned by `PropTilemapSpawner`).
- It renders at the correct world position and depth (in front of the tilemap, not behind).
- Animations play (open the Animator window with the spawned clone selected).
- Linked triggers and props react to each other.

If the spawn position is off by half a cell or anchored wrong, tune **Spawn Offset** on the PropTile asset.

---

## Common gotchas

- **Prefab spawns but is invisible:** Order in Layer on its SpriteRenderer is too low — bump it above the tilemaps' Order. See `add-props.md` Step 3.
- **Animation triggers but sprite never changes:** animation clip was recorded on a scene instance, not on the prefab in Prefab Mode. Re-record in Prefab Mode.
- **Painted tile is invisible / pink at edit time:** wrong palette (footprint mismatch) or the palette's preview sprite was deleted. Re-drag the PropTile into the correct palette.
- **Prefab doesn't spawn at all:** the PropTile's Prefab field is empty, or the cell is on a different tilemap (not Props). Confirm Active Tilemap was `Props` when painting.

---

## Related docs

- [`add-props.md`](add-props.md) — full prop creation reference (sorting, animation, activator templates, per-prop reference tables)
- [`prop-connections.md`](prop-connections.md) — Hold/Toggle/Initial Active and the activator/activatee event flow
- [`tilemaps.md`](tilemaps.md) — three-tilemap architecture (SolidPlatforms, Props, Platforms) and palette-by-size rationale
