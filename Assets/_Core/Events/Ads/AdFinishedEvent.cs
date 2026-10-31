namespace _Core.Events.Ads
{
    /// <summary>
    /// Published when a started ad ends, always after its <see cref="AdStartedEvent"/>; never for an ad that did not
    /// start. The template itself does not read it (it is a hook for game code such as analytics);
    /// <see cref="AdRequestCompletedEvent"/> carries the outcome of every request.
    /// </summary>
    public readonly struct AdFinishedEvent : IGameEvent
    {
        /// <summary>
        /// True when the ad completed (<c>AdResult.Completed</c>); false when it failed or timed out.
        /// </summary>
        public bool Success { get; }

        public AdFinishedEvent(bool success)
        {
            Success = success;
        }
    }
}