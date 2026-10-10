# Evaporator & Condenser — Heater / Freezer

The level design's **Heater** and **Freezer** (Area 2 Introduction onwards) are built from the
`Evaporator` and `Condenser` props. The names come from an earlier gas-form plan that was dropped;
the level design has no gas form, and `PlayerBodyState.Gas` is unused.

| Prop | Level design name | Converts |
|------|-------------------|----------|
| `Evaporator` | Heater | Ice → water, in place |
| `Condenser` | Freezer | Water → ice, released to the left of the machine |
| `Humidifier` | Humidifier | Anything → water, at room exits (see `area2-mechanics.md`) |

The Heater and Humidifier have the same effect but different roles. The Heater is an in-room puzzle
tool the player chooses to use, and can be switched by plates. The Humidifier is an unavoidable,
always-on reset placed at every exit.

---

## Evaporator (Heater)

### What it does
Any body standing on an active Evaporator that is not already in `valueToChangeTo` (Liquid on the
prefab) converts **in place**, keeping its momentum (`SoftBodyPlayer.changeBodyStateInPlace`).
It resolves the body actually touching it, so a split droplet converts on its own and stays half-size.

### Detection
`Physics2D.OverlapBoxAll` on `"Player"` + `"SoftBodyPoint"` layers, polled every frame. The zone sits
above the collider's top surface (`b.max.y`) and is cached in `Start()`. Tune **Detection Height**.

### Animator
| Parameter | Type | Meaning |
|-----------|------|---------|
| `IsActive` | Bool | `true` = idle animation; `false` = Off state |

### Activation config (PropTilemapSpawner cell overrides)
Same Hold/Toggle/Initial Active rules as other props — see `prop-connections.md`. Leave the Connection
ID empty for an always-on heater.

### Placements
- Area2-1 `(20, 1)` — Introduction heater beside the freezer.
- Area2-3 `(25, 1)` — Challenge retry heater.
- Sandbox, and the default Props tilemap in `Grid.prefab`.

---

## Condenser (Freezer)

When the player enters the left-side intake of an active Condenser, it converts to `valueToChangeTo`
(Solid). Ice output is placed on the floor to the left of the machine with no launch velocity
(`GetSolidExitPosition`). The footprint is a trigger so ice can walk back past it.

| Parameter | Type | Meaning |
|-----------|------|---------|
| `Condense` | Trigger | Plays the condense animation once (Loop Time off) |

Tune **Entry Zone Width/Height** so the blue gizmo matches the opening, and **Solid Exit Clearance**
for the gap between the released ice and the machine.

Known limitation: the Condenser still converts the object tagged `Player` rather than the body that
touched it, so it does not handle split droplets.

---

## Returning to liquid

`SoftBodyPlayer` records its liquid shape (point count, body radius, jump force) in `Awake` and restores
it whenever it returns to liquid. The main player keeps its configured jump after thawing, and split
droplets stay half-size.
