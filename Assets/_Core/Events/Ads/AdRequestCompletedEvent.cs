using _Core.Platform.Services.Ads;

namespace _Core.Events.Ads
{
    /// <summary>
    /// Published by <see cref="AdPolicyService"/> when a request announced by <see cref="AdRequestedEvent"/>
    /// has finished, before the caller's callback runs.
    /// </summary>
    public readonly struct AdRequestCompletedEvent : IGameEvent
    {
        /// <summary>
        /// Format of the finished request, as in its <see cref="AdRequestedEvent"/>.
        /// </summary>
        public AdType AdType { get; }

        /// <summary>
        /// The outcome, the same value the caller's callback receives right after this event.
        /// </summary>
        public AdResult Result { get; }

        public AdRequestCompletedEvent(AdType adType, AdResult result)
        {
            AdType = adType;
            Result = result;
        }
    }
}
