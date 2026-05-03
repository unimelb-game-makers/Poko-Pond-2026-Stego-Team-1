# Tilemap System

### Table of Contents
- [Overview](#overview)
- [The Three Tilemaps](#the-three-tilemaps)
- [Tile Palettes by Size](#tile-palettes-by-size)
- [Asset Locations](#asset-locations)
- [Adding a New Tile](#adding-a-new-tile)
  - [1. Import the Sprite](#1-import-the-sprite)
  - [2. Set the Custom Physics Shape](#2-set-the-custom-physics-shape)
  - [3. Create the Rule Tile Asset](#3-create-the-rule-tile-asset)
  - [4. Add to the Right Palette](#4-add-to-the-right-palette)
- [Tilemap Scene Setup](#tilemap-scene-setup)
  - [SolidPlatforms](#solidplatforms)
  - [Props](#props)
  - [Platforms](#platforms)
- [Editing the Map](#editing-the-map)
- [Layer Collision Matrix](#layer-collision-matrix)
- [Camera Culling Mask](#camera-culling-mask)

---

## Overview

Levels are built from three sibling tilemaps under a single **Grid** GameObject. Each tilemap has a distinct collision role and renders independently. Tile sprites are organised into three palette assets sized by the tile footprint: 1×1, 2×1, and 2×2.

The player is a softbody (multiple Rigidbody2D ring points) — collisions are real Unity physics, not raycast-based.

---

## The Three Tilemaps

```
Grid
├── SolidPlatforms    ← solid geometry (ground, walls, ceilings)
├── Props             ← spawns interactive prefabs at runtime
└── Platforms         ← one-way (jump-through, drop-through with S)
```

| Tilemap | GameObject Layer | Purpose | Player passes through? |
|---|---|---|---|
| **SolidPlatforms** | `Ground` | Solid geometry the softbody collides with from every direction | Never |
| **Props** | (no physics layer) | Anchor cells that `PropTilemapSpawner` reads at Start to instantiate prefabs (PressurePlate, Crusher, Evaporator, etc.). Tile sprites are hidden at runtime — the prefab provides all visuals and colliders | N/A — props handle their own colliders |
| **Platforms** | `Platform` | One-way platforms — solid from above, pass-through from below; drop through with S | Yes (jump-up + drop) |

For Props, see [`add-props.md`](add-props.md) and [`prop-connections.md`](prop-connections.md). The drop-through behaviour for Platforms is implemented by `PlatformDropThrough` on the SoftBodyPlayer GameObject.

---

## Tile Palettes by Size

Different tile assets occupy different cell footprints. Mixing different-sized tiles in the same palette breaks the palette grid preview — Unity sizes the palette grid to fit the largest tile, distorting smaller previews. The fix: one palette per cell footprint.

| Palette | File | Footprint | Use for |
|---|---|---|---|
| **1×1** | `Assets/Tilemaps/1x1.prefab` | 1 cell | Standard floor/wall tiles, single-cell props (Pressure Plate, Evaporator) |
| **2×1** | `Assets/Tilemaps/2x1.prefab` | 2 wide × 1 tall | Wide props that span two cells (Condenser) |
| **2×2** | `Assets/Tilemaps/2x2.prefab` | 2 wide × 2 tall | Large props or geometry pieces (Crusher) |

**Rule:** when adding a new tile asset, drop it into the palette whose footprint matches the tile's full visual size. A `PropTile` previewing a 64×32 sprite (2×1 cells at 32 PPU) goes in the **2×1 palette** — never the 1×1 palette, even though only one anchor cell is painted at runtime.

> Multi-cell **PropTiles** still occupy only one anchor cell on the actual tilemap — the oversized sprite simply overflows visually. The palette choice is purely for clean preview rendering at edit time.

---

## Asset Locations

| Asset type | Location |
|---|---|
| Tile sprite PNGs | `Assets/Art/Environment/Tiles/<theme>/` |
| Rule Tile assets (`.asset`) | Same folder as their sprites |
| `PropTile` assets (`.asset`) | `Assets/Tiles/Props/` |
| Tile Palette prefabs | `Assets/Tilemaps/{1x1,2x1,2x2}.prefab` |

---

## Adding a New Tile

### 1. Import the Sprite

1. Place the PNG in the appropriate `Assets/Art/Environment/Tiles/<theme>/` folder.
2. Select the sprite in the Project window and configure Import Settings:

| Setting | Value |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single |
| Pixels Per Unit | Match the sprite's pixel dimensions per cell (e.g. 32 for a 32×32 single-cell tile, **also** 32 for a 64×32 two-cell sprite — PPU is per-cell, not per-sprite) |
| Filter Mode | Point (no filter) |
| Compression | None |

3. Click **Apply**.

> **Pixels Per Unit is per-cell.** For multi-cell sprites, divide pixel size by cell count: a 64×32 sprite covering 2×1 cells uses PPU 32 (32 px per cell).

---

### 2. Set the Custom Physics Shape

Required for SolidPlatforms and Platforms tiles (any tile with collision). PropTiles don't need a physics shape — their prefab provides the collider.

1. Select the sprite → **Open Sprite Editor**.
2. Top-left dropdown → **Custom Physics Shape**.
3. Click **Generate** to auto-trace the visible outline.
4. Trim points so the shape covers only the solid surface — for edge tiles, exclude transparent areas.
5. **Apply** and close.

> Without a Custom Physics Shape, the TilemapCollider2D's Shape Count stays 0 and the player falls through.

---

### 3. Create the Rule Tile Asset

For SolidPlatforms / Platforms geometry tiles:

1. Right-click in the same folder → **Create → 2D → Tiles → Rule Tile**.
2. Name to match the sprite (e.g. `TileCenter`, `PlatformThin`).
3. **+** under Tiling Rules → drag the sprite into the rule's Sprite slot.
4. Leave neighbour boxes as **Don't Care** unless you want context-aware auto-tiling.
5. Confirm **Collider Type = Sprite** on the rule.

For Props, use `PropTile` instead — see [`add-props.md`](add-props.md).

---

### 4. Add to the Right Palette

1. Open **Window → 2D → Tile Palette**.
2. From the dropdown, choose the palette whose footprint matches your tile (`1x1`, `2x1`, or `2x2`).
3. Drag the Rule Tile (or PropTile) asset into the palette window.

---

## Tilemap Scene Setup

All three tilemaps live as siblings under a single **Grid** GameObject. Create the Grid via **Hierarchy → right-click → 2D Object → Tilemap → Rectangular** (the first one creates Grid + Tilemap together; subsequent ones are added as siblings via right-click on Grid).

### SolidPlatforms

The fully-solid geometry tilemap.

**Components on the SolidPlatforms GameObject:**

| Component | Key Settings |
|---|---|
| Tilemap | (auto) |
| Tilemap Renderer | Sorting Layer + Order to suit your scene |
| TilemapCollider2D | Composite Operation → **Merge** |
| Rigidbody2D | Body Type → **Static** |
| CompositeCollider2D | Geometry Type → **Polygons** |

**Layer:** `Ground`. SoftBodyPlayer's **Ground Layer** field must include `Ground`.

---

### Props

Spawns prefabs at runtime instead of rendering tile sprites.

**Components on the Props GameObject:**

| Component | Notes |
|---|---|
| Tilemap | (auto) |
| Tilemap Renderer | Disabled at runtime by `PropTilemapSpawner` — sprites are placement guides only |
| **PropTilemapSpawner** | Reads each `PropTile` cell, instantiates the prefab, applies Connection ID / Mode / Initial Active per cell |

**No collider components.** Each spawned prefab provides its own collider.

**Layer:** any non-physics layer (Default is fine).

See [`add-props.md`](add-props.md) for full Props workflow.

---

### Platforms

One-way pass-through platforms.

**Components on the Platforms GameObject:**

| Component | Key Settings |
|---|---|
| Tilemap | (auto) |
| Tilemap Renderer | Sorting Layer + Order to suit your scene |
| TilemapCollider2D | Composite Operation → **Merge** |
| Rigidbody2D | Body Type → **Static** |
| CompositeCollider2D | **Used By Effector** ✓ |
| PlatformEffector2D | Use One Way ✓, Surface Arc 180, Rotational Offset 0 |

**Layer:** `Platform` — must exist in Project Settings → Tags and Layers.

**Drop-through:** the `PlatformDropThrough` component on SoftBodyPlayer toggles the Platforms tilemap's `Rigidbody2D.simulated` flag for ~0.3s when S is held or when ring points have notable upward velocity. No setup beyond attaching the component is required — its defaults match the layer name.

> CompositeCollider2D + Rigidbody2D simulation toggling is what enables drop-through across all ring points at once. Per-collider `IgnoreLayerCollision` does not reliably break active softbody contacts.

---

## Editing the Map

1. Open **Window → 2D → Tile Palette**.
2. Pick the palette matching your tile's footprint (1×1, 2×1, 2×2).
3. In the Tile Palette window, set **Active Tilemap** to whichever of `SolidPlatforms`, `Props`, or `Platforms` you're editing.
4. Use the brush tools:

| Tool | Shortcut |
|---|---|
| Paint Brush | B |
| Box Fill | U |
| Eraser | D |
| Pick | I |

> After painting solid or platform geometry, check the TilemapCollider2D **Shape Count** — must be > 0. If 0, the tile is missing its Custom Physics Shape.

---

## Layer Collision Matrix

In **Project Settings → Physics 2D → Layer Collision Matrix**, ensure the following pairs are enabled:

| Pair | Enabled |
|---|---|
| Player × Ground | ✓ |
| SoftBodyPoint × Ground | ✓ |
| Player × Platform | ✓ |
| SoftBodyPoint × Platform | ✓ |
| SoftBodyPoint × SoftBodyPoint | ✗ (disabled — points must not collide with each other) |

---

## Camera Culling Mask

The Main Camera's Culling Mask must include every layer used for tilemap rendering, including any new layers added later. When you create a new layer (`Platform` for instance) Unity does **not** auto-include it — tiles will collide invisibly until you tick the layer in **Camera → Rendering → Culling Mask**.
