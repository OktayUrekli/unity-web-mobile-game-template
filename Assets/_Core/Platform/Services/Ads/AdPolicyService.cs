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
        private readonly Func<float> _clock;

        // Negative infinity until the first interstitial completes, so the first one is never on cooldown.
        private float _lastInterstitialTime = float.NegativeInfinity;
        private bool _isRequestInProgress;

        // Abandons the request in flight once its time is up; null while no request is in flight.
        private Action _checkTimeout;

        /// <summary>
        /// The wrapped platform implementation.
        /// </summary>
        public IAdsService Inner => _inner;

        /// <summary>
        /// Every request goes through this wrapper, so its own flag is the source of truth. The inner flag
        /// is ignored on purpose: after a timeout it may stay set until the SDK answers (or forever).
        /// </summary>
        public bool IsAdShowing => _isRequestInProgress;

        /// <summary>
        /// Seconds until an interstitial may be shown again; 0 when it is allowed now.
        /// </summary>
        public float InterstitialCooldownRemaining =>
            Mathf.Max(0f, _lastInterstitialTime + _interstitialCooldownSeconds - _clock());

        /// <param name="inner">The platform ads service to wrap.</param>
        /// <param name="interstitialCooldownSeconds">Minimum real time between two completed interstitials.</param>
        /// <param name="startTimeoutSeconds">Real seconds an ad may take to start (<see cref="AdStartedEvent"/>).</param>
        /// <param name="playTimeoutSeconds">Real seconds a started ad may run.</param>
        public AdPolicyService(
            IAdsService inner,
            float interstitialCooldownSeconds,
            float startTimeoutSeconds = 30f,
            float playTimeoutSeconds = 180f)
            : this(inner, interstitialCooldownSeconds, startTimeoutSeconds, playTimeoutSeconds, GetRealtime)
        {
        }

        /// <summary>
        /// Test seam: <paramref name="clock"/> returns the current real time in seconds in place of
        /// <see cref="Time.realtimeSinceStartup"/>, so EditMode tests move time instead of waiting.
        /// </summary>
        internal AdPolicyService(
            IAdsService inner,
            float interstitialCooldownSeconds,
            float startTimeoutSeconds,
            float playTimeoutSeconds,
            Func<float> clock)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
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

        /// <summary>
        /// Test seam: runs the watchdog's per-frame check now. It abandons the request in flight with
        /// <see cref="AdResult.TimedOut"/> when the ad has not started within the start timeout or not finished within
        /// the play timeout, measured on the clock the service was created with.
        /// </summary>
        internal void CheckTimeout()
        {
            _checkTimeout?.Invoke();
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
            float requestedAt = _clock();
            float startedAt = 0f;

            void OnAdStarted(AdStartedEvent gameEvent)
            {
                if (started)
                    return;

                started = true;
                startedAt = _clock();
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
                _checkTimeout = null;
                EventBus.Unsubscribe<AdStartedEvent>(OnAdStarted);

                if (adType == AdType.Interstitial && result == AdResult.Completed)
                    _lastInterstitialTime = _clock();

                // EventBus isolates handler exceptions; the callback always runs afterwards.
                EventBus.Publish(new AdRequestCompletedEvent(adType, result));
                SafeInvoke(callback, result);
            }

            void TimeOutIfDue()
            {
                if (completed)
                    return;

                float now = _clock();
                bool isTimedOut = started
                    ? now - startedAt >= _playTimeoutSeconds
                    : now - requestedAt >= _startTimeoutSeconds;

                if (!isTimedOut)
                    return;

                Debug.LogWarning(
                    $"Ads: {adType} request timed out ({(started ? "ad started but never finished" : "ad never started")}). " +
                    "Continuing without it.");

                // The platform would have published this when the ad ended. PauseManager resumes on the
                // AdRequestCompletedEvent that follows.
                if (started)
                    EventBus.Publish(new AdFinishedEvent(false));

                OnInnerResult(AdResult.TimedOut);
            }

            // Platform bridges publish AdStartedEvent when the ad actually begins (AdRequestTracker.NotifyStarted).
            EventBus.Subscribe<AdStartedEvent>(OnAdStarted);

            // Set before the platform call: an answer inside it clears the check, and a request made from the
            // callback sets its own.
            _checkTimeout = TimeOutIfDue;

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
                _ = WatchdogAsync(() => completed, TimeOutIfDue);
        }

        /// <summary>
        /// Runs <paramref name="timeOutIfDue"/> once per frame until the request completes. Uses <see cref="Task.Yield"/>
        /// instead of <c>Task.Delay</c>, which needs threads that WebGL does not have.
        /// </summary>
        private static async Task WatchdogAsync(Func<bool> isCompleted, Action timeOutIfDue)
        {
            try
            {
                while (!isCompleted())
                {
                    timeOutIfDue();
                    await Task.Yield();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static float GetRealtime()
        {
            return Time.realtimeSinceStartup;
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
