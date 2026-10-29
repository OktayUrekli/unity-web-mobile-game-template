// Compiled only when Yandex Games is the selected platform (Tools/Template/Platform).
#if PLATFORM_YANDEX
using System;
using _Core.Events;
using _Core.Events.Ads;
using _Core.Platform.Services.Ads;
using _Platforms.YandexGames.Core;
using UnityEngine;

namespace _Platforms.YandexGames.Services
{
    /// <summary>
    /// Fullscreen (interstitial) and rewarded ads and the sticky banner through the Yandex Games SDK
    /// (<c>ysdk.adv</c>). Yandex decides how often a fullscreen ad may show; a call sooner than that closes at
    /// once without an ad and answers <see cref="AdResult.Failed"/>. Registered only after a successful SDK
    /// initialization.
    /// </summary>
    public class YandexGamesAdsService : IAdsService
    {
        private bool _isAdShowing;
        private AdType _adType;
        private Action<AdResult> _callback;

        // The request in flight (one at a time): a start event published without its finish event yet, whether
        // AdPolicyService gave up on it (the SDK may still answer later), and whether the rewarded video counted as
        // watched.
        private bool _finishOwed;
        private bool _isTimedOut;
        private bool _rewarded;

        public bool IsAdShowing => _isAdShowing;

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
            // The Yandex SDK loads ads itself and has no preload call.
        }

        public void HasAdblock(Action<bool> callback)
        {
            // The Yandex SDK has no adblock detection; a blocked ad answers with an error instead.
            callback?.Invoke(false);
        }

        public void ShowBanner()
        {
            YandexGamesSdk.ShowBanner();
        }

        public void HideBanner()
        {
            YandexGamesSdk.HideBanner();
        }

        private void RequestAd(AdType adType, Action<AdResult> callback)
        {
            if (!YandexGamesSdk.IsInitialized)
            {
                callback?.Invoke(AdResult.NotAvailable);
                return;
            }

            if (_isAdShowing)
            {
                callback?.Invoke(AdResult.InProgress);
                return;
            }

            _isAdShowing = true;
            _adType = adType;
            _callback = callback;
            _finishOwed = false;
            _isTimedOut = false;
            _rewarded = false;
            EventBus.Subscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);

            if (adType == AdType.Rewarded)
                YandexGamesSdk.ShowRewardedAd(OnAdEvent);
            else
                YandexGamesSdk.ShowFullscreenAd(OnAdEvent);
        }

        private void OnAdEvent(string type, string value)
        {
            if (!_isAdShowing)
                return;

            switch (type)
            {
                case "open":
                    // After a timeout AdPolicyService no longer waits for the start.
                    if (_finishOwed || _isTimedOut)
                        return;

                    _finishOwed = true;

                    // Gameplay is already paused and muted by PauseManager and AudioManager for the whole request.
                    EventBus.Publish(new AdStartedEvent());
                    break;

                case "rewarded":
                    _rewarded = true;
                    break;

                case "close":
                    if (_adType == AdType.Rewarded)
                    {
                        Complete(_rewarded ? AdResult.Completed : AdResult.Failed);
                    }
                    else
                    {
                        if (value != "shown")
                            Debug.Log("Yandex: Fullscreen ad closed without being shown (too soon after the last one, or no ad).");

                        Complete(value == "shown" ? AdResult.Completed : AdResult.Failed);
                    }

                    break;

                case "error":
                    Debug.LogWarning($"Yandex: {_adType} ad failed: {value}");

                    // A rewarded video may report an error after it was counted as watched.
                    Complete(_adType == AdType.Rewarded && _rewarded ? AdResult.Completed : AdResult.Failed);
                    break;
            }
        }

        /// <summary>
        /// When AdPolicyService gives up on the ad it has published the finish event itself if the ad had started,
        /// so a late answer from the SDK must not publish it again.
        /// </summary>
        private void OnAdRequestCompleted(AdRequestCompletedEvent gameEvent)
        {
            if (gameEvent.Result != AdResult.TimedOut || !_isAdShowing)
                return;

            _isTimedOut = true;
            _finishOwed = false;

            // _isAdShowing stays set until the SDK answers, so new requests get InProgress until then.
        }

        /// <summary>
        /// Clears the in-flight flag, publishes the finish event owed for an ad that actually started, then always
        /// invokes the caller's callback (AdPolicyService ignores it after a timeout).
        /// </summary>
        private void Complete(AdResult result)
        {
            _isAdShowing = false;
            EventBus.Unsubscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);

            Action<AdResult> callback = _callback;
            _callback = null;

            // EventBus isolates handler exceptions, so the callback below always runs.
            if (_finishOwed)
            {
                _finishOwed = false;
                EventBus.Publish(new AdFinishedEvent(result == AdResult.Completed));
            }

            callback?.Invoke(result);
        }
    }
}
#endif
