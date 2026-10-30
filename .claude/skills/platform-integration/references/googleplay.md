# Google Play

How the project maps onto the Google Mobile Ads (GMA) Unity plugin 11.6.0 and Google's User Messaging Platform (UMP), from the plugin source (github.com/googleads/googleads-mobile-unity, tag v11.6.0) and developers.google.com/admob/unity. Only ads are bridged. AdMob policies and Play Console requirements live at support.google.com/admob and support.google.com/googleplay/android-developer: point the user there instead of guessing.

## Packages

- `com.google.ads.mobile` comes from the OpenUPM scoped registry (`Packages/manifest.json`, scope `com.google`). It depends on `com.unity.external-dependency-manager` (Unity registry), which resolves the Android libraries (`play-services-ads`, `user-messaging-platform`) through Gradle.
- The package compiles on every platform: its managed DLLs are small and start nothing on their own, so they are not gated (the exception to SKILL.md step 6). Only the bridge is gated by `PLATFORM_GOOGLEPLAY`, and the Android plug-ins link only into Android builds.
- `Assets/GoogleMobileAds/` holds the plugin's settings asset (`Resources/GoogleMobileAdsSettings.asset`, edited with Assets > Google Mobile Ads > Settings) and the plugin's `link.xml`, which the plugin would otherwise copy there on the first build.
- The first Android resolve or build can write Gradle templates under `Assets/Plugins/Android/`, Android resolver files under `ProjectSettings/` and `Packages/packages-lock.json`. Commit what it creates.

## Project services

| Project class | Interface | SDK calls | Notes |
|---|---|---|---|
| `GooglePlayPlatform` | `IPlatform` | none | Starts `GooglePlayMobileAds` and registers the ads and privacy services. Every other service is Null; storage is `PlayerPrefsSaveStorage`. `InitializeAsync` never waits on consent. |
| `GooglePlayMobileAds` | static | `ConsentInformation.Update`, `ConsentForm.LoadAndShowConsentFormIfRequired`, `CanRequestAds`, `MobileAds.Initialize`, `ConsentForm.ShowPrivacyOptionsForm` | Consent, then SDK start-up, in the background. `IsReady` only after `MobileAds.Initialize` calls back. One consent form at a time: the plugin keeps a single dismiss callback. |
| `GooglePlayAdsService` | `IAdsService` | `InterstitialAd`/`RewardedAd` `Load`/`Show`/`CanShowAd`/`Destroy`, `BannerView` | Both fullscreen formats are loaded when the SDK is ready and again after each close. An interstitial that isn't loaded answers `Failed` at once. A rewarded ad that isn't loaded loads and then shows, bounded by `AdStartTimeoutSeconds`. A rewarded ad answers `Completed` only after `OnUserEarnedReward`, which may arrive up to a second after the close. `NotAvailable` until `IsReady`. No adblock detection. |
| `GooglePlayPrivacyService` | `IPrivacyService` | `ShowPrivacyOptionsForm` | `IsPrivacyOptionsRequired` is UMP's `PrivacyOptionsRequirementStatus.Required`, known once the start-up consent flow ends. The game shows the entry with `PrivacyOptionsButton` (`_Core/UI/Components`) on a settings-screen Button. |

## IDs

- AdMob app ID: GMA settings asset only, never in code. The template ships Google's sample app ID, and GMA fails an Android build when it is empty.
- Ad unit IDs: `PlatformConfig.AdMobInterstitialAdUnitId`, `AdMobRewardedAdUnitId` and `AdMobBannerAdUnitId`. The Editor and Development builds always use Google's test ad units (`GooglePlayAdUnits`), so testing never touches real ads. A release build uses the configured IDs, and an empty one falls back to the test unit with a warning.
- `BuildValidator` blocks an Android build with an empty or malformed app ID (the SDK stops the app at launch), and a release build whose ad unit belongs to another AdMob account than the app ID or whose ad unit field holds an app ID. It warns when a release still uses Google's sample IDs.

## SDK behaviour the bridge relies on

- GMA raises ad and consent callbacks on background threads on Android, and the reward and the close on separate ones, so their order is not fixed. Every callback goes through `GooglePlayMobileAds.RunOnMainThread`, which queues it with `MobileAdsEventExecutor.ExecuteInUpdate` (inline when already on the main thread, as the Editor placeholders are). Never touch Unity objects or `EventBus` from a raw GMA callback.
- Android pauses the Unity player while a fullscreen ad is on screen (`OnApplicationPause(true)`), so the queued close callback runs on the first frame after it. `AdPolicyService`'s play timeout must stay longer than any ad.
- Ads are single-use. The bridge destroys a shown ad and loads the next one. A loaded ad expires after an hour, which the plugin's `CanShowAd` doesn't track, so the bridge replaces it after 55 minutes.
- UMP runs on every start. In a release build ads start only when `CanRequestAds()` is true, which it is after any choice on the GDPR form; a player who declines gets limited or no ads (`Failed`). `NotAvailable` means the consent flow didn't finish (no published GDPR message, form failed to load). A Development build whose consent update fails (no AdMob privacy message, offline) still starts the SDK with test ads.
- The Editor uses GMA's placeholder ads, which write `Time.timeScale` (0 on show, 1 on close). The bridge answers the close inline, so `PauseManager` has already resumed; it warns only when another pause is held then. The Editor also always reports the privacy options as required.
- The banner is a native view anchored at the bottom over the whole game, outside the Canvas. A game that shows it leaves that strip free. Hiding destroys it; showing it again loads a new one.

## AdMob account (user)

Before a release: create the app in AdMob, put its app ID in the GMA settings and its ad unit IDs in `PlatformConfig`, and publish a GDPR message under Privacy & messaging, or players in the EEA and UK get no ads. Play Console: declare ads and the advertising ID in the Data safety form. Never click your own live ads; test on Development builds.

## Local testing

- Editor: select Google Play with `Tools/Template/Platform` and play from `00_Bootstrap`. Expect `GooglePlay: Google Mobile Ads initialized.` and placeholder ads with real callbacks.
- Device: build `Android - Development` (APK). Google's test ads show a "Test Ad" label. To check the consent form outside the EEA, UMP needs debug geography (`ConsentDebugSettings`), which the bridge doesn't set.
