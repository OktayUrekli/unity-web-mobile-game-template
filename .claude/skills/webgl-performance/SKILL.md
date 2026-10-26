---
name: webgl-performance
description: Builds, download size, memory and browser behaviour for this template (2D or 3D) - CrazySDK Development/Release Build and Analyzer, Build Profiles (Web and Android), size limits, compression, heap and GC, web saves and audio. Use for builds, browser-only bugs, build size, load time, memory, stutter, or web audio and saves. Takes precedence over the plugin skill optimize-web, because the CrazySDK builder overrides Player Settings.
---

# Builds, size and the browser

The browser gives one thread, a heap that never shrinks, and download size as the first impression. C# performance rules are in `unity-scripting`; CrazySDK runtime behaviour in `platform-integration/references/crazygames.md`.

## Builds

- **CrazyGames:** `CrazySDK/Development Build` (localhost testing with the real SDK; doesn't run on CrazyGames itself) or `CrazySDK/Release Build` (upload the whole `Builds/CrazyGamesRelease` folder). The builder overrides Player Settings during the build and restores them afterwards, so `ProjectSettings.asset` is not what ships; it builds `EditorBuildSettings.scenes` and ignores Build Profiles. Details: `references/crazysdk-build-pipeline.md`.
- Release builds catch only explicitly thrown exceptions: a `NullReferenceException` passes through try/catch there. Null-check SDK-facing code and reproduce release-only bugs with a Development build.
- **Other portals:** `Web - Desktop - Release` (DXT) / `Web - Mobile - Release` (ASTC) with Unity's own Build.
- **Android:** `Android - Development` (APK) for phone tests, `Android - Release` (AAB) for Google Play, with Google Play selected as the platform. Keystores never go in the repo.
- Build Profiles never carry a Player Settings override: an active one replaces the template's defines, and the SDK and bridges stop compiling.
- `BuildValidator` (`Tools/Build/Validate Build Settings`) runs before every build and blocks a wrong scene list, a test scene or harness in the build, or a platform that doesn't match the target.

## Size

The Analyzer (after every non-development WebGL build) warns at total > 250 MB (rejected), initial load > 50 MB (may be rejected) and > 20 MB (may be disabled on mobile). QA measures the initial load until the first gameplay start, so keep `00_Bootstrap` and the menu light and call `BeginGameplay` as soon as the player can play.

Usual levers: texture max size and compression, no Read/Write or unneeded mipmaps, sprite atlases (2D), mesh compression and LODs (3D), mono and lower-quality long audio, nothing unused in `Resources/` (`Assets/CrazySDK/Resources/` is required), post-processing and HDR off when unused. More: `references/build-size-and-memory.md`.

## Memory and threads

- The heap grows up to `webGLMaximumMemorySize` and never shrinks; spikes (big scene loads, huge JSON, prewarming a large pool) are permanent. Design to fit 512 MB for mobile browsers.
- GC effectively runs between frames, so per-frame garbage piles up. Pool and keep hot paths allocation-free.
- No threads: `.Result`, `.Wait()` and spin-waits freeze the tab. `async`/`await` is fine; continuations resume on the main thread.

## Saves and audio in the browser

- Progress goes to the platform storage (CrazyGames: `CrazySDK.Data`), else `PlayerPrefsSaveStorage` (IndexedDB, per domain). Don't use raw `PlayerPrefs` for progress. Test persistence by reloading the tab.
- The audio context starts only after a user gesture: silent music before the first click is expected.
- "Run In Background" stays on. Off, WebGL stops updating without focus and the boot freezes on the loading screen until a click; gameplay still pauses on focus loss.
- Don't set `AudioListener.volume` or add another mute path: ads, platform mute and settings already go through the mixer.

Release flow: `/crazygames-release`.
