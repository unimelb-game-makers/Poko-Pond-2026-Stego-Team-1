# Water Battery (Hydroelectric Battery)

A prop that sucks in a **liquid** droplet, holds it, and powers anything linked to
it while it is running. The player presses **Left/Right** to be launched back out.
Design source: *Poko Pond Level Design Layout*, p.9 (Areas 3–4).

| | Small battery | Big battery |
|---|---|---|
| Prefab | `Prefabs/Props/WaterBattery.prefab` | `Prefabs/Props/BigWaterBattery.prefab` (variant, 1.5× scale, dark-gold tint placeholder) |
| PropTile | `Tiles/Factory/Props/WaterBattery_PropTile` | `Tiles/Factory/Props/BigWaterBattery_PropTile` |
| Palette | `2x2` | `2x2` |
| Takes | Half the player | Both halves (a whole droplet counts as both) |
| Whole droplet enters | Split automatically: one half stays in, the other pops back out the entry side and gets control | Accepted as-is |
| Before the Splitting Machine | Idle — no suction | Works with a whole droplet |
| Two halves enter | — | Merged inside, launched out as one whole droplet |

## Behaviour

```
Idle (red) → Sucking → Holding (big, one half) → Running (green) → Cooldown → Idle
```

- **Liquid only.** Solid and gas bodies are ignored.
- **Suction** reuses `SoftBodyPlayer.vaccumPoints` (the funnel squeeze). Capture happens
  when the droplet's centre reaches the intake, or after `Max Suction Time`.
- **Parked** droplets are frozen, hidden and non-colliding (`SoftBodyPlayer.Park`) —
  hazards cannot hit them and the camera stays on them if they are active.
- **Exit:** press Left/Right (after releasing any key held while being sucked in).
  `SoftBodyPlayer.ReleaseFromPark` places the droplet at the exit point with
  `Launch Speed` at `Launch Angle` above horizontal, mirrored for left exits.
- **Cooldown:** after an eject the battery ignores everything for `Cooldown Duration`
  (default 1 s) so the droplet is not re-captured.
- **Split interaction:** Tab can switch control to a parked half, then Left/Right ejects
  it. A parked half never auto-merges with the other half passing by.

## Signalling

The battery fires the **same events as a pressure plate**
(`EventManager.PressurePlateActivated/Deactivated`) using its connection id, so doors,
conveyors, crushers and grates work with no changes.

| Cell setting | Effect |
|---|---|
| Connection ID | Shared with the props it powers |
| One Shot = off | Powered only while running (Hold) |
| One Shot = on | Stays powered after the first run |

`Blower` and `MovingPlatform` are switchable too (see *Switchable fans and platforms*
below). Electric platforms are not yet switchable.

## Per-cell launch tuning

`PropTilemapSpawner` → Sync Cell List → on a battery cell enable
**Override Battery Launch** and set **Battery Launch Speed** / **Battery Launch Angle**.
Leave it off to use the prefab defaults (speed 9, angle 30°). Selecting a spawned
battery shows gizmos: suction zone (blue), intake + capture radius (yellow), and both
exit points with a launch-arc preview (magenta).

## Art

- Idle loop (funnels moving, red light): `hydrobattery_off.png`, imported at **258 PPU**
  so its 10×-scale art matches the 1× strips below.
- Fill → running (green light): `waterbatterysource.png`, **32 PPU**; frames 7–9 loop
  while running, frame 5 is shown while a big battery holds one half.
- Eject: `EjectBattery.png`, **32 PPU**.
- All slices use a **bottom-centre** pivot; PropTiles use `Spawn Offset (0, -0.5)`.
- The big battery reuses the same sprites (scaled + tinted) until dedicated art exists.

## Switchable fans and platforms

`Blower` and `MovingPlatform` have an **Activation** block (`PropActivation`): Connection
Id, Mode (Hold/Toggle) and Initial Active. Leave Connection Id empty and they behave as
before (always on). Tile-spawned blowers are configured by `PropTilemapSpawner` like any
`IPropActivatable`; scene-placed ones are set in the Inspector. A switched-off fan stops
blowing and spinning; a switched-off platform holds position (riders get zero velocity).

A battery parented to a moving platform keeps its parked droplet and suction point on
the platform as it moves.

## Area 3 wiring (design doc pp.8–11)

The Area 3 scenes originally used hand-placed battery art on top of stand-in pressure
plates. The doc has no plates in these rooms, so
**Tools → Poko Pond → Area 3 → Wire Water Batteries (Design Doc)**
(`Area3BatteryTileMigration`) replaces each plate with a battery tile on the same cell
and links what the doc says it powers. It is re-runnable after other branches edit
the scenes.

| Scene | Battery | Powers (doc) | Id |
|---|---|---|---|
| Area3-1 Intro | Tile (6,12) on the ledge | Red exit door (5,8) | `open_door` |
| Area3-2 Familiarity | Tile (-21,-5) by the fans | The three fans (Hold — must stay occupied) | `activate_fans` |
| Area3-2 | Tile (-19,3) upper ledge | Vertical moving platform | `move_platform` |
| Area3-2 | Hand-placed, parented to the moving platform | Red end door (0,-9) | `open_end` |

Area3-3 (Challenge) has no batteries yet; the doc's version needs about nine batteries
including a big one, plus switchable electric platforms.

Verify with `Area3BatteryProbe.ValidateArea31` / `ValidateArea32` in batch mode
(editor closed, no `-quit`).

## Known integration notes

- Splitting is unlocked per scene. Area3-2 relies on its own SplittingMachine at (1,-3);
  until it is used the small batteries there stay idle.
- `feat/lighting-ambience` adds `IPropPowered`; once merged, add it to `WaterBattery`
  (it already exposes `IsPowered`), `Blower` and `MovingPlatform` (both expose `IsActive`),
  then re-run the Area 3 builder.
- A "both batteries must be on" rule (Area 4 Challenge step 5) needs AND logic in the
  listener; doors currently listen to a single id.
