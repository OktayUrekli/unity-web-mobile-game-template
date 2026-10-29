---
name: platform-integration
description: The platform layer (Assets/_Core/Platform, SDK bridges in Assets/_Platforms) and how CrazyGames maps onto CrazySDK - adding a portal, store or service (ads, cloud save, login, leaderboards, purchases). Use for any portal SDK, PlatformManager, Capabilities or Null service work; takes precedence over the plugin skills levelplay-unity-integration, build-live-game and implement-in-app-purchases.
---

# Platform integration

Game code talks only to `PlatformManager.Instance.<Service>` and checks `Capabilities`. If an SDK type leaks outside `Assets/_Platforms/<Name>/`, switching platforms stops being a config change. `CrazyGamesPlatform` (Unity SDK) and `YandexGamesPlatform` (own `.jslib` over the JS SDK) are the reference implementations; read the closer one before writing a new one. Details: `references/crazygames.md`, `references/yandexgames.md`.

Layout: `_Core/Platform/Core/` (`IPlatform`, `PlatformCapabilities`, `PlatformFactory`, `PlatformManager`), `_Core/Platform/Platforms/NullPlatform.cs`, and per service `_Core/Platform/Services/<Name>/I<Name>Service.cs` + `Null/Null<Name>Service.cs`. Bridges live in `Assets/_Platforms/<PlatformType>/Core|Services|Testing/` (namespace `_Platforms.<Name>.*`) in Assembly-CSharp, which sees SDK assemblies; `_Core` grants Assembly-CSharp its `internal` `Set*Service` and capability setters.

## Contract

1. `Bootstrapper` awaits `PlatformManager.InitializeAsync()` before creating the other managers.
2. A bridge registers with `PlatformFactory.Register(type, () => new <Name>Platform())` from a `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` hook; an unregistered type gets `NullPlatform`. If the platform throws, everything falls back to `NullPlatform` and `State = Failed`.
3. When `InitializeAsync` returns, every service is registered, on success, SDK failure and timeout alike; missing ones get Null implementations and their capability flags are cleared. Prefer registering a fallback to throwing.
4. `InitializeAsync` always completes: every awaited SDK callback has a timeout (`PlatformConfig.SdkInitTimeoutSeconds`), or the boot hangs.

## Selecting a platform

`Tools/Template/Platform/...` (`Assets/_Core/Editor/PlatformSetup.cs`) sets `PlatformConfig.Platform`, one `PLATFORM_X` define on WebGL/Android/iOS/Standalone and the WebGL template (`Assets/WebGLTemplates/<PlatformType>` when present). Never edit these by hand. `BuildValidator` fails a build when they disagree.

- Bridges compile only under their define. Plug-ins under `Assets/_Platforms/<PlatformType>/` or listed in `PlatformBuildAssets.VendorPlugins` link only under it.
- CrazySDK itself compiles only under `PLATFORM_CRAZYGAMES` through the template's asmdefs in `Assets/CrazySDK` (`CrazyGames.SDK`, `.SDK.Editor`, `.SDK.Demo`), because its SiteLock crashes WebGL builds on other portals. Unity 6000.3 ignores define constraints on native plug-ins, so `PlatformBuildAssets` leaves `crazySDK.jslib` out of other builds. Re-importing the SDK drops those asmdefs; restore them.
- Android needs `com.unity.modules.androidjni` (Localization uses it).

## Adding a platform

1. Get the real SDK into the project and read its API; never guess vendor method names. Ask the user which plugin and version if it is missing.
2. `Assets/_Platforms/<PlatformType>/Core/<Name>Platform.cs`, `Services/`, a registration hook. Register every service and set every capability flag explicitly. Skeleton: `references/new-platform-template.md`.
3. Wrap every bridge file, registration included, in `#if PLATFORM_<NAME>`. A MonoBehaviour that scenes reference stays compiled and gates only its SDK calls (like `CrazyGamesBannerController`).
4. Add the define to `PlatformSetup.Defines` and a menu item, then select the platform with the menu.
5. A portal that needs its SDK script in the page gets `Assets/WebGLTemplates/<PlatformType>/`.
6. Gate the SDK's own code too, or it ships in every build (see CrazySDK above). Never edit SDK code.

## Adding a service

Interface (no SDK types, `Action<T>` callbacks) + Null service + `PlatformManager` property and `internal Set<Name>Service` + `Supports<Name>` flag + registration on every `IPlatform` (grep `: IPlatform`). A forgotten registration compiles and leaves the service null. Checklist: `references/new-service-checklist.md`.

## Fallbacks

- Null services keep every contract: callbacks still fire (`AdResult.NotAvailable`, `null` user, `false`). A swallowed callback turns "unsupported" into "hangs".
- Storage falls back to `PlayerPrefsSaveStorage`, never `NullSaveStorage` (it drops progress silently). `SupportsSave` stays false; it means platform or cloud save.
- Decide real vs fallback once at registration; real services still check SDK readiness per call.

## Ads

`PlatformManager` wraps every ads service in `AdPolicyService`: one request at a time (`InProgress`), interstitial cooldown (`Cooldown`), `AdRequestedEvent`/`AdRequestCompletedEvent`, an exactly-once callback, and a watchdog (`TimedOut`) when the ad doesn't start within `AdStartTimeoutSeconds` or finish within `AdPlayTimeoutSeconds`. A bridge doesn't repeat that; it publishes the events around the ad itself:

| Moment | Publish, in order |
|---|---|
| Ad started | `AdStartedEvent` |
| Finished | `AdFinishedEvent(true)`, callback `Completed` |
| Failed after start | `AdFinishedEvent(false)`, callback `Failed` |
| Failed before start / SDK not ready | callback `Failed` / `NotAvailable` only |

- Publish `AdStartedEvent` when the ad really starts, or the watchdog cuts slow-loading ads.
- Keep an own in-flight flag: some SDKs (CrazySDK) silently ignore a second request.
- Pausing is not the bridge's job; `PauseManager` handles the whole request. On `TimedOut`, undo whatever the SDK changed for the ad and don't publish `AdFinishedEvent` again.
- Banners stay out of gameplay.

## Other rules

- Convert SDK data at the boundary (`PortalUser` → `PlatformUser`). Server identity uses `GetUserToken`, never `PlatformUser.Id`.
- Platform mute requests publish `PlatformAudioMuteChangedEvent` (AudioManager applies it). Platform pause requests set `IGameService.IsPausedByPlatform` and publish `PlatformPauseChangedEvent` (AppLifecycleManager turns it into the app pause).
- `IGameService.Language` is the portal's language, cached at init; null when unknown.
- Purchases (`IPurchaseService`): deliver only on `PurchaseResult.Completed` or from `GetOwnedPurchases` at start, then `Consume` consumables. Pause around the purchase dialog like an ad. A store or payment SDK goes inside its platform's bridge, never in game code.
- The leaderboard is off unless `PlatformConfig.LeaderboardEnabled`; disabled, empty or invalid means the Null service.
- Log with a `"<Platform>: "` prefix.

## Verify

Play from `00_Bootstrap` (expect `Platform initialized successfully: <Type>`), try the failure path (CrazyGames: `alwaysThrowAdError`), and set `PlatformConfig` to an unimplemented type to check that the game still runs and saves. Manual harnesses that use SDK types go in `Assets/_Platforms/<PlatformType>/Testing/` inside a test scene (`unity-verify`).
