namespace _Core.Events.Platform
{
    /// <summary>
    /// Published by <c>AppLifecycleManager</c> when the app goes to the background, loses focus or the platform
    /// asks the game to pause (<see cref="IsPaused"/> true) and when all of them are over. Gameplay should open
    /// its pause menu on true. Not published for focus changes caused by an ad.
    /// </summary>
    public readonly struct ApplicationPauseChangedEvent : IGameEvent
    {
        public bool IsPaused { get; }

        public ApplicationPauseChangedEvent(bool isPaused)
        {
            IsPaused = isPaused;
        }
    }
}
