using _Core.Platform.Services.Ads;

namespace _Core.Events.Ads
{
    /// <summary>
    /// Published by <see cref="AdPolicyService"/> when a full-screen ad request is sent to the platform.
    /// Systems should block input and mute audio until <see cref="AdRequestCompletedEvent"/>.
    /// </summary>
    public readonly struct AdRequestedEvent : IGameEvent
    {
        public AdType AdType { get; }

        public AdRequestedEvent(AdType adType)
        {
            AdType = adType;
        }
    }
}
