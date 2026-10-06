using System;
using System.Threading.Tasks;
using _Core.Platform.Core;
using _Core.Platform.Services.Ads;

namespace _Core.Tests.Fakes
{
    /// <summary>
    /// <see cref="IPlatform"/> test double. It claims every capability, registers only the services the test
    /// gives it and answers with an already completed (or faulted) task, so
    /// <see cref="PlatformManager.InitializeAsync"/> finishes within the call.
    /// </summary>
    public class FakePlatform : IPlatform
    {
        /// <summary>
        /// Ads service registered by <see cref="InitializeAsync"/>, or null to register none.
        /// </summary>
        public IAdsService AdsService { get; set; }

        /// <summary>
        /// Exception thrown synchronously by <see cref="InitializeAsync"/> after registering services, or null.
        /// </summary>
        public Exception ThrowOnInitialize { get; set; }

        /// <summary>
        /// Exception returned as a faulted task by <see cref="InitializeAsync"/> after registering services, or null.
        /// </summary>
        public Exception FaultOnInitialize { get; set; }

        /// <summary>
        /// Number of <see cref="InitializeAsync"/> calls.
        /// </summary>
        public int InitializeCount { get; private set; }

        /// <summary>
        /// Claims every capability, so the test can see which flags the manager clears.
        /// </summary>
        public void ConfigureCapabilities(PlatformCapabilities capabilities)
        {
            capabilities.SupportsAds = true;
            capabilities.SupportsRewardedAds = true;
            capabilities.SupportsInterstitialAds = true;
            capabilities.SupportsBannerAds = true;

            capabilities.SupportsAnalytics = true;
            capabilities.SupportsAuthentication = true;
            capabilities.SupportsLeaderboard = true;
            capabilities.SupportsAchievements = true;
            capabilities.SupportsSave = true;
            capabilities.SupportsReview = true;
            capabilities.SupportsShare = true;
            capabilities.SupportsPurchases = true;
        }

        /// <summary>
        /// Registers <see cref="AdsService"/> when set, then fails as configured or returns a completed task.
        /// </summary>
        public Task InitializeAsync(PlatformManager manager)
        {
            InitializeCount++;

            if (AdsService != null)
                manager.SetAdsService(AdsService);

            if (ThrowOnInitialize != null)
                throw ThrowOnInitialize;

            return FaultOnInitialize != null
                ? Task.FromException(FaultOnInitialize)
                : Task.CompletedTask;
        }
    }
}
