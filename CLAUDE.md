# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity game-jam project "Progress Make you Weaker" (product name `ProgressMakeUWeaker`).

- Unity **6000.5.4f1** (Unity 6), **URP** 17.5, TextMesh Pro, uGUI.
- Input handling is set to **Both** (legacy Input Manager + new Input System 1.19). Current menu code uses uGUI `EventSystems` pointer handlers rather than input actions.
- No custom assembly definitions: all game scripts compile into `Assembly-CSharp`. There are no tests yet (Unity Test Framework is installed but unused).

## Building / running

There is no CLI build script. Open the project in the Unity Editor and press Play, or build via File > Build Profiles. The only scene in the build list is `Assets/Scenes/SampleScene.unity` (the main menu). `Assets/Elizabeth Warren caricature/Warren.unity` is the scene bundled with the imported character asset.

`ProjectSettings/EditorBuildSettings.asset` is frequently modified locally by the Editor; don't commit incidental changes to it unless the scene list was intentionally changed.

`Library/`, `Temp/`, `Logs/`, `UserSettings/`, and generated `.csproj`/`.slnx` files are Editor-generated; never edit them. When adding/moving/renaming assets, keep the accompanying `.meta` file (its GUID is what scenes and prefabs reference).

## Code architecture

All gameplay code so far lives in `Assets/UI Scipt/` (note the folder-name typo, it is referenced by `.meta` GUIDs, so don't rename casually). It is the main-menu scene logic: small, independent `MonoBehaviour`s wired together through `[SerializeField]` references in the Inspector rather than via `FindObjectOfType`/singletons.

Main menu flow in `SampleScene`:
- `MenuSlideIn`: hides the menu buttons in `Awake`, then slides and fades them in with a stagger after a delay.
- `MenuButtonReaction` (on each button) forwards pointer enter/exit to `MenuCharacterReaction` on the 3D character. The character drives Animator parameters `Happy`/`Sad` bools and the `ShakeHead` trigger, walks to a `quitSpot` when the Quit button is hovered, and uses `OnAnimatorIK` look-at to turn its head toward the hovered UI button (screen point → ray at head depth). This requires a Humanoid rig and IK Pass enabled on the Animator layer.
- `SettingsCameraTransition`: rotates the main camera to a `settingsView` transform to show the settings panel and back; it snapshots the initial slider/toggle/dropdown values in `Awake` so `ResetSettings` can restore them. `ResolutionsSettings` maps dropdown indices 0/1/2 to 1920x1080, 1600x900, and 1680x1050.
- `ButtonHoverScale`, `TextReveal`, `ShowTextAtAngle`, `ShowTextWhenLookingUp`: small visual effects.

Conventions used across these scripts:
- Animations are hand-written coroutines with smoothstep easing (`t * t * (3f - 2f * t)`), not tweening libraries or Animator for UI.
- Menu/camera/character motion uses `Time.unscaledDeltaTime` so it keeps working when `timeScale` is 0; follow that for any menu/pause UI.
- Missing Inspector references are reported with `Debug.LogError`/`LogWarning` and an early return. Code comments and log messages are written in Vietnamese.
