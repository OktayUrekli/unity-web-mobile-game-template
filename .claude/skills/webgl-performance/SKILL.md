---
name: webgl-performance
description: WebGL and CrazyGames build, size, memory and runtime work for this template (2D or 3D) - what the CrazySDK Development/Release Build and Analyzer do, size limits, texture/mesh/audio compression, heap and GC, no-threads rules, web saves, browser audio, and CrazyGames SDK runtime rules (gameplay start/stop, happytime, ads, banners, muteAudio). Use for WebGL builds, browser-only bugs, build size, load time, memory, stutter, web audio or saves, or a release upload. Takes precedence over the plugin skill optimize-web, because the CrazySDK build overrides several Player Settings.
---

# WebGL and CrazyGames runtime

The browser gives one thread, a heap that never shrinks, and download size as the first impression. General C# performance is in `unity-scripting`.

## Builds

Use `CrazySDK/Development Build` or `Release Build` (`Assets/CrazySDK/Scripts/Editor/Builder/`). The builder overrides Player Settings during the build and restores them afterwards, so `ProjectSettings.asset` is not what ships. Full table: `references/crazysdk-build-pipeline.md`.

- Development: no compression, full stack traces, output `Builds/CrazyGamesDevelopment`, served on localhost where the real SDK loads (good for testing ads and data). It does not work on CrazyGames itself.
- Release: Brotli, `ExplicitlyThrownExceptionsOnly`, LTO and engine stripping, IL2CPP size optimisation, extra ASTC build and, with "Runs on mobile web", 512/1024 MB memory variants. Output `Builds/CrazyGamesRelease` (upload the whole folder). Because only explicitly thrown exceptions are catchable, a `NullReferenceException` isn't caught by try/catch there: null-check SDK-facing code and reproduce release-only bugs with a Development build.
- It builds `EditorBuildSettings.scenes` and ignores Build Profiles.

## Size

The Analyzer (after every non-development WebGL build) warns at total > 250 MB (rejected), initial load > 50 MB (may be rejected) and > 20 MB (may be disabled on mobile). Initial load = wasm + framework + loader + data, and QA measures until the first gameplay start, so keep `00_Bootstrap`/`01_MainMenu` light and call `BeginGameplay` as soon as the player can play.

Usual levers: texture max size and compression, no Read/Write or unneeded mipmaps, sprite atlases (2D), mesh compression and LODs (3D), mono and lower-quality long audio, nothing unused in `Resources/` (but `Assets/CrazySDK/Resources/` is required), post-processing and HDR off when unused. More in `references/build-size-and-memory.md`.

## Memory and threads

- The heap grows up to `webGLMaximumMemorySize` and never shrinks; spikes (big scene loads, huge JSON, prewarming a large pool) are permanent. Design to fit in 512 MB for mobile browsers.
- GC effectively runs between frames, so per-frame garbage piles up until frame end. Pool and keep hot paths allocation-free.
- No threads: `.Result`/`.Wait()`/spin-waits freeze the tab, `Task.Run` has no workers. `async`/`await` is fine (continuations resume on the main thread, SDK callbacks included). Poll with `await Task.Yield()` and time with `Time.realtimeSinceStartup`.

## Saves and audio in the browser

- On CrazyGames progress goes to `CrazySDK.Data` via `CrazyGamesDataStorage`; elsewhere `PlayerPrefsSaveStorage` (IndexedDB, per domain). Don't use raw `PlayerPrefs` for progress. Writes flush on focus loss; `Flush()` after key moments. Test persistence by reloading the tab.
- The audio context starts only after a user gesture, so silent music before the first click is expected.
- Player Settings "Run In Background" stays on (the CrazySDK builder keeps it). Off, WebGL stops updating while the page has no focus, so boot freezes on the loading screen until the player clicks the game; gameplay still pauses on focus loss through `AppLifecycleManager`.
- During ads the SDK zeroes `AudioListener.volume` (and `timeScale` with `pauseGameDuringAd`, which `PauseManager` overrides with its own value) and restores them; the project also mutes the mixer through the ad request events. Don't set `AudioListener.volume` yourself or add another mute path; CrazyGames `muteAudio` is already applied through `PlatformAudioMuteChangedEvent`.

## CrazyGames runtime rules

Details: `references/crazygames-runtime-rules.md`.

- Gameplay start/stop through `GameplayStateManager` (level start and game over); pauses are reported by it automatically, ads are not.
- SDK 5.31.0 has no loading API; `IGameService.GameReady()` is a no-op there.
- `HappyTime()` for rare celebrations. Leaderboard needs `PlatformConfig.LeaderboardEnabled` and `LeaderboardEncryptionKey`.
- One ad request at a time; interstitial cooldown in `AdPolicyService`; placement policy is on docs.crazygames.com. `PauseManager` pauses the game for every ad request, whatever `pauseGameDuringAd` says.
- Banners need an active `CrazyBanner` **and** `RefreshBanners()`.
- The SDK script downloads at startup and bootstrap waits on it, behind a timeout.

For the full release flow use `/crazygames-release`.
