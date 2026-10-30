// Compiled only when Google Play is the selected platform (Tools/Template/Platform).
#if PLATFORM_GOOGLEPLAY
using System.Threading.Tasks;
using _Core.Configuration;
using _Core.Platform.Config;
using _Core.Platform.Core;
using _Core.Platform.Services.Achievement.Null;
using _Core.Platform.Services.Ads.Null;
using _Core.Platform.Services.Analytics.Null;
using _Core.Platform.Services.Authentication.Null;
using _Core.Platform.Services.Game.Null;
using _Core.Platform.Services.Leaderboard.Null;
using _Core.Platform.Services.Privacy.Null;
using _Core.Platform.Services.Purchase.Null;
using _Core.Platform.Services.Review.Null;
using _Core.Platform.Services.Share.Null;
using _Core.Platform.Services.User.Null;
using _Core.Save.Local;
using _Platforms.GooglePlay.Services;
using UnityEngine;

namespace _Platforms.GooglePlay.Core
{
    /// <summary>
    /// Provides the Google Play (Android) platform implementation: AdMob ads through the Google Mobile Ads Unity
    /// plugin. Saves stay on the device (PlayerPrefs); Play Games, in-app review and billing are not bridged.
    /// </summary>
    public class GooglePlayPlatform : IPlatform
    {
        public void ConfigureCapabilities(PlatformCapabilities capabilities)
        {
            capabilities.SupportsAds = true;
            capabilities.SupportsRewardedAds = true;
            capabilities.SupportsInterstitialAds = true;
            // Anchored adaptive banner at the bottom of the screen, over the game.
            capabilities.SupportsBannerAds = true;

            capabilities.SupportsAnalytics = false;
            // Play Games Services (sign-in, leaderboards, achievements, cloud saves) is not bridged.
            capabilities.SupportsAuthentication = false;
            capabilities.SupportsLeaderboard = false;
            capabilities.SupportsAchievements = false;
            capabilities.SupportsSave = false;
            // The Play In-App Review plugin is not bridged.
            capabilities.SupportsReview = false;
            capabilities.SupportsShare = false;
            // Google Play Billing (Unity IAP) is not bridged.
            capabilities.SupportsPurchases = false;
            // Google's privacy options form (UMP); PrivacyOptionsButton shows the entry only where it is required.
            capabilities.SupportsPrivacyOptions = true;
        }

        public Task InitializeAsync(PlatformManager manager)
        {
            Debug.Log("GooglePlay: Initialization started.");

            if (GooglePlayMobileAds.IsSupported)
            {
                // Consent and SDK start-up run in the background, so a consent form never holds up the boot; the
                // ads service answers NotAvailable until they finish.
                PlatformConfig config = ConfigurationManager.GameConfig.PlatformConfig;
                GooglePlayMobileAds.Start();
                manager.SetAdsService(new GooglePlayAdsService(
                    GooglePlayAdUnits.FromConfig(config), config.AdStartTimeoutSeconds));
                manager.SetPrivacyService(new GooglePlayPrivacyService());
            }
            else
            {
                Debug.Log("GooglePlay: Google Mobile Ads runs only on Android and in the Editor. Using fallback services.");
                manager.SetAdsService(new NullAdsService());
                manager.SetPrivacyService(new NullPrivacyService());
            }

            manager.SetGameService(new NullGameService());
            // Never NullSaveStorage: it drops all saves silently.
            manager.SetStorageService(new PlayerPrefsSaveStorage());
            manager.SetUserService(new NullUserService());
            manager.SetAuthenticationService(new NullAuthenticationService());
            manager.SetAnalyticsService(new NullAnalyticsService());
            manager.SetLeaderboardService(new NullLeaderboardService());
            manager.SetAchievementService(new NullAchievementService());
            manager.SetReviewService(new NullReviewService());
            manager.SetShareService(new NullShareService());
            manager.SetPurchaseService(new NullPurchaseService());

            return Task.CompletedTask;
        }
    }
}
#endif
