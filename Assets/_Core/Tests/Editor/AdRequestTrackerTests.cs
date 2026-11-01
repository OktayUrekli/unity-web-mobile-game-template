using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using _Core.Events;
using _Core.Events.Ads;
using _Core.Platform.Services.Ads;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _Core.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="AdRequestTracker"/>, the in-flight request of the platform ad bridges. The test
    /// plays both the SDK (Notify*, Complete) and <see cref="AdPolicyService"/>'s timeout (an
    /// <see cref="AdRequestCompletedEvent"/> with <see cref="AdResult.TimedOut"/>).
    /// </summary>
    public class AdRequestTrackerTests
    {
        private readonly List<string> _log = new();
        private int _timedOutCount;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _log.Clear();
            _timedOutCount = 0;
            EventBus.Subscribe<AdStartedEvent>(OnAdStarted);
            EventBus.Subscribe<AdFinishedEvent>(OnAdFinished);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
        }

        [Test]
        public void TryBegin_WhenIdle_StartsRequest_AndSubscribesForTimeout()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);

            Assert.IsTrue(tracker.TryBegin(AdType.Rewarded, RecordCallback));

            Assert.IsTrue(tracker.IsActive);
            Assert.AreEqual(AdType.Rewarded, tracker.AdType);
            Assert.IsTrue(tracker.IsCurrent(tracker.Id));
            Assert.AreEqual(1, EventBus.SubscriberCount<AdRequestCompletedEvent>());
            CollectionAssert.IsEmpty(_log);
        }

        [Test]
        public void TryBegin_WhileActive_ReturnsFalse_AndKeepsFirstRequest()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            tracker.TryBegin(AdType.Rewarded, RecordCallback);
            int firstId = tracker.Id;

            Assert.IsFalse(tracker.TryBegin(AdType.Interstitial, _ => _log.Add("SecondCallback")));

            Assert.AreEqual(firstId, tracker.Id);
            Assert.AreEqual(AdType.Rewarded, tracker.AdType);
            tracker.Complete(AdResult.Completed);
            CollectionAssert.AreEqual(new[] { "Callback:Completed" }, _log);
        }

        [Test]
        public void Complete_BeforeStart_InvokesCallbackOnly()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            tracker.TryBegin(AdType.Interstitial, RecordCallback);

            Assert.IsTrue(tracker.Complete(AdResult.Failed));

            CollectionAssert.AreEqual(new[] { "Callback:Failed" }, _log);
            Assert.IsFalse(tracker.IsActive);
            Assert.AreEqual(0, EventBus.SubscriberCount<AdRequestCompletedEvent>());
        }

        [Test]
        public void StartedAd_PublishesStarted_ThenFinished_ThenCallback()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            tracker.TryBegin(AdType.Rewarded, RecordCallback);

            Assert.IsTrue(tracker.NotifyStarted());
            tracker.Complete(AdResult.Completed);

            CollectionAssert.AreEqual(new[] { "Started", "Finished:True", "Callback:Completed" }, _log);
        }

        [Test]
        public void StartedAd_ThatFails_PublishesFinishedFalse()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            tracker.TryBegin(AdType.Interstitial, RecordCallback);

            tracker.NotifyStarted();
            tracker.Complete(AdResult.Failed);

            CollectionAssert.AreEqual(new[] { "Started", "Finished:False", "Callback:Failed" }, _log);
        }

        [Test]
        public void NotifyStarted_Twice_PublishesOneStartedEvent()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            tracker.TryBegin(AdType.Rewarded, RecordCallback);

            tracker.NotifyStarted();
            Assert.IsFalse(tracker.NotifyStarted());
            tracker.Complete(AdResult.Completed);

            CollectionAssert.AreEqual(new[] { "Started", "Finished:True", "Callback:Completed" }, _log);
        }

        [Test]
        public void SdkCallbacks_WhileIdle_AreIgnored()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);

            Assert.IsFalse(tracker.NotifyStarted());
            tracker.NotifyRewardEarned();
            Assert.IsFalse(tracker.Complete(AdResult.Completed));

            CollectionAssert.IsEmpty(_log);
            Assert.IsFalse(tracker.IsRewardEarned);
        }

        [Test]
        public void Complete_Twice_InvokesCallbackOnce()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            tracker.TryBegin(AdType.Rewarded, RecordCallback);
            tracker.NotifyStarted();

            tracker.Complete(AdResult.Completed);
            Assert.IsFalse(tracker.Complete(AdResult.Failed));

            CollectionAssert.AreEqual(new[] { "Started", "Finished:True", "Callback:Completed" }, _log);
        }

        [Test]
        public void Complete_ReleasesBeforeFinishedEventAndCallback_SoTheCallbackCanRequestAgain()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            bool activeInFinishedHandler = true;
            bool secondBegun = false;
            EventBus.Subscribe<AdFinishedEvent>(_ => activeInFinishedHandler = tracker.IsActive);

            tracker.TryBegin(AdType.Rewarded, _ => secondBegun = tracker.TryBegin(AdType.Interstitial, RecordCallback));
            tracker.NotifyStarted();
            tracker.Complete(AdResult.Completed);

            Assert.IsFalse(activeInFinishedHandler);
            Assert.IsTrue(secondBegun);
            Assert.IsTrue(tracker.IsActive);
            Assert.AreEqual(AdType.Interstitial, tracker.AdType);
            Assert.IsFalse(tracker.HasStarted, "The new request must not inherit the started state.");
        }

        [Test]
        public void ThrowingCallback_IsLogged_AndRequestIsReleased()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            tracker.TryBegin(AdType.Rewarded, _ => throw new InvalidOperationException("Callback boom"));

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Callback boom"));
            Assert.DoesNotThrow(() => tracker.Complete(AdResult.Completed));

            Assert.IsFalse(tracker.IsActive);
        }

        [Test]
        public void RewardEarned_IsTrackedPerRequest()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            tracker.TryBegin(AdType.Rewarded, RecordCallback);

            tracker.NotifyRewardEarned();
            Assert.IsTrue(tracker.IsRewardEarned);
            tracker.Complete(AdResult.Completed);

            tracker.TryBegin(AdType.Rewarded, RecordCallback);
            Assert.IsFalse(tracker.IsRewardEarned, "A new request must not inherit the reward.");
        }

        [Test]
        public void IsCurrent_IsFalseForAnOlderRequest_AndAfterRelease()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            tracker.TryBegin(AdType.Rewarded, RecordCallback);
            int firstId = tracker.Id;
            tracker.Complete(AdResult.Failed);

            Assert.IsFalse(tracker.IsCurrent(firstId));

            tracker.TryBegin(AdType.Rewarded, RecordCallback);
            Assert.IsFalse(tracker.IsCurrent(firstId));
            Assert.IsTrue(tracker.IsCurrent(tracker.Id));
        }

        [Test]
        public void TimeoutAfterStart_KeepsRequest_AndLateAnswerPublishesNoSecondFinish()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            tracker.TryBegin(AdType.Rewarded, RecordCallback);
            tracker.NotifyStarted();

            PublishTimeout(AdType.Rewarded);

            Assert.IsTrue(tracker.IsActive, "An ad on screen keeps its request until the SDK answers.");
            Assert.IsTrue(tracker.IsTimedOut);
            Assert.AreEqual(1, _timedOutCount);

            Assert.IsTrue(tracker.Complete(AdResult.Completed));

            // AdPolicyService publishes the finish event of a timed-out started ad itself.
            CollectionAssert.AreEqual(new[] { "Started", "Callback:Completed" }, _log);
            Assert.IsFalse(tracker.IsActive);
            Assert.AreEqual(0, EventBus.SubscriberCount<AdRequestCompletedEvent>());
        }

        [Test]
        public void TimeoutBeforeStart_ReleasesRequest_WhenConfigured()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            tracker.TryBegin(AdType.Interstitial, RecordCallback);

            PublishTimeout(AdType.Interstitial);

            Assert.IsFalse(tracker.IsActive);
            Assert.AreEqual(1, _timedOutCount);
            Assert.AreEqual(0, EventBus.SubscriberCount<AdRequestCompletedEvent>());

            // Late SDK answers for the released request are ignored; AdPolicyService has answered the caller.
            Assert.IsFalse(tracker.NotifyStarted());
            Assert.IsFalse(tracker.Complete(AdResult.Completed));
            CollectionAssert.IsEmpty(_log);
        }

        [Test]
        public void TimeoutBeforeStart_KeepsRequest_WhenConfigured_AndLateStartStillPairsItsEvents()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: false);
            tracker.TryBegin(AdType.Rewarded, RecordCallback);

            PublishTimeout(AdType.Rewarded);

            Assert.IsTrue(tracker.IsActive, "The SDK ignores new requests until it answers this one.");
            Assert.IsFalse(tracker.TryBegin(AdType.Interstitial, RecordCallback));

            Assert.IsTrue(tracker.NotifyStarted());
            tracker.Complete(AdResult.Completed);

            CollectionAssert.AreEqual(new[] { "Started", "Finished:True", "Callback:Completed" }, _log);
        }

        [Test]
        public void TimeoutHook_RunsOncePerRequest()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: false);
            tracker.TryBegin(AdType.Rewarded, RecordCallback);

            PublishTimeout(AdType.Rewarded);
            PublishTimeout(AdType.Rewarded);

            Assert.AreEqual(1, _timedOutCount);
        }

        [Test]
        public void TimeoutHook_SeesTheTimedOutRequest_BeforeItIsReleased()
        {
            AdRequestTracker tracker = null;
            bool activeInHook = false;
            bool startedInHook = true;
            tracker = new AdRequestTracker(true, () =>
            {
                activeInHook = tracker.IsActive;
                startedInHook = tracker.HasStarted;
            });
            tracker.TryBegin(AdType.Rewarded, RecordCallback);

            PublishTimeout(AdType.Rewarded);

            Assert.IsTrue(activeInHook);
            Assert.IsFalse(startedInHook);
            Assert.IsFalse(tracker.IsActive);
        }

        [Test]
        public void ThrowingTimeoutHook_IsLogged_AndUnstartedRequestIsStillReleased()
        {
            var tracker = new AdRequestTracker(true, () => throw new InvalidOperationException("Hook boom"));
            tracker.TryBegin(AdType.Rewarded, RecordCallback);

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Hook boom"));
            PublishTimeout(AdType.Rewarded);

            Assert.IsFalse(tracker.IsActive);
        }

        [Test]
        public void OtherResults_AreIgnored()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            tracker.TryBegin(AdType.Rewarded, RecordCallback);

            EventBus.Publish(new AdRequestCompletedEvent(AdType.Rewarded, AdResult.InProgress));
            EventBus.Publish(new AdRequestCompletedEvent(AdType.Rewarded, AdResult.Failed));

            Assert.IsTrue(tracker.IsActive);
            Assert.IsFalse(tracker.IsTimedOut);
            Assert.AreEqual(0, _timedOutCount);
        }

        [Test]
        public void TimeoutOfARequestThatEndedDuringItsDelivery_IsIgnored()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            Action<AdRequestCompletedEvent> sdkAnswers = null;
            sdkAnswers = _ =>
            {
                EventBus.Unsubscribe(sdkAnswers);
                tracker.Complete(AdResult.Failed);
            };

            // Subscribed before TryBegin, so it ends the request before the tracker's handler runs. EventBus still
            // calls that handler, which unsubscribed during the same publish.
            EventBus.Subscribe(sdkAnswers);
            tracker.TryBegin(AdType.Rewarded, RecordCallback);
            PublishTimeout(AdType.Rewarded);

            Assert.AreEqual(0, _timedOutCount, "The hook must not run for a request that already ended.");
            Assert.IsFalse(tracker.IsTimedOut);

            tracker.TryBegin(AdType.Interstitial, RecordCallback);
            PublishTimeout(AdType.Interstitial);

            Assert.AreEqual(1, _timedOutCount, "The next request's own timeout must still be handled.");
            Assert.IsFalse(tracker.IsActive);
            CollectionAssert.AreEqual(new[] { "Callback:Failed" }, _log);
        }

        [Test]
        public void UnstartedRequest_AfterAStartedAd_PublishesNoFinish()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            tracker.TryBegin(AdType.Rewarded, RecordCallback);
            tracker.NotifyStarted();
            tracker.Complete(AdResult.Completed);

            // For example a fullscreen ad that closes without opening: it must not get the earlier ad's finish.
            tracker.TryBegin(AdType.Interstitial, RecordCallback);
            tracker.Complete(AdResult.Failed);

            CollectionAssert.AreEqual(
                new[] { "Started", "Finished:True", "Callback:Completed", "Callback:Failed" }, _log);
        }

        [Test]
        public void RequestBegunWhileATimeoutIsDelivered_IsNotTimedOutByIt()
        {
            AdRequestTracker tracker = Create(releaseUnstartedOnTimeout: true);
            tracker.TryBegin(AdType.Rewarded, RecordCallback);
            bool secondBegun = false;

            // Subscribed after the tracker, so it runs once the tracker has released the timed-out request.
            EventBus.Subscribe<AdRequestCompletedEvent>(_ =>
                secondBegun = secondBegun || tracker.TryBegin(AdType.Interstitial, RecordCallback));

            PublishTimeout(AdType.Rewarded);

            Assert.IsTrue(secondBegun);
            Assert.IsTrue(tracker.IsActive);
            Assert.AreEqual(AdType.Interstitial, tracker.AdType);
            Assert.IsFalse(tracker.IsTimedOut, "The new request must not take the earlier timeout as its own.");
            Assert.AreEqual(1, _timedOutCount);
        }

        private AdRequestTracker Create(bool releaseUnstartedOnTimeout)
        {
            return new AdRequestTracker(releaseUnstartedOnTimeout, () => _timedOutCount++);
        }

        private static void PublishTimeout(AdType adType)
        {
            EventBus.Publish(new AdRequestCompletedEvent(adType, AdResult.TimedOut));
        }

        private void RecordCallback(AdResult result)
        {
            _log.Add($"Callback:{result}");
        }

        private void OnAdStarted(AdStartedEvent gameEvent)
        {
            _log.Add("Started");
        }

        private void OnAdFinished(AdFinishedEvent gameEvent)
        {
            _log.Add($"Finished:{gameEvent.Success}");
        }
    }
}
