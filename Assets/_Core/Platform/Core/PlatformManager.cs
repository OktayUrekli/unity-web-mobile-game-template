using _Core.Platform.Platforms;
using _Core.Platform.Services.Achievement;
using _Core.Platform.Services.Achievement.Null;
using _Core.Platform.Services.Ads.Null;
using _Core.Platform.Services.Analytics.Null;
using _Core.Platform.Services.Authentication.Null;
using _Core.Platform.Services.Game.Null;
using _Core.Platform.Services.Leaderboard.Null;
using _Core.Platform.Services.Purchase;
using _Core.Platform.Services.Purchase.Null;
using _Core.Platform.Services.Privacy;
using _Core.Platform.Services.Privacy.Null;
using _Core.Platform.Services.Review.Null;
using _Core.Platform.Services.Save.Null;
using _Core.Platform.Services.Share.Null;
using _Core.Platform.Services.User.Null;
using _Core.Platform.Services.Ads;
using _Core.Platform.Services.Analytics;
using _Core.Platform.Services.Authentication;
using _Core.Platform.Services.Leaderboard;
using _Core.Platform.Services.Review;
using _Core.Platform.Services.Share;
using _Core.Configuration;
using _Core.Platform.Config;
using _Core.Managers;
using _Core.Platform.Services.Save;
using _Core.Save.Local;

using System;
using System.Threading.Tasks;
using _Core.Platform.Services.Game;
using _Core.Platform.Services.User;
using UnityEngine;

namespace _Core.Platform.Core
{
    /// <summary>
    /// Manages the active platform and its services. Every service property is non-null once
    /// <see cref="InitializeAsync"/> has finished, even after a failure (Null implementations then); before that it
    /// may be null.
    /// </summary>
    public class PlatformManager : Singleton<PlatformManager>
    {
        // Set once the missing platform config has been reported, so repeated reads of CurrentPlatform log one error.
        private bool _missingConfigLogged;

        /// <summary>
        /// Initialization progress. Services work in both <see cref="PlatformState.Ready"/> and
        /// <see cref="PlatformState.Failed"/>.
        /// </summary>
        public PlatformState State { get; private set; } = PlatformState.None;

        /// <summary>
        /// True only when initialization succeeded. It is false after a failure, yet every service then works as its
        /// Null implementation.
        /// </summary>
        public bool IsReady => State == PlatformState.Ready;

        /// <summary>
        /// What the current platform supports; valid once <see cref="InitializeAsync"/> has finished.
        /// </summary>
        public PlatformCapabilities Capabilities { get; } = new();
        
        /// <summary>
        /// Gameplay reports and the portal's settings (language, mute and pause requests).
        /// </summary>
        public IGameService Game { get; private set; }

        /// <summary>
        /// The platform's ads service wrapped in <see cref="AdPolicyService"/>; the same object as
        /// <see cref="AdPolicy"/>.
        /// </summary>
        public IAdsService Ads { get; private set; }

        /// <summary>
        /// The platform's save storage, or the PlayerPrefs fallback. Game code saves through <c>SaveManager</c>, which
        /// caches this storage.
        /// </summary>
        public ISaveStorage Storage { get; private set; }

        /// <summary>
        /// The player's portal account.
        /// </summary>
        public IUserService User { get; private set; }

        /// <summary>
        /// Analytics events for the platform.
        /// </summary>
        public IAnalyticsService Analytics { get; private set; }

        /// <summary>
        /// Sign-in to the platform.
        /// </summary>
        public IAuthenticationService Authentication { get; private set; }

        /// <summary>
        /// Platform leaderboards; off unless <c>PlatformConfig.LeaderboardEnabled</c> is set.
        /// </summary>
        public ILeaderboardService Leaderboard { get; private set; }

        /// <summary>
        /// Platform achievements.
        /// </summary>
        public IAchievementService Achievement { get; private set; }

        /// <summary>
        /// The platform's rate-the-game dialog.
        /// </summary>
        public IReviewService Review { get; private set; }

        /// <summary>
        /// Sharing through the platform.
        /// </summary>
        public IShareService Share { get; private set; }

        /// <summary>
        /// In-app purchases.
        /// </summary>
        public IPurchaseService Purchases { get; private set; }

        /// <summary>
        /// The platform's privacy (ad consent) options.
        /// </summary>
        public IPrivacyService Privacy { get; private set; }
        
        /// <summary>
        /// The platform selected in <see cref="PlatformConfig"/>. Never throws: when
        /// <see cref="ConfigurationManager.GameConfig"/> or its <c>PlatformConfig</c> is missing it returns
        /// <see cref="PlatformType.None"/> (the Null platform) and logs one error per manager.
        /// </summary>
        public PlatformType CurrentPlatform
        {
            get
            {
                PlatformConfig config = GetPlatformConfig();
                return config != null ? config.Platform : PlatformType.None;
            }
        }

        /// <summary>
        /// Initializes the configured platform and its services.
        /// Never throws: on failure every service is a Null implementation
        /// and <see cref="State"/> is <see cref="PlatformState.Failed"/>.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (State == PlatformState.Initializing ||
                State == PlatformState.Ready)
            {
                return;
            }

            State = PlatformState.Initializing;
            try
            {
                IPlatform platform = PlatformFactory.Create(CurrentPlatform);
                platform.ConfigureCapabilities(Capabilities);
                await platform.InitializeAsync(this);
                RegisterMissingServices();
                SyncCapabilitiesWithServices();
                WrapAdsWithPolicy();
                State = PlatformState.Ready;
                Debug.Log($"Platform initialized successfully: {CurrentPlatform}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogWarning(
                    $"Platform initialization failed ({CurrentPlatform}). Using Null services.");

                RegisterFallbackServices();
                WrapAdsWithPolicy();
                State = PlatformState.Failed;
            }
        }

        /// <summary>
        /// The ad policy wrapping the platform ads service (cooldown, in-flight guard, request events).
        /// Same object as <see cref="Ads"/>; null before initialization.
        /// </summary>
        public AdPolicyService AdPolicy => Ads as AdPolicyService;

        /// <summary>
        /// Wraps the registered platform ads service in <see cref="AdPolicyService"/> so every caller
        /// goes through the same ad rules. Must run after capabilities are synced, which inspect the raw service.
        /// </summary>
        private void WrapAdsWithPolicy()
        {
            if (Ads is AdPolicyService)
                return;

            PlatformConfig config = GetPlatformConfig();

            Ads = config != null
                ? new AdPolicyService(
                    Ads ?? new NullAdsService(),
                    config.InterstitialCooldownSeconds,
                    config.AdStartTimeoutSeconds,
                    config.AdPlayTimeoutSeconds)
                : new AdPolicyService(Ads ?? new NullAdsService(), 180f);
        }

        /// <summary>
        /// The configured <see cref="PlatformConfig"/>, or null when <see cref="ConfigurationManager.GameConfig"/>
        /// or its <c>PlatformConfig</c> is missing. The first miss is logged as an error; later reads stay quiet.
        /// </summary>
        private PlatformConfig GetPlatformConfig()
        {
            GameConfig gameConfig = ConfigurationManager.GameConfig;

            if (gameConfig != null && gameConfig.PlatformConfig != null)
                return gameConfig.PlatformConfig;

            if (!_missingConfigLogged)
            {
                _missingConfigLogged = true;
                Debug.LogError(gameConfig == null
                    ? "PlatformManager: ConfigurationManager.GameConfig is null (is the GameConfig assigned on the " +
                      "Bootstrapper in 00_Bootstrap?). Using PlatformType.None."
                    : $"PlatformManager: GameConfig '{gameConfig.name}' has no PlatformConfig assigned. Using PlatformType.None.");
            }

            return null;
        }

        /// <summary>
        /// Replaces every service with its Null implementation (storage: PlayerPrefs)
        /// so that services are never null after a failed initialization.
        /// </summary>
        private void RegisterFallbackServices()
        {
            // NullPlatform registers synchronously and returns a completed task.
            var fallback = new NullPlatform();
            fallback.ConfigureCapabilities(Capabilities);
            _ = fallback.InitializeAsync(this);
        }

        /// <summary>
        /// Fills any service a platform forgot to register with its Null implementation.
        /// </summary>
        private void RegisterMissingServices()
        {
            if (Game == null) LogMissing(nameof(Game));
            Game ??= new NullGameService();
            if (Ads == null) LogMissing(nameof(Ads));
            Ads ??= new NullAdsService();
            if (Storage == null) LogMissing(nameof(Storage));
            Storage ??= new PlayerPrefsSaveStorage();
            if (User == null) LogMissing(nameof(User));
            User ??= new NullUserService();
            if (Analytics == null) LogMissing(nameof(Analytics));
            Analytics ??= new NullAnalyticsService();
            if (Authentication == null) LogMissing(nameof(Authentication));
            Authentication ??= new NullAuthenticationService();
            if (Leaderboard == null) LogMissing(nameof(Leaderboard));
            Leaderboard ??= new NullLeaderboardService();
            if (Achievement == null) LogMissing(nameof(Achievement));
            Achievement ??= new NullAchievementService();
            if (Review == null) LogMissing(nameof(Review));
            Review ??= new NullReviewService();
            if (Share == null) LogMissing(nameof(Share));
            Share ??= new NullShareService();
            if (Purchases == null) LogMissing(nameof(Purchases));
            Purchases ??= new NullPurchaseService();
            if (Privacy == null) LogMissing(nameof(Privacy));
            Privacy ??= new NullPrivacyService();
        }

        private void LogMissing(string serviceName)
        {
            Debug.LogWarning(
                $"Platform {CurrentPlatform} did not register the {serviceName} service. Using the Null implementation.");
        }

        /// <summary>
        /// Clears capability flags whose service was registered as a Null implementation,
        /// so <see cref="Capabilities"/> reflects what actually works.
        /// </summary>
        private void SyncCapabilitiesWithServices()
        {
            if (Ads is NullAdsService)
            {
                Capabilities.SupportsAds = false;
                Capabilities.SupportsRewardedAds = false;
                Capabilities.SupportsInterstitialAds = false;
                Capabilities.SupportsBannerAds = false;
            }

            // PlayerPrefs is a local fallback, not platform (cloud) save.
            if (Storage is NullSaveStorage || Storage is PlayerPrefsSaveStorage)
                Capabilities.SupportsSave = false;

            if (Analytics is NullAnalyticsService)
                Capabilities.SupportsAnalytics = false;

            if (Authentication is NullAuthenticationService)
                Capabilities.SupportsAuthentication = false;

            if (Leaderboard is NullLeaderboardService)
                Capabilities.SupportsLeaderboard = false;

            if (Achievement is NullAchievementService)
                Capabilities.SupportsAchievements = false;

            if (Review is NullReviewService)
                Capabilities.SupportsReview = false;

            if (Share is NullShareService)
                Capabilities.SupportsShare = false;

            if (Purchases is NullPurchaseService)
                Capabilities.SupportsPurchases = false;

            if (Privacy is NullPrivacyService)
                Capabilities.SupportsPrivacyOptions = false;
        }
        
        /// <summary>
        /// Completes once <see cref="InitializeAsync"/> is no longer running, whether it succeeded or failed; at once
        /// when it has not started.
        /// </summary>
        public async Task WaitUntilReadyAsync()
        {
            while (State == PlatformState.Initializing)
            {
                await Task.Yield();
            }
        }
        
        /// <summary>
        /// True when <paramref name="platform"/> is <see cref="CurrentPlatform"/>, whether or not it initialized.
        /// </summary>
        public bool IsPlatform(PlatformType platform)
        {
            return CurrentPlatform == platform;
        }
        
        internal void SetGameService(IGameService service)
        {
            Game = service;
        }

        internal void SetStorageService(ISaveStorage service)
        {
            Storage = service;
        }

        internal void SetUserService(IUserService service)
        {
            User = service;
        }
        
        internal void SetAdsService(IAdsService service)
        {
            Ads = service;
        }

        internal void SetAnalyticsService(IAnalyticsService service)
        {
            Analytics = service;
        }
        
        internal void SetAuthenticationService(IAuthenticationService service)
        {
            Authentication = service;
        }

        internal void SetLeaderboardService(ILeaderboardService service)
        {
            Leaderboard = service;
        }
        
        internal void SetAchievementService(IAchievementService service)
        {
            Achievement = service;
        }
        
        internal void SetReviewService(IReviewService service)
        {
            Review = service;
        }

        internal void SetShareService(IShareService service)
        {
            Share = service;
        }

        internal void SetPurchaseService(IPurchaseService service)
        {
            Purchases = service;
        }

        internal void SetPrivacyService(IPrivacyService service)
        {
            Privacy = service;
        }
    }
}