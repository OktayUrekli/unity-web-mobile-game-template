---
name: core-systems
description: How to use and extend the framework in Assets/_Core (except Platform) - Bootstrapper and Singleton managers, GameConfig configs, EventBus, PauseManager and GameplayStateManager, SaveManager, SettingsManager, AudioManager and SoundData, PoolManager, SceneLoader and SceneTransition, and how game content (UI, sounds, scenes, pools) plugs in without touching _Core. Use when adding a manager, config, scene, sound, pool, event, save data or setting, or when game code calls these systems.
---

# Core systems (Assets/_Core)

Read the source for signatures; this file records the design and the traps. Check `unity-code-review/references/known-issues.md` before relying on edge-case behaviour.

## Bootstrap order

`Bootstrapper` (in `00_Bootstrap`): `Awake` → `ConfigurationManager.Initialize(gameConfig)`. `Start` (first boot only, when no `PersistentRoot` exists) → `EventBus.Clear()` → create `PersistentRoot` (DontDestroyOnLoad) → LocalizationManager + `BeginLoad()` (Localization loads in parallel with the platform) → **await `PlatformManager.InitializeAsync()`** → AudioManager.Init → SaveManager.Init → SettingsManager.Init (publishes the audio settings AudioManager subscribed to) → **await LocalizationManager.InitAsync** (picks the saved language) → PoolManager → UIManager → SceneTransition → AdBlockerOverlay → PauseManager.Init → GameplayStateManager.Init → AppLifecycleManager.Init → EventSystem. Each step has its own try/catch; then `SceneConfig.FirstScene` loads asynchronously (in the Editor, `Bootstrapper.ReturnScenePath` set by a `BootstrapGuard` wins) and `IGameService.GameReady()` is called once it is shown.

The `LoadingScreen` prefab (`Assets/_Project/Prefabs/UI/LoadingScreen.prefab`, script `_Core.UI.LoadingScreen`) lives only in `00_Bootstrap` and is assigned to the Bootstrapper. The Bootstrapper reports weighted step progress (platform, localization, managers), fills the bar, then loads the first scene without holding its activation. Games change only its logo sprite and name text (empty text shows Product Name; the game name is not localized).

Consequences:
- Platform services are null before the await, so anything reading them is created after it.
- `EventBus.Clear()` wipes subscriptions made earlier in the bootstrap scene. Don't load `00_Bootstrap` again at runtime.
- Add a `BootstrapGuard` to every scene that should be playable directly.

## Adding a manager

Derive from `_Core.Managers.Singleton<T>`; keep `Awake` for identity (`base.Awake(); if (IsDuplicate) return;`) and put dependent setup and subscriptions in a public `Init()`, undone in an `internal Shutdown()` that `OnDestroy` calls (EditMode tests call it, since `OnDestroy` does not run there). `AddComponent<T>()` runs `Awake` before the Bootstrapper can pass anything in, so `Init()` makes the order explicit. Add a `Create<Name>()` step in `Bootstrapper`, placed after everything its `Init()` touches.

Managers talk through events rather than calling each other when the dependency would point "sideways" (SettingsManager publishes `AudioSettingsChangedEvent` instead of calling AudioManager).

A config for it: a `ScriptableObject` with `[CreateAssetMenu(menuName = "Configuration/...")]`, `[SerializeField] private` fields with read-only properties, a slot on `GameConfig`, and the asset assigned in `GameConfig.asset` (an Editor step; tell the user if you can't do it). Config assets live in `Assets/_Project/ScriptableObjects/Config`; their classes stay in `_Core`. Log a clear error when the slot is empty. Game tuning does not go in `GameConfig`; it lives in `_Project` ScriptableObjects.

## Game content: no enums in _Core

| Content | How game code refers to it | Registration | Missing |
|---|---|---|---|
| Screen / popup | its type: `UIManager.ShowScreen<T>()`, `ShowPopup<T>()` | prefab in `UIConfig` (root carries the `UIScreen`/`UIPopup` subclass) | error logged, returns null |
| Sound / music | a `SoundData` asset in a serialized field | none (Create > Audio > Sound) | warning, nothing plays |
| Scene | name constant in `_Project.Systems.GameScenes` | Build Settings; the first scene after boot in `SceneConfig` | error logged, nothing loads |
| Pool | string id (keep `const`s in one `_Project` class) | `PoolData` in `PoolConfig`, or `PoolManager.CreatePool` | error logged, `Get` returns null |

`UIConfig` keys prefabs by the exact component type on their root; two prefabs with the same type are an error.

## EventBus

Static, one channel per compile-time event type (`EventBus<T>` internally): no dictionary lookup, boxing or allocation per publish. Events are `readonly struct`s implementing `IGameEvent`, in `_Core/Events/<Area>/` or `_Project/Events/<Area>/`. Each handler runs in its own try/catch. Handlers added or removed during a publish take effect from the next publish. Subscribe with method groups so you can unsubscribe.

## Pause and gameplay state

- `PauseManager` (`_Core.Gameplay`) is the only owner of `Time.timeScale`. The game is paused while any `PauseSource` holds a pause: `PauseSource.Menu` (game code; released when the scene changes), `PauseSource.Ad` (the whole ad request, automatic), `PauseSource.Application` (app background/focus loss or the platform's pause request, automatic), or a game's own `new PauseSource("Tutorial")`. `TimeScale` is the scale while running (slow motion). Writing `Time.timeScale` directly is undone in `LateUpdate` with a one-time warning (this also repairs CrazySDK's late cleanup after an ad timeout). Every change publishes `PauseStateChangedEvent`.
- `GameplayPauseHandler`: derive systems that run on unscaled time or must stop work at pause; `Pause()`/`Resume()` are called once per change, and right away when enabled during a pause.
- `GameplayStateManager.BeginGameplay()/EndGameplay()` mark a level/round. `IsPlaying` (what the platform is told, `GameplayStateChangedEvent`) is the session minus pauses by a source with `StopsPlatformGameplay` (all except Ad). It ends the session on active scene change, so call `BeginGameplay` from `Start`, not `Awake`.
- `AppLifecycleManager` (`_Core.Managers`): mobile `targetFrameRate` from `PlatformConfig.MobileTargetFrameRate`, screen kept awake while playing (mobile), and `ApplicationPauseChangedEvent` on background/focus loss (not during an ad request, not for Editor focus) or while the platform asks for a pause (`PlatformPauseChangedEvent`; both sources combined, one event per change).
- `Haptics.Vibrate()` (`_Core.Feedback`): honours `SettingsManager.IsVibrationEnabled`; Android/iOS only (`Haptics.IsSupported`).

## Save and settings

- `SaveManager.Instance.Save(key, data)` / `Load(key, out T data)` / `Delete` / `Flush()`, `T : SaveData`. `JsonUtility`: `[Serializable]`, public fields, no properties/dictionaries/polymorphism. `Load` returns false and a fresh `new T()` when nothing is stored.
- `Save` only updates an in-memory cache; storage is written after the `SaveConfig` debounce, on focus loss/pause/quit, when a WebGL page is hidden or closed (`SaveManager.jslib`), or on `Flush()`. Call `Flush()` after key moments. A failed write stays pending and is retried on a timer (2 s, doubling up to 60 s). A key whose storage read failed (`Load` returns false) is never written blindly: `SaveCache` reads it again first and, if it holds data this session never saw, keeps that data and drops the change (logged).
- Storage is the platform's `ISaveStorage`; a null or `NullSaveStorage` is replaced by `PlayerPrefsSaveStorage`. Async backends (`IAsyncSaveStorage`) need `SaveManager.PreloadAsync(keys)` before `Load`. Size is checked against 1 MB (CrazyGames).
- Evolve save classes by adding defaulted fields (no version change). To rename, remove or reinterpret a field, raise the class's `CurrentVersion` and convert older data in `Migrate(fromVersion)` (0 = saved before versioning), keeping the old field until players have migrated. Key `"settings"` belongs to `SettingsManager`.
- `SettingsManager`: properties `IsMusicEnabled`, `IsSfxEnabled`, `IsVibrationEnabled`, `Language`, `IsFirstLaunch`; setters `SetMusicEnabled`, `SetSfxEnabled`, `SetVibrationEnabled`, `SetLanguage`, `CompleteFirstLaunch` persist and publish (`AudioSettingsChangedEvent`, `LanguageChangedEvent`); call `Commit()` when the settings UI closes. Music/SFX are on/off, not volumes.
- Language: empty `Language` follows the platform's `IGameService.Language`, else `Application.systemLanguage` ("en-US" picks "en"). `LocalizationManager` switches `LocalizationSettings.SelectedLocale` on `LanguageChangedEvent`; boot waits for it at most 10 s (frame-polled). Strings owned by `_Core` live in the `Core` table (`LocalizationManager.CoreTable`).

## Audio, pools, scenes

- `AudioManager.PlayMusic(SoundData)` / `StopMusic()` / `PlaySfx(SoundData)` / `PlayButtonClick()`. The mixer (`AudioConfig`) must expose `MusicVolume` and `SFXVolume` (0 dB on, -80 dB off/muted). Ads and platform mute/pause requests mute it automatically. `PlayMusic` keeps a track that is already playing, so `SceneMusic` (one per scene) never restarts shared music. `ButtonClickSound` on a button plays `AudioConfig.ButtonClickSound` (or its own sound); don't play clicks from handlers.
- `PoolManager.Get(id)`, `Get(id, position, rotation, parent)` (placed before activation), `Get<T>(...)`, `Release(obj)`, `CreatePool(id, prefab, size)`, `ReleaseAll()`. Pools live under `PersistentRoot`; every object still out is released when a scene unloads (objects destroyed with the scene are forgotten). `Get` activates then calls `IPoolable.OnSpawn()` (root object only). Release exactly once (a second `Release` is ignored with a warning).
- Scene changes made by the player go through `SceneTransition.LoadAsync(name)`: fade out (unscaled), async load, one frame, fade in; input blocked, a second request ignored. `SceneLoader.Load/LoadAsync(name)` load without the fade. Every `SceneLoader` load publishes `SceneLoadStartedEvent`, on which `UIManager` hides all UI. Don't hold activation (`allowSceneActivation = false`): Unity then also holds Addressables loads, Localization included, and the WebGL boot can stall.

## Verify

Play from `00_Bootstrap` (or a guarded scene). Harness style: `Assets/_Project/Testing/SaveTest.cs` in `99_Test`.
