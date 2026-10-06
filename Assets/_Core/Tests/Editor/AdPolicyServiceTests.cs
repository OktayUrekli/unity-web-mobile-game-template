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
    /// test says so; no test waits for the real-time watchdog. Time.realtimeSinceStartup barely moves
    /// within a test, so cooldowns are either 0 (always elapsed) or very large (never elapsed).
    /// </summary>
    public class AdPolicyServiceTests
    {
        private const float LongCooldownSeconds = 10000f;

        // Large timeouts so the watchdog of a request left pending by a failing test never fires mid-run.
        private const float LongTimeoutSeconds = 10000f;

        private FakeAdsService _inner;
        private readonly List<AdResult> _results = new();
        private readonly List<string> _eventLog = new();

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _inner = new FakeAdsService();
            _results.Clear();
            _eventLog.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            // Finish anything still pending so the policy's watchdog loop exits.
            _inner.ThrowOnShow = null;
            _inner.AnswerAllPending(AdResult.Failed);

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

        private AdPolicyService CreatePolicy(float interstitialCooldownSeconds)
        {
            return new AdPolicyService(_inner, interstitialCooldownSeconds, LongTimeoutSeconds, LongTimeoutSeconds);
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
