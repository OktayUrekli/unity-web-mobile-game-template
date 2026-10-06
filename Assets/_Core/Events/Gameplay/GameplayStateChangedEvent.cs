namespace _Core.Events.Gameplay
{
    /// <summary>
    /// Published by <c>GameplayStateManager</c> when active play starts or stops
    /// (level start/resume vs. pause menu, game over, leaving the level). Not raised for ads.
    /// </summary>
    public readonly struct GameplayStateChangedEvent : IGameEvent
    {
        public bool IsActive { get; }

        public GameplayStateChangedEvent(bool isActive)
        {
            IsActive = isActive;
        }
    }
}
