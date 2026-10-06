using _Core.Platform.Services.Ads;

namespace _Core.Events.Ads
{
    /// <summary>
    /// Published by <see cref="AdPolicyService"/> when a request announced by <see cref="AdRequestedEvent"/>
    /// has finished, before the caller's callback runs.
    /// </summary>
    public readonly struct AdRequestCompletedEvent : IGameEvent
    {
        public AdType AdType { get; }
        public AdResult Result { get; }

        public AdRequestCompletedEvent(AdType adType, AdResult result)
        {
            AdType = adType;
            Result = result;
        }
    }
}
