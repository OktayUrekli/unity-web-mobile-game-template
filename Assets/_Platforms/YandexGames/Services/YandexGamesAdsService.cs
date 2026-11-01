// Compiled only when Yandex Games is the selected platform (Tools/Template/Platform).
#if PLATFORM_YANDEX
using System;
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
        // The request in flight. An ad on screen keeps it until Yandex closes the ad, so new requests get InProgress
        // until then. One that never opened is released when AdPolicyService gives up on it: Yandex may never answer
        // it, and every later request would get InProgress for the rest of the session. A late answer for it is
        // ignored: OnAdEvent drops events while no request is in flight, and YandexGamesSdk drops the events of an
        // older ad once a newer one starts.
        private readonly AdRequestTracker _request = new(releaseUnstartedOnTimeout: true);

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

            if (!_request.TryBegin(adType, callback))
            {
                callback?.Invoke(AdResult.InProgress);
                return;
            }

            int requestId = _request.Id;

            try
            {
                if (adType == AdType.Rewarded)
                    YandexGamesSdk.ShowRewardedAd(OnAdEvent);
                else
                    YandexGamesSdk.ShowFullscreenAd(OnAdEvent);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                // The SDK may already have answered (an error event) before throwing.
                if (_request.IsCurrent(requestId))
                    _request.Complete(AdResult.Failed);
            }
        }

        private void OnAdEvent(string type, string value)
        {
            if (!_request.IsActive)
                return;

            switch (type)
            {
                case "open":
                    // Gameplay is already paused and muted by PauseManager and AudioManager for the whole request.
                    // A second "open" is ignored.
                    _request.NotifyStarted();
                    break;

                case "rewarded":
                    _request.NotifyRewardEarned();
                    break;

                case "close":
                    if (_request.AdType == AdType.Rewarded)
                    {
                        _request.Complete(_request.IsRewardEarned ? AdResult.Completed : AdResult.Failed);
                    }
                    else
                    {
                        if (value != "shown")
                            Debug.Log("Yandex: Fullscreen ad closed without being shown (too soon after the last one, or no ad).");

                        _request.Complete(value == "shown" ? AdResult.Completed : AdResult.Failed);
                    }

                    break;

                case "error":
                    Debug.LogWarning($"Yandex: {_request.AdType} ad failed: {value}");

                    // A rewarded video may report an error after it was counted as watched.
                    _request.Complete(_request.AdType == AdType.Rewarded && _request.IsRewardEarned
                        ? AdResult.Completed
                        : AdResult.Failed);
                    break;
            }
        }
    }
}
#endif
