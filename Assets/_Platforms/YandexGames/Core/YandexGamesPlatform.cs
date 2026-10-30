// Compiled only when Yandex Games is the selected platform (Tools/Template/Platform).
#if PLATFORM_YANDEX
using System.Threading.Tasks;
using _Core.Configuration;
using _Core.Platform.Config;
using _Core.Platform.Core;
using _Core.Platform.Services.Achievement.Null;
using _Core.Platform.Services.Ads.Null;
using _Core.Platform.Services.Analytics.Null;
using _Core.Platform.Services.Authentication.Null;
using _Core.Platform.Services.Game.Null;
using _Core.Platform.Services.Leaderboard;
using _Core.Platform.Services.Leaderboard.Null;
using _Core.Platform.Services.Purchase.Null;
using _Core.Platform.Services.Privacy.Null;
using _Core.Platform.Services.Review.Null;
using _Core.Platform.Services.Share.Null;
using _Core.Platform.Services.User.Null;
using _Core.Save.Local;
using _Platforms.YandexGames.Services;
using UnityEngine;

namespace _Platforms.YandexGames.Core
{
    /// <summary>
    /// Provides the Yandex Games platform implementation on the Yandex Games JS SDK v2 (YandexGames.jslib).
    /// </summary>
    public class YandexGamesPlatform : IPlatform
    {
        public void ConfigureCapabilities(PlatformCapabilities capabilities)
        {
            capabilities.SupportsAds = true;
            capabilities.SupportsRewardedAds = true;
            capabilities.SupportsInterstitialAds = true;
            // Sticky banner; needs "Use the API to display a sticky-banner" in the Developer Console.
            capabilities.SupportsBannerAds = true;

            capabilities.SupportsAnalytics = false;
            // Sign-in (ysdk.auth) is not bridged yet: after it the cloud data belongs to another player.
            capabilities.SupportsAuthentication = false;
            // Cleared again by PlatformManager when the Null leaderboard is registered (disabled in PlatformConfig).
            capabilities.SupportsLeaderboard = true;
            capabilities.SupportsAchievements = false;
            capabilities.SupportsSave = true;
            capabilities.SupportsReview = true;
            capabilities.SupportsShare = false;
            // Yandex payments (ysdk.payments) are not bridged yet.
            capabilities.SupportsPurchases = false;
            capabilities.SupportsPrivacyOptions = false;
        }

        public async Task InitializeAsync(PlatformManager manager)
        {
            Debug.Log("Yandex: Initialization started.");

            bool sdkReady = false;

            if (YandexGamesSdk.IsAvailable)
            {
                float timeoutSeconds = ConfigurationManager.GameConfig.PlatformConfig.SdkInitTimeoutSeconds;
                sdkReady = await YandexGamesSdk.InitializeAsync(timeoutSeconds);

                if (!sdkReady)
                {
                    Debug.LogWarning(
                        $"Yandex: SDK did not initialize within {timeoutSeconds} seconds. Using fallback services.");
                }
            }
            else
            {
                Debug.Log(
                    "Yandex: SDK is not available (Editor, or a page without /sdk.js). Using fallback services.");
            }

            RegisterServices(manager, sdkReady);
        }

        private static void RegisterServices(PlatformManager manager, bool sdkReady)
        {
            if (sdkReady)
            {
                manager.SetGameService(new YandexGamesGameService());
                manager.SetAdsService(new YandexGamesAdsService());

                if (YandexGamesSdk.HasPlayerData)
                {
                    manager.SetStorageService(new YandexGamesDataStorage(YandexGamesSdk.TakeInitialData()));
                }
                else
                {
                    Debug.LogWarning("Yandex: Player data did not load. Saving locally (PlayerPrefs) instead.");
                    manager.SetStorageService(new PlayerPrefsSaveStorage());
                }

                manager.SetLeaderboardService(CreateLeaderboardService());
                manager.SetReviewService(new YandexGamesReviewService());
            }
            else
            {
                manager.SetGameService(new NullGameService());
                manager.SetAdsService(new NullAdsService());
                // Never NullSaveStorage: it drops all saves silently.
                manager.SetStorageService(new PlayerPrefsSaveStorage());
                manager.SetLeaderboardService(new NullLeaderboardService());
                manager.SetReviewService(new NullReviewService());
            }

            manager.SetUserService(new NullUserService());
            manager.SetAuthenticationService(new NullAuthenticationService());
            manager.SetAnalyticsService(new NullAnalyticsService());
            manager.SetAchievementService(new NullAchievementService());
            manager.SetShareService(new NullShareService());
            manager.SetPurchaseService(new NullPurchaseService());
            manager.SetPrivacyService(new NullPrivacyService());
        }

        /// <summary>
        /// The leaderboard is off unless <c>PlatformConfig.LeaderboardEnabled</c> is set.
        /// </summary>
        private static ILeaderboardService CreateLeaderboardService()
        {
            PlatformConfig config = ConfigurationManager.GameConfig.PlatformConfig;

            if (!config.LeaderboardEnabled)
            {
                Debug.Log("Yandex: Leaderboard disabled in PlatformConfig.");
                return new NullLeaderboardService();
            }

            return new YandexGamesLeaderboardService(config.DefaultLeaderboardId);
        }
    }
}
#endif
