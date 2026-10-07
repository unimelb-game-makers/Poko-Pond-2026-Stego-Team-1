# Poko Pond · Main Menu

The start-screen prefab, controller, and supporting scripts for Poko Pond.
Pure uGUI (Canvas + RectTransform), 1920×1080 reference, palette anchored on
`#1A1426`. Edit the prefab directly — there is no builder tool.

## Files

| Path | Purpose |
| --- | --- |
| `MainMenu.prefab` | The menu hierarchy. Instance lives in `Assets/Scenes/StartMenu.unity`. |
| `MainMenuController.cs` | Keyboard/mouse nav, scene load, quit. Attach to the Canvas root. |
| `Scripts/MenuItemHover.cs` | Per-row caret + colour tween on hover/select. |
| `Scripts/VideoBackground.cs` | Drives the optional looping background video via a RenderTexture. Clears the RT to `#1A1426` on Awake and hides the RawImage if no clip is set. |
| `BackgroundRT.renderTexture` | Render target the video plays into. |
| `Textures/caret_triangle.png` | Sprite used by each menu item's caret. |
| `Fonts/` | Drop-in TMP SDF fonts (Press Start 2P / VT323 / JetBrains Mono) when generated. Until then the menu uses the built-in LiberationSans SDF. |

## Prefab hierarchy

```
MainMenu (Canvas, Screen Space Overlay, pixel-perfect)
├── BackgroundLayers
│   ├── Clear    — solid #1A1426 Image (stretched)
│   └── Video    — RawImage (disabled until VideoClip assigned) + VideoPlayer + VideoBackground.cs
├── Hero
│   └── PokoPond — "POKO POND" title, TMP
├── Menu         — VerticalLayoutGroup
│   ├── ContinueItem — hidden until save restoration exists
│   ├── NewGameItem
│   ├── OptionsItem
│   ├── CreditsItem
│   └── QuitItem (isDanger=true, tints to #E87A8A)
├── OptionsPanel — inactive stub
├── CreditsPanel — inactive
└── EventSystem
```

## Controller wiring

Select `MainMenu` (the Canvas root) in the prefab and fill `MainMenuController`:

- `Continue Button`, `New Game Button`, `Options Button`, `Credits Button`, `Quit Button` — the five menu items.
- `New Game Scene` — defaults to `Area1-1` (the former Introduction scene).
- `Options Panel` and `Credits Panel` — wire to their corresponding children.

`New Game Scene` must be in **File → Build Profiles → Scene List**. `LoadSceneSafe` logs a clear error and refuses to load a scene that isn't in the list. Continue is hidden and disabled until save restoration is implemented; `OnContinue` does not load a scene.

The controller rebuilds explicit keyboard navigation using only active, interactable buttons. New Game receives initial focus, and background clicks restore the last usable selection. Opening either panel disables the menu and clears its focus; closing restores the previous button states and selection.

## Magenta safety net

Three defences prevent the "hot pink instead of background" regression:

1. `BackgroundLayers/Video` RawImage is **disabled by default** (`m_Enabled: 0`). `VideoBackground.cs` only enables it when a VideoClip is assigned, and clears `BackgroundRT` to `#1A1426` on Awake so an uninitialised RenderTexture never shows through.
2. The hero TMP text uses the default LiberationSans SDF material. Outline materials on disk (`OutlineMaterial_Dark.mat`, `OutlineMaterial_Mid.mat`) are **unassigned** — don't assign them without testing on the actual build target.
3. There are no custom shader overlays (scanlines / vignette / halftone) in the prefab. Don't re-add them without verifying the shaders compile on the build target.

## Controls

- `↑` / `↓` / `W` / `S` — move selection
- `Enter` / `Space` — activate
- `Esc` — close the options or credits panel and restore menu focus
- Mouse hover — selects an active, interactable row (stays in sync with keyboard)

## Outstanding

- Save system for `Continue` — hidden and disabled until real progress can be restored.
- Options panel is a dark stub; real audio/video/input UI not built.
- No background video clip recorded yet.

## Validation

Run the `PokoMenuReview.PlayMode` assembly in Unity Test Runner’s PlayMode tab.
The seven regression tests cover startup/navigation, focus after background clicks,
unavailable Continue, both modal panels, preserving disabled buttons, and New Game
loading `Area1-1`. Panel-close tests exercise the same closing method as Escape;
physical keyboard input and visual layout should also be checked in Game view.
