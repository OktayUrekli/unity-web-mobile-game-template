# Yandex Games

How the project maps onto the Yandex Games JS SDK v2, from the official docs (yandex.com/dev/games/doc/en/sdk/) and `@types/ysdk`. There is no Unity plugin: `Assets/_Platforms/YandexGames/Plugins/YandexGames.jslib` calls `YaGames` directly and `YandexGamesSdk.cs` wraps it. Moderation rules that aren't in code live at yandex.com/dev/games/doc/en/concepts/requirements: point the user there instead of guessing.

## Project services

| Project class | Interface | SDK calls | Notes |
|---|---|---|---|
| `YandexGamesPlatform` | `IPlatform` | `YaGames.init()`, `getPlayer()`, `player.getData()` | Init loads the player and all its data within `SdkInitTimeoutSeconds`; real services only after a successful init. |
| `YandexGamesGameService` | `IGameService` | `features.LoadingAPI.ready`, `features.GameplayAPI.start/stop`, `environment.i18n.lang`, `on('game_api_pause' / 'game_api_resume')` | Pause requests become `IsPausedByPlatform` + `PlatformPauseChangedEvent`, except a pause during the game's own ad request. |
| `YandexGamesAdsService` | `IAdsService` | `adv.showFullscreenAdv`, `adv.showRewardedVideo`, `adv.showBannerAdv/hideBannerAdv` | Fullscreen: `onClose(wasShown)` false = `Failed`. Rewarded: `Completed` only after `onRewarded`. No prefetch, no adblock detection. |
| `YandexGamesDataStorage` | `ISaveStorage` + `IAsyncSaveStorage` | `player.setData(wholeObject, false)` | Reads from the data loaded at init; writes of one frame go out as one `setData`. PlayerPrefs when the player didn't load. |
| `YandexGamesLeaderboardService` | `ILeaderboardService` | `isAvailableMethod('leaderboards.setScore')`, `leaderboards.setScore` | Null ID uses `PlatformConfig.DefaultLeaderboardId` (technical name). `ShowLeaderboard` is a no-op. |
| `YandexGamesReviewService` | `IReviewService` | `feedback.canReview`, `feedback.requestReview` | |

User, Authentication, Purchases, Analytics, Achievement and Share are Null. Sign-in (`auth.openAuthDialog`) isn't bridged because the cloud data then belongs to another player; payments (`ysdk.payments`) aren't bridged.

## Where the SDK is available

The YandexGames WebGL template loads `/sdk.js` (a relative path, required for uploads). Only Yandex and `@yandex-games/sdk-dev-proxy` serve it; anywhere else, the Editor included, `YandexGamesSdk.IsAvailable` is false and every service is a fallback.

## SDK behaviour the bridge relies on

- `game_api_pause`/`game_api_resume` fire around Yandex's ads (including the automatic start-up ad), purchase windows, hidden tabs and minimized windows. The SDK itself calls `GameplayAPI.stop()` on pause and `start()` on resume; the jslib calls `stop()` again after a resume when the game had reported gameplay as stopped meanwhile (pause menu).
- `LoadingAPI.ready()` is a moderation requirement; `Bootstrapper` sends it through `GameReady()` once the first scene is shown.
- The game must start in `i18n.lang` (requirement 2.14, checked per language on the debug panel). `LocalizationManager` does, unless the player picked a language.
- `setData`: 200 KB per player, 100 calls per 5 minutes; `flush` false queues the write. Replace versus merge is undocumented, so the bridge always sends every key.
- Leaderboards and reviews work only for signed-in players; `setScore` at most once per second, never negative.
- Yandex decides how often fullscreen ads show; a call too soon closes with `wasShown` false. Rewarded ads have no frequency limit. `PlatformConfig.InterstitialCooldownSeconds` still applies on top.

## Developer Console

- Sticky banner: Advertising tab, "Use the API to display a sticky-banner", or Yandex shows it the whole session.
- Leaderboard: create it, then put its technical name in `PlatformConfig.DefaultLeaderboardId` and turn on `LeaderboardEnabled`.
- Upload: a ZIP whose root holds `index.html` (the build folder's contents), at most 100 MB uncompressed, no spaces or Cyrillic in names. Yandex hosting serves `.br` files with `Content-Encoding: br` (checked on a live game), so Brotli needs no Decompression Fallback there.
- Name trap: an Addressables bundle takes its group's name by default, and Localization names its groups after each Locale (`Русский`). The string-table groups therefore use Bundle Naming "Use Hash of Filename"; a group added later (a new Locale) needs the same. `BuildValidator` logs an error listing offending names after a Yandex build.

## Local testing

Build with a Development Web profile for this; a Release build's wasm link can take over an hour. `npx @yandex-games/sdk-dev-proxy -p <build folder> --dev-mode=true` serves the build at https://localhost:8080 with mocked SDK calls: placeholder ads with real callbacks, player data and leaderboards in localStorage. URL mocks: `?mocks={"isAuthorized":true}`. Without `--dev-mode` and with `--app-id=<draft id>` it opens the game inside a Yandex draft. The proxy serves files without `Content-Encoding`, so a Brotli build needs Decompression Fallback (or compression off) for this test. Its mocks don't send `game_api_pause`/`game_api_resume` around ads and accept `setScore` from guests, so those two paths need a draft on Yandex.
