# Tilemap System

### Table of Contents
- [Overview](#overview)
- [Asset Locations](#asset-locations)
- [Adding a New Tile](#adding-a-new-tile)
  - [1. Import the Sprite](#1-import-the-sprite)
  - [2. Set the Custom Physics Shape](#2-set-the-custom-physics-shape)
  - [3. Create the Rule Tile Asset](#3-create-the-rule-tile-asset)
  - [4. Add to the Tile Palette](#4-add-to-the-tile-palette)
- [Tilemap Scene Setup](#tilemap-scene-setup)
  - [Hierarchy Structure](#hierarchy-structure)
  - [Required Components](#required-components)
  - [Layer Assignment](#layer-assignment)
- [Editing the Map](#editing-the-map)
- [Platform Collision Behaviour](#platform-collision-behaviour)

---

## Overview

The game uses Unity's Tilemap system for all ground and platform surfaces. Platforms are fully solid — the player cannot jump through them from below, and there is no drop-through mechanic.

The player uses a **raycast-based movement system** (not Rigidbody2D physics). The tilemap's colliders are detected by `Physics2D.Raycast`, so the tilemap must be on the correct layer for ground detection to work.

---

## Asset Locations

| Asset type | Location |
|---|---|
| Tile sprite PNGs | `Assets/Art/Environment/Tiles/<theme>/` |
| Rule Tile assets (`.asset`) | Same folder as their sprites |
| Tile Palette assets | `Assets/Tilemaps/` |

**Example — factory theme:**
```
Assets/
  Art/
    Environment/
      Tiles/
        SolidFloor/
          factory/
            factory_tile.png
            factory_tile_edge_left.png
            factory_tile_edge_right.png
            TileCenter.asset          ← Rule Tile
            TileEdgeLeft.asset        ← Rule Tile
            TileEdgeRight.asset       ← Rule Tile
  Tilemaps/
    Factory.prefab                    ← Tile Palette
```

---

## Adding a New Tile

### 1. Import the Sprite

1. Place the PNG in the appropriate `Assets/Art/Environment/Tiles/<theme>/` folder
2. Select the sprite in the Project window and configure the Import Settings:

| Setting | Value |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single |
| Pixels Per Unit | Match the sprite's pixel dimensions (e.g. 16 for a 16×16 sprite) |
| Filter Mode | Point (no filter) |
| Compression | None |

3. Click **Apply**

> **Pixels Per Unit is critical.** If PPU doesn't match the sprite size, tiles will render smaller than their grid cell and leave gaps.

---

### 2. Set the Custom Physics Shape

Every tile sprite **must** have a Custom Physics Shape defined, otherwise the TilemapCollider2D will generate no collision shapes for that tile (Shape Count stays 0) and the player will fall through.

1. Select the sprite → click **Open Sprite Editor** in the Inspector
2. In the top-left dropdown of the Sprite Editor, select **Custom Physics Shape**
3. Click **Generate** to auto-trace the visible outline
4. Adjust the generated points if needed — for edge tiles, trim the shape to match only the platform surface area you want to be solid
5. Click **Apply** and close the Sprite Editor

> For edge tiles (e.g. `factory_tile_edge_left`), the sprite doesn't fill the full tile cell. Shape the physics outline to cover only the visible ledge so the collider matches the art exactly.

---

### 3. Create the Rule Tile Asset

1. In the Project window, right-click inside the same folder as the sprite
2. **Create > 2D > Tiles > Rule Tile**
3. Name it to match the sprite (e.g. `TileEdgeLeft`)
4. Select the new asset → in the Inspector, click **+** under Tiling Rules
5. Drag the matching sprite into the **Sprite** slot of that rule
6. Leave all neighbour direction boxes as **Don't Care** (the default green arrows/x marks) unless you need context-aware auto-tiling
7. Confirm **Collider Type** on the rule is set to **Sprite** (not None)

---

### 4. Add to the Tile Palette

1. Open **Window > 2D > Tile Palette**
2. Select the relevant palette from the dropdown (e.g. `Factory`)
3. Drag the new Rule Tile asset into the palette window

The tile is now ready to paint.

---

## Tilemap Scene Setup

This section describes how to create a new Tilemap from scratch. If a Tilemap already exists in the scene, skip to [Editing the Map](#editing-the-map).

### Hierarchy Structure

```
Grid
└── SolidPlatforms        ← Tilemap GameObject
```

Create via: **Hierarchy > right-click > 2D Object > Tilemap > Rectangular**

---

### Required Components

All of the following must be on the **SolidPlatforms** Tilemap GameObject (not the Grid parent):

| Component | Key Settings |
|---|---|
| **Tilemap** | (added automatically) |
| **Tilemap Renderer** | (added automatically) |
| **TilemapCollider2D** | Composite Operation → `Merge` |
| **CompositeCollider2D** | Geometry Type → `Polygons` |
| **Rigidbody2D** | Body Type → `Static` (auto-added by CompositeCollider2D) |

> The Rigidbody2D is required by CompositeCollider2D but must be set to **Static** so the tilemap never moves. The player's raycast-based movement is not affected by this — `Physics2D.Raycast` detects static colliders normally.

Setting Composite Operation to `Merge` on the TilemapCollider2D causes it to merge all adjacent tile colliders into one optimised shape, which improves performance and prevents raycasts from catching on internal tile edges.

---

### Layer Assignment

- Set the **SolidPlatforms** Tilemap GameObject's Layer to **Ground**
- Leave the **Grid** parent on the Default layer — only the Tilemap child needs to be on Ground
- The PlayerMovement script's **Ground Layer** field must include the Ground layer

---

## Editing the Map

1. Open **Window > 2D > Tile Palette**
2. Select the correct palette (e.g. `Factory`) from the dropdown
3. In the Scene or Tile Palette view, select the **SolidPlatforms** Tilemap as the active target
4. Choose a tile from the palette and use the tools to paint:

| Tool | Shortcut | Use |
|---|---|---|
| Paint Brush | B | Paint individual tiles |
| Box Fill | U | Fill a rectangular area |
| Eraser | D | Remove tiles |
| Pick | I | Sample a tile already in the scene |

**Typical platform layout:**
- **Bottom floor** — fill a full horizontal row with `TileCenter`
- **Floating platform** — `TileEdgeLeft` + one or more `TileCenter` + `TileEdgeRight`

> After painting, check the TilemapCollider2D **Shape Count** in the Inspector — it should be greater than 0. If it reads 0, a tile is missing its Custom Physics Shape (see [Step 2](#2-set-the-custom-physics-shape)).

---

## Platform Collision Behaviour

Platforms are fully solid in all directions:

- **Landing on top** — a downward raycast from the player's feet snaps them to the surface
- **Hitting the underside** — an upward raycast from the player's head stops upward velocity immediately; the player cannot jump through platforms from below
- **Drop-through** — not implemented; there is no way for the player to fall through a platform intentionally

Both raycasts are visualised as gizmos in the Scene view when the Player GameObject is selected (green/red at feet, cyan at head).
