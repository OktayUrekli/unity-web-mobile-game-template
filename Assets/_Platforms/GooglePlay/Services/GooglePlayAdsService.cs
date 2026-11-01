// Compiled only when Google Play is the selected platform (Tools/Template/Platform).
#if PLATFORM_GOOGLEPLAY
using System;
using System.Threading.Tasks;
using _Core.Platform.Services.Ads;
using _Platforms.GooglePlay.Core;
using GoogleMobileAds.Api;
using UnityEngine;

namespace _Platforms.GooglePlay.Services
{
    /// <summary>
    /// Interstitial, rewarded and banner ads through Google Mobile Ads (AdMob). Fullscreen ads are loaded ahead of
    /// time and again after each one closes. An interstitial that is not loaded yet answers
    /// <see cref="AdResult.Failed"/> at once, because the game flow waits on it; a rewarded ad that is not loaded yet
    /// is loaded and then shown, because the player asked for it (AdPolicyService's start timeout bounds the wait).
    /// A rewarded ad answers <see cref="AdResult.Completed"/> only after the SDK reported the reward.
    /// The banner is an anchored adaptive banner at the bottom of the screen, drawn over the game, and is destroyed
    /// when hidden. Everything answers <see cref="AdResult.NotAvailable"/> until <see cref="GooglePlayMobileAds.IsReady"/>.
    /// </summary>
    public class GooglePlayAdsService : IAdsService
    {
        // After a failed load, prefetches and finished ads don't load that format again before this. A rewarded
        // request the player made always loads.
        private const float LoadRetryDelaySeconds = 30f;

        // Google: a loaded fullscreen ad expires after an hour. The plugin's CanShowAd does not track this.
        private const double AdExpirySeconds = 55 * 60;

        // The SDK reports the reward and the close on separate threads, so the reward can arrive just after the
        // close. A rewarded ad closed without it waits this long (real time) before it answers Failed.
        private const float LateRewardGraceSeconds = 1f;

        // A rewarded ad that finishes loading this close to AdPolicyService's start timeout is kept for the next
        // request instead of shown, so the timeout cannot abandon an ad that is opening.
        private const float LateShowMarginSeconds = 3f;

        private readonly GooglePlayAdUnits _adUnits;
        private readonly float _adStartTimeoutSeconds;

        private InterstitialAd _interstitial;
        private DateTime _interstitialLoadedAt;
        private bool _isInterstitialLoading;
        private float _interstitialRetryAt;

        private RewardedAd _rewarded;
        private DateTime _rewardedLoadedAt;
        private bool _isRewardedLoading;
        private float _rewardedRetryAt;

        private BannerView _banner;
        private bool _isBannerWanted;

        // The request in flight. Callbacks of a shown ad carry its request ID, so a late callback of an abandoned ad
        // is ignored. An ad on screen keeps the request until it closes; one that never opened is destroyed and its
        // request released when AdPolicyService gives up (OnRequestTimedOut).
        private readonly AdRequestTracker _request;
        private float _requestedAt;
        private Action _destroyShownAd;

        // The rewarded request that waits for a load to show its ad, or 0. Checked with AdRequestTracker.IsCurrent,
        // so it needs no reset when that request ends.
        private int _waitingForLoadRequestId;

        /// <param name="adUnits">The ad unit IDs this build uses.</param>
        /// <param name="adStartTimeoutSeconds"><c>PlatformConfig.AdStartTimeoutSeconds</c>, used by AdPolicyService.</param>
        public GooglePlayAdsService(GooglePlayAdUnits adUnits, float adStartTimeoutSeconds)
        {
            _adUnits = adUnits;
            _adStartTimeoutSeconds = adStartTimeoutSeconds;
            _request = new AdRequestTracker(releaseUnstartedOnTimeout: true, onTimedOut: OnRequestTimedOut);

            GooglePlayMobileAds.Ready += OnSdkReady;
            if (GooglePlayMobileAds.IsReady)
                OnSdkReady();
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
            if (adType == AdType.Rewarded)
                LoadRewarded(ignoreRetryDelay: false);
            else
                LoadInterstitial(ignoreRetryDelay: false);
        }

        public void HasAdblock(Action<bool> callback)
        {
            // Not an issue in an app; a blocked request fails to load like any unfilled one.
            callback?.Invoke(false);
        }

        public void ShowBanner()
        {
            _isBannerWanted = true;

            // Before the SDK is ready the banner is created by OnSdkReady.
            if (GooglePlayMobileAds.IsReady && _banner == null)
                CreateBanner();
        }

        public void HideBanner()
        {
            _isBannerWanted = false;
            DestroyBanner();
        }

        private void OnSdkReady()
        {
            LoadInterstitial(ignoreRetryDelay: true);
            LoadRewarded(ignoreRetryDelay: true);

            if (_isBannerWanted && _banner == null)
                CreateBanner();
        }

        private void RequestAd(AdType adType, Action<AdResult> callback)
        {
            if (!GooglePlayMobileAds.IsReady)
            {
                callback?.Invoke(AdResult.NotAvailable);
                return;
            }

            if (!_request.TryBegin(adType, callback))
            {
                callback?.Invoke(AdResult.InProgress);
                return;
            }

            if (adType == AdType.Interstitial && !CanShow(_interstitial, _interstitialLoadedAt))
            {
                Debug.Log("GooglePlay: Interstitial not loaded yet, skipped.");
                LoadInterstitial(ignoreRetryDelay: false);
                _request.Complete(AdResult.Failed);
                return;
            }

            _requestedAt = Time.realtimeSinceStartup;
            _destroyShownAd = null;

            if (adType == AdType.Rewarded && !CanShow(_rewarded, _rewardedLoadedAt))
            {
                _waitingForLoadRequestId = _request.Id;
                LoadRewarded(ignoreRetryDelay: true);
                return;
            }

            ShowLoadedAd();
        }

        /// <summary>
        /// Shows the loaded ad of the current request. Ads are single-use, so the slot is emptied and the next one is
        /// loaded after this one closes.
        /// </summary>
        private void ShowLoadedAd()
        {
            int requestId = _request.Id;

            try
            {
                if (_request.AdType == AdType.Rewarded)
                {
                    RewardedAd ad = _rewarded;
                    _rewarded = null;
                    _destroyShownAd = ad.Destroy;
                    ad.OnAdFullScreenContentOpened += () => GooglePlayMobileAds.RunOnMainThread(() => OnAdOpened(requestId));
                    ad.OnAdFullScreenContentClosed += () => GooglePlayMobileAds.RunOnMainThread(() => OnAdClosed(requestId));
                    ad.OnAdFullScreenContentFailed += error =>
                        GooglePlayMobileAds.RunOnMainThread(() => OnAdFailedToShow(requestId, error));
                    ad.Show(reward => GooglePlayMobileAds.RunOnMainThread(() => OnUserEarnedReward(requestId)));
                }
                else
                {
                    InterstitialAd ad = _interstitial;
                    _interstitial = null;
                    _destroyShownAd = ad.Destroy;
                    ad.OnAdFullScreenContentOpened += () => GooglePlayMobileAds.RunOnMainThread(() => OnAdOpened(requestId));
                    ad.OnAdFullScreenContentClosed += () => GooglePlayMobileAds.RunOnMainThread(() => OnAdClosed(requestId));
                    ad.OnAdFullScreenContentFailed += error =>
                        GooglePlayMobileAds.RunOnMainThread(() => OnAdFailedToShow(requestId, error));
                    ad.Show();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                // A placeholder ad may already have answered before throwing.
                if (_request.IsCurrent(requestId))
                    FinishShownAd(AdResult.Failed);
            }
        }

        private void OnAdOpened(int requestId)
        {
            // Gameplay is already paused and muted by PauseManager and AudioManager for the whole request.
            if (_request.IsCurrent(requestId))
                _request.NotifyStarted();
        }

        private void OnUserEarnedReward(int requestId)
        {
            if (_request.IsCurrent(requestId))
                _request.NotifyRewardEarned();
        }

        private void OnAdClosed(int requestId)
        {
            if (!_request.IsCurrent(requestId))
                return;

            if (_request.AdType == AdType.Rewarded && !_request.IsRewardEarned)
            {
                _ = FinishRewardedAfterCloseAsync(requestId);
                return;
            }

            FinishShownAd(AdResult.Completed);
        }

        /// <summary>
        /// Waits up to <see cref="LateRewardGraceSeconds"/> for a reward that arrives after the close, then answers.
        /// A rewarded ad closed before the reward was earned grants nothing. Polls once per frame with
        /// <see cref="Task.Yield"/>, like AdPolicyService's watchdog.
        /// </summary>
        private async Task FinishRewardedAfterCloseAsync(int requestId)
        {
            try
            {
                float deadline = Time.realtimeSinceStartup + LateRewardGraceSeconds;
                while (_request.IsCurrent(requestId) && !_request.IsRewardEarned && Time.realtimeSinceStartup < deadline)
                    await Task.Yield();

                // A show failure or a second close may have answered meanwhile.
                if (_request.IsCurrent(requestId))
                    FinishShownAd(_request.IsRewardEarned ? AdResult.Completed : AdResult.Failed);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (_request.IsCurrent(requestId))
                    FinishShownAd(AdResult.Failed);
            }
        }

        private void OnAdFailedToShow(int requestId, AdError error)
        {
            if (!_request.IsCurrent(requestId))
                return;

            Debug.LogWarning($"GooglePlay: {_request.AdType} ad failed to show: {error?.GetMessage()}");
            FinishShownAd(_request.AdType == AdType.Rewarded && _request.IsRewardEarned
                ? AdResult.Completed
                : AdResult.Failed);
        }

        /// <summary>
        /// Ends the request whose ad was shown, then loads the next ad of that format.
        /// </summary>
        private void FinishShownAd(AdResult result)
        {
            AdType adType = _request.AdType;
            DestroyShownAd();
            _request.Complete(result);

            if (adType == AdType.Rewarded)
                LoadRewarded(ignoreRetryDelay: false);
            else
                LoadInterstitial(ignoreRetryDelay: false);
        }

        /// <summary>
        /// AdPolicyService gave up on the request. An ad on screen keeps the request open until it closes; an ad that
        /// never opened is destroyed here and the tracker then releases its request (a rewarded ad that loads later is
        /// kept for the next request, and late callbacks of the destroyed ad are ignored).
        /// </summary>
        private void OnRequestTimedOut()
        {
            if (!_request.HasStarted)
                DestroyShownAd();
        }

        private void DestroyShownAd()
        {
            Action destroy = _destroyShownAd;
            _destroyShownAd = null;

            try
            {
                destroy?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void LoadInterstitial(bool ignoreRetryDelay)
        {
            if (!GooglePlayMobileAds.IsReady || _isInterstitialLoading || CanShow(_interstitial, _interstitialLoadedAt))
                return;

            // Expired or already shown: never offered again.
            DestroyAd(_interstitial);
            _interstitial = null;

            if (!ignoreRetryDelay && Time.realtimeSinceStartup < _interstitialRetryAt)
                return;

            _isInterstitialLoading = true;

            try
            {
                InterstitialAd.Load(_adUnits.Interstitial, new AdRequest(), (ad, error) =>
                    GooglePlayMobileAds.RunOnMainThread(() => OnInterstitialLoaded(ad, error)));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                OnInterstitialLoaded(null, null);
            }
        }

        private void OnInterstitialLoaded(InterstitialAd ad, LoadAdError error)
        {
            _isInterstitialLoading = false;

            if (ad == null)
            {
                Debug.LogWarning($"GooglePlay: Interstitial failed to load: {error?.GetMessage()}");
                _interstitialRetryAt = Time.realtimeSinceStartup + LoadRetryDelaySeconds;
                return;
            }

            DestroyAd(_interstitial);
            _interstitial = ad;
            _interstitialLoadedAt = DateTime.UtcNow;
        }

        private void LoadRewarded(bool ignoreRetryDelay)
        {
            if (!GooglePlayMobileAds.IsReady || _isRewardedLoading || CanShow(_rewarded, _rewardedLoadedAt))
                return;

            // Expired or already shown: never offered again.
            DestroyAd(_rewarded);
            _rewarded = null;

            if (!ignoreRetryDelay && Time.realtimeSinceStartup < _rewardedRetryAt)
                return;

            _isRewardedLoading = true;

            try
            {
                RewardedAd.Load(_adUnits.Rewarded, new AdRequest(), (ad, error) =>
                    GooglePlayMobileAds.RunOnMainThread(() => OnRewardedLoaded(ad, error)));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                OnRewardedLoaded(null, null);
            }
        }

        private void OnRewardedLoaded(RewardedAd ad, LoadAdError error)
        {
            _isRewardedLoading = false;

            if (ad == null)
            {
                Debug.LogWarning($"GooglePlay: Rewarded ad failed to load: {error?.GetMessage()}");
                _rewardedRetryAt = Time.realtimeSinceStartup + LoadRetryDelaySeconds;

                if (_request.IsCurrent(_waitingForLoadRequestId))
                    _request.Complete(AdResult.Failed);

                return;
            }

            DestroyAd(_rewarded);
            _rewarded = ad;
            _rewardedLoadedAt = DateTime.UtcNow;

            if (_request.IsCurrent(_waitingForLoadRequestId))
            {
                _waitingForLoadRequestId = 0;

                float margin = Mathf.Min(LateShowMarginSeconds, _adStartTimeoutSeconds * 0.5f);
                if (Time.realtimeSinceStartup - _requestedAt > _adStartTimeoutSeconds - margin)
                {
                    Debug.Log("GooglePlay: Rewarded ad loaded too late for this request; kept for the next one.");
                    _request.Complete(AdResult.Failed);
                    return;
                }

                ShowLoadedAd();
            }
        }

        private void CreateBanner()
        {
            try
            {
                // Google's recommended anchored adaptive size; the safe width is 0 in the Editor.
                int width = MobileAds.Utils.GetDeviceSafeWidth();
                AdSize size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(
                    width > 0 ? width : AdSize.FullWidth);

                var banner = new BannerView(_adUnits.Banner, size, AdPosition.Bottom);
                banner.OnBannerAdLoadFailed += error => GooglePlayMobileAds.RunOnMainThread(() =>
                    Debug.LogWarning($"GooglePlay: Banner failed to load: {error?.GetMessage()}"));

                _banner = banner;
                banner.LoadAd(new AdRequest());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                DestroyBanner();
            }
        }

        private void DestroyBanner()
        {
            BannerView banner = _banner;
            _banner = null;

            try
            {
                banner?.Destroy();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void DestroyAd(InterstitialAd ad)
        {
            try
            {
                ad?.Destroy();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void DestroyAd(RewardedAd ad)
        {
            try
            {
                ad?.Destroy();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static bool CanShow(InterstitialAd ad, DateTime loadedAt)
        {
            return ad != null && ad.CanShowAd() && !IsExpired(loadedAt);
        }

        private static bool CanShow(RewardedAd ad, DateTime loadedAt)
        {
            return ad != null && ad.CanShowAd() && !IsExpired(loadedAt);
        }

        private static bool IsExpired(DateTime loadedAt)
        {
            return (DateTime.UtcNow - loadedAt).TotalSeconds > AdExpirySeconds;
        }
    }
}
#endif
