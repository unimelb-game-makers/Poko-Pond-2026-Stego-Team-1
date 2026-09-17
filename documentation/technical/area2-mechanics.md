# Area 2 mechanics

The level-design PDF (Area 2, pages 5-7) specifies an initially inactive conveyor
and crushers, activated by the ice-only plate, followed by a return trip through
the hazards. The humidifier restores water form before the next room.

Before implementation, all fetched `origin/*` branches were inspected for
conveyor/humidifier scripts and matching work in commit history. No implementation
was present. Electrified moving platforms already existed on main; the new
stationary electric surface shares their contact-damage convention and
`ElectrifyingPlatformEffect` sparks. Normal thin platforms already existed.

## Conveyor

- Source: `Art/Environment/Props/Conveyor/conveyer_block.png`, unchanged from the
  supplied image. Unity slices the 256x32 strip into eight horizontal 32x32
  sprites at 32 PPU, with point filtering, no mipmaps and no compression.
- `Animations/Environment/Props/Conveyor/Conveyor_Run.anim` loops all eight
  frames at 12 FPS. Its Animator pauses when the belt is off.
- `Prefabs/Props/ConveyorBelt.prefab` is one physical 1x1 block. Paint adjacent
  `ConveyorBelt_PropTile` cells on **Props** to extend a belt. The prefab supplies
  the collider, so remove overlapping SolidPlatforms tiles at those cells.
- The tile is in the existing **1x1** palette. It uses the same prefab-spawning,
  preview-sprite and per-cell connection workflow as other props.
- `ConveyorBelt` implements `IPropConnectable` and `IPropActivatable`; empty IDs
  allow always-on belts, while Hold/Toggle and Initial Active use existing rules.
- Area2-2 paints 14 blocks at `(11..24, 0)`. They start off and share the
  `familiarity_crushers` one-shot plate connection with the crushers and exit.
  The active surface carries liquid or ice left at 2 units/second.
- `SoftBodyPlayer.OfferSurfaceVelocity` combines adjoining tiles without stacking
  speed. Transport runs before collision resolution and expires every physics
  step. Airborne bodies are not carried; movement input remains available.

## Familiarity return route

Spawn is `(4,6)` on the upper-left entrance ledge. Walk off its right edge,
cross the inactive machinery, freeze at the right-hand station, and return to
the ice-only plate. Then cross back through the active machinery to the lower
left door. The exit at `(1,1)` has **Exit To Left** enabled and leads to Area2-3.

Only the Area2-specific crusher prefab is retimed: 0.2s slam, 1.8s recovery,
1s rest. The generic crusher prefab is unchanged. The recovery window gives
the non-jumping ice player time to traverse the belt between impacts.

## Electric and normal ledges

The safe freezer landing is at tile row 13. The five lower ledges are:

| Tile row | Central strip at x=12..15 |
| --- | --- |
| 11 | Electric |
| 9 | Normal |
| 7 | Electric |
| 5 | Normal |
| 3 | Electric; the switch has a safe landing at x=18 |

The diagram's electric sections occupy part of each ledge, leaving room to
steer around them. Normal sections use the existing ThinPlatforms tiles.
`ElectricPlatform_PropTile` replaces the thin tiles under each live strip and
spawns a static one-way platform with sparks. Contact kills both liquid and ice.
Dropping through a platform does not turn off its electrical hazard. This tile
is also available in the 1x1 palette, tinted to distinguish it in the editor.

## Humidifiers

`Humidifier_PropTile` is in the 1x1 palette; its one-cell anchor creates a
walk-through three-unit mist zone. Each exit places it immediately beyond the
door, so conversion happens before scene loading. It resolves the actual
contacted soft body and restores liquid in place, preserving velocity. Already
liquid bodies are left alone. Ice-to-water conversion is tested; a playable gas
body is not yet implemented by the underlying player controller.

No final humidifier art exists. Its two nozzle blocks, faint mist and
**HUMIDIFIER / ART TODO** label are explicitly temporary. Replace those visual
children when artwork is available; keep the trigger volume and script.

## Generation and checks

In an isolated Unity copy, use `-batchmode -quit -executeMethod
Area2MechanicsBuilder.BuildBatch` to import/build assets, update the palette,
and apply the mechanics to the three scenes. This is an authoring/migration
operation, not a runtime generator. It intentionally updates the named mechanic
cells and familiarity layout. `Area2RoomSceneBuilder.BuildBatch` also invokes it
after rebuilding the rooms from the archived combined reference.

`Area2MechanicsBuilder.ValidateBatch` checks exact slice rectangles, PPU, prefab
and palette references, the left-exit layout, and the safe/live ledge pattern.

Run `-batchmode -executeMethod Area2MechanicsProbe.RunBatch` without `-quit` or
`-nographics` for Play Mode checks. It walks the complete familiarity route
without teleporting, including the real freezer and plate, active machinery,
physical humidity and left exit. Separate focused cases check off/on animation,
all eight frames, seam speed, liquid/ice carriage, airborne exclusion, each
humidifier, each live row against both states, safe ledges/drop-through, and
the existing Level2 electric moving-platform effects. Focused cases use
teleports to isolate behavior. `Area2RoomTransitionProbe.RunBatch` additionally
checks the three-scene progression, room-2 retry reset, and solid joint geometry
after teleporting. Soft-body point sorting now also reorders the matching rest
offsets and rebuilds derived indices, preventing state changes and teleports
from scrambling the joint network.
