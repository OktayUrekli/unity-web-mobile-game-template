using System;
using System.Text.RegularExpressions;
using _Core.Events;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace _Core.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="EventBus"/>.
    /// </summary>
    public class EventBusTests
    {
        private readonly struct TestEvent : IGameEvent
        {
            public readonly int Value;

            public TestEvent(int value)
            {
                Value = value;
            }
        }

        private readonly struct OtherTestEvent : IGameEvent
        {
        }

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
        }

        [Test]
        public void Publish_DeliversEventToSubscriber()
        {
            int received = 0;
            int callCount = 0;
            Action<TestEvent> handler = e =>
            {
                received = e.Value;
                callCount++;
            };

            EventBus.Subscribe(handler);
            EventBus.Publish(new TestEvent(42));

            Assert.AreEqual(1, callCount);
            Assert.AreEqual(42, received);
        }

        [Test]
        public void Publish_DeliversToAllSubscribersOfThatTypeOnly()
        {
            int firstCount = 0;
            int secondCount = 0;
            int otherCount = 0;
            Action<TestEvent> first = _ => firstCount++;
            Action<TestEvent> second = _ => secondCount++;
            Action<OtherTestEvent> other = _ => otherCount++;

            EventBus.Subscribe(first);
            EventBus.Subscribe(second);
            EventBus.Subscribe(other);
            EventBus.Publish(new TestEvent(1));

            Assert.AreEqual(1, firstCount);
            Assert.AreEqual(1, secondCount);
            Assert.AreEqual(0, otherCount);
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EventBus.Publish(new TestEvent(1)));
        }

        [Test]
        public void Unsubscribe_StopsDelivery_AndKeepsOtherHandlers()
        {
            int removedCount = 0;
            int keptCount = 0;
            Action<TestEvent> removed = _ => removedCount++;
            Action<TestEvent> kept = _ => keptCount++;

            EventBus.Subscribe(removed);
            EventBus.Subscribe(kept);
            EventBus.Unsubscribe(removed);
            EventBus.Publish(new TestEvent(1));

            Assert.AreEqual(0, removedCount);
            Assert.AreEqual(1, keptCount);
        }

        [Test]
        public void Unsubscribe_LastHandler_LeavesNoSubscriber()
        {
            int callCount = 0;
            Action<TestEvent> handler = _ => callCount++;

            EventBus.Subscribe(handler);
            EventBus.Unsubscribe(handler);

            Assert.AreEqual(0, EventBus.SubscriberCount<TestEvent>(),
                "Unsubscribing the last handler should leave no handler for the event type.");

            EventBus.Publish(new TestEvent(1));
            Assert.AreEqual(0, callCount);

            // The bus must accept a new subscription for the removed type.
            EventBus.Subscribe(handler);
            EventBus.Publish(new TestEvent(1));
            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void Unsubscribe_HandlerNeverSubscribed_DoesNotThrow()
        {
            Action<TestEvent> handler = _ => { };

            Assert.DoesNotThrow(() => EventBus.Unsubscribe(handler));
        }

        [Test]
        public void Publish_ThrowingHandler_IsLoggedAndOtherHandlersStillRun()
        {
            int beforeCount = 0;
            int afterCount = 0;
            Action<TestEvent> before = _ => beforeCount++;
            Action<TestEvent> throwing = _ => throw new InvalidOperationException("EventBusTests boom");
            Action<TestEvent> after = _ => afterCount++;

            EventBus.Subscribe(before);
            EventBus.Subscribe(throwing);
            EventBus.Subscribe(after);

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: EventBusTests boom"));
            Assert.DoesNotThrow(() => EventBus.Publish(new TestEvent(1)));

            Assert.AreEqual(1, beforeCount);
            Assert.AreEqual(1, afterCount);
        }

        [Test]
        public void Clear_RemovesAllSubscriptions()
        {
            int testCount = 0;
            int otherCount = 0;
            Action<TestEvent> testHandler = _ => testCount++;
            Action<OtherTestEvent> otherHandler = _ => otherCount++;

            EventBus.Subscribe(testHandler);
            EventBus.Subscribe(otherHandler);
            EventBus.Clear();

            EventBus.Publish(new TestEvent(1));
            EventBus.Publish(new OtherTestEvent());

            Assert.AreEqual(0, testCount);
            Assert.AreEqual(0, otherCount);
            Assert.AreEqual(0, EventBus.SubscriberCount<TestEvent>());
            Assert.AreEqual(0, EventBus.SubscriberCount<OtherTestEvent>());
        }

        [Test]
        public void Unsubscribe_DuringPublish_TakesEffectFromTheNextPublish()
        {
            int secondCount = 0;
            Action<TestEvent> second = _ => secondCount++;
            Action<TestEvent> first = _ => EventBus.Unsubscribe(second);

            EventBus.Subscribe(first);
            EventBus.Subscribe(second);

            EventBus.Publish(new TestEvent(1));
            Assert.AreEqual(1, secondCount, "The handler list of a running publish must not change.");

            EventBus.Publish(new TestEvent(2));
            Assert.AreEqual(1, secondCount);
        }

        [Test]
        public void Subscribe_DuringPublish_TakesEffectFromTheNextPublish()
        {
            int lateCount = 0;
            Action<TestEvent> late = _ => lateCount++;
            Action<TestEvent> subscriber = null;
            subscriber = _ =>
            {
                EventBus.Unsubscribe(subscriber);
                EventBus.Subscribe(late);
            };

            EventBus.Subscribe(subscriber);

            EventBus.Publish(new TestEvent(1));
            Assert.AreEqual(0, lateCount);

            EventBus.Publish(new TestEvent(2));
            Assert.AreEqual(1, lateCount);
        }

        [Test]
        public void Subscribe_SameHandlerTwice_IsCalledTwice_AndUnsubscribeRemovesOne()
        {
            int callCount = 0;
            Action<TestEvent> handler = _ => callCount++;

            EventBus.Subscribe(handler);
            EventBus.Subscribe(handler);
            EventBus.Publish(new TestEvent(1));
            Assert.AreEqual(2, callCount);

            EventBus.Unsubscribe(handler);
            EventBus.Publish(new TestEvent(1));
            Assert.AreEqual(3, callCount);
        }

        [Test]
        public void Publish_DoesNotAllocate()
        {
            int sum = 0;
            Action<TestEvent> handler = e => sum += e.Value;
            EventBus.Subscribe(handler);

            // The first publish builds the cached handler array.
            EventBus.Publish(new TestEvent(1));

            Assert.That(() => EventBus.Publish(new TestEvent(1)), Is.Not.AllocatingGCMemory());
        }
    }
}
