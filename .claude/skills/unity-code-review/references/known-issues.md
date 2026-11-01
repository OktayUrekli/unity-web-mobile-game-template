# Known issues

Defects currently present in this repository's code, scenes and settings. Other skills point here instead of describing bugs themselves.

- Remove or update an entry in the same change that fixes or alters the defect.
- Add an entry only for a real defect that stays unfixed and could mislead a later session, and only when the user agrees or a change knowingly leaves it in place. Ideas, style points and one-off observations stay out.
- Locate code by `path` + type/method, never line numbers. Re-read the code before quoting an entry; it may already be fixed.
- Format: `path` - `Type.Member` - severity (**Critical**, **Major**, **Minor**, **Nit**, as in the review scale) - problem. **Fix:** one line.

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
- `Assets/_Core/UI/Components/SafeAreaFitter.cs` - `SafeAreaFitter` - **Minor** - on a notched Android phone the safe area looked slightly off (symptom not recorded), before screens and popups were changed to keep their background full-screen and fit only a `SafeArea` child. Not re-checked since. **Fix:** check both orientations in the Device Simulator or on a notched device; if still off, compare `Screen.safeArea` with the applied anchors (also check `androidRenderOutsideSafeArea`).

## Build / Packages

- `Assets/_Project/Scripts/Editor/BuildValidator.cs` - `OnPostprocessBuild` - **Minor** - every `GAME_2D` WebGL build warns that the 3D Physics module is included: the engine keeps it for UI Toolkit (`Required by UIElements Module`) and for referenced `BoxCollider`/`Collider`/`Rigidbody` classes, so `GAME_2D` + `DOTWEEN_NOPHYSICS` doesn't strip it. Terrain and VFX modules are included in 2D builds too. The cost is a few MB of initial load; it matters only when a game nears the 20 MB mobile limit. **Fix:** find which assets or assemblies reference the 3D, Terrain and VFX classes (Build Report `strippingInfo`, Project Auditor) before deciding to drop UI Toolkit or those references.
