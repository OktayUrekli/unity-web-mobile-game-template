---
name: gameplay-feature
description: How gameplay in Assets/_Project (2D or 3D) plugs into this template - file placement, ScriptableObject tuning, pausing (PauseManager, PauseSource, GameplayPauseHandler), pooled spawning, saving progress, sounds, rewarded/interstitial ads, GameplayStateManager. Use when adding or changing gameplay - player, enemy, spawner, projectile, pickup, score, level, game over, continue, power-up, timer, reward, gameplay scene - or when asked "how do I make X work in this template".
---

# Gameplay on this template

Design the game itself however fits the request; this skill covers only the contracts with the framework. C# rules and 2D/3D physics choice: `unity-scripting`. Framework details: `core-systems`. Screens and popups: `ui-development`.

The real example is `Assets/_Project/Scripts/Systems/GameplayController.cs` in `02_Gameplay` (HUD, pause and game-over popups, best score via `ProgressStore`, `HappyTime`, leaderboard, rewarded continue, interstitials, `GameplayStateManager`). Extend or replace it rather than adding a second session owner.

## Placement

Actors in `_Project/Scripts/Gameplay/` (`_Project.Gameplay`), session/flow controllers in `Scripts/Systems/`, ScriptableObject classes and `SaveData` subclasses in `Scripts/Data/`, UI scripts in `Scripts/UI/`, events in `_Project/Events/Gameplay/`, assets in `ScriptableObjects/`, `Prefabs/Gameplay/`, `Scenes/Gameplay/`, sounds (`SoundData`) in `Audio/Sounds/`. A new playable scene needs Build Settings + a name in `_Project.Systems.GameScenes` + a `BootstrapGuard` (+ a `SceneMusic` if it has music).

Tuning values go in `_Project` ScriptableObjects referenced from the scene; never write runtime state into them (in the Editor it persists into the asset).

## Pausing

`PauseManager` owns `Time.timeScale`; never write it. The game pauses while any source holds a pause:

- Pause menu: the `PausePopup` holds `PauseSource.Menu` while it is open (`OnShown`/`OnHidden`), so opening it pauses and closing it any way (button, Back, leaving the scene) resumes. Open it from a HUD button, `UIBackRequestedEvent` or `ApplicationPauseChangedEvent` (app backgrounded / tab unfocused / platform pause request).
- Ads: `AdRequestedEvent` → (ad starts) `AdStartedEvent` → `AdFinishedEvent` → `AdRequestCompletedEvent` → your callback. `PauseManager` pauses for the whole request; audio mute and UI input blocking are handled too.
- Other pauses (tutorial, cutscene): `static readonly PauseSource Tutorial = new("Tutorial")` and `PauseManager.Instance.Pause/Resume(Tutorial)`. Slow motion: `PauseManager.Instance.TimeScale`.
- Scaled time (`Time.deltaTime`, physics, Animator, non-`SetUpdate(true)` tweens) stops by itself. Derive systems on unscaled time, or that must stop work, from `GameplayPauseHandler` (`Pause()`/`Resume()` once per change). For many pooled objects, pause them from their owner instead of one subscription each.
- Input callbacks keep firing while paused: gate them on `PauseManager.Instance.IsPaused`.

## Spawning

Repeated spawns use a pool (`PoolConfig` entry or `PoolManager.CreatePool`). `PoolManager.Instance.Get(id, position, rotation)` places the object before it activates; reset per-spawn state in `OnSpawn` (velocity included); stop tweens in `OnDespawn`; return it with `Release(obj)`. Everything still out is released when the scene unloads.

## Progress and sounds

`SaveData` subclass with public defaulted fields, key as `const string`; save at meaningful moments and `Flush()` at run end. Sounds: a `[SerializeField] private SoundData hitSound;` played with `AudioManager.Instance.PlaySfx(hitSound)`; scene music through `SceneMusic`; button clicks through `ButtonClickSound`.

## Platform services

Only through `PlatformManager.Instance`. `Capabilities` decides whether to *offer* a feature; the `AdResult` decides whether it worked.

- Rewarded: grant only when the callback gets `AdResult.Completed`, never on ad events. Start the callback with `if (this == null) return;`. `_Core.UI.Components.RewardedAdButton` already handles capability, adblock and in-progress states.
- Interstitials only at natural breaks. A cooldown (`PlatformConfig.InterstitialCooldownSeconds`, 180 s) can answer `Cooldown` without showing anything; the flow must continue for every result.
- `GameplayStateManager.BeginGameplay()` when a level or round starts (from `Start`) or continues, `EndGameplay()` at game over or when leaving. Pauses and ads need no calls: the pause menu and app pauses are reported to the platform automatically.
- `Game.HappyTime()` for rare celebratory moments; `Leaderboard.SubmitScore` only when `SupportsLeaderboard`.
- A missing service call is added to the interface, its Null service and each platform first (`platform-integration`).

## Before calling it done

- Played `00_Bootstrap` → menu → gameplay → menu → gameplay: no leftover pooled objects or doubled handlers.
- Rewarded and interstitial ads tried in the Editor (CrazySDK preview): gameplay freezes and resumes, reward granted once.
- Escape opens the pause popup; closing it resumes.
- New UI prefabs are in `UIConfig`, scenes in Build Settings and `GameScenes`, sounds assigned in their fields; compiles (`unity-verify`); review with `unity-code-review`.
