using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using _Core.Events;
using _Core.Events.Ads;
using _Core.Platform.Services.Ads;
using _Core.Tests.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _Core.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="AdPolicyService"/>. The fake platform answers synchronously when the
    /// test says so. The policy reads a fake clock (<c>_now</c>) that moves only when a test moves it, and
    /// <see cref="AdPolicyService.CheckTimeout"/> runs the watchdog's check at once, so no test waits.
    /// </summary>
    public class AdPolicyServiceTests
    {
        private const float LongCooldownSeconds = 10000f;
        private const float CooldownSeconds = 60f;
        private const float StartTimeoutSeconds = 30f;
        private const float PlayTimeoutSeconds = 180f;

        // Not zero, like Time.realtimeSinceStartup a while after start-up.
        private const float StartTime = 1000f;

        private FakeAdsService _inner;
        private FakeTrackedAdsService _bridge;
        private float _now;
        private readonly List<AdResult> _results = new();
        private readonly List<string> _eventLog = new();

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _inner = new FakeAdsService();
            _bridge = null;
            _now = StartTime;
            _results.Clear();
            _eventLog.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            // Finish anything still pending so the policy's watchdog loop exits.
            _inner.ThrowOnShow = null;
            _inner.AnswerAllPending(AdResult.Failed);
            _bridge?.Answer(AdResult.Failed);

            EventBus.Unsubscribe<AdRequestedEvent>(OnAdRequested);
            EventBus.Unsubscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);
            EventBus.Clear();
        }

        [Test]
        public void Rewarded_CompletedResult_ReachesCallback()
        {
            AdPolicyService policy = CreatePolicy(0f);

            policy.ShowRewardedAd(RecordResult);
            Assert.IsTrue(policy.IsAdShowing, "The request should be in progress until the platform answers.");
            Assert.AreEqual(0, _results.Count);

            _inner.Answer(AdResult.Completed);

            CollectionAssert.AreEqual(new[] { AdResult.Completed }, _results);
            Assert.IsFalse(policy.IsAdShowing);
        }

        [Test]
        public void Callback_InvokedExactlyOnce_WhenInnerCallsBackTwice()
        {
            AdPolicyService policy = CreatePolicy(0f);

            policy.ShowRewardedAd(RecordResult);
            _inner.Answer(AdResult.Completed);
            _inner.Answer(AdResult.Failed);

            CollectionAssert.AreEqual(new[] { AdResult.Completed }, _results);
            Assert.IsFalse(policy.IsAdShowing);
        }

        [Test]
        public void SecondRequest_WhileFirstInProgress_ReturnsInProgress_WithoutReachingInner()
        {
            AdPolicyService policy = CreatePolicy(0f);
            var secondResults = new List<AdResult>();

            policy.ShowRewardedAd(RecordResult);
            policy.ShowInterstitialAd(result => secondResults.Add(result));

            CollectionAssert.AreEqual(new[] { AdResult.InProgress }, secondResults);
            Assert.AreEqual(1, _inner.RewardedRequestCount);
            Assert.AreEqual(0, _inner.InterstitialRequestCount);

            // The first request is unaffected and still finishes normally.
            Assert.IsTrue(policy.IsAdShowing);
            _inner.Answer(AdResult.Completed);
            CollectionAssert.AreEqual(new[] { AdResult.Completed }, _results);
        }

        [Test]
        public void Request_AfterPreviousFinished_IsAllowedAgain()
        {
            AdPolicyService policy = CreatePolicy(0f);

            policy.ShowRewardedAd(RecordResult);
            _inner.Answer(AdResult.Completed);
            policy.ShowRewardedAd(RecordResult);
            _inner.Answer(AdResult.Failed);

            CollectionAssert.AreEqual(new[] { AdResult.Completed, AdResult.Failed }, _results);
            Assert.AreEqual(2, _inner.RewardedRequestCount);
        }

        [Test]
        public void Interstitial_AfterCompletedInterstitial_ReturnsCooldown_WhenCooldownPositive()
        {
            AdPolicyService policy = CreatePolicy(LongCooldownSeconds);

            policy.ShowInterstitialAd(RecordResult);
            _inner.Answer(AdResult.Completed);
            Assert.Greater(policy.InterstitialCooldownRemaining, 0f);

            policy.ShowInterstitialAd(RecordResult);

            CollectionAssert.AreEqual(new[] { AdResult.Completed, AdResult.Cooldown }, _results);
            Assert.AreEqual(1, _inner.InterstitialRequestCount, "A request on cooldown must not reach the platform.");
            Assert.IsFalse(policy.IsAdShowing);
        }

        [Test]
        public void FirstInterstitial_IsNeverOnCooldown()
        {
            AdPolicyService policy = CreatePolicy(LongCooldownSeconds);

            Assert.AreEqual(0f, policy.InterstitialCooldownRemaining);

            policy.ShowInterstitialAd(RecordResult);
            Assert.AreEqual(1, _inner.InterstitialRequestCount);
            _inner.Answer(AdResult.Completed);
        }

        [Test]
        public void Interstitial_AfterCompletedInterstitial_IsAllowed_WhenCooldownZero()
        {
            AdPolicyService policy = CreatePolicy(0f);

            policy.ShowInterstitialAd(RecordResult);
            _inner.Answer(AdResult.Completed);
            Assert.AreEqual(0f, policy.InterstitialCooldownRemaining);

            policy.ShowInterstitialAd(RecordResult);
            Assert.AreEqual(2, _inner.InterstitialRequestCount);
            _inner.Answer(AdResult.Completed);

            CollectionAssert.AreEqual(new[] { AdResult.Completed, AdResult.Completed }, _results);
        }

        [Test]
        public void FailedInterstitial_DoesNotStartCooldown()
        {
            AdPolicyService policy = CreatePolicy(LongCooldownSeconds);

            policy.ShowInterstitialAd(RecordResult);
            _inner.Answer(AdResult.Failed);
            Assert.AreEqual(0f, policy.InterstitialCooldownRemaining);

            policy.ShowInterstitialAd(RecordResult);
            Assert.AreEqual(2, _inner.InterstitialRequestCount);
            _inner.Answer(AdResult.Completed);

            CollectionAssert.AreEqual(new[] { AdResult.Failed, AdResult.Completed }, _results);
        }

        [Test]
        public void Rewarded_IsNotBlockedByInterstitialCooldown()
        {
            AdPolicyService policy = CreatePolicy(LongCooldownSeconds);

            policy.ShowInterstitialAd(RecordResult);
            _inner.Answer(AdResult.Completed);

            policy.ShowRewardedAd(RecordResult);
            Assert.AreEqual(1, _inner.RewardedRequestCount);
            _inner.Answer(AdResult.Completed);

            CollectionAssert.AreEqual(new[] { AdResult.Completed, AdResult.Completed }, _results);
        }

        [Test]
        public void InnerThrows_ReturnsFailed_AndLogsException()
        {
            AdPolicyService policy = CreatePolicy(0f);
            _inner.ThrowOnShow = new InvalidOperationException("FakeAdsService boom");

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: FakeAdsService boom"));
            policy.ShowRewardedAd(RecordResult);

            CollectionAssert.AreEqual(new[] { AdResult.Failed }, _results);
            Assert.IsFalse(policy.IsAdShowing, "A failed request must not leave the policy blocked.");
        }

        [Test]
        public void ThrowingCallback_IsLogged_AndPolicyIsNotBlocked()
        {
            AdPolicyService policy = CreatePolicy(0f);

            policy.ShowRewardedAd(_ => throw new InvalidOperationException("Callback boom"));

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Callback boom"));
            Assert.DoesNotThrow(() => _inner.Answer(AdResult.Completed));
            Assert.IsFalse(policy.IsAdShowing);
        }

        [Test]
        public void Request_PublishesRequestedThenCompletedEvent_BeforeCallback()
        {
            AdPolicyService policy = CreatePolicy(0f);
            EventBus.Subscribe<AdRequestedEvent>(OnAdRequested);
            EventBus.Subscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);

            policy.ShowRewardedAd(result => _eventLog.Add($"Callback:{result}"));
            CollectionAssert.AreEqual(new[] { "Requested:Rewarded" }, _eventLog);

            _inner.Answer(AdResult.Completed);

            CollectionAssert.AreEqual(
                new[] { "Requested:Rewarded", "Completed:Rewarded:Completed", "Callback:Completed" },
                _eventLog);
        }

        [Test]
        public void InnerThrows_StillPublishesCompletedEventWithFailed()
        {
            AdPolicyService policy = CreatePolicy(0f);
            EventBus.Subscribe<AdRequestedEvent>(OnAdRequested);
            EventBus.Subscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);
            _inner.ThrowOnShow = new InvalidOperationException("FakeAdsService boom");

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: FakeAdsService boom"));
            policy.ShowInterstitialAd(RecordResult);

            CollectionAssert.AreEqual(
                new[] { "Requested:Interstitial", "Completed:Interstitial:Failed" },
                _eventLog);
        }

        [Test]
        public void RejectedRequests_PublishNoEvents()
        {
            AdPolicyService policy = CreatePolicy(LongCooldownSeconds);

            policy.ShowInterstitialAd(RecordResult);
            _inner.Answer(AdResult.Completed);

            EventBus.Subscribe<AdRequestedEvent>(OnAdRequested);
            EventBus.Subscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);

            // Cooldown rejection.
            policy.ShowInterstitialAd(RecordResult);
            CollectionAssert.IsEmpty(_eventLog);

            // In-progress rejection: only the accepted first request publishes.
            policy.ShowRewardedAd(RecordResult);
            _eventLog.Clear();
            policy.ShowRewardedAd(RecordResult);
            CollectionAssert.IsEmpty(_eventLog);

            _inner.Answer(AdResult.Completed);
            CollectionAssert.AreEqual(
                new[] { AdResult.Completed, AdResult.Cooldown, AdResult.InProgress, AdResult.Completed },
                _results);
        }

        [Test]
        public void InterstitialCooldown_CountsDownWithTheClock()
        {
            AdPolicyService policy = CreatePolicy(CooldownSeconds);

            policy.ShowInterstitialAd(RecordResult);
            _inner.Answer(AdResult.Completed);
            Assert.AreEqual(CooldownSeconds, policy.InterstitialCooldownRemaining, 0.001f);

            _now += CooldownSeconds - 1f;
            Assert.AreEqual(1f, policy.InterstitialCooldownRemaining, 0.001f);
            policy.ShowInterstitialAd(RecordResult);

            _now += 1f;
            Assert.AreEqual(0f, policy.InterstitialCooldownRemaining);
            policy.ShowInterstitialAd(RecordResult);
            _inner.Answer(AdResult.Completed);

            CollectionAssert.AreEqual(new[] { AdResult.Completed, AdResult.Cooldown, AdResult.Completed }, _results);
            Assert.AreEqual(2, _inner.InterstitialRequestCount);
        }

        [Test]
        public void Cooldown_StartsWhenTheInterstitialCompletes_NotWhenRequested()
        {
            AdPolicyService policy = CreatePolicy(CooldownSeconds);

            policy.ShowInterstitialAd(RecordResult);
            _now += 20f;
            _inner.Answer(AdResult.Completed);

            _now += CooldownSeconds - 1f;
            Assert.AreEqual(1f, policy.InterstitialCooldownRemaining, 0.001f);
        }

        [Test]
        public void TimedOutInterstitial_DoesNotStartCooldown()
        {
            AdPolicyService policy = CreatePolicy(CooldownSeconds);

            policy.ShowInterstitialAd(RecordResult);
            _now += StartTimeoutSeconds;
            policy.CheckTimeout();

            Assert.AreEqual(0f, policy.InterstitialCooldownRemaining);
            CollectionAssert.AreEqual(new[] { AdResult.TimedOut }, _results);
        }

        [Test]
        public void UnstartedRequest_TimesOutAfterStartTimeout()
        {
            AdPolicyService policy = CreatePolicy(0f);
            SubscribeEventLog();

            policy.ShowRewardedAd(RecordResult);
            _now += StartTimeoutSeconds - 0.5f;
            policy.CheckTimeout();
            Assert.IsTrue(policy.IsAdShowing, "Not timed out before the start timeout.");

            _now += 0.5f;
            policy.CheckTimeout();

            CollectionAssert.AreEqual(new[] { AdResult.TimedOut }, _results);
            Assert.IsFalse(policy.IsAdShowing);
            CollectionAssert.AreEqual(new[] { "Requested:Rewarded", "Completed:Rewarded:TimedOut" }, _eventLog,
                "An ad that never started gets no finish event.");
        }

        [Test]
        public void StartedRequest_UsesPlayTimeout_AndPublishesFinishBeforeCompleted()
        {
            AdPolicyService policy = CreatePolicy(0f);
            SubscribeEventLog();

            policy.ShowRewardedAd(RecordResult);
            _now += 5f;
            EventBus.Publish(new AdStartedEvent());

            _now += StartTimeoutSeconds;
            policy.CheckTimeout();
            Assert.IsTrue(policy.IsAdShowing, "A started ad is bound by the play timeout only.");

            _now += PlayTimeoutSeconds - StartTimeoutSeconds;
            policy.CheckTimeout();

            CollectionAssert.AreEqual(new[] { AdResult.TimedOut }, _results);
            CollectionAssert.AreEqual(
                new[] { "Requested:Rewarded", "Started", "Finished:False", "Completed:Rewarded:TimedOut" },
                _eventLog);
        }

        [Test]
        public void LateAnswer_AfterTimeout_IsIgnored_AndPolicyAcceptsTheNextRequest()
        {
            AdPolicyService policy = CreatePolicy(0f);
            SubscribeEventLog();

            policy.ShowRewardedAd(RecordResult);
            _now += StartTimeoutSeconds;
            policy.CheckTimeout();

            LogAssert.Expect(LogType.Log, new Regex("late Rewarded result Completed ignored"));
            _inner.Answer(AdResult.Completed);

            CollectionAssert.AreEqual(new[] { AdResult.TimedOut }, _results, "No reward after a timeout.");
            CollectionAssert.AreEqual(new[] { "Requested:Rewarded", "Completed:Rewarded:TimedOut" }, _eventLog);

            policy.ShowRewardedAd(RecordResult);
            Assert.AreEqual(2, _inner.RewardedRequestCount);
            Assert.IsTrue(policy.IsAdShowing);
        }

        [Test]
        public void CheckTimeout_WithoutRequest_DoesNothing()
        {
            AdPolicyService policy = CreatePolicy(0f);
            SubscribeEventLog();

            _now += 100000f;
            policy.CheckTimeout();

            policy.ShowInterstitialAd(RecordResult);
            _inner.Answer(AdResult.Completed);
            policy.CheckTimeout();

            CollectionAssert.AreEqual(new[] { AdResult.Completed }, _results);
            CollectionAssert.AreEqual(new[] { "Requested:Interstitial", "Completed:Interstitial:Completed" }, _eventLog);
        }

        [Test]
        public void StartedEvent_OutsideARequest_DoesNotCountForTheNextOne()
        {
            AdPolicyService policy = CreatePolicy(0f);

            EventBus.Publish(new AdStartedEvent());
            policy.ShowRewardedAd(RecordResult);
            _now += StartTimeoutSeconds;
            policy.CheckTimeout();

            CollectionAssert.AreEqual(new[] { AdResult.TimedOut }, _results);
        }

        [Test]
        public void Timeouts_BelowOneSecond_AreRaisedToOneSecond()
        {
            var policy = new AdPolicyService(_inner, 0f, 0f, 0f, () => _now);

            policy.ShowRewardedAd(RecordResult);
            _now += 0.5f;
            policy.CheckTimeout();
            Assert.IsTrue(policy.IsAdShowing);

            _now += 0.5f;
            policy.CheckTimeout();
            CollectionAssert.AreEqual(new[] { AdResult.TimedOut }, _results);
        }

        [Test]
        public void Timeout_ReleasesUnstartedBridgeRequest_SoTheNextRequestReachesTheSdk()
        {
            AdPolicyService policy = CreatePolicyOverBridge(releaseUnstartedOnTimeout: true);

            policy.ShowRewardedAd(RecordResult);
            _now += StartTimeoutSeconds;
            policy.CheckTimeout();

            Assert.IsFalse(_bridge.IsAdShowing);
            Assert.AreEqual(1, _bridge.TimedOutCount);

            policy.ShowRewardedAd(RecordResult);
            Assert.AreEqual(2, _bridge.SdkRequestCount);
            _bridge.Answer(AdResult.Completed);

            CollectionAssert.AreEqual(new[] { AdResult.TimedOut, AdResult.Completed }, _results);
        }

        [Test]
        public void Timeout_AfterStart_BridgeHoldsRequest_UntilTheAdCloses()
        {
            AdPolicyService policy = CreatePolicyOverBridge(releaseUnstartedOnTimeout: true);
            SubscribeEventLog();

            policy.ShowRewardedAd(RecordResult);
            _bridge.Open();
            _now += PlayTimeoutSeconds;
            policy.CheckTimeout();

            Assert.IsTrue(_bridge.IsAdShowing, "The ad is still on screen.");
            policy.ShowInterstitialAd(RecordResult);
            Assert.AreEqual(1, _bridge.SdkRequestCount, "The bridge answers InProgress while its ad is on screen.");

            _bridge.Reward();
            LogAssert.Expect(LogType.Log, new Regex("late Rewarded result Completed ignored"));
            _bridge.Answer(AdResult.Completed);

            Assert.IsFalse(_bridge.IsAdShowing);
            CollectionAssert.AreEqual(new[] { AdResult.TimedOut, AdResult.InProgress }, _results);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Requested:Rewarded", "Started", "Finished:False", "Completed:Rewarded:TimedOut",
                    "Requested:Interstitial", "Completed:Interstitial:InProgress"
                },
                _eventLog,
                "One finish event per started ad: the policy's, none from the late close.");
        }

        [Test]
        public void Timeout_BeforeStart_BridgeThatKeepsRequests_PairsTheLateAdsEvents()
        {
            AdPolicyService policy = CreatePolicyOverBridge(releaseUnstartedOnTimeout: false);
            SubscribeEventLog();

            policy.ShowRewardedAd(RecordResult);
            _now += StartTimeoutSeconds;
            policy.CheckTimeout();

            policy.ShowRewardedAd(RecordResult);
            Assert.AreEqual(1, _bridge.SdkRequestCount, "The SDK ignores new requests until it answers.");

            _bridge.Open();
            LogAssert.Expect(LogType.Log, new Regex("late Rewarded result Completed ignored"));
            _bridge.Answer(AdResult.Completed);

            CollectionAssert.AreEqual(new[] { AdResult.TimedOut, AdResult.InProgress }, _results);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Requested:Rewarded", "Completed:Rewarded:TimedOut",
                    "Requested:Rewarded", "Completed:Rewarded:InProgress",
                    "Started", "Finished:True"
                },
                _eventLog);
        }

        [Test]
        public void RequestFromATimedOutCallback_TimesOutFromItsOwnRequestTime()
        {
            AdPolicyService policy = CreatePolicy(0f);
            policy.ShowRewardedAd(result =>
            {
                RecordResult(result);
                policy.ShowInterstitialAd(RecordResult);
            });

            _now += StartTimeoutSeconds;
            policy.CheckTimeout();
            Assert.IsTrue(policy.IsAdShowing, "The interstitial requested from the callback is in flight.");

            _now += StartTimeoutSeconds - 1f;
            policy.CheckTimeout();
            Assert.IsTrue(policy.IsAdShowing, "Its start timeout counts from its own request.");

            _now += 1f;
            policy.CheckTimeout();

            CollectionAssert.AreEqual(new[] { AdResult.TimedOut, AdResult.TimedOut }, _results);
            Assert.IsFalse(policy.IsAdShowing);
        }

        private AdPolicyService CreatePolicy(float interstitialCooldownSeconds)
        {
            return new AdPolicyService(
                _inner, interstitialCooldownSeconds, StartTimeoutSeconds, PlayTimeoutSeconds, () => _now);
        }

        private AdPolicyService CreatePolicyOverBridge(bool releaseUnstartedOnTimeout)
        {
            _bridge = new FakeTrackedAdsService(releaseUnstartedOnTimeout);
            return new AdPolicyService(_bridge, 0f, StartTimeoutSeconds, PlayTimeoutSeconds, () => _now);
        }

        private void SubscribeEventLog()
        {
            EventBus.Subscribe<AdRequestedEvent>(OnAdRequested);
            EventBus.Subscribe<AdStartedEvent>(OnAdStarted);
            EventBus.Subscribe<AdFinishedEvent>(OnAdFinished);
            EventBus.Subscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);
        }

        private void OnAdStarted(AdStartedEvent gameEvent)
        {
            _eventLog.Add("Started");
        }

        private void OnAdFinished(AdFinishedEvent gameEvent)
        {
            _eventLog.Add($"Finished:{gameEvent.Success}");
        }

        private void RecordResult(AdResult result)
        {
            _results.Add(result);
        }

        private void OnAdRequested(AdRequestedEvent gameEvent)
        {
            _eventLog.Add($"Requested:{gameEvent.AdType}");
        }

        private void OnAdRequestCompleted(AdRequestCompletedEvent gameEvent)
        {
            _eventLog.Add($"Completed:{gameEvent.AdType}:{gameEvent.Result}");
        }
    }
}
