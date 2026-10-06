namespace _Core.Events.Platform
{
    /// <summary>
    /// Published by a platform bridge when the platform asks the game to pause or resume (for example
    /// Yandex Games game_api_pause/game_api_resume).
    /// <c>AppLifecycleManager</c> turns it into <see cref="ApplicationPauseChangedEvent"/> and
    /// <c>AudioManager</c> silences the game while it is paused.
    /// </summary>
    public readonly struct PlatformPauseChangedEvent : IGameEvent
    {
        public bool IsPaused { get; }

        public PlatformPauseChangedEvent(bool isPaused)
        {
            IsPaused = isPaused;
        }
    }
}
