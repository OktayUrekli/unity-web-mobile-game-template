# Known issues (living list)

The single, authoritative list of defects currently present in `Assets/_Core`, `Assets/_Project` and project settings. Other skills point here instead of describing bugs themselves.

Rules for keeping it true:
- **Remove or update an entry in the same change that fixes it.** A fix without the list update is incomplete.
- Add an entry when a review finds a pre-existing defect that is not fixed right away.
- Locate code by `path` + type/method, never line numbers. Re-read the code before quoting an entry - it may already be fixed.
- Severity uses the review scale: **Critical**, **Major**, **Minor**, **Nit**.
- Format: `path` - `Type.Member` - severity - problem. **Fix:** one line.

## Contents
- Ads
- Platform
- Save
- Settings
- Audio
- Mobile
- Build / Packages

## Ads

- `Assets/_Platforms/CrazyGames/Services/CrazyGamesAdsService.cs` - timeout - **Nit** - after `AdPolicyService` gives up on a CrazyGames ad, the game resumes while the SDK may still answer: a late `Completed` for a rewarded ad is ignored (no reward), and the SDK's own cleanup writes the time scale it stored at request time (0), which `PauseManager` overwrites in its next `LateUpdate`. **Fix:** none needed unless players report a lost reward.

## Platform

- `Assets/_Core/Platform/Core/PlatformManager.cs` - `PlatformManager.InitializeAsync` - **Nit** - a second call after `State == Failed` keeps the ads service wrapped by the first attempt; a platform that claims ads then keeps `SupportsAds = true` over Null ads. The Bootstrapper never retries. **Fix:** reset services and capabilities at the start of a retry.
- `Assets/_Project/Scenes/MainMenu/01_MainMenu.unity` - "CrazyBanner" object - **Minor** - with another platform selected, CrazySDK is not compiled, so its `CrazyGames.CrazyBanner` component shows as a missing script (Editor warning, and at runtime when the menu loads in other builds); `CrazyGamesBannerController` still hides the object. **Fix:** let the CrazyGames bridge instantiate a CrazyGames-only banner prefab on `ShowBanner()` instead of keeping it in the scene.

## Save

- `Assets/_Core/Save/SaveManager.cs` - `SaveManager.OnPageHidden` - **Nit** - a WebGL page that is hidden or closed flushes through `SaveManager.jslib` (`visibilitychange`, `pagehide`), but the write itself is up to the storage: the PlayerPrefs fallback syncs to IndexedDB asynchronously, and the browser may close the page before that finishes. **Fix:** none needed unless seen; key moments already call `Flush()`.
- `Assets/_Core/Save/SaveManager.cs` - `SaveManager.Load` / `Save` - **Nit** - data written by a newer build (`saveVersion` above `CurrentVersion`) loads with its unknown fields ignored, and the next `Save` from the older build drops them. Only matters if an older build can run after a newer one (cached page, staged rollout). **Fix:** keep the raw JSON of newer-version data and refuse to overwrite it, if this is ever seen.

## Settings

- `Assets/_Core/Localization/LocalizationManager.cs` - `LocalizationManager.ResolveStartupLanguage` - **Nit** - without a saved choice or a platform language (`IGameService.Language`), first launch falls back to `Application.systemLanguage`, and `ToLanguageCode` maps only common languages. **Fix:** extend the map when adding a Locale whose language is missing.

## Audio

- `Assets/_Core/Audio/AudioManager.cs` - `AudioManager.PlaySfx` - **Nit** - with all four SFX sources busy at other pitches, the next sound reuses one and re-pitches its one-shots. **Fix:** raise `SfxSourceCount` if a game plays many differently pitched sounds at once.

## Mobile

- `Assets/_Core/Feedback/Haptics.cs` - `Haptics.Vibrate` - **Nit** - no-op on WebGL (mobile browsers support `navigator.vibrate`), so `Haptics.IsSupported` is false there and the settings popup hides the vibration toggle. **Fix:** jslib bridge if a web game needs it.
- `Assets/_Core/UI/Components/SafeAreaFitter.cs` - `SafeAreaFitter` - **Minor** - on the first Android phone test (development APK, 2026-10-04) the safe area was "not quite right" on a notched screen; the symptom was not recorded. The fitter then sat on `UIRoot`, so screen backgrounds and popup dims stopped at the notch; since 2026-10-04 each screen/popup keeps its background full-screen and fits only a `SafeArea` child. Not yet re-checked in the Device Simulator or on the phone. **Fix:** check both orientations on a notched device; if still off, compare `Screen.safeArea` with the applied anchors (also check `androidRenderOutsideSafeArea`).

## Build / Packages

- `Assets/_Project/Scripts/Editor/BuildValidator.cs` - `OnPostprocessBuild` - **Minor** - every GAME_2D WebGL build warns that the 3D Physics module is included: the engine keeps it for UI Toolkit (`Required by UIElements Module`) and for referenced `BoxCollider`/`Collider`/`Rigidbody` classes, so `GAME_2D` + `DOTWEEN_NOPHYSICS` does not strip it (Release Build 2026-10-02: initial 11.85 MB, total 16.56 MB; with Localization + Addressables: initial 13.2 MB (wasm 8.0, data 5.1), total 18.3 MB). Terrain and VFX modules are also included in a 2D build. **Fix:** find which assets/assemblies reference the 3D, Terrain and VFX classes (Build Report `strippingInfo`, Project Auditor) before deciding to drop UI Toolkit or those references.
