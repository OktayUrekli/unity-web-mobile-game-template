using _Core.Platform.Core;
using System.Threading.Tasks;

namespace _Core.Platform.Platforms
{
    /// <summary>
    /// Default platform implementation.
    /// </summary>
    public class NullPlatform : IPlatform
    {
        public Task InitializeAsync(PlatformManager manager)
        {
            // Register safe fallback services for unsupported platforms.
            manager.SetGameService(
                new Services.Game.Null.NullGameService());

            manager.SetAdsService(
                new Services.Ads.Null.NullAdsService());

            manager.SetStorageService(
                new _Core.Save.Local.PlayerPrefsSaveStorage());

            manager.SetUserService(
                new Services.User.Null.NullUserService());

            manager.SetAnalyticsService(
                new Services.Analytics.Null.NullAnalyticsService());

            manager.SetAuthenticationService(
                new Services.Authentication.Null.NullAuthenticationService());

            manager.SetLeaderboardService(
                new Services.Leaderboard.Null.NullLeaderboardService());

            manager.SetAchievementService(
                new Services.Achievement.Null.NullAchievementService());

            manager.SetReviewService(
                new Services.Review.Null.NullReviewService());

            manager.SetShareService(
                new Services.Share.Null.NullShareService());

            manager.SetPurchaseService(
                new Services.Purchase.Null.NullPurchaseService());

            // Null platform has no SDK initialization requirements.
            return Task.CompletedTask;
        }

        public void ConfigureCapabilities(PlatformCapabilities capabilities)
        {
            capabilities.SupportsAds = false;
            capabilities.SupportsRewardedAds = false;
            capabilities.SupportsInterstitialAds = false;
            capabilities.SupportsBannerAds = false;

            capabilities.SupportsAnalytics = false;
            capabilities.SupportsAuthentication = false;
            capabilities.SupportsLeaderboard = false;
            capabilities.SupportsAchievements = false;
            capabilities.SupportsSave = false;
            capabilities.SupportsReview = false;
            capabilities.SupportsShare = false;
            capabilities.SupportsPurchases = false;
        }
    }
}