using _Core.Events;
using _Core.Events.Gameplay;
using _Core.Managers;
using _Core.Platform.Core;
using UnityEngine.SceneManagement;

namespace _Core.Gameplay
{
    /// <summary>
    /// Tracks whether the player is actively playing and reports it to the platform
    /// (<c>IGameService.GameplayStart/GameplayStop</c>) exactly once per change.
    /// Call <see cref="BeginGameplay"/> when a level or round starts and <see cref="EndGameplay"/> at game over and
    /// before leaving the level. Pauses need no calls: while <see cref="PauseManager"/> is paused by a source that
    /// stops platform gameplay (a menu, the app in the background), gameplay is reported as stopped and it is
    /// reported as started again on resume. Ads are left to the platform SDK. Changing the active scene ends gameplay.
    /// </summary>
    public class GameplayStateManager : Singleton<GameplayStateManager>
    {
        private bool _isReportedPlaying;

        /// <summary>
        /// True between <see cref="BeginGameplay"/> and <see cref="EndGameplay"/>, paused or not.
        /// </summary>
        public bool IsGameplayActive { get; private set; }

        /// <summary>
        /// True while gameplay is active and not paused by a menu or the app going to the background: what the
        /// platform is told and what <see cref="GameplayStateChangedEvent"/> reports.
        /// </summary>
        public bool IsPlaying => IsGameplayActive && !IsHaltedByPause();

        /// <summary>
        /// Subscribes to scene and pause changes. Called by the Bootstrapper.
        /// </summary>
        public void Init()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            EventBus.Subscribe<PauseStateChangedEvent>(OnPauseStateChanged);
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
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            EventBus.Unsubscribe<PauseStateChangedEvent>(OnPauseStateChanged);
        }

        /// <summary>
        /// Marks gameplay as started (level or round start, continue after game over). Repeated calls are ignored.
        /// </summary>
        public void BeginGameplay()
        {
            IsGameplayActive = true;
            Report();
        }

        /// <summary>
        /// Marks gameplay as over (game over, leaving the level). Repeated calls are ignored.
        /// </summary>
        public void EndGameplay()
        {
            IsGameplayActive = false;
            Report();
        }

        private void OnPauseStateChanged(PauseStateChangedEvent gameEvent)
        {
            Report();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            // A new level calls BeginGameplay itself; anything left running belongs to the old scene.
            EndGameplay();
        }

        private static bool IsHaltedByPause()
        {
            PauseManager pauseManager = PauseManager.Instance;
            return pauseManager != null && pauseManager.StopsPlatformGameplay;
        }

        // Tells the platform and listeners when the effective state changes, never twice in a row.
        private void Report()
        {
            bool playing = IsPlaying;
            if (playing == _isReportedPlaying)
                return;

            _isReportedPlaying = playing;

            PlatformManager platform = PlatformManager.Instance;
            if (platform != null && platform.Game != null)
            {
                if (playing)
                    platform.Game.GameplayStart();
                else
                    platform.Game.GameplayStop();
            }

            EventBus.Publish(new GameplayStateChangedEvent(playing));
        }
    }
}
