# CrazyGames runtime rules (SDK 5.31.0)

Facts below are from `Assets/CrazySDK/Scripts/**` and `Assets/Plugins/crazySDK.jslib`. Portal
policies that are not visible in code (ad frequency, QA checklists) live at docs.crazygames.com -
check there instead of guessing. Check `.claude/skills/unity-code-review/references/known-issues.md`
for currently known defects in the project's CrazyGames wrappers.

## Contents
- Availability matrix
- Game lifecycle
- Ads
- Banners
- Audio / settings
- User / leaderboard
- Data
- Debug logging

## Availability matrix

| Where | `CrazySDK.IsAvailable` | What the project registers |
|---|---|---|
| Unity Editor | true (SDK simulates) | real CrazyGames services; Data -> PlayerPrefs; ads -> 3 s preview |
| WebGL on localhost / 127.0.0.1 / ::1 | true | real services, real JS SDK loaded from sdk.crazygames.com |
| WebGL on *.crazygames.com / *.dev-crazygames.be | true | real services |
| WebGL on any other domain | false | fallback services (no ads; storage depends on the fallback) |
| Non-WebGL player | false | fallback services (`Init` would throw) |

`CrazyGamesSettings.whitelistedDomains` controls SiteLock (which domains the build may run on); it
does not make `IsAvailable` true.

## Game lifecycle

`CrazySDK.Game` (wrapped by `IGameService`):
- `GameplayStart()` / `GameplayStop()` / `HappyTime()` - throw if the SDK isn't initialized
  (`WrapSDKAction`). `CrazyGamesGameService` checks `IsAvailable && IsInitialized` before each call
  and wraps it in try/catch.
- `IGameService.GameReady()` is a no-op here: no loading-start/stop API exists in this version.

Game code never calls `GameplayStart/Stop` directly: `GameplayStateManager` (`_Core.Gameplay`,
created by the Bootstrapper) keeps them paired, ignores repeated calls and ends gameplay itself when
the active scene changes. Suggested integration (game-side):

```csharp
using _Core.Gameplay;
using UnityEngine;

namespace _Project.Gameplay
{
    /// <summary>
    /// Reports gameplay start to the platform when a level begins.
    /// </summary>
    public class LevelPlatformLifecycle : MonoBehaviour
    {
        private void Start()
        {
            // Start, not Awake: the scene change that loaded this level ends gameplay.
            GameplayStateManager.Instance.BeginGameplay();
        }
    }
}
```

Call `EndGameplay()` at game over and when leaving the level. Pause menus and app pauses are reported
as gameplayStop automatically (through `PauseManager`), and gameplayStart again on resume. Ads need no
calls - the SDK handles gameplay state around ads itself. Use `PlatformManager.Instance.Game.HappyTime()`
for rare celebratory moments (level completed, new best score).

## Ads

`CrazySDK.Ad.RequestAd(CrazyAdType.Midgame | Rewarded, started, error, finished)`:
- Throws if not initialized; returns silently (no callback) on non-WebGL/non-Editor, and when a
  request is already in progress.
- Before the request: stores and sets `Time.timeScale = 0` if `pauseGameDuringAd` (see
  `CrazyGamesSettings.asset`), `AudioListener.volume = 0`, `Application.runInBackground = true`.
  All restored on finish/error.
- Error can arrive without `started`.
- `PrefetchAd(type)` preloads; `HasAdblock(cb)` / `HasAdblockAsync()` / `AdblockStatus`.
- The jslib releases pointer lock when the adblock popup opens.

Project wrapper `CrazyGamesAdsService` publishes `AdStartedEvent` on start and `AdFinishedEvent(bool)`
when a started ad ends, then invokes the callback with an `AdResult`. `PlatformManager` wraps it in
`AdPolicyService`, which publishes `AdRequestedEvent`/`AdRequestCompletedEvent` around the whole
request (`PauseManager` pauses with `PauseSource.Ad`, `AudioManager` mutes, `AdBlockerOverlay` blocks
UI input), answers `InProgress` for overlapping requests and `Cooldown` for interstitials inside the
cooldown, and invokes the caller's callback exactly once.
Consequences for gameplay code:
- `PauseManager` owns `Time.timeScale`, with or without `pauseGameDuringAd`; it restores its own value
  when the SDK writes one (also after a late SDK answer to a timed-out request).
- Anything driven by unscaled time (`Time.unscaledDeltaTime`, `WaitForSecondsRealtime`, DOTween
  with `SetUpdate(true)`, UI animations) keeps running during ads unless it is a
  `GameplayPauseHandler`.
- Grant rewards only in the callback when `result == AdResult.Completed`; guard against double grants.

## Banners

- `CrazyBanner` (prefab `Assets/CrazySDK/Resources/CrazyBanner.prefab`) registers itself with
  `CrazySDK.Banner` in `Awake`. On WebGL its placeholder image is made transparent.
- `CrazySDK.Banner.RefreshBanners()` requests ads for every banner that is `activeInHierarchy`;
  sizes 728x90, 300x250, 320x50, 320x100, 468x60; position from the child `Banner` RectTransform.
- The project's `CrazyGamesBannerController` is a `Singleton` component on the project prefab variant
  `Assets/_Project/Prefabs/Ads/CrazyBanner.prefab` (used in `01_MainMenu`, anchored bottom-center), not on the vendor prefab; it deactivates itself in `Awake`;
  `Show()`/`Hide()` toggle the GameObject and `Refresh()` requests the ad - activating alone shows
  nothing, so `ShowBanner()` calls `Show()` then `Refresh()`. Access it with an explicit `!= null` check (Unity fake-null), never `?.`.

## Audio / settings

- `CrazySDK.Game.Settings` -> `GameSettings { disableChat, muteAudio }` (Editor returns defaults).
- `AddSettingsChangeListener(Action<GameSettings>)` fires when the portal changes settings.
  `CrazyGamesGameService` reads `muteAudio` at registration and on every change, exposes it as
  `IGameService.IsAudioMutedByPlatform` and publishes `PlatformAudioMuteChangedEvent`; `AudioManager`
  keeps both mixer groups at -80 dB while it is set. In the Editor the listener never fires - call the
  service's public `OnSettingsChanged(GameSettings)` to simulate it.

## User / leaderboard

- `CrazySDK.User.AddAuthListener` is registered by `CrazyGamesUserService` only once something
  subscribes to `IUserService.UserChanged` (CrazyGames QA reports whether the game listens).
  Signing out on the portal reloads the page, so there is no sign-out callback.
- Identify players on a game server with `GetUserToken`, never with `PlatformUser.Id`.
- `CrazySDK.User.SubmitScore(encryptedScore, score)` needs the score AES-encrypted with the key from
  the developer portal (`PlatformConfig.LeaderboardEncryptionKey`). Without a valid key the leaderboard
  is the Null service and `SupportsLeaderboard` is false. One leaderboard per game.

## Data

- `CrazySDK.Data.SetString/GetString/HasKey/DeleteKey/DeleteAll` (+ Int/Float). WebGL -> portal
  data API; Editor -> `PlayerPrefs`. The JS side logs and swallows errors; C# gets no failure signal.
- `CrazySDK.User.SyncUnityGameData()` pushes Unity `PlayerPrefs` to automatic progress save.

## Debug logging

SDK logs (`[CrazySDK] ...`) are on in the Editor (unless `disableSdkLogs`), in development builds,
or with `?sdk_debug=true` in the URL; always off on whitelisted domains.
