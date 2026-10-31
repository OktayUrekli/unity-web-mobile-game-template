using _Core.Gameplay;

namespace _Core.Tests.Fakes
{
    /// <summary>
    /// <see cref="GameplayPauseHandler"/> that counts its Pause/Resume calls. Edit Mode runs no OnEnable/OnDisable,
    /// so tests call <see cref="Enable"/> and <see cref="Disable"/> themselves. Lives in the runtime test assembly
    /// for the same reason as <see cref="FakePoolable"/>.
    /// </summary>
    public class FakePauseHandler : GameplayPauseHandler
    {
        /// <summary>
        /// Number of <see cref="Pause"/> calls.
        /// </summary>
        public int PauseCount { get; private set; }

        /// <summary>
        /// Number of <see cref="Resume"/> calls.
        /// </summary>
        public int ResumeCount { get; private set; }

        /// <summary>
        /// The handler's own pause state (true between its Pause and Resume calls).
        /// </summary>
        public bool HandlerIsPaused => IsPaused;

        /// <summary>
        /// Runs <c>OnEnable</c>: subscribes and applies the current pause state.
        /// </summary>
        public void Enable()
        {
            OnEnable();
        }

        /// <summary>
        /// Runs <c>OnDisable</c>: unsubscribes.
        /// </summary>
        public void Disable()
        {
            OnDisable();
        }

        /// <summary>
        /// Counts the call.
        /// </summary>
        public override void Pause()
        {
            PauseCount++;
        }

        /// <summary>
        /// Counts the call.
        /// </summary>
        public override void Resume()
        {
            ResumeCount++;
        }
    }
}
