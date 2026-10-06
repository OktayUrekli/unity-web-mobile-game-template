---
name: platform-integration
description: The platform layer (Assets/_Core/Platform, SDK bridges in Assets/_Platforms) - adding a platform (Yandex Games, Google Play, Poki...) or a service/capability (ads, cloud save, login, leaderboards, achievements, analytics, review, share), and how CrazyGames maps onto CrazySDK. Use whenever a task mentions a game portal or SDK, PlatformManager, PlatformFactory, IPlatform, Capabilities, a Null service, ads, cloud save, login or leaderboards. Takes precedence over the plugin skills levelplay-unity-integration and build-live-game; LevelPlay and Unity Gaming Services are not added.
---

# Platform integration

Game code talks only to `PlatformManager.Instance.<Service>` and checks `Capabilities`. If an SDK type leaks outside `Assets/_Platforms/<Name>/`, switching platforms stops being a config change. `CrazyGamesPlatform` is the canonical implementation; read it before writing a new one. Known defects in this layer are in `unity-code-review/references/known-issues.md`; don't copy them.

Layout: `_Core/Platform/Core/` (`IPlatform`, `PlatformCapabilities`, `PlatformFactory`, `PlatformManager`), `_Core/Platform/Platforms/NullPlatform.cs`, `_Core/Platform/Services/<Name>/I<Name>Service.cs` + `Null/Null<Name>Service.cs` for Game, Ads, Save (`ISaveStorage`), User, Analytics, Authentication, Leaderboard, Achievement, Review, Share, Purchase. Bridges: `Assets/_Platforms/<Name>/Core|Services|Testing/` (namespace `_Platforms.<Name>.*`) in Assembly-CSharp, which auto-references SDK assemblies; `_Core` grants Assembly-CSharp its `internal` `Set*Service`/capability setters.

## Contract

1. `Bootstrapper` awaits `PlatformManager.InitializeAsync()` before creating the other managers.
2. Each bridge calls `PlatformFactory.Register(type, () => new <Name>Platform())` from a `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` (see `CrazyGamesPlatformRegistration`); unregistered types get `NullPlatform`. `PlatformFactory.Create(type)` → `ConfigureCapabilities` (sync, before SDK init) → `await InitializeAsync(manager)` → missing services get Null implementations and their flags are cleared. If the platform throws, everything falls back to `NullPlatform` and `State = Failed`.
3. When `InitializeAsync` returns, **all eleven services are registered** (on success, SDK failure and timeout alike). Prefer registering fallbacks over throwing.
4. `InitializeAsync` always completes: every awaited SDK callback has a timeout (`PlatformConfig.SdkInitTimeoutSeconds`), or bootstrap hangs on the splash scene. `CrazySDK.Init` never calls back on failure.

## Adding a platform

Get the real SDK plugin into the project first and read its API; never guess vendor method names, and ask the user which plugin/version if it's missing. Then `Assets/_Platforms/<Name>/Core/<Name>Platform.cs` + `Services/` + a registration hook, register all eleven services, set every capability flag explicitly to match what's registered, wrap every bridge file in its `PLATFORM_<NAME>` define (registration included, so the type falls back to `NullPlatform`; a MonoBehaviour that scenes reference stays compiled and gates only its SDK calls, like `CrazyGamesBannerController`), add the define to `PlatformSetup.Defines` and a menu item, then select it with `Tools/Template/Platform/...` (sets `PlatformConfig.Platform`, the define and the WebGL template; `BuildValidator` fails the build when they disagree). Name the bridge folder after the `PlatformType` (`Assets/_Platforms/YandexGames`): native/precompiled plug-ins under it, and those listed in `PlatformBuildAssets.VendorPlugins`, are linked only while that platform's define is set. A portal that needs its SDK script in the page gets `Assets/WebGLTemplates/<PlatformType>/`; the tool selects it (`PROJECT:<PlatformType>`), otherwise Unity's Default. SDK code itself is never edited. Gate the SDK too, or it ships in every build: `Assets/CrazySDK` compiles only under `PLATFORM_CRAZYGAMES` through template-owned asmdefs (`CrazyGames.SDK`, `.SDK.Editor`, `.SDK.Demo`; the SDK's own `CrazyGamesDefineManager` stays unconstrained), because its SiteLock crashes WebGL builds on any other domain. Re-importing the SDK drops those asmdefs; `BuildValidator` then blocks other portals' WebGL builds. Unity 6000.3 ignores define constraints on native plug-ins, so `PlatformBuildAssets` leaves `crazySDK.jslib` out with `PluginImporter.SetIncludeInBuildDelegate` while `PLATFORM_CRAZYGAMES` isn't set (the constraint in its `.meta` only records intent). Selecting another platform also removes the `CrazySDK/*` menus. Skeleton: `references/new-platform-template.md`.

## Fallbacks

- Null services honour every contract: callbacks still fire (`AdResult.NotAvailable`, `null` user, `false`). A swallowed callback turns "unsupported" into "hangs".
- Storage is the exception: the fallback is `PlayerPrefsSaveStorage`, because `NullSaveStorage` silently drops progress. `SupportsSave` stays false (it means platform/cloud save). Async backends also implement `IAsyncSaveStorage`.
- Decide real vs fallback once at registration; real services still check SDK readiness per call.
- `CrazySDK.IsAvailable` is true in the Editor (simulated), on localhost and on CrazyGames domains, false elsewhere and on non-WebGL players. Editor ad behaviour: `Assets/CrazySDK/Resources/CrazyGamesSettings.asset`.

## Ads

`PlatformManager` wraps every ads service in `AdPolicyService` (in-flight guard → `InProgress`, interstitial cooldown → `Cooldown`, `AdRequestedEvent`/`AdRequestCompletedEvent`, exactly-once callback, and a watchdog → `TimedOut` when the ad does not start within `PlatformConfig.AdStartTimeoutSeconds` or finish within `AdPlayTimeoutSeconds` after `AdStartedEvent`; late results are ignored). `AdPolicyService.IsAdShowing` is the policy's own flag, not the platform's. Platform services don't re-implement that, but they do publish the events around the ad itself:

| Moment | Publish, in order |
|---|---|
| Ad started | `AdStartedEvent` |
| Finished | `AdFinishedEvent(true)`, callback `Completed` |
| Failed after start | `AdFinishedEvent(false)`, callback `Failed` |
| Failed before start / SDK not ready | callback `Failed` / `NotAvailable` only |

Publish `AdStartedEvent` when the ad really starts, or the watchdog cuts ads that load slower than the start timeout. Keep an own `_isAdShowing`: CrazySDK silently ignores a second request (no callback). Pausing is not the bridge's job: `PauseManager` pauses for the whole request and owns `Time.timeScale` (it overwrites whatever an SDK writes). On `AdRequestCompletedEvent` with `TimedOut`, undo the other things the SDK changed for the ad (CrazySDK: `AudioListener.volume`, `runInBackground`, restored only in its own callbacks) and don't publish `AdFinishedEvent` again; the policy already did for a started ad. CrazyGames interstitial = `CrazyAdType.Midgame`.

Banners: `CrazyGamesBannerController` on `Assets/_Project/Prefabs/Ads/CrazyBanner.prefab` (in `01_MainMenu`) starts inactive; `ShowBanner()` activates it **and** calls `Refresh()` (`CrazySDK.Banner.RefreshBanners()`), since activation alone requests nothing. Banners stay out of gameplay.

## Adding a service

New capability: interface (no SDK types, `Action<T>` callbacks) + Null service + `PlatformManager` property and `internal Set<Name>Service` + `Supports<Name>` flag + registration in **every** `IPlatform` (grep `: IPlatform`). A forgotten registration compiles and leaves the service null. Checklist: `references/new-service-checklist.md`.

## Other rules

- Convert SDK data at the boundary (`PortalUser` → `PlatformUser`); server identity uses `GetUserToken`, not `PlatformUser.Id`.
- Platform mute requests publish `PlatformAudioMuteChangedEvent`; `AudioManager` applies it. Pause requests (Yandex game_api_pause) set `IGameService.IsPausedByPlatform` and publish `PlatformPauseChangedEvent`: `AppLifecycleManager` turns it into `ApplicationPauseChangedEvent` (pause menu) and `AudioManager` goes silent.
- `IGameService.Language` is the portal's language ("ru", "en-US"; null when unknown). `LocalizationManager` uses it at start after the saved choice and before the device language; return it from the SDK's environment/system info, cached at init.
- Purchases (`IPurchaseService`, `SupportsPurchases`): deliver only on `PurchaseResult.Completed` or from `GetOwnedPurchases` at start, then `Consume` consumables. Pause gameplay around the platform's purchase dialog like an ad. CrazyGames has none (it sells through Xsolla, not bridged).
- The leaderboard is off unless `PlatformConfig.LeaderboardEnabled` (default off); its key `LeaderboardEncryptionKey` lives there too. Disabled, empty or invalid means the Null service.
- Log with a `"<Platform>: "` prefix.
- CrazyGames service details: `references/crazygames-reference.md`.

## Verify

Play from `00_Bootstrap` (expect `Platform initialized successfully: <Type>`), use the harnesses in `99_Test` (`CrazyGamesAdsTest`, `CrazyGamesUserTest`, `SaveTest`, `GameplayPauseTest`), tick `alwaysThrowAdError` for the failure path, and set `PlatformConfig` to an unimplemented type to check that the game still runs and saves.
