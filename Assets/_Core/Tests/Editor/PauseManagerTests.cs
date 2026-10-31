using System.Collections.Generic;
using _Core.Events;
using _Core.Events.Ads;
using _Core.Events.Gameplay;
using _Core.Events.Platform;
using _Core.Gameplay;
using _Core.Platform.Core;
using _Core.Platform.Services.Ads;
using _Core.Tests.Fakes;
using NUnit.Framework;
using UnityEngine;

namespace _Core.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="PauseManager"/> and how <see cref="GameplayStateManager"/> follows it. Awake does
    /// not run in Edit Mode, so the managers are registered with <c>Singleton.SetInstanceForTests</c>;
    /// PlatformManager.Instance is cleared so no platform is called. The time scale is restored in TearDown.
    /// </summary>
    public class PauseManagerTests
    {
        private static readonly PauseSource TutorialSource = new("Tutorial");

        private readonly List<bool> _pauseEvents = new();
        private readonly List<bool> _gameplayEvents = new();
        private GameObject _gameObject;
        private PauseManager _pauseManager;
        private GameplayStateManager _gameplayStateManager;
        private PauseManager _previousPauseManager;
        private GameplayStateManager _previousGameplayStateManager;
        private PlatformManager _previousPlatformManager;
        private float _previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _pauseEvents.Clear();
            _gameplayEvents.Clear();
            _previousTimeScale = Time.timeScale;
            _previousPauseManager = PauseManager.Instance;
            _previousGameplayStateManager = GameplayStateManager.Instance;
            _previousPlatformManager = PlatformManager.Instance;
            PlatformManager.SetInstanceForTests(null);

            _gameObject = new GameObject(nameof(PauseManagerTests)) { hideFlags = HideFlags.HideAndDontSave };
            _pauseManager = _gameObject.AddComponent<PauseManager>();
            _pauseManager.Init();
            PauseManager.SetInstanceForTests(_pauseManager);

            _gameplayStateManager = _gameObject.AddComponent<GameplayStateManager>();
            _gameplayStateManager.Init();
            GameplayStateManager.SetInstanceForTests(_gameplayStateManager);

            EventBus.Subscribe<PauseStateChangedEvent>(OnPauseStateChanged);
            EventBus.Subscribe<GameplayStateChangedEvent>(OnGameplayStateChanged);
        }

        [TearDown]
        public void TearDown()
        {
            // OnDestroy does not run in Edit Mode; drop the scene subscriptions Init made.
            _pauseManager.Shutdown();
            _gameplayStateManager.Shutdown();
            EventBus.Clear();
            PauseManager.SetInstanceForTests(_previousPauseManager);
            GameplayStateManager.SetInstanceForTests(_previousGameplayStateManager);
            PlatformManager.SetInstanceForTests(_previousPlatformManager);

            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);

            Time.timeScale = _previousTimeScale;
        }

        [Test]
        public void PauseAndResume_DriveTheTimeScale_AndPublishEachChange()
        {
            _pauseManager.Pause(PauseSource.Menu);
            Assert.IsTrue(_pauseManager.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);

            _pauseManager.Resume(PauseSource.Menu);
            Assert.IsFalse(_pauseManager.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);

            CollectionAssert.AreEqual(new[] { true, false }, _pauseEvents);
        }

        [Test]
        public void TwoSources_GameResumesOnlyWhenBothRelease()
        {
            _pauseManager.Pause(PauseSource.Menu);
            _pauseManager.Pause(PauseSource.Ad);
            _pauseManager.Resume(PauseSource.Ad);

            Assert.IsTrue(_pauseManager.IsPaused, "The menu still holds a pause.");
            Assert.AreEqual(0f, Time.timeScale);

            _pauseManager.Resume(PauseSource.Menu);
            Assert.IsFalse(_pauseManager.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void SameSourceTwice_IsOnePause_AndUnknownResumeIsIgnored()
        {
            _pauseManager.Pause(PauseSource.Menu);
            _pauseManager.Pause(PauseSource.Menu);
            _pauseManager.Resume(PauseSource.Ad);

            CollectionAssert.AreEqual(new[] { true }, _pauseEvents);

            _pauseManager.Resume(PauseSource.Menu);
            Assert.IsFalse(_pauseManager.IsPaused);
        }

        [Test]
        public void TimeScale_AppliesWhileRunning_AndComesBackAfterAPause()
        {
            _pauseManager.TimeScale = 0.5f;
            Assert.AreEqual(0.5f, Time.timeScale);

            _pauseManager.Pause(PauseSource.Menu);
            Assert.AreEqual(0f, Time.timeScale);

            _pauseManager.Resume(PauseSource.Menu);
            Assert.AreEqual(0.5f, Time.timeScale);
        }

        [Test]
        public void AdRequest_PausesUntilTheRequestCompletes()
        {
            EventBus.Publish(new AdRequestedEvent(AdType.Interstitial));
            Assert.IsTrue(_pauseManager.IsPausedBy(PauseSource.Ad));

            EventBus.Publish(new AdRequestCompletedEvent(AdType.Interstitial, AdResult.TimedOut));
            Assert.IsFalse(_pauseManager.IsPaused);
        }

        [Test]
        public void ApplicationPause_PausesUntilTheAppReturns()
        {
            EventBus.Publish(new ApplicationPauseChangedEvent(true));
            Assert.IsTrue(_pauseManager.IsPausedBy(PauseSource.Application));

            EventBus.Publish(new ApplicationPauseChangedEvent(false));
            Assert.IsFalse(_pauseManager.IsPaused);
        }

        [Test]
        public void SceneChange_ReleasesSceneSources_AndKeepsPersistentOnes()
        {
            var tutorial = new PauseSource("Tutorial");
            _pauseManager.Pause(PauseSource.Menu);
            _pauseManager.Pause(tutorial);
            _pauseManager.Pause(PauseSource.Ad);

            _pauseManager.ReleaseSceneSources();

            Assert.IsFalse(_pauseManager.IsPausedBy(PauseSource.Menu));
            Assert.IsFalse(_pauseManager.IsPausedBy(tutorial));
            Assert.IsTrue(_pauseManager.IsPausedBy(PauseSource.Ad));
            Assert.IsTrue(_pauseManager.IsPaused);
        }

        [Test]
        public void Gameplay_IsReportedStoppedWhileAMenuPauses_ButNotForAds()
        {
            _gameplayStateManager.BeginGameplay();
            Assert.IsTrue(_gameplayStateManager.IsPlaying);

            // Ads: the platform SDK reports gameplay around ads itself.
            _pauseManager.Pause(PauseSource.Ad);
            Assert.IsTrue(_gameplayStateManager.IsPlaying);
            _pauseManager.Resume(PauseSource.Ad);

            _pauseManager.Pause(PauseSource.Menu);
            Assert.IsFalse(_gameplayStateManager.IsPlaying);
            Assert.IsTrue(_gameplayStateManager.IsGameplayActive, "A pause does not end the session.");

            _pauseManager.Resume(PauseSource.Menu);
            Assert.IsTrue(_gameplayStateManager.IsPlaying);

            _gameplayStateManager.EndGameplay();
            _gameplayStateManager.EndGameplay();

            CollectionAssert.AreEqual(new[] { true, false, true, false }, _gameplayEvents);
        }

        [Test]
        public void BeginGameplay_WhilePaused_IsReportedOnResume()
        {
            _pauseManager.Pause(PauseSource.Menu);
            _gameplayStateManager.BeginGameplay();

            Assert.IsFalse(_gameplayStateManager.IsPlaying);
            CollectionAssert.IsEmpty(_gameplayEvents);

            _pauseManager.Resume(PauseSource.Menu);
            CollectionAssert.AreEqual(new[] { true }, _gameplayEvents);
        }

        [Test]
        public void PauseHandler_FollowsTheLiveState_WhenAnEarlierHandlerPausesAgainDuringThePublish()
        {
            _pauseManager.Pause(PauseSource.Menu);

            // Subscribed before the handler: pauses again as soon as the menu releases, inside the same publish, so
            // the handler then receives the nested 'true' before the outer, stale 'false'.
            EventBus.Subscribe<PauseStateChangedEvent>(PauseAgainOnResume);

            var handlerObject = new GameObject(nameof(FakePauseHandler)) { hideFlags = HideFlags.HideAndDontSave };
            FakePauseHandler handler = handlerObject.AddComponent<FakePauseHandler>();

            try
            {
                handler.Enable();
                Assert.AreEqual(1, handler.PauseCount, "A handler enabled during a pause is paused at once.");

                _pauseManager.Resume(PauseSource.Menu);

                Assert.IsTrue(_pauseManager.IsPaused, "The tutorial source still holds a pause.");
                Assert.IsTrue(handler.HandlerIsPaused, "The handler must follow the live pause state, not the stale event value.");
                Assert.AreEqual(0, handler.ResumeCount);
            }
            finally
            {
                handler.Disable();
                Object.DestroyImmediate(handlerObject);
            }
        }

        private void PauseAgainOnResume(PauseStateChangedEvent gameEvent)
        {
            if (!gameEvent.IsPaused)
                _pauseManager.Pause(TutorialSource);
        }

        private void OnPauseStateChanged(PauseStateChangedEvent gameEvent)
        {
            _pauseEvents.Add(gameEvent.IsPaused);
        }

        private void OnGameplayStateChanged(GameplayStateChangedEvent gameEvent)
        {
            _gameplayEvents.Add(gameEvent.IsActive);
        }
    }
}
