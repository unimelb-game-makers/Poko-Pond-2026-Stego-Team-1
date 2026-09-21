# Sandbox door and splitting gallery

Climb the right-hand shaft to the previously empty upper chamber. The gallery
joins the existing top landing at y=16. Three springs at prop cells (16,-5),
(19,2), and (22,9) bridge the shaft's tall gaps: start on the lower-right floor,
bounce and steer right onto each next spring, then onto the gallery near x=27.
The Sandbox-only `SandboxGallerySpring` prefab/PropTile reuses the trampoline
art and animation with bounce strength 24; the original strength-16 trampoline
is unchanged. A small intermediate step at (26,14) remains available.
S/Down drops out of the one-way gallery floor, so the red
door cannot trap access to the rest of Sandbox.

| Prop cell | Demo | Connection |
| --- | --- | --- |
| (28,16) | Splitting machine: unlock Left Shift; Tab switches droplets | None |
| (24,16) | Green door: opens on approach, closes when clear | None; intentionally no plate |
| (20,16), (18,16) | One-shot plate and yellow permanent-unlock door | `sandbox_demo_yellow` |
| (14,16), (12,16) | Reusable hold plate and red door | `sandbox_demo_red` |

Leave one split droplet on the red plate and approach the door with the other.
Removing the holding droplet relocks red. Neither demo plate affects green or
the other linked door. Labels and coloured floor lines explain the pairings.

Demo props use production PropTiles; ascent springs use the Sandbox variant.
Existing painted cells and prop
connections are preserved. Camera zoom is unchanged; a Sandbox-only camera
boundary extends right/up enough to include the gallery at the current wide zoom.

`SandboxMechanicsDemoBuilder.Build` adds/updates the gallery without clearing the
scene. In an isolated Unity copy, run `SandboxMechanicsDemoBuilder.ValidateBatch`
with `-batchmode` (without `-quit` or `-nographics`). Its editor-only probe verifies
splitter activation, actual split droplets holding/releasing red, yellow latching,
green proximity, independent connections and camera visibility. The ascent test
starts the player at the shaft floor and reaches the gallery through three real
spring contacts, with only horizontal steering supplied by the probe (no vertical
velocity injection or mid-climb teleports). Subsequent focused mechanic tests
position actors by teleport. Preview images include the gallery and ascent shaft.
