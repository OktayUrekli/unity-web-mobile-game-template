// Compiled only when CrazyGames is the selected platform (Tools/Template/Platform).
#if PLATFORM_CRAZYGAMES
using System.Threading.Tasks;
using CrazyGames;
using _Core.Configuration;
using _Core.Platform.Config;
using _Core.Platform.Core;
using _Platforms.CrazyGames.Services;
using _Core.Platform.Services.Achievement.Null;
using _Core.Platform.Services.Analytics.Null;
using _Core.Platform.Services.Authentication.Null;
using _Core.Platform.Services.Leaderboard;
using _Core.Platform.Services.Leaderboard.Null;
using _Core.Platform.Services.Review.Null;
using _Core.Platform.Services.Share.Null;
using _Core.Platform.Services.Purchase.Null;
using _Core.Platform.Services.Game.Null;
using _Core.Platform.Services.Ads.Null;
using _Core.Save.Local;
using _Core.Platform.Services.User.Null;

using UnityEngine;

namespace _Platforms.CrazyGames.Core
{
    /// <summary>
    /// Provides the CrazyGames platform implementation.
    /// </summary>
    public class CrazyGamesPlatform : IPlatform
    {
        public void ConfigureCapabilities(PlatformCapabilities capabilities)
        {
            // CrazyGames SDK v3 currently provides these platform features.
            capabilities.SupportsAds = true;
            capabilities.SupportsRewardedAds = true;
            capabilities.SupportsInterstitialAds = true;
            capabilities.SupportsBannerAds = true;

            capabilities.SupportsAnalytics = false;
            capabilities.SupportsAuthentication = true;
            // Cleared again by PlatformManager when the Null leaderboard is registered (disabled or no key).
            capabilities.SupportsLeaderboard = true;
            capabilities.SupportsAchievements = false;
            capabilities.SupportsSave = true;
            capabilities.SupportsReview = false;
            capabilities.SupportsShare = false;
            // CrazyGames sells items only through Xsolla (GetXsollaUserToken); the template has no Xsolla bridge.
            capabilities.SupportsPurchases = false;
        }

        public async Task InitializeAsync(PlatformManager manager)
        {
            Debug.Log("CrazyGames: Initialization started.");

            if (CrazySDK.IsAvailable)
            {
                if (!CrazySDK.IsInitialized)
                {
                    float timeoutSeconds = ConfigurationManager.GameConfig.PlatformConfig.SdkInitTimeoutSeconds;
                    bool initialized = await InitializeSdkAsync(timeoutSeconds);

                    Debug.Log(
                        $"CrazyGames: SDK Initialized = {CrazySDK.IsInitialized}");

                    if (!initialized)
                    {
                        Debug.LogWarning(
                            $"CrazyGames: SDK did not initialize within {timeoutSeconds} seconds. " +
                            "Using fallback services.");
                    }
                }
            }
            else
            {
                Debug.Log(
                    "CrazyGames: SDK is not available. " +
                    "Using fallback services.");
            }

            RegisterServices(manager);
        }

        /// <summary>
        /// Calls <c>CrazySDK.Init</c> and waits for its callback, at most <paramref name="timeoutSeconds"/>.
        /// The SDK has no error callback, so a failed script load would otherwise hang forever.
        /// Polls once per frame instead of <c>Task.Delay</c>, which relies on threads unavailable on WebGL.
        /// </summary>
        /// <returns>True when the SDK reported initialization before the timeout.</returns>
        private static async Task<bool> InitializeSdkAsync(float timeoutSeconds)
        {
            var completion = new TaskCompletionSource<bool>();

            CrazySDK.Init(() =>
            {
                completion.TrySetResult(true);
            });

            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (!completion.Task.IsCompleted &&
                   Time.realtimeSinceStartup < deadline)
            {
                await Task.Yield();
            }

            return completion.Task.IsCompleted && CrazySDK.IsInitialized;
        }
        
        private void RegisterServices(PlatformManager manager)
        {
            bool sdkReady = CrazySDK.IsAvailable && CrazySDK.IsInitialized;

            if (sdkReady)
            {
                manager.SetGameService(
                    new CrazyGamesGameService());

                manager.SetAdsService(
                    new CrazyGamesAdsService());

                manager.SetStorageService(
                    new CrazyGamesDataStorage());

                var userService = new CrazyGamesUserService();
                manager.SetUserService(userService);

                if (userService.IsAvailable)
                {
                    manager.SetAuthenticationService(
                        new CrazyGamesAuthenticationService(userService));

                    // Cache the signed-in user so IsSignedIn is correct from the first frame it answers.
                    userService.GetCurrentUser(null);
                }
                else
                {
                    manager.SetAuthenticationService(
                        new NullAuthenticationService());
                }

                manager.SetLeaderboardService(
                    CreateLeaderboardService());
            }
            else
            {
                manager.SetGameService(
                    new NullGameService());

                manager.SetAdsService(
                    new NullAdsService());

                manager.SetStorageService(
                    new PlayerPrefsSaveStorage());

                manager.SetUserService(
                    new NullUserService());

                manager.SetAuthenticationService(
                    new NullAuthenticationService());

                manager.SetLeaderboardService(
                    new NullLeaderboardService());
            }

            manager.SetAnalyticsService(
                new NullAnalyticsService());

            manager.SetAchievementService(
                new NullAchievementService());

            manager.SetReviewService(
                new NullReviewService());

            manager.SetShareService(
                new NullShareService());

            manager.SetPurchaseService(
                new NullPurchaseService());
        }

        /// <summary>
        /// The leaderboard is off unless <c>PlatformConfig.LeaderboardEnabled</c> is set, and it needs the
        /// encryption key from the developer portal; without a valid key it stays disabled instead of failing
        /// on every submission.
        /// </summary>
        private static ILeaderboardService CreateLeaderboardService()
        {
            PlatformConfig config = ConfigurationManager.GameConfig.PlatformConfig;

            if (!config.LeaderboardEnabled)
            {
                Debug.Log("CrazyGames: Leaderboard disabled in PlatformConfig.");
                return new NullLeaderboardService();
            }

            string key = config.LeaderboardEncryptionKey;

            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.Log("CrazyGames: No leaderboard encryption key configured, leaderboard disabled.");
                return new NullLeaderboardService();
            }

            if (!CrazyGamesLeaderboardService.IsValidKey(key))
            {
                Debug.LogError("CrazyGames: Leaderboard encryption key is invalid (expected base64 AES key), leaderboard disabled.");
                return new NullLeaderboardService();
            }

            return new CrazyGamesLeaderboardService(key);
        }
    }
}
#endif
