using System;
using System.Threading.Tasks;
using _Core.Events;
using _Core.Events.Ads;
using UnityEngine;

namespace _Core.Platform.Services.Ads
{
    /// <summary>
    /// Wraps the platform <see cref="IAdsService"/> and applies the ad rules in one place:
    /// one full-screen request at a time, an interstitial cooldown, request events that drive
    /// input blocking and muting, and a callback that is always invoked exactly once.
    /// A watchdog abandons a request with <see cref="AdResult.TimedOut"/> when the platform never answers,
    /// so a lost SDK callback cannot leave the game blocked.
    /// </summary>
    public class AdPolicyService : IAdsService
    {
        private readonly IAdsService _inner;
        private readonly float _interstitialCooldownSeconds;
        private readonly float _startTimeoutSeconds;
        private readonly float _playTimeoutSeconds;

        // Negative infinity until the first interstitial completes, so the first one is never on cooldown.
        private float _lastInterstitialTime = float.NegativeInfinity;
        private bool _isRequestInProgress;

        /// <summary>
        /// The wrapped platform implementation.
        /// </summary>
        public IAdsService Inner => _inner;

        // Every request goes through this wrapper, so its own flag is the source of truth. The inner flag
        // is ignored on purpose: after a timeout it may stay set until the SDK answers (or forever).
        public bool IsAdShowing => _isRequestInProgress;

        /// <summary>
        /// Seconds until an interstitial may be shown again; 0 when it is allowed now.
        /// </summary>
        public float InterstitialCooldownRemaining =>
            Mathf.Max(0f, _lastInterstitialTime + _interstitialCooldownSeconds - Time.realtimeSinceStartup);

        /// <param name="inner">The platform ads service to wrap.</param>
        /// <param name="interstitialCooldownSeconds">Minimum real time between two completed interstitials.</param>
        /// <param name="startTimeoutSeconds">Real seconds an ad may take to start (<see cref="AdStartedEvent"/>).</param>
        /// <param name="playTimeoutSeconds">Real seconds a started ad may run.</param>
        public AdPolicyService(
            IAdsService inner,
            float interstitialCooldownSeconds,
            float startTimeoutSeconds = 30f,
            float playTimeoutSeconds = 180f)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _interstitialCooldownSeconds = Mathf.Max(0f, interstitialCooldownSeconds);
            _startTimeoutSeconds = Mathf.Max(1f, startTimeoutSeconds);
            _playTimeoutSeconds = Mathf.Max(1f, playTimeoutSeconds);
        }

        public void ShowRewardedAd(Action<AdResult> callback)
        {
            Request(AdType.Rewarded, callback);
        }

        public void ShowInterstitialAd(Action<AdResult> callback)
        {
            float remaining = InterstitialCooldownRemaining;
            if (remaining > 0f)
            {
                Debug.Log($"Ads: Interstitial skipped, cooldown {remaining:0}s remaining.");
                SafeInvoke(callback, AdResult.Cooldown);
                return;
            }

            Request(AdType.Interstitial, callback);
        }

        public void PrefetchAd(AdType adType)
        {
            if (IsAdShowing)
                return;

            try
            {
                _inner.PrefetchAd(adType);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public void HasAdblock(Action<bool> callback)
        {
            _inner.HasAdblock(callback);
        }

        public void ShowBanner()
        {
            _inner.ShowBanner();
        }

        public void HideBanner()
        {
            _inner.HideBanner();
        }

        private void Request(AdType adType, Action<AdResult> callback)
        {
            if (IsAdShowing)
            {
                Debug.Log($"Ads: {adType} request ignored, another ad is in progress.");
                SafeInvoke(callback, AdResult.InProgress);
                return;
            }

            _isRequestInProgress = true;
            EventBus.Publish(new AdRequestedEvent(adType));

            bool completed = false;
            bool started = false;
            float requestedAt = Time.realtimeSinceStartup;
            float startedAt = 0f;

            void OnAdStarted(AdStartedEvent gameEvent)
            {
                if (started)
                    return;

                started = true;
                startedAt = Time.realtimeSinceStartup;
            }

            void OnInnerResult(AdResult result)
            {
                // Guard against platforms that call back twice, or after the watchdog gave up.
                if (completed)
                {
                    if (result != AdResult.InProgress)
                        Debug.Log($"Ads: late {adType} result {result} ignored (request already finished).");
                    return;
                }

                completed = true;
                _isRequestInProgress = false;
                EventBus.Unsubscribe<AdStartedEvent>(OnAdStarted);

                if (adType == AdType.Interstitial && result == AdResult.Completed)
                    _lastInterstitialTime = Time.realtimeSinceStartup;

                // EventBus isolates handler exceptions; the callback always runs afterwards.
                EventBus.Publish(new AdRequestCompletedEvent(adType, result));
                SafeInvoke(callback, result);
            }

            bool IsTimedOut()
            {
                float now = Time.realtimeSinceStartup;
                return started
                    ? now - startedAt >= _playTimeoutSeconds
                    : now - requestedAt >= _startTimeoutSeconds;
            }

            void OnTimedOut()
            {
                Debug.LogWarning(
                    $"Ads: {adType} request timed out ({(started ? "ad started but never finished" : "ad never started")}). " +
                    "Continuing without it.");

                // The platform would have published this when the ad ended. PauseManager resumes on the
                // AdRequestCompletedEvent that follows.
                if (started)
                    EventBus.Publish(new AdFinishedEvent(false));

                OnInnerResult(AdResult.TimedOut);
            }

            // Platform bridges publish AdStartedEvent when the ad actually begins (see the platform-integration skill).
            EventBus.Subscribe<AdStartedEvent>(OnAdStarted);

            try
            {
                if (adType == AdType.Rewarded)
                    _inner.ShowRewardedAd(OnInnerResult);
                else
                    _inner.ShowInterstitialAd(OnInnerResult);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                OnInnerResult(AdResult.Failed);
            }

            if (!completed)
                _ = WatchdogAsync(() => completed, IsTimedOut, OnTimedOut);
        }

        /// <summary>
        /// Polls once per frame until the request completes or times out. Uses <see cref="Task.Yield"/>
        /// instead of <c>Task.Delay</c>, which needs threads that WebGL does not have.
        /// </summary>
        private static async Task WatchdogAsync(Func<bool> isCompleted, Func<bool> isTimedOut, Action onTimedOut)
        {
            try
            {
                while (!isCompleted())
                {
                    if (isTimedOut())
                    {
                        onTimedOut();
                        return;
                    }

                    await Task.Yield();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void SafeInvoke(Action<AdResult> callback, AdResult result)
        {
            try
            {
                callback?.Invoke(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
