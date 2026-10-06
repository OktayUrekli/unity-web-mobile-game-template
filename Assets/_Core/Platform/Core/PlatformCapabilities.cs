namespace _Core.Platform.Core
{
    /// <summary>
    /// Defines the capabilities supported by the current platform.
    /// </summary>
    public class PlatformCapabilities
    {
        public bool SupportsAds { get; internal set; }

        public bool SupportsRewardedAds { get; internal set; }

        public bool SupportsInterstitialAds { get; internal set; }

        public bool SupportsBannerAds { get; internal set; }

        public bool SupportsAnalytics { get; internal set; }

        public bool SupportsAuthentication { get; internal set; }

        public bool SupportsLeaderboard { get; internal set; }

        public bool SupportsAchievements { get; internal set; }

        public bool SupportsSave { get; internal set; }

        public bool SupportsReview { get; internal set; }

        public bool SupportsShare { get; internal set; }

        public bool SupportsPurchases { get; internal set; }
    }
}