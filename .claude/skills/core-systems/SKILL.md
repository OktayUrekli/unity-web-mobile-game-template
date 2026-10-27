---
name: core-systems
description: Design and traps of the Assets/_Core framework (bootstrap, managers, EventBus, pause and gameplay state, save, settings, localization, audio, pools, scenes) and how game content plugs in. Use when adding or changing these systems or calling them from game code.
---

# Core systems (Assets/_Core)

Read the source for signatures and order; this file keeps the design decisions and the traps. Platform services are in `platform-integration`, UI in `ui-development`.

## Bootstrap

`Bootstrapper` (in `00_Bootstrap`) runs once per app start and creates every manager in code; read it for the order. Each step has its own try/catch, so one failing manager doesn't stop the boot. In the Editor a `BootstrapGuard` returns to the scene it came from. The `LoadingScreen` prefab lives only in `00_Bootstrap`; a game changes its logo and name text.

- Platform services exist only after `PlatformManager.InitializeAsync()`; anything that reads them is created after it.
- `EventBus.Clear()` wipes earlier subscriptions. Never load `00_Bootstrap` again at runtime.
- Every directly playable scene needs a `BootstrapGuard`.

## Adding a manager or config

- Derive from `Singleton<T>`. `Awake` only sets identity (`base.Awake(); if (IsDuplicate) return;`). Dependent setup and subscriptions go in a public `Init()` that the Bootstrapper calls, undone in an `internal Shutdown()` that `OnDestroy` calls (EditMode tests call it directly). Add the `Create<Name>()` step after everything its `Init()` touches.
- Managers that would depend on each other "sideways" talk through events (SettingsManager publishes `AudioSettingsChangedEvent`; AudioManager applies it).
- Config: a `ScriptableObject` class in `_Core` with `[CreateAssetMenu(menuName = "Configuration/...")]`, private serialized fields and read-only properties, a slot on `GameConfig`, the asset in `Assets/_Project/ScriptableObjects/Config`. Assigning it is an Editor step: tell the user if you can't. Log a clear error when the slot is empty. Game tuning doesn't go in `GameConfig`; it lives in `_Project` ScriptableObjects.

## Game content: no enums in _Core

| Content | Game code refers to it by | Registration | Missing |
|---|---|---|---|
| Screen / popup | its type: `UIManager.ShowScreen<T>()`, `ShowPopup<T>()` | prefab in `UIConfig` (root carries the `UIScreen`/`UIPopup` subclass; one prefab per type) | error, returns null |
| Sound / music | a `SoundData` asset in a serialized field | none (Create > Audio > Sound) | warning, nothing plays |
| Scene | a name constant in `_Project.Systems.GameScenes` | Build Settings; the first scene after boot in `SceneConfig` | error, nothing loads |
| Pool | a string id (`const`s in one `_Project` class) | `PoolConfig` entry or `PoolManager.CreatePool` | error, `Get` returns null |

## EventBus

Static, one channel per event type, no allocation per publish. Events are `readonly struct`s implementing `IGameEvent` in `_Core/Events/<Area>/` or `_Project/Events/<Area>/`. A throwing handler doesn't stop the others. Handlers added or removed during a publish take effect from the next publish. Subscribe with method groups so you can unsubscribe.

## Pause and gameplay state

- `PauseManager` is the only owner of `Time.timeScale`. The game is paused while any `PauseSource` holds a pause: `Menu` (game code; released when the scene changes), `Ad` (whole ad request, automatic), `Application` (background, focus loss or a platform pause request, automatic), or a game's own `new PauseSource("Tutorial")`. Any other write to `Time.timeScale` is undone in `LateUpdate` with a one-time warning. Each change publishes `PauseStateChangedEvent`.
- `GameplayPauseHandler`: base class for systems on unscaled time or that must stop work at pause; `Pause()`/`Resume()` run once per change, and at once when enabled during a pause.
- `GameplayStateManager.BeginGameplay()/EndGameplay()` mark a level or round. What the platform is told (`IsPlaying`) is the session minus pauses by a source with `StopsPlatformGameplay` (all but `Ad`). It ends the session when the active scene changes, so call `BeginGameplay` from `Start`, not `Awake`.
- `AppLifecycleManager` publishes `ApplicationPauseChangedEvent` on background or focus loss (not during an ad request or for Editor focus) and while the platform asks for a pause; it also sets the mobile frame rate and keeps the screen awake while playing.
- `Haptics.Vibrate()` honours the vibration setting and works only on Android/iOS (`Haptics.IsSupported`).

## Save, settings, localization

- `SaveManager.Save(key, data)` / `Load(key, out T)` with `T : SaveData` and `JsonUtility` (public fields; no properties, dictionaries or polymorphism). `Load` returns false and a fresh `new T()` when nothing is stored.
- `Save` writes to an in-memory cache. Storage is written after a debounce, on focus loss, pause or quit, when a WebGL page is hidden or closed, or on `Flush()`: call `Flush()` after key moments. Failed writes retry with back-off. A key whose read failed is never written blindly: it is read again first, and data this session never saw wins.
- Storage is the platform's `ISaveStorage`, with `PlayerPrefsSaveStorage` as the fallback. Async backends need `SaveManager.PreloadAsync(keys)` before `Load`. Saves are checked against 1 MB (CrazyGames).
- Evolving save classes: see the `SaveData` rule in CLAUDE.md; keep a renamed field until players have migrated. Key `"settings"` belongs to `SettingsManager`.
- `SettingsManager` setters persist and publish (`AudioSettingsChangedEvent`, `LanguageChangedEvent`); the settings UI calls `Commit()` when it closes. Music and SFX are on/off, not volumes.
- Language: the saved choice, else the platform's `IGameService.Language`, else the device language ("en-US" picks "en"). Boot waits for Localization at most 10 s. `_Core` strings live in the `Core` table.

## Audio, pools, scenes

- `AudioManager.PlayMusic/StopMusic/PlaySfx(SoundData)`. The mixer in `AudioConfig` must expose `MusicVolume` and `SFXVolume`. Ads and platform mute requests mute it automatically. `PlayMusic` keeps a track that is already playing, so `SceneMusic` never restarts shared music. Buttons click through `ButtonClickSound`, never from handlers.
- `PoolManager.Get(id, position, rotation, parent)` places the object before activation, then calls `IPoolable.OnSpawn()`. Release exactly once with `Release(obj)`. Everything still out is released when a scene unloads.
- Player-driven scene changes use `SceneTransition.LoadAsync(name)` (fade, input blocked, a second request ignored); `SceneLoader` loads without the fade. Every load hides all UI. Never hold activation (`allowSceneActivation = false`): Unity then also holds Addressables loads, Localization included, and the WebGL boot can stall.

## Verify

Play from `00_Bootstrap` or a guarded scene; add EditMode tests for new `_Core` logic (`unity-verify`).
