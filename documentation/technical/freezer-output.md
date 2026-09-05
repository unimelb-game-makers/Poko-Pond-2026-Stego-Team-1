# Freezer output and no-jump routes

The Condenser prefab is a walk-through station: its footprint collider is a
trigger, while its left intake still detects the player through an overlap query.
This lets ice return to a pressure plate and then pass the station again without
requiring a jump.

For Solid output, `Condenser.GetSolidExitPosition` places the body left of the
footprint, including its half-width and a configurable clearance. A downward
Ground/Platform raycast finds the real supporting surface, including thin
platforms. The output has no launch velocity. Non-Solid transformations retain
their previous output behavior.

The clearance calculation assumes SoftBodyPlayer's current one-unit square of
solid points, plus the point collider radius. Update both if the ice size changes.
Place stations with clear, supported floor on their left and leave enough space
between output and plate that freezing does not immediately activate the plate.

Area2's introduction and familiarity rooms use left-return hints. Its challenge
room uses S/Down taps to descend through the one-way landings. Holding Down can
skip landings; the hint explicitly says to tap.
The challenge switch is at cell `(82, 4)`, on top of the bottom landing at y=3,
not buried in that landing. This change leaves the existing platform-drop controls
and timing unchanged.

## Regression check

In an isolated copy of the Unity project, run Unity with `-batchmode` and
`-executeMethod Area2FreezerRouteValidation.RunBatch`. Do not use `-quit` or
`-nographics`: the Play Mode probe exits on success/failure. It repairs/saves the
Area2 scene in that copy, then tests all three real freezer intakes, pressure
plates, and no-jump continuation paths. The probe supplies horizontal velocity
instead of keyboard input and invokes the same drop handler used by S/Down;
it does not teleport or jump along a tested route. Initial teleports only set up
each independent room. The probe is excluded from player builds.
