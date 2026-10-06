using _Core.Events;
using _Core.Events.Gameplay;
using UnityEngine;

namespace _Core.Gameplay
{
    /// <summary>
    /// Turns <see cref="PauseManager"/> changes into <see cref="Pause"/>/<see cref="Resume"/> calls, each exactly once
    /// per change. Derive systems that keep running at <c>Time.timeScale = 0</c> from it: anything on unscaled time,
    /// tweens with <c>SetUpdate(true)</c>, audio that should stop, physics bodies that must not keep their velocity.
    /// A component enabled while the game is paused gets <see cref="Pause"/> right away.
    /// </summary>
    public abstract class GameplayPauseHandler : MonoBehaviour, IPausable
    {
        /// <summary>
        /// True between this component's <see cref="Pause"/> and <see cref="Resume"/> calls.
        /// </summary>
        protected bool IsPaused { get; private set; }

        protected virtual void OnEnable()
        {
            EventBus.Subscribe<PauseStateChangedEvent>(OnPauseStateChanged);

            PauseManager pauseManager = PauseManager.Instance;
            Apply(pauseManager != null && pauseManager.IsPaused);
        }

        protected virtual void OnDisable()
        {
            EventBus.Unsubscribe<PauseStateChangedEvent>(OnPauseStateChanged);
        }

        private void OnPauseStateChanged(PauseStateChangedEvent gameEvent)
        {
            Apply(gameEvent.IsPaused);
        }

        private void Apply(bool paused)
        {
            if (paused == IsPaused)
                return;

            IsPaused = paused;

            if (paused)
                Pause();
            else
                Resume();
        }

        /// <summary>
        /// Called when the game pauses (menu, ad, app in the background).
        /// </summary>
        public abstract void Pause();

        /// <summary>
        /// Called when the game resumes. Never writes <c>Time.timeScale</c>; <see cref="PauseManager"/> owns it.
        /// </summary>
        public abstract void Resume();
    }
}
