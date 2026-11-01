namespace _Core.Events.Platform
{
    /// <summary>
    /// Published by <c>AppLifecycleManager</c> when the app goes to the background, loses focus or the platform
    /// asks the game to pause (<see cref="IsPaused"/> true) and when all of them are over. <c>PauseManager</c> pauses
    /// the game in between. A running round pauses with <c>PauseSource.Menu</c> on true and shows its pause menu on
    /// false, once the player is back, as the sample <c>GameplayController</c> does: a menu shown on true could take
    /// the click that brings the focus back to a web game, and the menu pause keeps the game paused when
    /// <c>PauseManager</c> releases the app pause. Not published for focus changes caused by an ad.
    /// </summary>
    public readonly struct ApplicationPauseChangedEvent : IGameEvent
    {
        /// <summary>
        /// The new value of <c>AppLifecycleManager.IsApplicationPaused</c>.
        /// </summary>
        public bool IsPaused { get; }

        public ApplicationPauseChangedEvent(bool isPaused)
        {
            IsPaused = isPaused;
        }
    }
}
