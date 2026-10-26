# CrazyGames and CrazySDK

How the project maps onto CrazySDK, and SDK behaviour verified in its source (`Assets/CrazySDK/Scripts/**`, `Assets/Plugins/crazySDK.jslib`). The SDK version is `CrazySDK.Version` in `Assets/CrazySDK/Scripts/CrazySDK.cs`; re-check the facts below after an SDK update. Portal policies that aren't in code (ad frequency, QA rules) live at docs.crazygames.com: point the user there instead of guessing.

## Contents
- Project services
- Where the SDK is available
- SDK behaviour
- Game lifecycle
- Ads and banners
- Audio, user, leaderboard, data
- Debug logging

## Project services

| Project class | Interface | SDK calls | Notes |
|---|---|---|---|
| `CrazyGamesPlatform` | `IPlatform` | `IsAvailable`, `IsInitialized`, `Init(Action)` | Wraps `Init` in a `TaskCompletionSource<bool>` with a timeout; registers real services only when available and initialized, Null services otherwise. |
| `CrazyGamesAdsService` | `IAdsService` | `Ad.RequestAd(type, started, error, finished)`, `Ad.PrefetchAd`, `Ad.HasAdblock` | Rewarded → `CrazyAdType.Rewarded`, interstitial → `CrazyAdType.Midgame`. Publishes the ad start/finish events, answers `NotAvailable` when the SDK isn't ready and `InProgress` while its own request runs. |
| `CrazyGamesBannerController` | MonoBehaviour singleton | `Banner.RefreshBanners()` | On the project prefab `Assets/_Project/Prefabs/Ads/CrazyBanner.prefab` (in `01_MainMenu`, bottom-center), not on the vendor prefab. Inactive after `Awake`; `ShowBanner()` = `Show()` (activate) + `Refresh()` (request). Access it with `!= null`, never `?.`. |
| `CrazyGamesDataStorage` | `ISaveStorage` | `Data.HasKey/SetString/GetString/DeleteKey` | Throws `InvalidOperationException` if the SDK isn't ready (only registered after a successful init). |
| `CrazyGamesGameService` | `IGameService` | `Game.GameplayStart/GameplayStop/HappyTime`, `Game.Settings.muteAudio`, `Game.AddSettingsChangeListener`, `User.SystemInfo.locale` | Every call checks `IsAvailable && IsInitialized` inside try/catch. `GameReady()` is a no-op (no loading API in this SDK). `Language` is null in the Editor. |
| `CrazyGamesUserService` | `IUserService` | `User.IsUserAccountAvailable`, `GetUser`, `ShowAuthPrompt`, `ShowAccountLinkPrompt`, `GetUserToken`, `AddAuthListener` | `PortalUser` → `PlatformUser`. Registers `AddAuthListener` lazily on the first `UserChanged` subscriber (portal QA reports whether the game listens). |
| `CrazyGamesAuthenticationService` | `IAuthenticationService` | via the user service | Registered only when `IsUserAccountAvailable`. `SignOut` only logs; signing out on the portal reloads the page. |
| `CrazyGamesLeaderboardService` | `ILeaderboardService` | `User.SubmitScore(encryptedScore, score)` | AES-CTR encryption with `PlatformConfig.LeaderboardEncryptionKey` (base64, from the developer portal). One leaderboard per game; `ShowLeaderboard` is a no-op. Null service when disabled or the key is empty or invalid. |

Analytics, Achievement, Review, Share and Purchase → Null services. CrazyGames sells through Xsolla, which isn't bridged.

## Where the SDK is available

| Where | `CrazySDK.IsAvailable` | Services |
|---|---|---|
| Unity Editor | true (simulated) | real; Data → PlayerPrefs; ads → 3 s preview |
| WebGL on localhost / 127.0.0.1 / ::1 | true | real, JS SDK loaded from sdk.crazygames.com |
| WebGL on *.crazygames.com / *.dev-crazygames.be | true | real |
| WebGL on any other domain | false | fallbacks |
| Non-WebGL player | false | fallbacks (`Init` would throw) |

`CrazyGamesSettings.whitelistedDomains` controls SiteLock (where the build may run); it doesn't make `IsAvailable` true. Editor behaviour (ad errors, user responses) comes from `Assets/CrazySDK/Resources/CrazyGamesSettings.asset`. Selecting another platform removes the `CrazySDK/*` menus.

## SDK behaviour

- `Init(callback)`: in the Editor it initializes and calls back at once. On WebGL the callback fires after the remote SDK's `init()` resolves; there is no error callback, so a failed script load never calls back. Calling it again is safe.
- Most module calls throw before init (`WrapSDKAction/WrapSDKFunc`).
- All modules are MonoBehaviours on a `DontDestroyOnLoad` object `CrazySDKSingleton`, created lazily; JS calls back through `SendMessage`.
- A `BeforeSceneLoad` hook resets statics (disabled domain reload works) and runs `SiteLock.Check()` on WebGL.
- The game module also has invite and room APIs (`InviteLink`, `UpdateRoom`, `SetGameContext`, ...) that the project doesn't wrap.

## Game lifecycle

Game code never calls `GameplayStart/Stop`; `GameplayStateManager` keeps them paired and ends gameplay on scene change. The pause menu and app pauses are reported as gameplay stop automatically. Ads need no calls. Use `HappyTime()` for rare celebrations (level done, new best).

## Ads and banners

- `RequestAd` throws if not initialized, and returns **without any callback** when a request is already running. An error can arrive without `started`.
- Before a request the SDK stores and zeroes `Time.timeScale` (only with `pauseGameDuringAd`), zeroes `AudioListener.volume` and forces `Application.runInBackground = true`; it restores all three in its own callbacks only. `PauseManager` overrides the time scale either way. On a `TimedOut` request the bridge restores volume and `runInBackground` itself.
- Unscaled-time code (`unscaledDeltaTime`, `SetUpdate(true)` tweens, UI animations) keeps running during ads unless it is a `GameplayPauseHandler`.
- `PrefetchAd(type)` preloads; `HasAdblock(cb)` detects adblock. The jslib releases pointer lock when the adblock popup opens.
- `CrazyBanner` registers itself in `Awake`; `RefreshBanners()` requests ads for every `activeInHierarchy` banner (728x90, 300x250, 320x50, 320x100, 468x60; position from the child `Banner` RectTransform). Activating a banner alone shows nothing.

## Audio, user, leaderboard, data

- `Game.Settings` = `{ disableChat, muteAudio }`. The settings listener fires when the portal changes them; the project exposes `muteAudio` as `IGameService.IsAudioMutedByPlatform` and publishes `PlatformAudioMuteChangedEvent`. In the Editor the listener never fires: call the service's public `OnSettingsChanged(GameSettings)` to simulate it.
- Editor user responses come from `CrazyGamesSettings` (`getUserResponse`, `authPromptResponse`, ...).
- `Data` (`SetString/GetString/HasKey/DeleteKey/DeleteAll`) goes to the portal data API on WebGL and PlayerPrefs in the Editor. `SetString(key, null)` deletes. The JS side swallows errors; C# gets no failure signal. `User.SyncUnityGameData()` pushes PlayerPrefs to the portal's automatic progress save.

## Debug logging

SDK logs (`[CrazySDK] ...`) are on in the Editor (unless `disableSdkLogs`), in development builds or with `?sdk_debug=true` in the URL; always off on whitelisted domains.
