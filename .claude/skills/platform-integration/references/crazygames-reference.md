# CrazyGames implementation reference

CrazySDK version in the project: `CrazySDK.Version = "5.31.0"` (`Assets/CrazySDK/Scripts/CrazySDK.cs`).
The JS bridge is `Assets/Plugins/crazySDK.jslib`, which loads
`https://sdk.crazygames.com/crazygames-sdk-v3.js` at runtime.

## Project services -> SDK calls

| Project class | Interface | SDK calls used | Notes |
|---|---|---|---|
| `CrazyGamesPlatform` | `IPlatform` | `CrazySDK.IsAvailable`, `IsInitialized`, `Init(Action)` | Wraps `Init` in a `TaskCompletionSource<bool>`; registers real services only when available+initialized, Null services otherwise. |
| `CrazyGamesAdsService` | `IAdsService` | `CrazySDK.Ad.RequestAd(CrazyAdType, started, error, finished)`, `Ad.PrefetchAd`, `Ad.HasAdblock` | Rewarded -> `CrazyAdType.Rewarded`, interstitial -> `CrazyAdType.Midgame`. Publishes Ad/Gameplay events, answers `AdResult` (`NotAvailable` when the SDK is not ready, `InProgress` while its own request runs). Wrapped by `AdPolicyService` in `PlatformManager`. |
| `CrazyGamesBannerController` | (MonoBehaviour singleton) | `CrazySDK.Banner.RefreshBanners()` | Component on the project prefab variant `Assets/_Project/Prefabs/Ads/CrazyBanner.prefab` (used in `01_MainMenu`, anchored bottom-center), not on the vendor prefab. Deactivates itself in `Awake`; `Show/Hide` = `SetActive`; `Refresh()` requests the ad; `ShowBanner()` calls both. |
| `CrazyGamesDataStorage` | `ISaveStorage` | `CrazySDK.Data.HasKey/SetString/GetString/DeleteKey` | Throws `InvalidOperationException` if SDK not ready. |
| `CrazyGamesGameService` | `IGameService` | `CrazySDK.Game.GameplayStart/GameplayStop/HappyTime`, `Game.Settings.muteAudio`, `User.SystemInfo.locale` (`Language`, null in the Editor), `Game.AddSettingsChangeListener` | Every SDK call checks `IsAvailable && IsInitialized` and is wrapped in try/catch. Publishes `PlatformAudioMuteChangedEvent` when `muteAudio` changes (public `OnSettingsChanged(GameSettings)` simulates the portal in Editor tests). `GameReady()` is a no-op (SDK 5.31 has no such API). Game code reaches it through `GameplayStateManager`. |
| `CrazyGamesUserService` | `IUserService` | `CrazySDK.User.IsUserAccountAvailable`, `GetUser`, `ShowAuthPrompt`, `ShowAccountLinkPrompt`, `GetUserToken`, `AddAuthListener` | Maps `PortalUser` -> `PlatformUser(__dangerousUserId, username, profilePictureUrl)`; `Id` is informational, identify players on a server with the token. Keeps `CurrentUser`; registers `AddAuthListener` lazily on the first `UserChanged` subscriber (CrazyGames QA reports whether the game listens for sign-ins). |
| `CrazyGamesAuthenticationService` | `IAuthenticationService` | via `CrazyGamesUserService` | `IsSignedIn` = `CurrentUser != null`; `SignIn` -> `ShowLogin`; `SignOut` only logs (the portal handles sign-out). Registered only when `IsUserAccountAvailable`; the platform caches the user once at init (`GetCurrentUser(null)`). |
| `CrazyGamesLeaderboardService` | `ILeaderboardService` | `CrazySDK.User.SubmitScore(encryptedScore, score)` | Encrypts the score with AES-CTR (own port of the SDK sample, no dependency on the SDK Demo folder) using `PlatformConfig.LeaderboardEncryptionKey` (base64, from the developer portal). One leaderboard per game: the leaderboard ID is ignored; `ShowLeaderboard` is a no-op. `CreateLeaderboardService` registers `NullLeaderboardService` when `LeaderboardEnabled` is off or the key is empty or invalid, so `PlatformManager` clears `SupportsLeaderboard`. |

Analytics, Achievement, Review, Share -> Null services.

## SDK behaviour worth knowing (verified in SDK source)

### `CrazySDK` (Scripts/CrazySDK.cs)
- `IsAvailable`: `true` in Editor; on WebGL `true` for localhost/127.0.0.1/::1 and hosts ending in
  `crazygames.com` / `dev-crazygames.be`; otherwise `false`.
- `Init(callback)`: throws on non-Editor, non-WebGL players. In Editor it sets `IsInitialized`
  immediately and invokes the callback synchronously. On WebGL the callback fires from
  `JSLibCallback_Init` after the remote SDK's `init()` promise resolves; there is no error
  callback, so a failed script load means the callback never runs.
- Calling `Init` multiple times is safe (callbacks are queued).
- `InitAsync()` exists (wraps `Init` via `CrazyTaskUtil.FromCallback`).
- All modules are MonoBehaviours on a `DontDestroyOnLoad` GameObject named `CrazySDKSingleton`,
  created lazily by any module getter. JS callbacks target it via `SendMessage`.
- `WrapSDKAction/WrapSDKFunc` throw if `!IsInitialized` - most module calls throw before init.
- A `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` resets statics (supports disabled domain
  reload) and runs `SiteLock.Check()` on WebGL.

### `AdModule` (Scripts/Modules/AdModule.cs)
- `RequestAd` throws if not initialized.
- If a request is already in progress it logs and returns **without calling any callback**.
- Before requesting: saves and sets `Time.timeScale = 0` when `Settings.pauseGameDuringAd`,
  sets `AudioListener.volume = 0`, forces `Application.runInBackground = true`. `CleanupAd` restores
  all three on finish or error. The stored time scale is 0 (PauseManager paused for the request first); PauseManager sets its own value after the request and in every LateUpdate.
- Editor: simulates a 3-second ad with a `CrazyAdPreview` overlay unless
  `disableAdPreviews`; `alwaysThrowAdError` forces the error path.
- `PrefetchAd(CrazyAdType)` exists; `HasAdblock(Action<bool>)` / `HasAdblockAsync()` /
  `AdblockStatus` report adblock detection (wrapped by `IAdsService.PrefetchAd(AdType)` /
  `HasAdblock(Action<bool>)`).

### `GameModule` (Scripts/Modules/GameModule.cs)
- `GameplayStart()`, `GameplayStop()`, `HappyTime()`.
- `Settings` (`GameSettings { disableChat, muteAudio }`) and
  `AddSettingsChangeListener/RemoveSettingsChangeListener` - the portal can ask the game to mute
  (surfaced as `IGameService.IsAudioMutedByPlatform` + `PlatformAudioMuteChangedEvent`, applied by `AudioManager`).
- Also invite/room APIs (`InviteLink`, `ShowInviteButton`, `UpdateRoom`, `LeftRoom`,
  `SetGameContext`, `AddJoinRoomListener`) - not wrapped by the project.

### `DataModule` (Scripts/Modules/DataModule.cs)
- WebGL: `DataSetItemSDK/DataGetItemSDK/...` (CrazyGames cloud data). Editor: `PlayerPrefs`.
- `SetString(key, null)` deletes the key.

### `UserModule`
- `IsUserAccountAvailable`, `GetUser`, `ShowAuthPrompt`, `ShowAccountLinkPrompt`,
  `GetUserToken`, `AddAuthListener`, `SyncUnityGameData()` (pushes PlayerPrefs to CrazyGames
  automatic progress save), `SubmitScore`, `SystemInfo`, `ListFriends`; most have `*Async` twins.
- Editor responses are driven by `CrazyGamesSettings` (`getUserResponse`, `authPromptResponse`, ...).

### `BannerModule` / `CrazyBanner`
- `CrazyBanner` registers itself in `Awake`; `RefreshBanners()` sends every *active*
  (`activeInHierarchy`) banner's size/position to the JS SDK. Sizes: 728x90, 300x250, 320x50,
  320x100, 468x60. In Editor, refresh just recolors the placeholder.

## Known defects

Check `.claude/skills/unity-code-review/references/known-issues.md` for currently known defects in the CrazyGames integration (platform init, capabilities, ads, banner, missing SDK wrappers). Do not copy them into a new platform.
