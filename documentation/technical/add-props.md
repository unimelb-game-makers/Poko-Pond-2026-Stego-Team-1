# Adding Environmental Props via Tilemaps

Environmental props (pressure plates, crushers, levers, doors, etc.) are placed through the **Props tilemap** rather than by dragging prefabs into the scene. This keeps level building consistent — geometry and props are both drawn with the same Tile Palette workflow.

For how activators connect to activatees, see [`prop-connections.md`](prop-connections.md).

---

## How It Works

Two assets drive the system:

| Asset | Script | Purpose |
|-------|--------|---------|
| `PropTile` | `PropTile.cs` | Tile — spawns a prefab at runtime |
| Props Tilemap | `PropTilemapSpawner.cs` | Reads the tilemap at Start, instantiates all prefabs, hides tile sprites |

At runtime, `PropTilemapSpawner` scans every cell, instantiates prefabs for `PropTile` cells, disables the `TilemapRenderer` so tile sprites are hidden, and applies connection IDs from the Cell Overrides list. Everything is editor-only for placement — prefabs provide all runtime visuals and logic.

**Multi-cell props** (e.g. a 2×2 crusher) use a single `PropTile` with an oversized sprite — the sprite covers the full visual footprint while only occupying one cell on the tilemap. No filler tiles are needed.

---

## Scene Setup (one-time per level)

1. Select the **Grid** GameObject in the Hierarchy.
2. Right-click → **2D Object → Tilemap → Rectangular**. Rename it `Props`.
3. Add the **PropTilemapSpawner** component to the `Props` Tilemap GameObject.
4. In the Tile Palette window (**Window → 2D → Tile Palette**), set **Active Tilemap** to `Props`.

Keep the Props tilemap on a layer with **no physics collisions** — prop collision is handled by the prefabs themselves.

---

## Creating a New Prop — Step by Step

### Step 1 — Build the prefab

Create a prefab as normal:

- Add the relevant script (e.g. `PressurePlate`, `Lever`) as a component.
- Configure all Inspector fields on the **prefab asset** in the Project window — not on a scene instance. The tilemap system spawns from the asset, so scene-level Inspector overrides are lost at runtime.
- If the prop connects to other props (a plate activating a crusher), implement `IPropConnectable` on the script — see [`prop-connections.md`](prop-connections.md).
- Save the prefab to `Assets/Prefabs/`.

### Step 2 — Prepare the sprite

**Pivot:** The Props tilemap uses **Tile Anchor `(0.5, 0, 0)`**, aligning sprites to the bottom of each cell.

1. Select the prop sprite in the Project window → click **Sprite Editor**.
2. Set **Pivot** to **Bottom** for floor props. For ceiling-mounted props (like a crusher hanging down), use **Top Center** or **Top Left** — whichever corner will be the painted cell.
3. Click **Apply**.

**Multi-cell props:** make the sprite the full physical size of the prop's footprint. At 32 PPU, a 2×2 tile prop needs a 64×64px sprite. The sprite will overflow visually into adjacent cells — that is intentional and correct. Only one tile cell is ever painted on the tilemap.

### Step 3 — Set the Sorting Layer / Order in Layer

Prefabs spawned by the tilemap render at world position but will appear behind the tilemap unless their SpriteRenderer is configured to draw in front.

1. Open the prefab in the Project window.
2. Select the root (or whichever child has the SpriteRenderer).
3. Check what **Sorting Layer** and **Order in Layer** the scene tilemaps use (select a Tilemap → look at its **Tilemap Renderer** component).
4. Set the prop's **Order in Layer** higher than the tilemaps. If the geometry tilemap is Order 0, set the prop to Order 1 or 2.
5. Repeat for any child SpriteRenderers.

**If you skip this step:** the Animator state will change (visible in the Animator window) but nothing looks different visually — sprite swaps happen behind the tilemap.

### Step 4 — Verify animation bindings

Unity animation clips bind property curves to GameObjects by their **relative path from the Animator**. If the animation was recorded on a scene instance that differs from the prefab asset, bindings silently fail on spawned clones — the state plays but sprites do not change.

To verify and fix:

1. With the prop selected, open **Window → Animation → Animation**.
2. Check each property row — it shows `ObjectName : Property`. The `ObjectName` must exactly match the name of the GameObject (or child path) the curve targets.
3. If a binding is wrong, double-click the **prefab asset** to open it in Prefab Mode and re-record the animation there — not on a scene instance.
4. Ensure the Animator Controller is assigned on the **prefab asset**, not as a scene override.

### Step 5 — Create the PropTile asset

1. In `Assets/Tiles/Props/`, right-click → **Create → Tiles → Prop Tile**.
2. Name it clearly, e.g. `PressurePlate_PropTile`.
3. Fill in:
   - **Preview Sprite** — the prop sprite (full size). Used as a placement guide in the editor only — hidden at runtime. For multi-cell props this will visually overflow into adjacent palette cells, which is fine.
   - **Prefab** — the prefab from Step 1.
   - **Spawn Offset** — leave at `(0, 0, 0)` initially. Adjust after testing if the prefab appears offset.

One `PropTile` asset per prop type — connection IDs are set per-cell in PropTilemapSpawner, not on the tile asset.

### Step 6 — Add the tile to the Tile Palette

1. Open **Window → 2D → Tile Palette**.
2. Drag the `PropTile` asset into the palette.

One entry per prop type.

### Step 7 — Paint props in the scene

1. Set **Active Tilemap** to `Props`.
2. Select the **Brush** tool.
3. Click the cell where the prop should anchor (bottom for floor props, top for ceiling props).

For multi-cell props, paint only the **one anchor cell** — the oversized sprite handles the rest visually.

### Step 8 — Set connection IDs (for linked props)

If the prop needs to connect to another prop (plate → crusher):

1. Select the **Props** Tilemap GameObject.
2. In the **PropTilemapSpawner** component, right-click → **Sync Cell List**.
   - All PropTile cells appear in the list with their prop name and cell coordinates.
   - Existing connection IDs are preserved on re-sync.
3. Find the two cells you want to link and type the **same string** in both Connection ID fields (e.g. `crusher_a`).
4. With PropTilemapSpawner selected, the Scene view labels and colour-codes each cell — linked props share the same colour.

See [`prop-connections.md`](prop-connections.md) for the full connection system.

### Step 9 — Test in Play mode

Enter Play mode and check:

- The spawned GameObject appears in the Hierarchy under the Grid.
- The prop is at the correct world position and depth.
- Animation plays correctly (open the Animator window with the clone selected to watch state changes).
- Linked props respond to each other.

Adjust **Spawn Offset** on the `PropTile` asset if the position needs tuning.

---

## Implementing a New Activator (Lever, Button, etc.)

An activator is a prop the player interacts with that fires an event. Model new activators on `PressurePlate`:

1. **Implement `IPropConnectable`** — one method, sets the internal ID:
   ```csharp
   public class Lever : MonoBehaviour, IPropConnectable
   {
       [SerializeField] private string leverId;
       public void SetConnectionId(string id) => leverId = id;
   }
   ```
2. **Fire through EventManager** — call `EventManager.PressurePlateActivated(id)` on interaction, or add a new event pair to `EventManager` if the semantics are distinct.
3. **Cache the detection zone in `Start()`** from collider bounds — never re-read bounds each frame, as animations can shift them.
4. **Animate via an Animator** with a Bool parameter. Document the parameter name in the script header.
5. **Set Sorting Layer** on the prefab before testing (Step 3 above).

---

## Current Props Reference

### Pressure Plate (1×1)

| Field | Value |
|-------|-------|
| Tile asset | `PressurePlate_PropTile` |
| Anchor cell | The single painted cell |
| Prefab | `PressurePlate` prefab |
| Sprite pivot | Bottom |
| Sorting Order | Higher than all tilemaps |
| Implements | `IPropConnectable` → sets `plateId` |
| Animator parameter | `IsPressed` (Bool) |
| Connection | Match Connection ID with linked crusher in Cell Overrides |

---

## Fixing the Pink Tile Bug

Unity leaves behind a pink placeholder cell when a tile asset is deleted from the Project while the Tilemap still references it.

**Preferred fix — Eraser in Tile Palette:**
1. Open **Window → 2D → Tile Palette**.
2. Set **Active Tilemap** to the affected tilemap — confirm this matches.
3. Select the **Eraser** tool (E) and click each pink cell.

**If the eraser does not work:** the Active Tilemap dropdown is pointing to the wrong tilemap. Select the affected Tilemap GameObject in the Hierarchy first.

**Nuclear option:**
```csharp
[ContextMenu("Clear All Tiles")]
void ClearAll() => GetComponent<UnityEngine.Tilemaps.Tilemap>().ClearAllTiles();
```

**Prevention:** always erase painted tiles before deleting a tile asset from the Project.

---

## Checklist for a New Prop

- [ ] Prefab built and saved to `Assets/Prefabs/`
- [ ] `IPropConnectable` implemented if the prop links to another prop
- [ ] Sprite sized to full footprint (multi-cell props use one oversized sprite)
- [ ] Sprite pivot set correctly (Bottom for floor props, Top for ceiling props)
- [ ] SpriteRenderer **Order in Layer** set higher than tilemaps
- [ ] Animation clips recorded in **Prefab Mode** (not on a scene instance)
- [ ] Animation bindings verified in the Animation window
- [ ] `PropTile` asset created with preview sprite + prefab reference
- [ ] Tile added to the Tile Palette
- [ ] Tile painted on the `Props` Tilemap (one cell per prop)
- [ ] PropTilemapSpawner → Sync Cell List → Connection IDs filled in
- [ ] Tested in Play mode — correct position, depth, and animation
- [ ] `Spawn Offset` tuned if needed
