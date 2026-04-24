# Softbody Player — Technical Documentation

## Table of Contents
- [Overview](#overview)
- [How It Works](#how-it-works)
  - [Ring of Physics Points](#ring-of-physics-points)
  - [Spring Network](#spring-network)
  - [Shape Restoration](#shape-restoration)
  - [Pressure Simulation](#pressure-simulation)
  - [Collision Resolution](#collision-resolution)
  - [Mesh Rendering](#mesh-rendering)
- [Animation System](#animation-system)
- [Split & Merge System](#split--merge-system)
- [One-Time Unity Setup](#one-time-unity-setup)
- [Important: No Collider on the Player GameObject](#important-no-collider-on-the-player-gameobject)
- [Inspector Reference](#inspector-reference)
- [Tuning Guide](#tuning-guide)
- [Scripting API](#scripting-api)

---

## Overview

The softbody player is implemented in `Assets/Scripts/Player/SoftBodyPlayer.cs`. Instead of a single rigid collider, the player body is simulated as a ring of `N` Rigidbody2D points connected by springs. A fan-triangulated mesh is rebuilt every frame from the physics positions, smoothed with a Catmull-Rom spline, to produce a continuous blob silhouette.

The system has no dependency on any external physics assets — it uses only Unity's built-in 2D physics engine.

---

## How It Works

### Ring of Physics Points

At startup, `N` GameObjects are spawned at scene root (not as children of the player). Each carries a `Rigidbody2D` and a `CircleCollider2D`. Their initial positions are placed on an ellipse:

```
x = cos(angle) × bodyRadius
y = sin(angle) × bodyRadius × domeHeightScale
```

`domeHeightScale < 1` makes the body wider than it is tall, giving the reference blob shape. The player transform itself **never** leaves the centroid position — it tracks `_center` each `LateUpdate` so that Cinemachine and other followers work correctly.

All ring points are assigned to a dedicated `SoftBodyPoint` physics layer that is configured to:
- Not collide with each other
- Only collide with layers inside the `groundLayer` mask

A zero-friction `PhysicsMaterial2D` (friction = 0, bounciness = 0.15) is automatically created and assigned to every point collider so the blob slides off corners cleanly.

---

### Spring Network

Three tiers of `SpringJoint2D` components connect the ring points. All frequencies and dampings are derived from a single master pair (`springFrequency`, `springDamping`) using fixed ratios:

| Tier | Connection | Frequency | Damping | Purpose |
|------|-----------|-----------|---------|---------|
| Surface | i ↔ i+1 | `springFrequency × 1.0` | `springDamping × 1.0` | Elastic skin, defines the outline |
| Skip-2 | i ↔ i+2 | `springFrequency × 0.70` | `springDamping × 0.79` | Prevents fold-over between adjacent points |
| Quarter-span | i ↔ i+N/4 | `springFrequency × 0.50` | `springDamping × 0.64` | Cross-bracing, general shape stiffness |

Total joints: `3 × N` (~90 for the default 30 points), compared to `N×(N-1)/2 = 435` for a naive all-to-all network. The sparse network allows organic deformation rather than acting as a rigid shell.

Diameter springs (i ↔ i+N/2) are intentionally omitted — they pull the equator inward and produce an hourglass shape.

---

### Shape Restoration

Every `FixedUpdate`, each point receives a force pushing it toward its spawn offset relative to the current centroid, plus any active animation bias:

```
desired = _center + _offsets[i] + _animOffsets[i]
force   = (desired - point.position) × restoreForce × _restoreMultiplier
```

This acts like a soft frame: the body tries to maintain its dome shape without rigidly enforcing it. Higher `restoreForce` produces a bouncier, more shape-consistent blob; lower values make it more liquid and deformable. `_restoreMultiplier` is temporarily raised during the landing squash animation so the shape snaps in faster.

---

### Pressure Simulation

The enclosed area of the ring polygon is computed each step using the shoelace formula. A pressure force is applied outward along every edge, proportional to the area deficit:

```
pressureMag = (restArea / currentArea - 1) × pressureForce × _pressureMultiplier
```

The inverse formula grows aggressively as the blob flattens (like a pressurised gas), giving strong push-back on landing squash. The force is clamped asymmetrically: up to `4× pressureForce` resisting compression, and `1× pressureForce` resisting over-expansion.

`_pressureMultiplier` is reduced during the landing squash and rise animations so pressure does not fight the coded deformations.

The rest area is `π × bodyRadius² × domeHeightScale`, matching the spawn ellipse.

---

### Collision Resolution

All collision handling is consolidated in a single `ResolveCollisions()` pass that runs first in `FixedUpdate`:

**1. Proactive sweep** (per point)  
A `Physics2D.CircleCast` is run from the point's position at the end of the last step to its current position. If it crossed a ground surface, the point is snapped to the surface before the mesh is ever rebuilt. Points that moved less than `pointRadius` are skipped — slow resting contact cannot have tunneled.

**2. Reactive fallback** (per point)  
An `OverlapCircle` check finds any remaining ground contact. `ColliderDistance2D` gives the minimum-separation exit vector. Penetrations shallower than 0.01 m are ignored (deadband prevents oscillation from micro-contact).

**3. Neighbor constraints** (adjacent pairs)  
After all forces are applied, adjacent ring points are prevented from stretching beyond `1.5×` their spawn rest distance. Both position and relative velocity are corrected equally. This prevents platforms from slipping through the gap between two adjacent points.

---

### Mesh Rendering

`LateUpdate` rebuilds a fan-triangulated mesh with one centre vertex and `N × subdivisionsPerSegment` ring vertices.

**Laplacian pre-smoothing** runs `meshSmoothingPasses` times on the raw physics positions before the spline. Each pass blends each point 33% toward its neighbours (`weight 4:1:1`), removing spike artefacts from fast impacts without flattening the dome extremes.

**Centripetal Catmull-Rom** (α = 0.5) is then evaluated between each adjacent pair of pre-smoothed points at `subdivisionsPerSegment` intervals. The centripetal parametrisation guarantees no cusps or self-intersections even when control points are unevenly spaced (common during landing).

**Visual lean shear** is applied as a final per-vertex pass after the spline. Each vertex's X position is offset by `leanDir × moveLeanAmount × leanBlend × shear`, where `shear` scales from 0 at the bottom of the blob to 1 at the top. This creates a forward tilt without adding any net force to the physics ring.

UVs are computed as the normalised offset from the centroid, so any material applied to the `MeshRenderer` maps consistently regardless of deformation.

---

## Animation System

All coded animations work by writing per-point **offset biases** (`_animOffsets[]`) that are added to each point's restore force target each `FixedUpdate`. The restore force then pulls physics points toward the animated rest position, giving natural lag and blending.

Every animation offset is designed so that the sum across all ring points equals zero — no net force is injected into the simulation. The lean is the exception: it is a uniform horizontal value applied per-point, which would create a net sideways force through restore. It is therefore applied **only in `RebuildMesh`** as a mesh-space shear and never touches physics.

### Blend weights

| Weight | Active when | Controls |
|--------|-------------|----------|
| `_idleBlend` | Grounded, no input | Radial breathing pulse |
| `_moveBlend` | **Grounded** and horizontal input held | Squash/stretch bob cycle only |
| `_leanBlend` | Horizontal input held (grounded **or** airborne) | Visual lean shear — persists through jumps |
| `_riseBlend` | Airborne, rising — scales with upward velocity | Vertical stretch + horizontal squeeze |
| `_fallBlend` | Airborne, falling — scales with downward velocity | Horizontal spread + vertical flatten |
| `_landingSquashT` | First `landingDuration` seconds after touching ground | Wide/flat squash spike |

All weights lerp at `animBlendSpeed` per second except `_landingSquashT`, which is a timer that decays linearly from 1 to 0.

### Why bob is grounded-only

The move bob (`_moveBlend`) only activates on the ground and its phase only advances while grounded. Allowing the bob to run while airborne produces an oscillating restore-force signal that excites the spring network during jumps and creates a visible ripple. The lean (`_leanBlend`) is tracked separately so it continues through the arc of a moving jump.

### Why the jump uses uniform velocity

The jump sets `rb.linearVelocity.y = jumpForce` identically on every point. Any per-point variation (applying impulse to only the top half, weighted by height, etc.) creates a velocity gradient across the ring that propagates as a compression wave through the spring network, producing a visible ripple. Identical `Δvy` means no differential motion and no wave.

### Force modulation

During landing, `pressureForce` is scaled by `landingPressureScale` (default 0.2) so it does not resist the squash, and `restoreForce` is scaled by `landingRestoreScale` (default 1.8) so the squash snaps in quickly. During rise, pressure is reduced to 65% so the vertical stretch is not crushed back down.

---

## Split & Merge System

Implemented in `Assets/Scripts/Player/PlayerSplitController.cs`. Attach to the same GameObject as `SoftBodyPlayer` (or any persistent manager in the scene).

### Split

**Left Shift** triggers a split. The main player fades out over ~0.06 s, then two half-size `SoftBodyPlayer` instances are spawned at the left and right half-positions sampled from `GetHalfState`. Each droplet has:

- **Half mass** (`pointMass × 0.5`)
- **Half area** (`bodyRadius / √2`) — radius reduced by `1/√2` to preserve area ratio
- **Burst velocity**: both droplets receive a small upward kick (`splitBurstY`). The active droplet additionally receives a horizontal burst in the facing direction (`splitBurstX`).
- **Passive repel**: the passive droplet's horizontal velocity is *replaced* (not added to) with the mirrored average pre-split velocity: `vel.x = -avgVelX × splitPassiveVelocityScale`. When the player is still at the moment of split, `avgVelX = 0` so the passive droplet receives no horizontal force.
- **Passive drag**: the passive droplet applies only a fraction of `moveDrag` (`splitPassiveDragFraction`, default 0.04), so the repel velocity coasts naturally and decelerates gradually instead of stopping abruptly.

The droplet facing the player's pre-split direction becomes active; the other is passive (`InputEnabled = false`). **Tab** swaps the active droplet (camera pans across via `CameraFollowProxy.SwitchTarget`).

### Merge

Merge is triggered automatically when the two droplet centres come within `mergeProximityRadius` (default 0.4 m) after the `mergeCooldownDuration` has elapsed (default 0.75 s).

**Spawn position**: the merged blob is teleported to the midpoint of the two droplet centres at the exact frame the proximity threshold is crossed. Position is read via `RenderCenter` (averaging ring-point `transform.position` directly from the GOs) inside `PlayerSplitController.LateUpdate`, where Unity's interpolation has already updated for this render frame — no physics-step lag.

**Platform depenetration**: immediately after `TeleportTo`, `DepenetrateFromGround()` is called. The full-size player is larger than the half-size droplets, and the merge may occur on a platform surface, so some ring points can end up inside the geometry. `DepenetrateFromGround` scans downward from above each overlapping ring point to find the exact surface Y, then shifts the entire ring upward by the worst-case penetration depth.

**Velocity**: the average `linearVelocity` across all ring points of both droplets is captured at the moment the proximity threshold is crossed and applied to the reformed main player.

**Face direction**: the active droplet's `LastFaceDir` (or current horizontal input if held) is preserved through the merge.

### Pressure Transfer

When the active droplet performs a **ground pound** (C while airborne) and lands, the passive droplet is launched upward at exactly its own `jumpForce` velocity — a fixed predictable height regardless of fall impact speed. The passive droplet must be grounded for the transfer to fire; holding C cannot repeatedly air-launch it.

### PlayerSplitController Inspector Fields

| Field | Default | Description |
|-------|---------|-------------|
| **Split — Split Burst X** | 1.5 | Horizontal burst (m/s) added to each droplet's velocity on split. Active receives +X, passive does not receive this burst. |
| **Split — Split Burst Y** | 1.0 | Upward burst (m/s) added to both droplets on split. |
| **Split — Split Passive Velocity Scale** | 1.0 | Scales how much of the pre-split horizontal speed is reflected onto the passive droplet. 1 = full mirror, 0 = no repel. |
| **Split — Split Passive Drag Fraction** | 0.04 | Fraction of `moveDrag` applied to the passive droplet. Lower values make the repel coast longer. |
| **Merge — Merge Proximity Radius** | 0.4 | Centre-to-centre distance (world units) that triggers auto-merge. |
| **Merge — Merge Cooldown Duration** | 0.75 | Seconds after a split before merging is allowed. Prevents immediate re-merge after spawn. |
| **Pressure Transfer — Pressure Squash Duration** | 0.28 | Duration of extra landing-squash animation on the active droplet after a successful pressure transfer. |

### SoftBodyPlayer fields used by the split system

`passiveDragFraction` is a `[HideInInspector]` field on `SoftBodyPlayer`. It is **not set in the Inspector** — `PlayerSplitController.SpawnDroplet` writes it from `splitPassiveDragFraction` at spawn time. Edit `splitPassiveDragFraction` on `PlayerSplitController` instead.

### Why TeleportTo has three internal steps

When `TeleportTo` is called after a merge, mainPlayer was frozen since the split. Three things must happen atomically to avoid artefacts on the first post-teleport frame:

1. **Interpolation flush** — toggling `rb.interpolation` to `None` then back to `Interpolate` clears Unity's internal "previous position" buffer. Without this, the interpolation system blends between the old frozen location and the new position, visually snapping the player toward the split point for several frames.
2. **`transform.position` write** — directly assigning `_pointGOs[i].transform.position` makes the visual correct this same frame, before the next LateUpdate.
3. **`_prevPositions` sync** — `ResolveCollisions` uses a `CircleCast` from `_prevPositions[i]` (last frame's physics position) to the current position to detect tunneling. Without syncing, the cast sweeps from the frozen split position all the way to the merge position, hitting any ground in between and snapping the player to an intermediate collision point.

### Why DepenetrateFromGround uses a downward raycast

`DepenetrateFromGround` is called immediately after `TeleportTo` on merge. It could instead use `ColliderDistance2D` to measure penetration depth, but `TeleportTo` is called from `LateUpdate` — after the physics step. At that point the physics engine has not yet synced the ring-point colliders to their new positions, so `ColliderDistance2D` reads stale collider data and returns incorrect depths.

The downward raycast approach bypasses this entirely: it casts from a known world position (`pos.y + bodyRadius * 3`) straight down to find the platform's surface Y, then computes the required lift as `surfaceY + pointRadius − pos.y`. Platform colliders are always at their correct world positions, so the raycast is reliable regardless of physics sync state.

---

## One-Time Unity Setup

These steps are required once per project before the player will work:

1. **Create the `SoftBodyPoint` layer**  
   Edit → Project Settings → Tags and Layers → add layer named exactly `SoftBodyPoint`

2. **Configure the Layer Collision Matrix**  
   Edit → Project Settings → Physics 2D → Layer Collision Matrix  
   - Uncheck `SoftBodyPoint × SoftBodyPoint` (points must not collide with each other)  
   - Ensure `SoftBodyPoint × Ground` is checked

3. **Assign the layer in the Inspector**  
   On the `SoftBodyPlayer` component, set **Soft Body Point Layer** to `SoftBodyPoint`

4. **Set the Ground Layer mask**  
   On `SoftBodyPlayer`, assign all ground/platform layers to the **Ground Layer** field

5. **Remove any stale scripts**  
   If the Player GameObject shows "Missing Script" warnings in the Inspector, right-click each missing component → Remove Component. These are remnants of the old `PlayerMovement` controller.

---

## Important: No Collider on the Player GameObject

The main player GameObject has **no Rigidbody2D and no Collider2D**. All physics is on the ring point GameObjects (layer `SoftBodyPoint`). This has two consequences for other systems:

### Dialogue triggers — already fixed

`DialogueTrigger` previously found the player with `GameObject.FindGameObjectWithTag("Player")` and measured distance to `transform.position`. It now uses `FindObjectOfType<SoftBodyPlayer>()` and checks against `SoftBodyPlayer.Center`, which is always the accurate centroid regardless of tag configuration. Both `KeyCode.E` and `KeyCode.Z` open dialogue.

### Physics-based detection — requires attention

Any script that detects the player via `Physics2D.OverlapBox`, `Physics2D.OverlapCircle`, trigger colliders, or a `LayerMask.GetMask("Player")` will **not work** because there is no collider on the player layer. Examples include `PressurePlate`. These scripts must be updated to either:
- Use `FindObjectOfType<SoftBodyPlayer>().Center` for distance-based detection, or
- Use `Physics2D.OverlapBox` with the `SoftBodyPoint` layer mask instead of `Player`

---

## Inspector Reference

### Body Shape
| Field | Default | Description |
|-------|---------|-------------|
| Point Count | 30 | Number of physics points on the ring. Higher = smoother physics but more joints and raycasts per frame. |
| Body Radius | 0.55 | Horizontal radius of the spawn ellipse in world units. |
| Dome Height Scale | 1 | Vertical-to-horizontal ratio. Values below 1 produce a wider-than-tall blob shape. |

### Point Physics
| Field | Default | Description |
|-------|---------|-------------|
| Point Mass | 0.1 | Mass of each Rigidbody2D. Total blob mass = pointMass × pointCount. |
| Point Radius | 0.05 | Radius of each CircleCollider2D. Also used as the sweep movement threshold. |
| Point Material | *(auto)* | PhysicsMaterial2D for point colliders. If left empty, a zero-friction material is created automatically at runtime. |
| Soft Body Point Layer | SoftBodyPoint | Name of the Unity layer assigned to ring points. Must exist in Tags and Layers. |

### Spring Network
| Field | Default | Description |
|-------|---------|-------------|
| Spring Frequency | 10 | Master stiffness. All spring tiers are derived from this value. Higher = stiffer, less wobbly body. |
| Spring Damping | 0.7 | Master damping ratio (0–1). Higher = oscillations die out faster. |

### Shape Restoration
| Field | Default | Description |
|-------|---------|-------------|
| Restore Force | 60 | Force pushing each point toward its spawn offset from the centroid. Acts as a soft shape frame. |
| Pressure Force | 14 | Outward force applied when the blob's area is below its rest area. Resists squashing on landing. |

### Movement
| Field | Default | Description |
|-------|---------|-------------|
| Move Force | 4 | Horizontal force per point when input is held. |
| Max Move Speed | 7 | Horizontal velocity cap per point before input forces are suppressed. |
| Move Drag | 6 | Braking force per point when no input is held. |
| Air Control Fraction | 0.4 | Fraction of Move Force applied while airborne. Allows mid-air steering without enabling wall-sticking. |

### Jump
| Field | Default | Description |
|-------|---------|-------------|
| Jump Force | 13.5 | Upward velocity (m/s) set on every ring point simultaneously when jumping. Applied as a direct velocity set, not an impulse, to ensure consistent height and no compression wave. |

### Gravity
| Field | Default | Description |
|-------|---------|-------------|
| Base Gravity Scale | 3 | Gravity scale applied to all points at rest and while rising. |
| Max Gravity Scale | 7 | Gravity scale ramped toward while the blob is falling. Gives snappier fall feel. |
| Gravity Ramp Rate | 4 | How fast (scale units per second) the gravity scale transitions between base and max. |

### Ground Detection
| Field | Default | Description |
|-------|---------|-------------|
| Ground Layer | *(mask)* | LayerMask identifying all ground/platform surfaces. |
| Ground Check Fraction | 0.2 | Fraction of bottommost points used to poll for ground contact each step. |

### Level Bounds (optional)
| Field | Default | Description |
|-------|---------|-------------|
| Level Bounds | *(none)* | PolygonCollider2D defining the playable area. Points outside the bounds receive a restoring force. |
| Boundary Force | 40 | Strength of the force pushing points back inside the level bounds. |

### Rendering
| Field | Default | Description |
|-------|---------|-------------|
| Body Color | Blue | Fallback colour used when no Body Material is assigned. |
| Body Material | *(none)* | Material applied to the MeshRenderer. If empty, a default `Sprites/Default` material is created with Body Color. |
| Sorting Layer Name | Default | Sorting layer for the MeshRenderer. |
| Sorting Order | 0 | Sorting order within the sorting layer. |
| Subdivisions Per Segment | 4 | Catmull-Rom subdivisions between each physics point. 1 = raw physics outline, 4 = smooth silhouette, 8 = very smooth (higher vertex count). |
| Mesh Smoothing Passes | 4 | Laplacian pre-smooth passes before the spline. Removes spike artefacts on fast impacts. 0 = off, 4 = recommended. |

### Animation — Idle
| Field | Default | Description |
|-------|---------|-------------|
| Idle Bob Amplitude | 0.02 | Radius of the gentle radial breathing pulse in world units. |
| Idle Bob Frequency | 1.2 | Breathing cycles per second. |

### Animation — Move
| Field | Default | Description |
|-------|---------|-------------|
| Move Lean Amount | 0.3 | How far the top of the body shears forward in the direction of travel (world units). Applied as a visual-only mesh shear — does not add net force to physics. Lean persists through jumps via a separate `_leanBlend`. |
| Move Bob Amplitude | 0.01 | Squash/stretch amplitude per step cycle. Only active while grounded. |
| Move Bob Frequency | 2 | Bob cycles per second at full move speed. |

### Animation — Rise
| Field | Default | Description |
|-------|---------|-------------|
| Rise Stretch Amount | 0.08 | How much the body stretches vertically while rising. |
| Rise Squeeze Amount | 0.055 | How much the sides squeeze inward while rising. |
| Rise Velocity Full | 3.5 | Upward velocity (m/s) at which the rise blend reaches full strength. |

### Animation — Fall
| Field | Default | Description |
|-------|---------|-------------|
| Fall Spread Amount | 0.07 | How much the sides spread outward while falling. Bottom points are weighted 1.5× for a hanging-belly silhouette. |
| Fall Flatten Amount | 0.042 | How much the body compresses vertically while falling. |
| Fall Velocity Full | 4 | Downward velocity (m/s) at which the fall blend reaches full strength. |

### Animation — Landing
| Field | Default | Description |
|-------|---------|-------------|
| Landing Squash Spread | 0.13 | How much the sides spread on impact. |
| Landing Squash Flatten | 0.09 | How much the body flattens on impact. |
| Landing Duration | 0.26 | Seconds the landing squash takes to decay back to rest. Squash peaks at the moment of contact and decays linearly. |

### Animation — Blending
| Field | Default | Description |
|-------|---------|-------------|
| Anim Blend Speed | 4 | How fast idle/move/rise/fall blend weights transition when the player changes state (lerp rate per second). Higher = snappier cuts, lower = smoother crossfades. |
| Landing Pressure Scale | 0.2 | Pressure force multiplier during the landing squash. Reducing it prevents pressure from fighting the animation. |
| Landing Restore Scale | 1.8 | Restore force multiplier during the landing squash. Boosting it snaps the squash shape in faster. |

---

## Tuning Guide

### The blob feels too rigid / not slimy enough
- Lower `springFrequency` (try 6–8)
- Lower `restoreForce` (try 40–50)
- Lower `springDamping` slightly (try 0.55–0.65) to allow more oscillation

### The blob collapses or loses its shape
- Raise `restoreForce` (try 70–80)
- Raise `pressureForce` (try 20–30)
- Raise `springFrequency`

### The blob is too bouncy
- Raise `springDamping` (try 0.85–0.95)
- Lower `pressureForce` (try 6–9)
- Assign a custom `pointMaterial` with `bounciness = 0` to remove physical rebound off surfaces

### Landing feels too flat / not enough bounce-back
- Raise `pressureForce` (primary control for squash bounce-back)
- Raise `restoreForce`

### Silhouette is spiky on jump or landing
- Raise `meshSmoothingPasses` (try 4–5)
- Raise `subdivisionsPerSegment` (try 6–8)
- These are purely visual — physics is unaffected

### Player sticks to walls when pressing into them
- Lower `airControlFraction` (try 0.25–0.35)

### Jump feels weak or doesn't reach height
- Raise `jumpForce` — it is a direct m/s value, so the effect is predictable

### Jump height is inconsistent
- Do not modify `HandleJump` to use `AddForce` or `+=` on velocity — only setting velocity directly (`=`) guarantees consistent height regardless of pre-existing point velocities

### Player falls too slowly / floats
- Raise `maxGravityScale`
- Raise `gravityRampRate` so peak fall gravity is reached faster

### Platform clipping still occasionally visible
- Ensure `CollisionDetectionMode` on ring point Rigidbody2Ds is `Continuous` (set automatically at spawn)
- Lowering the physics timestep (Edit → Project Settings → Time → Fixed Timestep to 0.01) reduces the window in which tunneling can occur

### Movement lean isn't visible / too subtle
- Raise `moveLeanAmount` (default 0.3 — try 0.35–0.45 for a more dramatic tilt)
- Lower `animBlendSpeed` so the lean lingers longer when changing direction

### Movement bob feels too strong
- Lower `moveBobAmplitude` (default 0.01)
- Lower `moveBobFrequency` to slow the cycle

### Ripple or wobble visible on moving jumps
- The bob phase is grounded-only by design — if a ripple appears, check that nothing else is applying asymmetric per-point forces while airborne
- Raise `springDamping` and `meshSmoothingPasses` to damp and hide residual spring oscillations

### Landing squash is too weak or too strong
- `landingSquashSpread` and `landingSquashFlatten` control the shape
- `landingDuration` controls how long it takes to restore
- Lower `landingPressureScale` further (toward 0) if pressure is visibly fighting the squash

### Animations feel too snappy / too sluggish switching states
- Raise `animBlendSpeed` for snappier cuts between idle/move/rise/fall
- Lower it for smoother crossfades

### NPC dialogue cannot be triggered
- `DialogueTrigger` uses `FindObjectOfType<SoftBodyPlayer>()` — ensure exactly one `SoftBodyPlayer` exists in the scene
- Both `KeyCode.E` (Interact Key) and `KeyCode.Z` (Interact Key Alt) open dialogue by default
- Check the `interactRadius` gizmo in Scene view to confirm the player is actually entering range

---

## Scripting API

`SoftBodyPlayer` exposes a small public surface for other scripts to interact with:

```csharp
// Whether the bottom ring points are currently touching ground
bool IsGrounded { get; }

// Current centroid position (world space) — use this instead of transform.position
// in other scripts, as transform.position is set in LateUpdate and lags one frame in Update
Vector2 Center { get; }

// All ring Rigidbody2Ds — read-only, do not modify the array
Rigidbody2D[] Points { get; }

// Whether this player instance is accepting player input (false on passive split droplets)
bool InputEnabled { get; set; }

// Last horizontal face direction (+1 = right, -1 = left)
float LastFaceDir { get; }

// Fired by the active droplet after a ground pound lands, with the downward impact velocity
event System.Action<float> OnGroundPoundLand;

// Teleport all ring points to center with initialVelocity.
// Always call Unfreeze() first — rb.position writes on a frozen Rigidbody2D are discarded.
// Internally: flushes the interpolation buffer (None→Interpolate toggle), writes
// transform.position directly for same-frame visual correctness, and syncs _prevPositions
// so ResolveCollisions doesn't sweep from the old frozen location across the level.
void TeleportTo(Vector2 center, Vector2 initialVelocity);

// Call after TeleportTo on merge to prevent ring points from clipping into platforms.
// Scans downward from above each overlapping ring point to find the surface Y, then
// shifts the entire ring up by the worst penetration depth. Safe to call from LateUpdate —
// uses positional raycasts rather than collider distance queries, which are stale until
// the next physics step.
void DepenetrateFromGround();

// Freeze all physics simulation (used by dialogue and split system)
void Freeze();

// Resume physics simulation
void Unfreeze();

// Set the body + face opacity (0 = transparent, 1 = fully visible)
void SetBodyAlpha(float alpha);

// Show or hide the full GameObject
void SetVisible(bool visible);

// Force a specific face direction immediately without waiting for UpdateFace
void InitFaceDirection(float dir);
```

### Finding the player from other scripts

Do not use `GameObject.FindGameObjectWithTag("Player")` — the main player GameObject may not have that tag, and the ring point GameObjects are not tagged. Use component lookup instead:

```csharp
SoftBodyPlayer player = FindObjectOfType<SoftBodyPlayer>();
Vector2 playerPos = player.Center;
```

### Example — detecting landing for a sound effect

```csharp
public class PlayerLandingSound : MonoBehaviour
{
    private SoftBodyPlayer _player;
    private bool _wasGrounded;

    private void Awake() => _player = FindObjectOfType<SoftBodyPlayer>();

    private void Update()
    {
        bool grounded = _player.IsGrounded;
        if (grounded && !_wasGrounded)
            AudioManager.Play("land");
        _wasGrounded = grounded;
    }
}
```

### Example — proximity detection without physics

```csharp
// Works correctly because Center is always the accurate centroid,
// regardless of ring point positions or layer configuration.
SoftBodyPlayer player = FindObjectOfType<SoftBodyPlayer>();
if (Vector2.Distance(transform.position, player.Center) < detectionRadius)
    DoSomething();
```

### Example — triggering dialogue freeze

```csharp
// Handled automatically by PlayerDialogueHandler via EventManager.
// Call directly only if you need manual control outside the dialogue system:
FindObjectOfType<SoftBodyPlayer>().Freeze();
// ... do something ...
FindObjectOfType<SoftBodyPlayer>().Unfreeze();
```
