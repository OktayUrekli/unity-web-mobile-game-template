namespace _Core.Events.Gameplay
{
    /// <summary>
    /// Published by <c>GameplayStateManager</c> when active play starts or stops
    /// (level start/resume vs. pause menu, game over, leaving the level). Not raised for ads.
    /// </summary>
    public readonly struct GameplayStateChangedEvent : IGameEvent
    {
        /// <summary>
        /// Same value as <c>GameplayStateManager.IsPlaying</c>: true from <c>BeginGameplay</c> to <c>EndGameplay</c>,
        /// except while a pause that stops platform gameplay (a menu, a scene transition, the app in the background)
        /// holds.
        /// </summary>
        public bool IsActive { get; }

        public GameplayStateChangedEvent(bool isActive)
        {
            IsActive = isActive;
        }
    }
}
