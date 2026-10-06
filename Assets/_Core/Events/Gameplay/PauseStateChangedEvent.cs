namespace _Core.Events.Gameplay
{
    /// <summary>
    /// Published by <c>PauseManager</c> whenever a pause source is added or removed, so <see cref="IsPaused"/> may be
    /// unchanged (a second source joined). Handlers that act on pause/resume compare it with their own state.
    /// </summary>
    public readonly struct PauseStateChangedEvent : IGameEvent
    {
        /// <summary>
        /// True while at least one source pauses the game.
        /// </summary>
        public bool IsPaused { get; }

        public PauseStateChangedEvent(bool isPaused)
        {
            IsPaused = isPaused;
        }
    }
}
