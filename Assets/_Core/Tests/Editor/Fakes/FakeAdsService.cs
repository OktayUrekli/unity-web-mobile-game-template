using System;
using System.Collections.Generic;
using _Core.Platform.Services.Ads;

namespace _Core.Tests.Fakes
{
    /// <summary>
    /// <see cref="IAdsService"/> test double that stores every ad callback so the test decides
    /// when (and how often) the "platform" answers.
    /// </summary>
    public class FakeAdsService : IAdsService
    {
        private readonly List<Action<AdResult>> _pendingCallbacks = new();

        /// <summary>
        /// Exception thrown by the next Show* call, or null to behave normally.
        /// </summary>
        public Exception ThrowOnShow { get; set; }

        /// <summary>
        /// Number of <see cref="ShowRewardedAd"/> calls that reached this service.
        /// </summary>
        public int RewardedRequestCount { get; private set; }

        /// <summary>
        /// Number of <see cref="ShowInterstitialAd"/> calls that reached this service.
        /// </summary>
        public int InterstitialRequestCount { get; private set; }

        /// <summary>
        /// The callback of the most recent Show* call, or null when none was made.
        /// </summary>
        public Action<AdResult> LastCallback { get; private set; }

        /// <summary>
        /// Always false; <see cref="AdPolicyService"/> tracks its own in-progress flag.
        /// </summary>
        public bool IsAdShowing => false;

        /// <summary>
        /// Records the request and stores its callback.
        /// </summary>
        public void ShowRewardedAd(Action<AdResult> callback)
        {
            RewardedRequestCount++;
            Store(callback);
        }

        /// <summary>
        /// Records the request and stores its callback.
        /// </summary>
        public void ShowInterstitialAd(Action<AdResult> callback)
        {
            InterstitialRequestCount++;
            Store(callback);
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
        /// Invokes the most recent callback with <paramref name="result"/>. It stays stored, so
        /// calling this again simulates a platform that calls back twice.
        /// </summary>
        public void Answer(AdResult result)
        {
            if (LastCallback == null)
                throw new InvalidOperationException("No ad was requested from FakeAdsService.");

            LastCallback(result);
        }

        /// <summary>
        /// Answers every stored callback with <paramref name="result"/> and forgets them.
        /// Used in TearDown so no <see cref="AdPolicyService"/> watchdog keeps polling after a test.
        /// </summary>
        public void AnswerAllPending(AdResult result)
        {
            Action<AdResult>[] callbacks = _pendingCallbacks.ToArray();
            _pendingCallbacks.Clear();

            foreach (Action<AdResult> callback in callbacks)
                callback?.Invoke(result);
        }

        private void Store(Action<AdResult> callback)
        {
            if (ThrowOnShow != null)
                throw ThrowOnShow;

            LastCallback = callback;
            _pendingCallbacks.Add(callback);
        }
    }
}
