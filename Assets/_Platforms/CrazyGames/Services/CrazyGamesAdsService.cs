// Compiled only when CrazyGames is the selected platform (Tools/Template/Platform).
#if PLATFORM_CRAZYGAMES
using System;
using CrazyGames;
using _Core.Platform.Services.Ads;
using UnityEngine;

namespace _Platforms.CrazyGames.Services
{
    /// <summary>
    /// Handles advertisement operations through the CrazyGames SDK.
    /// </summary>
    public class CrazyGamesAdsService : IAdsService
    {
        // The request in flight. CrazySDK ignores a new request until it has answered the last one (no callback at
        // all), so a request stays in flight after AdPolicyService gives up on it, even one whose ad never started:
        // new requests then get InProgress at once instead of waiting for another timeout.
        private readonly AdRequestTracker _request;

        // What CrazySDK changes for an ad and restores only in its own finish/error callback. The time scale it
        // also changes (pauseGameDuringAd) is left to PauseManager, which owns it and restores its own value.
        private float _volumeBeforeAd;
        private bool _runInBackgroundBeforeAd;

        /// <summary>
        /// Creates the service. <c>CrazyGamesPlatform</c> registers it only once CrazySDK is available and initialized.
        /// </summary>
        public CrazyGamesAdsService()
        {
            _request = new AdRequestTracker(releaseUnstartedOnTimeout: false, onTimedOut: OnRequestTimedOut);
        }

        public bool IsAdShowing => _request.IsActive;

        public void ShowRewardedAd(Action<AdResult> callback)
        {
            RequestAd(AdType.Rewarded, callback);
        }

        public void ShowInterstitialAd(Action<AdResult> callback)
        {
            RequestAd(AdType.Interstitial, callback);
        }

        public void PrefetchAd(AdType adType)
        {
            if (!CanShowAds() || _request.IsActive)
                return;

            CrazySDK.Ad.PrefetchAd(ToCrazyAdType(adType));
        }

        public void HasAdblock(Action<bool> callback)
        {
            if (!CanShowAds())
            {
                callback?.Invoke(false);
                return;
            }

            // Answers immediately once detection has finished, otherwise when it does. The SDK answers every queued
            // caller in one loop, so a throwing callback must not reach it and starve the callers after it.
            CrazySDK.Ad.HasAdblock(hasAdblock =>
            {
                try
                {
                    callback?.Invoke(hasAdblock);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            });
        }

        public void ShowBanner()
        {
            if (!CanShowAds())
                return;

            // Explicit null check: the ?. operator bypasses Unity's fake-null.
            CrazyGamesBannerController controller = CrazyGamesBannerController.Instance;
            if (controller == null)
            {
                Debug.LogWarning("CrazyGames: No CrazyGamesBannerController in the scene, banner not shown.");
                return;
            }

            controller.Show();

            // Activating the banner object does not fetch an ad; refreshing does.
            controller.Refresh();
        }

        public void HideBanner()
        {
            // Explicit null check: the ?. operator bypasses Unity's fake-null.
            CrazyGamesBannerController controller = CrazyGamesBannerController.Instance;
            if (controller != null)
                controller.Hide();

            // On WebGL the banner is an overlay on the page: deactivating (or destroying) the banner object only
            // leaves it out of the next refresh. Refreshing now, with no visible banner, removes the overlay. The
            // IsShutDown check keeps a call during quit from creating the SDK object again.
            if (CanShowAds() && !CrazySDK.IsShutDown)
                CrazySDK.Banner.RefreshBanners();
        }

        private void RequestAd(AdType adType, Action<AdResult> callback)
        {
            if (!CanShowAds())
            {
                callback?.Invoke(AdResult.NotAvailable);
                return;
            }

            // The SDK silently ignores a second request while one is in progress
            // (no callback at all), so answer overlapping requests here.
            if (!_request.TryBegin(adType, callback))
            {
                callback?.Invoke(AdResult.InProgress);
                return;
            }

            // CrazyGames calls interstitial advertisements "midgame" ads.
            CrazyAdType crazyAdType = ToCrazyAdType(adType);
            _volumeBeforeAd = AudioListener.volume;
            _runInBackgroundBeforeAd = Application.runInBackground;
            int requestId = _request.Id;

            try
            {
                CrazySDK.Ad.RequestAd(
                    crazyAdType,
                    OnAdStarted,
                    error =>
                    {
                        Debug.LogWarning($"CrazyGames: {crazyAdType} ad failed: {error}");
                        _request.Complete(AdResult.Failed);
                    },
                    () => _request.Complete(AdResult.Completed));
            }
            catch (Exception exception)
            {
                // RequestAd throws when the SDK is not initialized. Its Editor simulation can answer inside the call,
                // so only this request is completed, and only while it is still in flight.
                Debug.LogException(exception);
                if (_request.IsCurrent(requestId))
                    _request.Complete(AdResult.Failed);
            }
        }

        private void OnAdStarted()
        {
            // Started after AdPolicyService gave up and the volume was restored: mute again, as the SDK did at
            // request time. The SDK's own cleanup restores it when this ad ends.
            if (_request.IsTimedOut)
                AudioListener.volume = 0f;

            // Gameplay is already paused by PauseManager for the whole request (AdRequestedEvent).
            _request.NotifyStarted();
        }

        /// <summary>
        /// Undoes what the SDK changed for the ad when AdPolicyService gives up on it. The SDK restores these only
        /// in its own finish/error callback, so without this the game stays muted until the SDK answers.
        /// </summary>
        private void OnRequestTimedOut()
        {
            AudioListener.volume = _volumeBeforeAd;
            Application.runInBackground = _runInBackgroundBeforeAd;
        }

        private static CrazyAdType ToCrazyAdType(AdType adType)
        {
            return adType == AdType.Rewarded
                ? CrazyAdType.Rewarded
                : CrazyAdType.Midgame;
        }

        private bool CanShowAds()
        {
            return CrazySDK.IsAvailable &&
                   CrazySDK.IsInitialized;
        }
    }
}
#endif
