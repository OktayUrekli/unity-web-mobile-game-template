using System;

namespace _Core.Platform.Services.Ads
{
    /// <summary>
    /// Provides platform-independent advertisement operations.
    /// Game code reaches it through <c>PlatformManager.Instance.Ads</c>, which wraps the platform
    /// implementation in <see cref="AdPolicyService"/> (in-flight guard, interstitial cooldown, request events).
    /// </summary>
    public interface IAdsService
    {
        /// <summary>
        /// True while a full-screen ad request is in progress (from request until its callback).
        /// </summary>
        bool IsAdShowing { get; }

        /// <summary>
        /// Shows a rewarded advertisement. The callback is always invoked exactly once;
        /// grant the reward only for <see cref="AdResult.Completed"/>.
        /// </summary>
        void ShowRewardedAd(Action<AdResult> callback);

        /// <summary>
        /// Shows an interstitial (midgame) advertisement. The callback is always invoked exactly once.
        /// </summary>
        void ShowInterstitialAd(Action<AdResult> callback);

        /// <summary>
        /// Preloads an advertisement so a later request starts faster. Does nothing when unsupported.
        /// </summary>
        void PrefetchAd(AdType adType);

        /// <summary>
        /// Reports whether the player uses an ad blocker. The callback may be deferred until detection
        /// finishes; platforms without detection report false.
        /// </summary>
        void HasAdblock(Action<bool> callback);

        /// <summary>
        /// Shows a banner advertisement.
        /// </summary>
        void ShowBanner();

        /// <summary>
        /// Hides the currently displayed banner advertisement.
        /// </summary>
        void HideBanner();
    }
}
