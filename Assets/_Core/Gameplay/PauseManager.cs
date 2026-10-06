using System.Collections.Generic;
using _Core.Events;
using _Core.Events.Ads;
using _Core.Events.Gameplay;
using _Core.Events.Platform;
using _Core.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Core.Gameplay
{
    /// <summary>
    /// The single owner of <see cref="Time.timeScale"/>. The game is paused while at least one
    /// <see cref="PauseSource"/> holds a pause: a menu (<see cref="PauseSource.Menu"/>, set by game code), an ad request
    /// (<see cref="PauseSource.Ad"/>) or the app being in the background / the platform asking for a pause
    /// (<see cref="PauseSource.Application"/>); the last two are tracked here automatically. Every change publishes
    /// <see cref="PauseStateChangedEvent"/>; <see cref="GameplayPauseHandler"/> turns it into Pause/Resume calls and
    /// <see cref="GameplayStateManager"/> reports it to the platform. Sources that do not persist across scenes are
    /// released when the active scene changes, so a pause menu never freezes the next scene.
    /// </summary>
    public class PauseManager : Singleton<PauseManager>
    {
        private readonly List<PauseSource> _sources = new();
        private float _timeScale = 1f;
        private bool _externalChangeLogged;

        /// <summary>
        /// True while at least one source pauses the game.
        /// </summary>
        public bool IsPaused => _sources.Count > 0;

        /// <summary>
        /// The time scale while the game is not paused (1 by default; lower for slow motion). Never write
        /// <see cref="Time.timeScale"/> directly: this manager restores its own value every frame.
        /// </summary>
        public float TimeScale
        {
            get => _timeScale;
            set
            {
                _timeScale = Mathf.Max(0f, value);
                ApplyTimeScale();
            }
        }

        /// <summary>
        /// True while a source that stops platform gameplay pauses the game (any source except <see cref="PauseSource.Ad"/>).
        /// </summary>
        public bool StopsPlatformGameplay
        {
            get
            {
                foreach (PauseSource source in _sources)
                {
                    if (source.StopsPlatformGameplay)
                        return true;
                }

                return false;
            }
        }

        private float TargetTimeScale => IsPaused ? 0f : _timeScale;

        /// <summary>
        /// Subscribes to the ad, app and scene events that pause the game. Called by the Bootstrapper before
        /// <c>AppLifecycleManager</c>, which may report a pause while it initializes.
        /// </summary>
        public void Init()
        {
            EventBus.Subscribe<AdRequestedEvent>(OnAdRequested);
            EventBus.Subscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);
            EventBus.Subscribe<ApplicationPauseChangedEvent>(OnApplicationPauseChanged);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            ApplyTimeScale();
        }

        protected override void OnDestroy()
        {
            Shutdown();
            base.OnDestroy();
        }

        /// <summary>
        /// Undoes <see cref="Init"/>. Runs from <c>OnDestroy</c>; EditMode tests call it directly because
        /// <c>OnDestroy</c> does not run there.
        /// </summary>
        internal void Shutdown()
        {
            EventBus.Unsubscribe<AdRequestedEvent>(OnAdRequested);
            EventBus.Unsubscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);
            EventBus.Unsubscribe<ApplicationPauseChangedEvent>(OnApplicationPauseChanged);
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        /// <summary>
        /// True while <paramref name="source"/> holds a pause.
        /// </summary>
        public bool IsPausedBy(PauseSource source)
        {
            return source != null && _sources.Contains(source);
        }

        /// <summary>
        /// Adds a pause held by <paramref name="source"/>. Pausing again with the same source does nothing.
        /// </summary>
        public void Pause(PauseSource source)
        {
            if (source == null || _sources.Contains(source))
                return;

            _sources.Add(source);
            OnSourcesChanged();
        }

        /// <summary>
        /// Releases the pause held by <paramref name="source"/>. The game runs again once no source holds a pause.
        /// </summary>
        public void Resume(PauseSource source)
        {
            if (source == null || !_sources.Remove(source))
                return;

            OnSourcesChanged();
        }

        /// <summary>
        /// Releases every source that does not persist across scenes. Runs when the active scene changes.
        /// </summary>
        internal void ReleaseSceneSources()
        {
            if (_sources.RemoveAll(source => !source.PersistsAcrossScenes) > 0)
                OnSourcesChanged();
        }

        private void LateUpdate()
        {
            // CrazySDK (pauseGameDuringAd) writes the time scale around ads and can answer after the request was
            // abandoned; game code may write it too. This manager's value wins.
            float target = TargetTimeScale;
            if (Time.timeScale == target)
                return;

            if (!_externalChangeLogged)
            {
                _externalChangeLogged = true;
                Debug.LogWarning(
                    $"PauseManager: Time.timeScale was changed to {Time.timeScale} outside PauseManager; restored to {target}. " +
                    "Use PauseManager.Pause/Resume or PauseManager.TimeScale instead.");
            }

            Time.timeScale = target;
        }

        private void OnSourcesChanged()
        {
            ApplyTimeScale();
            EventBus.Publish(new PauseStateChangedEvent(IsPaused));
        }

        private void ApplyTimeScale()
        {
            Time.timeScale = TargetTimeScale;
        }

        private void OnAdRequested(AdRequestedEvent gameEvent)
        {
            Pause(PauseSource.Ad);
        }

        private void OnAdRequestCompleted(AdRequestCompletedEvent gameEvent)
        {
            Resume(PauseSource.Ad);
        }

        private void OnApplicationPauseChanged(ApplicationPauseChangedEvent gameEvent)
        {
            if (gameEvent.IsPaused)
                Pause(PauseSource.Application);
            else
                Resume(PauseSource.Application);
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            ReleaseSceneSources();
        }
    }
}
