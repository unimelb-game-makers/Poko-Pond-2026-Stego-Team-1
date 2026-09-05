# Splitting Machine

The splitting machine is the one-off progression object that unlocks the player's existing split ability.

## Player progression state

`PlayerSplitController` starts with `splittingUnlocked` disabled on the shared Player prefab. While locked, Left Shift is ignored. Calling `UnlockSplitting()` enables the normal split flow and fires `EventManager.OnSplittingUnlocked` once.

For isolated mechanic testing, enable **Splitting Unlocked** on the Player prefab instance or call `SetSplittingUnlocked(true)`.

## Machine behaviour

`SplittingMachine` looks for the player inside its activation box. Stepping onto the machine unlocks splitting automatically. The machine is one-shot and gains a subtle green tint after activation.

The production `SplittingMachine.prefab` and `SplittingMachine_PropTile.asset` use `splitting_machine.png` at 320 PPU, which preserves the source's effective 32-pixel grid. The PropTile is included in the `2x2` Tile Palette. Place its single anchor cell on a Props tilemap in the Area 3 introduction room; the Area 2 builder intentionally does not place it because the level-design document introduces it in Area 3.

Run **Tools > Poko Pond > Mechanics > Rebuild Door and Splitting Machine** after changing either generated prefab or its artwork.
