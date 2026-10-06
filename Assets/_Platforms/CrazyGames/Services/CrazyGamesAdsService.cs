// Compiled only when CrazyGames is the selected platform (Tools/Template/Platform).
#if PLATFORM_CRAZYGAMES
using System;
using CrazyGames;
using _Core.Platform.Services.Ads;
using _Core.Events;
using _Core.Events.Ads;
using UnityEngine;

namespace _Platforms.CrazyGames.Services
{
    /// <summary>
    /// Handles advertisement operations through the CrazyGames SDK.
    /// </summary>
    public class CrazyGamesAdsService : IAdsService
    {
        private bool _isAdShowing;

        // The request in flight (one at a time): a start event published without its finish event yet, and
        // whether AdPolicyService gave up on it (the SDK may still answer later).
        private bool _finishOwed;
        private bool _isTimedOut;

        // What CrazySDK changes for an ad and restores only in its own finish/error callback. The time scale it
        // also changes (pauseGameDuringAd) is left to PauseManager, which owns it and restores its own value.
        private float _volumeBeforeAd;
        private bool _runInBackgroundBeforeAd;

        public bool IsAdShowing => _isAdShowing;

        public void ShowRewardedAd(Action<AdResult> callback)
        {
            RequestAd(CrazyAdType.Rewarded, callback);
        }

        public void ShowInterstitialAd(Action<AdResult> callback)
        {
            // CrazyGames calls interstitial advertisements "midgame" ads.
            RequestAd(CrazyAdType.Midgame, callback);
        }

        public void PrefetchAd(AdType adType)
        {
            if (!CanShowAds() || _isAdShowing)
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

            // Answers immediately once detection has finished, otherwise when it does.
            CrazySDK.Ad.HasAdblock(callback);
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
            CrazyGamesBannerController controller = CrazyGamesBannerController.Instance;
            if (controller != null)
                controller.Hide();
        }

        private void RequestAd(
            CrazyAdType adType,
            Action<AdResult> callback)
        {
            if (!CanShowAds())
            {
                callback?.Invoke(AdResult.NotAvailable);
                return;
            }

            // The SDK silently ignores a second request while one is in progress
            // (no callback at all), so answer overlapping requests here.
            if (_isAdShowing)
            {
                callback?.Invoke(AdResult.InProgress);
                return;
            }

            _isAdShowing = true;
            _finishOwed = false;
            _isTimedOut = false;
            _volumeBeforeAd = AudioListener.volume;
            _runInBackgroundBeforeAd = Application.runInBackground;
            EventBus.Subscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);

            try
            {
                CrazySDK.Ad.RequestAd(
                    adType,
                    OnAdStarted,
                    error =>
                    {
                        Debug.LogWarning($"CrazyGames: {adType} ad failed: {error}");
                        Complete(AdResult.Failed, callback);
                    },
                    () =>
                    {
                        Complete(AdResult.Completed, callback);
                    });
            }
            catch (Exception exception)
            {
                // RequestAd throws when the SDK is not initialized.
                Debug.LogException(exception);
                Complete(AdResult.Failed, callback);
            }
        }

        private void OnAdStarted()
        {
            // Started after AdPolicyService gave up and the volume was restored: mute again, as the SDK did at
            // request time. The SDK's own cleanup restores it when this ad ends.
            if (_isTimedOut)
                AudioListener.volume = 0f;

            _finishOwed = true;

            // Gameplay is already paused by PauseManager for the whole request (AdRequestedEvent).
            EventBus.Publish(new AdStartedEvent());
        }

        /// <summary>
        /// Undoes what the SDK changed for the ad when AdPolicyService gives up on it. The SDK restores these only
        /// in its own finish/error callback, so without this the game stays muted until the SDK answers.
        /// </summary>
        private void OnAdRequestCompleted(AdRequestCompletedEvent gameEvent)
        {
            if (gameEvent.Result != AdResult.TimedOut || !_isAdShowing || _isTimedOut)
                return;

            _isTimedOut = true;

            // AdPolicyService has published the finish event itself if the ad had started.
            _finishOwed = false;

            AudioListener.volume = _volumeBeforeAd;
            Application.runInBackground = _runInBackgroundBeforeAd;

            // _isAdShowing stays set until the SDK answers: the SDK ignores new requests until then (no callback
            // at all), so they get InProgress at once instead of waiting for another timeout.
        }

        /// <summary>
        /// Clears the in-flight flag, publishes the finish event owed for an ad that actually started,
        /// then always invokes the caller's callback (AdPolicyService ignores it after a timeout).
        /// </summary>
        private void Complete(AdResult result, Action<AdResult> callback)
        {
            _isAdShowing = false;
            EventBus.Unsubscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);

            // EventBus isolates handler exceptions, so the callback below always runs.
            if (_finishOwed)
            {
                _finishOwed = false;
                EventBus.Publish(new AdFinishedEvent(result == AdResult.Completed));
            }

            callback?.Invoke(result);
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
