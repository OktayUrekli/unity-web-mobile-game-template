namespace _Core.Events.UI
{
    /// <summary>
    /// Published by <c>UIManager</c> when Back/Escape is pressed and no popup was open to close
    /// (for example to open a pause menu during gameplay).
    /// </summary>
    public readonly struct UIBackRequestedEvent : IGameEvent
    {
    }
}
