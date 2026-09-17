# Area 2 scenes

Area 2 is split at its three existing yellow exit doors:

| Scene | Section | Gate connection | Destination |
| --- | --- | --- | --- |
| `Area2-1` | Introduction | `intro_door` | `Area2-2` |
| `Area2-2` | Familiarity | `familiarity_crushers` | `Area2-3` |
| `Area2-3` | Challenge | `challenge_exit` | `Area3-1` |

Open `Assets/Scenes/Area2-1.unity` to play the sequence. All four destination
scenes are enabled in Build Settings. StartMenu still opens Sandbox.
`Area3-1` was brought in from `origin/feat/area3` at `ae99ee8`; the rest of that
branch, including the Area 1 renames, was not merged by this change.

Each Area 2 scene contains only its own room geometry, props and markers, plus
the shared player, camera, UI and dialogue scaffolding. Tile coordinates are
local to each room: the former combined-scene offsets were 0, 30 and 64.
Each room has its own safe spawn and camera bounds. Area2-2 follows the PDF's
upper-left entrance and lower-left return exit. The conveyor and crushers are
off on the outward journey; the ice-only plate activates both and unlocks the
exit. Area2-3 has electric strips on the first, third and fifth descent ledges,
with normal ledges between them and a safe freezer landing above. Physical
humidifiers restore liquid form at all three exits; their artwork remains a
labelled placeholder. See [Area 2 mechanics](area2-mechanics.md).

## Door transitions

The Props tilemap's cell overrides now expose **Exit Scene** for Door cells.
An empty value keeps the existing door behavior. A configured value attaches
`SceneDoorExit` when the prop spawns. The door must be unlocked and fully open,
and the active player's body centre must cross to the configured side before
the next scene loads. Touching the locked door or merely activating its plate
does not finish the room. **Exit To Left** is enabled for Area2-2. The gate
keeps its existing one-shot plate behavior.

Scenes load with a fresh player in liquid form, matching the design document's
humidifier reset between rooms. Death/retry reloads only the current scene and
resets its puzzles. These exits are forward-only.

## Combined reference and generation

The old `Area2.unity` is archived as
`Assets/Editor/SceneTemplates/Area2-Combined.unity`, outside the playable scene
list. The original combined builder and freezer-route regression use this
reference. Edit the three playable scenes directly for subsequent level work.

`Area2RoomSceneBuilder.BuildBatch` regenerates the three room scenes from that
reference and **overwrites room edits**. Run it only in an isolated Unity project
copy, with `-batchmode -quit -executeMethod Area2RoomSceneBuilder.BuildBatch`.
It trims tilemaps and per-cell settings, localises coordinates, moves room
markers, configures spawns/camera bounds, registers destinations, and applies
the completed mechanics/return-route pass through `Area2MechanicsBuilder`.

Use **Tools > Poko Pond > Area 2 > Validate Split Scenes** for structural
validation. For isolated Play Mode checks run Unity with `-batchmode
-executeMethod Area2RoomTransitionProbe.RunBatch` (without `-quit` or
`-nographics`). The probe checks all three liquid spawns, locked-exit rejection,
real freezer and solid-plate activation, physical crossing through each open
door, arrival in Area3-1, and room-2 death/retry. Teleports set up focused cases;
this is not a complete puzzle-route playthrough.
