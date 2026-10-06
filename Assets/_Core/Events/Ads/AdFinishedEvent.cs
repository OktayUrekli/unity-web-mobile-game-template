using _Core.Events;

namespace _Core.Events.Ads
{
    /// <summary>
    /// Published when an advertisement finishes or fails.
    /// </summary>
    public readonly struct AdFinishedEvent : IGameEvent
    {
        public bool Success { get; }

        public AdFinishedEvent(bool success)
        {
            Success = success;
        }
    }
}