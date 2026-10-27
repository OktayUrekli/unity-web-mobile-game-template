---
name: gameplay-feature
description: How game code in Assets/_Project connects to the template - placement, tuning data, pausing, pooled spawning, progress, sounds, ads and gameplay start/stop. Use when adding or changing gameplay, 2D or 3D.
---

# Gameplay on this template

Design the game however the request needs; this skill covers only the contracts with the framework. C# rules: `unity-scripting`. Framework details: `core-systems`. Screens and popups: `ui-development`.

One session owner per gameplay scene runs the loop: HUD, pause and game over, saving, ads, `GameplayStateManager`. Find it by its `BeginGameplay()` call, then extend or replace it; never add a second owner.

## Placement

- Actors in `_Project/Scripts/Gameplay/`, session and flow controllers in `Scripts/Systems/`, ScriptableObject and `SaveData` classes in `Scripts/Data/`, UI in `Scripts/UI/`, events in `_Project/Events/Gameplay/`.
- Assets in `ScriptableObjects/`, `Prefabs/Gameplay/`, `Scenes/Gameplay/`, `Audio/Sounds/`.
- A new playable scene needs Build Settings, a name in `GameScenes`, a `BootstrapGuard`, and a `SceneMusic` if it has music.
- Tuning values live in `_Project` ScriptableObjects. Never write runtime state into them (in the Editor it persists into the asset).

## Pausing

The mechanics are in `core-systems`. On the game side:

- The `PausePopup` holds `PauseSource.Menu` while open; open it from a HUD button, `UIBackRequestedEvent` or `ApplicationPauseChangedEvent`.
- Tutorials and cutscenes use their own `PauseSource`. Slow motion uses `PauseManager.Instance.TimeScale`.
- Scaled time stops by itself. Systems on unscaled time derive from `GameplayPauseHandler`; for many pooled objects, pause them from their owner.
- Input callbacks keep firing while paused: gate them on `PauseManager.Instance.IsPaused`.

## Spawning, progress, sounds

- Repeated spawns use a pool. Reset per-spawn state (velocity included) in `OnSpawn`, stop tweens in `OnDespawn`.
- Progress: a `SaveData` subclass with defaulted public fields and a `const string` key; save at meaningful moments and `Flush()` at the end of a run.
- Sounds: `[SerializeField] private SoundData hitSound;` played with `AudioManager.Instance.PlaySfx(hitSound)`.

## Platform services

Only through `PlatformManager.Instance`. `Capabilities` decides whether to offer a feature; the `AdResult` decides whether it worked.

- Rewarded: grant only on `AdResult.Completed` in the callback, never on ad events; start the callback with `if (this == null) return;`. `RewardedAdButton` already handles capability, adblock and busy states.
- Interstitials only at natural breaks. The cooldown can answer `Cooldown` without showing anything; the flow continues on every result.
- `BeginGameplay()` when a level or round starts (from `Start`) or continues, `EndGameplay()` at game over or when leaving. Pauses and ads need no calls.
- `Game.HappyTime()` for rare celebrations; `Leaderboard.SubmitScore` only when `SupportsLeaderboard`.
- A missing service call is added to the interface, its Null service and every platform first (`platform-integration`).

## Before calling it done

- Bootstrap → menu → gameplay → menu → gameplay: no leftover pooled objects or doubled handlers.
- Rewarded and interstitial ads in the Editor (CrazySDK preview): gameplay freezes and resumes, the reward is granted once.
- Escape opens the pause popup; closing it resumes.
- New UI prefabs are in `UIConfig`, scenes in Build Settings and `GameScenes`, sound fields assigned. Compile with `unity-verify`; review with `unity-code-review`.
