# New platform skeleton

Copy-ready structure for a new platform, modelled on `CrazyGamesPlatform`. Everything named
`<Name>SdkBridge` / `SomeVendorApi` is a **placeholder**: replace it with the real plugin API after
reading the plugin's source. Do not ship guessed SDK calls.

## Contents
- Files
- Platform class (capabilities, init with timeout, service registration)
- Ads service (event contract, in-flight guard)
- Storage service
- Registration

## Files

```
Assets/_Platforms/<PlatformType>/    (Assembly-CSharp, next to the SDK; folder = PlatformType name, e.g. YandexGames)
  Core/<Name>Platform.cs
  Core/<Name>PlatformRegistration.cs   (registers with PlatformFactory)
  Services/<Name>AdsService.cs
  Services/<Name>DataStorage.cs        (if the platform has cloud/player data)
  Services/<Name>GameService.cs        (if the platform has lifecycle signals)
  Services/<Name>UserService.cs        (if the platform has accounts)
  ... one file per supported interface
  Plugins/                             (the bridge's own .jslib/.aar; linked only under its define)
Assets/WebGLTemplates/<PlatformType>/  (WebGL portals that need their SDK script in index.html)
```

Plug-ins under the bridge folder and the template folder are picked up by name (`PlatformBuildAssets`);
SDK plug-ins that stay in a vendor folder go into `PlatformBuildAssets.VendorPlugins`.

## Platform class

```csharp
using System.Threading.Tasks;
using _Core.Platform.Core;
using _Platforms.YandexGames.Services;
using _Core.Platform.Services.Achievement.Null;
using _Core.Platform.Services.Ads.Null;
using _Core.Platform.Services.Analytics.Null;
using _Core.Platform.Services.Authentication.Null;
using _Core.Platform.Services.Game.Null;
using _Core.Platform.Services.Leaderboard.Null;
using _Core.Platform.Services.Review.Null;
using _Core.Save.Local;
using _Core.Platform.Services.Share.Null;
using _Core.Platform.Services.Purchase.Null;
using _Core.Platform.Services.User.Null;
using UnityEngine;

namespace _Platforms.YandexGames.Core
{
    /// <summary>
    /// Provides the Yandex Games platform implementation.
    /// </summary>
    public class YandexPlatform : IPlatform
    {
        // Generous: the SDK script is downloaded from the network on first load.
        private const float InitTimeoutSeconds = 15f;

        private bool _isSdkReady;

        public void ConfigureCapabilities(PlatformCapabilities capabilities)
        {
            // Set every flag explicitly so the platform's feature set is readable at a glance.
            capabilities.SupportsAds = true;
            capabilities.SupportsRewardedAds = true;
            capabilities.SupportsInterstitialAds = true;
            capabilities.SupportsBannerAds = false;

            capabilities.SupportsAnalytics = false;
            capabilities.SupportsAuthentication = false;
            capabilities.SupportsLeaderboard = false;
            capabilities.SupportsAchievements = false;
            capabilities.SupportsSave = true;
            capabilities.SupportsReview = false;
            capabilities.SupportsShare = false;
            capabilities.SupportsPurchases = false;
        }

        public async Task InitializeAsync(PlatformManager manager)
        {
            Debug.Log("Yandex: Initialization started.");

            if (YandexSdkBridge.IsAvailable)
            {
                var completion = new TaskCompletionSource<bool>();

                // TrySet* tolerates SDKs that invoke callbacks more than once.
                YandexSdkBridge.Init(
                    () => completion.TrySetResult(true),
                    error => completion.TrySetResult(false));

                bool completed = await WaitWithTimeoutAsync(completion.Task, InitTimeoutSeconds);
                _isSdkReady = completed && completion.Task.Result; // safe: task is completed

                if (!_isSdkReady)
                    Debug.LogWarning("Yandex: SDK init failed or timed out. Using fallback services.");
            }
            else
            {
                Debug.Log("Yandex: SDK is not available. Using fallback services.");
            }

            RegisterServices(manager);
        }

        private void RegisterServices(PlatformManager manager)
        {
            // Every service must be registered; game code never null-checks them.
            if (_isSdkReady)
            {
                manager.SetAdsService(new YandexAdsService());
                manager.SetStorageService(new YandexDataStorage());
            }
            else
            {
                manager.SetAdsService(new NullAdsService());
                // Never NullSaveStorage: it drops all saves silently.
                manager.SetStorageService(new PlayerPrefsSaveStorage());
            }

            manager.SetGameService(new NullGameService());
            manager.SetUserService(new NullUserService());
            manager.SetAnalyticsService(new NullAnalyticsService());
            manager.SetAuthenticationService(new NullAuthenticationService());
            manager.SetLeaderboardService(new NullLeaderboardService());
            manager.SetAchievementService(new NullAchievementService());
            manager.SetReviewService(new NullReviewService());
            manager.SetShareService(new NullShareService());
            manager.SetPurchaseService(new NullPurchaseService());
        }

        /// <summary>
        /// Awaits the task, or returns false if it does not complete within the timeout.
        /// Frame polling keeps this working on single-threaded WebGL.
        /// </summary>
        private static async Task<bool> WaitWithTimeoutAsync(Task task, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup > deadline)
                    return false;

                await Task.Yield();
            }

            return true;
        }
    }
}
```

Reading `.Result` is fine only after `IsCompleted` is true - it never blocks then. Anywhere else,
`.Result` / `.Wait()` would freeze a WebGL tab.

If a second platform needs `WaitWithTimeoutAsync`, move it into a shared static helper under
`_Core/Platform/Core/` instead of copying it.

## Ads service (event contract)

`PlatformManager` wraps this service in `AdPolicyService` (global in-flight guard, interstitial
cooldown, `AdRequestedEvent`/`AdRequestCompletedEvent`, exactly-once callback), so the platform
service only maps the SDK, publishes the start/finish events and keeps its own in-flight flag.

```csharp
using System;
using _Core.Events;
using _Core.Events.Ads;
using _Core.Platform.Services.Ads;
using UnityEngine;

namespace _Platforms.YandexGames.Services
{
    /// <summary>
    /// Handles advertisement operations through the Yandex Games SDK.
    /// </summary>
    public class YandexAdsService : IAdsService
    {
        private bool _isAdShowing;
        private bool _adStarted;

        public bool IsAdShowing => _isAdShowing;

        public void ShowRewardedAd(Action<AdResult> callback)
        {
            if (!YandexSdkBridge.IsInitialized)
            {
                callback?.Invoke(AdResult.NotAvailable);
                return;
            }

            // A second request while one is showing must still answer its callback.
            if (_isAdShowing)
            {
                callback?.Invoke(AdResult.InProgress);
                return;
            }

            _isAdShowing = true;
            _adStarted = false;

            // Placeholder API: map the vendor's open/rewarded/close/error callbacks.
            YandexSdkBridge.ShowRewarded(
                onOpen: OnAdStarted,
                onRewarded: () => FinishAd(AdResult.Completed, callback),
                onClosedWithoutReward: () => FinishAd(AdResult.Failed, callback),
                onError: error =>
                {
                    Debug.LogWarning($"Yandex: rewarded ad failed: {error}");
                    FinishAd(AdResult.Failed, callback);
                });
        }

        public void ShowInterstitialAd(Action<AdResult> callback)
        {
            // Same shape as ShowRewardedAd.
        }

        public void PrefetchAd(AdType adType)
        {
            // Preload through the SDK if it supports it; otherwise do nothing.
        }

        public void HasAdblock(Action<bool> callback)
        {
            // Report false when the SDK has no adblock detection.
            callback?.Invoke(false);
        }

        public void ShowBanner() { }

        public void HideBanner() { }

        private void OnAdStarted()
        {
            _adStarted = true;
            EventBus.Publish(new AdStartedEvent());
        }

        private void FinishAd(AdResult result, Action<AdResult> callback)
        {
            if (!_isAdShowing)
                return; // guard against duplicate close/error callbacks

            _isAdShowing = false;

            // Restore game state before the caller runs reward logic, but only undo a pause
            // that actually happened. EventBus isolates handler exceptions, so the callback runs.
            if (_adStarted)
            {
                EventBus.Publish(new AdFinishedEvent(result == AdResult.Completed));
            }

            callback?.Invoke(result);
        }
    }
}
```

## Storage service

`ISaveStorage` is synchronous (`HasSave/Save/Load/Delete` with string keys and JSON values from
`SaveManager`). If the vendor's storage is async (fetch player data from a server), load it
during `InitializeAsync` into an in-memory cache and have the service read/write the cache, flushing
writes through the SDK. Don't block on an async call inside `Load`.

Mirror `CrazyGamesDataStorage.EnsureSdkReady()`: throwing `InvalidOperationException` when the SDK
is not ready is acceptable in the real service because the platform only registers it after a
successful init.

## Registration

`_Core` never references a bridge; the bridge registers itself before the bootstrap scene loads
(same pattern as `CrazyGamesPlatformRegistration`):

```csharp
#if PLATFORM_YANDEX
using _Core.Platform.Core;
using UnityEngine;

namespace _Platforms.YandexGames.Core
{
    public static class YandexPlatformRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            PlatformFactory.Register(PlatformType.YandexGames, () => new YandexPlatform());
        }
    }
}
#endif
```

Use the `#if` only if the SDK is optional or target-specific; wrap the platform/services files in the
same define, and an unregistered type falls back to `NullPlatform`. Add the define under Player
Settings > Scripting Define Symbols for the relevant target(s).
