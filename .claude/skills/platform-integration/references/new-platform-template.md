# New platform skeleton

Copy-ready structure for a new platform, modelled on `CrazyGamesPlatform`, for a made-up portal `MyPortal`.
Everything named `<Name>SdkBridge` / `SomeVendorApi` is a **placeholder**: replace it with the real plugin API
after reading the plugin's source. Do not ship guessed SDK calls.

## Contents
- Files
- Platform class (capabilities, init with timeout, service registration)
- Ads service (AdRequestTracker)
- Storage service
- Registration

## Files

```
Assets/_Platforms/<PlatformType>/    (Assembly-CSharp, next to the SDK; folder = PlatformType name, e.g. MyPortal)
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
using _Core.Configuration;
using _Core.Platform.Core;
using _Platforms.MyPortal.Services;
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
using _Core.Platform.Services.Privacy.Null;
using _Core.Platform.Services.User.Null;
using UnityEngine;

namespace _Platforms.MyPortal.Core
{
    /// <summary>
    /// Provides the MyPortal platform implementation.
    /// </summary>
    public class MyPortalPlatform : IPlatform
    {
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
            capabilities.SupportsPrivacyOptions = false;
        }

        public async Task InitializeAsync(PlatformManager manager)
        {
            Debug.Log("MyPortal: Initialization started.");

            if (MyPortalSdkBridge.IsAvailable)
            {
                var completion = new TaskCompletionSource<bool>();

                // TrySet* tolerates SDKs that invoke callbacks more than once.
                MyPortalSdkBridge.Init(
                    () => completion.TrySetResult(true),
                    error => completion.TrySetResult(false));

                float timeoutSeconds = ConfigurationManager.GameConfig.PlatformConfig.SdkInitTimeoutSeconds;
                bool completed = await WaitWithTimeoutAsync(completion.Task, timeoutSeconds);
                _isSdkReady = completed && completion.Task.Result; // safe: task is completed

                if (!_isSdkReady)
                    Debug.LogWarning("MyPortal: SDK init failed or timed out. Using fallback services.");
            }
            else
            {
                Debug.Log("MyPortal: SDK is not available. Using fallback services.");
            }

            RegisterServices(manager);
        }

        private void RegisterServices(PlatformManager manager)
        {
            // Every service must be registered; game code never null-checks them.
            if (_isSdkReady)
            {
                manager.SetAdsService(new MyPortalAdsService());
                manager.SetStorageService(new MyPortalDataStorage());
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
            manager.SetPrivacyService(new NullPrivacyService());
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

Each bridge keeps its own short realtime loop like this one.

## Ads service (AdRequestTracker)

`PlatformManager` wraps this service in `AdPolicyService` (global in-flight guard, interstitial
cooldown, `AdRequestedEvent`/`AdRequestCompletedEvent`, exactly-once callback, timeouts). The platform
service keeps its own request in an `AdRequestTracker` and only maps the SDK onto it; the tracker
publishes `AdStartedEvent`/`AdFinishedEvent` in order, answers the callback after releasing the request
and handles `AdPolicyService`'s timeout (see the `Ads` section of SKILL.md).

```csharp
using System;
using _Core.Platform.Services.Ads;
using UnityEngine;

namespace _Platforms.MyPortal.Services
{
    /// <summary>
    /// Handles advertisement operations through the MyPortal SDK.
    /// </summary>
    public class MyPortalAdsService : IAdsService
    {
        // True: an ad that never started is released when AdPolicyService gives up on it, and the SDK may still
        // answer it after a newer request began, so every SDK callback checks its request id. False only for an
        // SDK that ignores new requests until it answers the last one (CrazySDK). Pass an onTimedOut hook when the
        // SDK changes something for the ad that it restores only in its own callbacks.
        private readonly AdRequestTracker _request = new(releaseUnstartedOnTimeout: true);

        public bool IsAdShowing => _request.IsActive;

        public void ShowRewardedAd(Action<AdResult> callback)
        {
            if (!MyPortalSdkBridge.IsInitialized)
            {
                callback?.Invoke(AdResult.NotAvailable);
                return;
            }

            // A second request while one is in flight must still answer its callback.
            if (!_request.TryBegin(AdType.Rewarded, callback))
            {
                callback?.Invoke(AdResult.InProgress);
                return;
            }

            int requestId = _request.Id;

            // Answers for an earlier request, which AdPolicyService gave up on, must not end this one.
            void IfCurrent(Action answer)
            {
                if (_request.IsCurrent(requestId))
                    answer();
            }

            try
            {
                // Placeholder API: map the vendor's open/rewarded/close/error callbacks. A rewarded ad counts as
                // completed only after the reward callback, also when an error follows it.
                MyPortalSdkBridge.ShowRewarded(
                    onOpen: () => IfCurrent(() => _request.NotifyStarted()),
                    onRewarded: () => IfCurrent(_request.NotifyRewardEarned),
                    onClose: () => IfCurrent(() =>
                        _request.Complete(_request.IsRewardEarned ? AdResult.Completed : AdResult.Failed)),
                    onError: error =>
                    {
                        Debug.LogWarning($"MyPortal: rewarded ad failed: {error}");
                        IfCurrent(() =>
                            _request.Complete(_request.IsRewardEarned ? AdResult.Completed : AdResult.Failed));
                    });
            }
            catch (Exception exception)
            {
                // Uncaught, the request would stay in flight and every later request would get InProgress.
                // The SDK may have answered before throwing.
                Debug.LogException(exception);
                if (_request.IsCurrent(requestId))
                    _request.Complete(AdResult.Failed);
            }
        }

        public void ShowInterstitialAd(Action<AdResult> callback)
        {
            // Same shape as ShowRewardedAd, callbacks checked the same way: open = NotifyStarted,
            // close = Complete(Completed), error = Complete(Failed).
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

If the platform caps its save data, the storage also implements `ILimitedSaveStorage` (bytes counted
as `SaveCache.MeasureBytes` counts them; `StoredKeys` lists every stored key when the SDK can, null
otherwise), and the platform passes the same limit to its PlayerPrefs fallback
(`new PlayerPrefsSaveStorage(limitBytes)`), so Editor runs refuse saves past the platform's limit too.

## Registration

`_Core` never references a bridge; the bridge registers itself before the bootstrap scene loads
(same pattern as `CrazyGamesPlatformRegistration`):

```csharp
#if PLATFORM_MYPORTAL
using _Core.Platform.Core;
using UnityEngine;

namespace _Platforms.MyPortal.Core
{
    public static class MyPortalPlatformRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            PlatformFactory.Register(PlatformType.MyPortal, () => new MyPortalPlatform());
        }
    }
}
#endif
```

Wrap the platform and service files in the same define, so with another platform selected the type
is unregistered and falls back to `NullPlatform`. Add the define to `PlatformSetup.Defines` with a
menu item and select the platform through `Tools/Template/Platform/...`; never edit Scripting Define
Symbols by hand.
