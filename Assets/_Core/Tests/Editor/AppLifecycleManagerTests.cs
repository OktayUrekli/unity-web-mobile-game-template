using System.Collections.Generic;
using System.Reflection;
using _Core.Events;
using _Core.Events.Platform;
using _Core.Managers;
using _Core.Platform.Core;
using NUnit.Framework;
using UnityEngine;

namespace _Core.Tests
{
    /// <summary>
    /// EditMode tests for how <see cref="AppLifecycleManager"/> combines the app's own pause (background, focus)
    /// with the platform's pause request into one <see cref="ApplicationPauseChangedEvent"/> stream. Unity's
    /// OnApplicationPause message is invoked through reflection; PlatformManager.Instance is cleared so no ad is showing.
    /// </summary>
    public class AppLifecycleManagerTests
    {
        private readonly List<bool> _pauseEvents = new();
        private PlatformManager _previousPlatformManager;
        private GameObject _gameObject;
        private AppLifecycleManager _manager;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _pauseEvents.Clear();
            _previousPlatformManager = PlatformManager.Instance;
            PlatformManager.SetInstanceForTests(null);

            _gameObject = new GameObject(nameof(AppLifecycleManagerTests)) { hideFlags = HideFlags.HideAndDontSave };
            _manager = _gameObject.AddComponent<AppLifecycleManager>();
            _manager.Init();
            EventBus.Subscribe<ApplicationPauseChangedEvent>(OnPauseChanged);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
            PlatformManager.SetInstanceForTests(_previousPlatformManager);

            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void PlatformPause_PublishesPauseAndResume()
        {
            EventBus.Publish(new PlatformPauseChangedEvent(true));
            Assert.IsTrue(_manager.IsApplicationPaused);

            EventBus.Publish(new PlatformPauseChangedEvent(false));
            Assert.IsFalse(_manager.IsApplicationPaused);

            CollectionAssert.AreEqual(new[] { true, false }, _pauseEvents);
        }

        [Test]
        public void PlatformPause_WhileAppIsInBackground_StaysPausedUntilBothResume()
        {
            SendApplicationPause(true);
            EventBus.Publish(new PlatformPauseChangedEvent(true));
            SendApplicationPause(false);
            Assert.IsTrue(_manager.IsApplicationPaused, "The platform still asks for a pause.");

            EventBus.Publish(new PlatformPauseChangedEvent(false));

            Assert.IsFalse(_manager.IsApplicationPaused);
            CollectionAssert.AreEqual(new[] { true, false }, _pauseEvents);
        }

        [Test]
        public void RepeatedPlatformPause_PublishesOnce()
        {
            EventBus.Publish(new PlatformPauseChangedEvent(true));
            EventBus.Publish(new PlatformPauseChangedEvent(true));

            CollectionAssert.AreEqual(new[] { true }, _pauseEvents);
        }

        private void SendApplicationPause(bool paused)
        {
            MethodInfo method = typeof(AppLifecycleManager).GetMethod(
                "OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "AppLifecycleManager.OnApplicationPause was renamed; update this test.");
            method.Invoke(_manager, new object[] { paused });
        }

        private void OnPauseChanged(ApplicationPauseChangedEvent gameEvent)
        {
            _pauseEvents.Add(gameEvent.IsPaused);
        }
    }
}
