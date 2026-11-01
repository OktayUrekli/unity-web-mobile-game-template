using System;
using _Core.Platform.Services.Ads;

namespace _Core.Tests.Fakes
{
    /// <summary>
    /// <see cref="IAdsService"/> test double built on <see cref="AdRequestTracker"/> like the platform bridges. The
    /// test plays the SDK: <see cref="Open"/>, <see cref="Reward"/> and <see cref="Answer"/>.
    /// </summary>
    internal sealed class FakeTrackedAdsService : IAdsService
    {
        private readonly AdRequestTracker _request;

        /// <param name="releaseUnstartedOnTimeout">Passed to the tracker (false behaves like CrazySDK).</param>
        public FakeTrackedAdsService(bool releaseUnstartedOnTimeout)
        {
            _request = new AdRequestTracker(releaseUnstartedOnTimeout, () => TimedOutCount++);
        }

        /// <summary>
        /// Requests that reached the "SDK" (not answered InProgress by the tracker).
        /// </summary>
        public int SdkRequestCount { get; private set; }

        /// <summary>
        /// Calls of the tracker's timeout hook.
        /// </summary>
        public int TimedOutCount { get; private set; }

        /// <summary>
        /// The tracker's in-flight flag, as in the bridges.
        /// </summary>
        public bool IsAdShowing => _request.IsActive;

        /// <summary>
        /// Requests a rewarded ad from the fake SDK.
        /// </summary>
        public void ShowRewardedAd(Action<AdResult> callback)
        {
            Request(AdType.Rewarded, callback);
        }

        /// <summary>
        /// Requests an interstitial from the fake SDK.
        /// </summary>
        public void ShowInterstitialAd(Action<AdResult> callback)
        {
            Request(AdType.Interstitial, callback);
        }

        /// <summary>
        /// Does nothing.
        /// </summary>
        public void PrefetchAd(AdType adType)
        {
        }

        /// <summary>
        /// Reports no ad blocker.
        /// </summary>
        public void HasAdblock(Action<bool> callback)
        {
            callback?.Invoke(false);
        }

        /// <summary>
        /// Does nothing.
        /// </summary>
        public void ShowBanner()
        {
        }

        /// <summary>
        /// Does nothing.
        /// </summary>
        public void HideBanner()
        {
        }

        /// <summary>
        /// The SDK reports that the ad started.
        /// </summary>
        public void Open()
        {
            _request.NotifyStarted();
        }

        /// <summary>
        /// The SDK reports the reward.
        /// </summary>
        public void Reward()
        {
            _request.NotifyRewardEarned();
        }

        /// <summary>
        /// The SDK answers the request; ignored when the tracker has released it.
        /// </summary>
        public void Answer(AdResult result)
        {
            _request.Complete(result);
        }

        private void Request(AdType adType, Action<AdResult> callback)
        {
            if (!_request.TryBegin(adType, callback))
            {
                callback?.Invoke(AdResult.InProgress);
                return;
            }

            SdkRequestCount++;
        }
    }
}
