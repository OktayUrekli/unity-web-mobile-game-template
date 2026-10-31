namespace _Core.Platform.Core
{
    /// <summary>
    /// Defines the capabilities supported by the current platform. The platform sets the flags, and
    /// <see cref="PlatformManager.InitializeAsync"/> clears each one whose service turned out to be a fallback.
    /// </summary>
    public class PlatformCapabilities
    {
        /// <summary>
        /// True when the platform shows ads; each format has its own flag.
        /// </summary>
        public bool SupportsAds { get; internal set; }

        /// <summary>
        /// True when the platform shows rewarded ads.
        /// </summary>
        public bool SupportsRewardedAds { get; internal set; }

        /// <summary>
        /// True when the platform shows interstitial (midgame) ads.
        /// </summary>
        public bool SupportsInterstitialAds { get; internal set; }

        /// <summary>
        /// True when the platform shows banner ads (<c>IAdsService.ShowBanner</c>).
        /// </summary>
        public bool SupportsBannerAds { get; internal set; }

        /// <summary>
        /// True when analytics events reach the platform.
        /// </summary>
        public bool SupportsAnalytics { get; internal set; }

        /// <summary>
        /// True when the player can sign in to the platform.
        /// </summary>
        public bool SupportsAuthentication { get; internal set; }

        /// <summary>
        /// True when scores reach a platform leaderboard; false while it is off or not set up in <c>PlatformConfig</c>.
        /// </summary>
        public bool SupportsLeaderboard { get; internal set; }

        /// <summary>
        /// True when the platform has achievements.
        /// </summary>
        public bool SupportsAchievements { get; internal set; }

        /// <summary>
        /// True when saves go to the platform's own storage; false for the PlayerPrefs fallback, which still saves
        /// locally.
        /// </summary>
        public bool SupportsSave { get; internal set; }

        /// <summary>
        /// True when the platform can show its rate-the-game dialog.
        /// </summary>
        public bool SupportsReview { get; internal set; }

        /// <summary>
        /// True when the platform can share text or images.
        /// </summary>
        public bool SupportsShare { get; internal set; }

        /// <summary>
        /// True when the platform has in-app purchases; <c>IPurchaseService.IsAvailable</c> says whether they work
        /// right now.
        /// </summary>
        public bool SupportsPurchases { get; internal set; }

        /// <summary>
        /// True when the platform has a privacy options form; <c>IPrivacyService.IsPrivacyOptionsRequired</c> says
        /// whether the game must offer it.
        /// </summary>
        public bool SupportsPrivacyOptions { get; internal set; }
    }
}