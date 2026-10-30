// Compiled only when Google Play is the selected platform (Tools/Template/Platform).
#if PLATFORM_GOOGLEPLAY
using _Core.Platform.Config;
using UnityEngine;

namespace _Platforms.GooglePlay.Core
{
    /// <summary>
    /// The AdMob ad unit IDs this session uses: the ones in <see cref="PlatformConfig"/> in release builds, Google's
    /// test ad units in the Editor, in Development builds and for any ID left empty. Real ad units in a Development
    /// build would count the developer's own views and clicks, which AdMob treats as invalid traffic.
    /// </summary>
    public sealed class GooglePlayAdUnits
    {
        // Google's Android demo ad units: https://developers.google.com/admob/unity/test-ads
        private const string TestInterstitialId = "ca-app-pub-3940256099942544/1033173712";
        private const string TestRewardedId = "ca-app-pub-3940256099942544/5224354917";
        private const string TestBannerId = "ca-app-pub-3940256099942544/9214589741";

        /// <summary>
        /// Interstitial ad unit ID.
        /// </summary>
        public string Interstitial { get; }

        /// <summary>
        /// Rewarded ad unit ID.
        /// </summary>
        public string Rewarded { get; }

        /// <summary>
        /// Anchored adaptive banner ad unit ID.
        /// </summary>
        public string Banner { get; }

        private GooglePlayAdUnits(string interstitial, string rewarded, string banner)
        {
            Interstitial = interstitial;
            Rewarded = rewarded;
            Banner = banner;
        }

        /// <summary>
        /// Picks the ad units for this build from <paramref name="config"/> and logs which ones are used.
        /// </summary>
        public static GooglePlayAdUnits FromConfig(PlatformConfig config)
        {
            bool forceTestAds = Debug.isDebugBuild;

            var adUnits = new GooglePlayAdUnits(
                Resolve(config.AdMobInterstitialAdUnitId, TestInterstitialId, forceTestAds),
                Resolve(config.AdMobRewardedAdUnitId, TestRewardedId, forceTestAds),
                Resolve(config.AdMobBannerAdUnitId, TestBannerId, forceTestAds));

            if (forceTestAds)
            {
                Debug.Log("GooglePlay: Editor or Development build, using Google's test ad units.");
            }
            else if (adUnits.Interstitial == TestInterstitialId || adUnits.Rewarded == TestRewardedId ||
                     adUnits.Banner == TestBannerId)
            {
                Debug.LogWarning("GooglePlay: An AdMob ad unit ID is empty in PlatformConfig; that format shows " +
                                 "Google's test ads and earns nothing.");
            }

            return adUnits;
        }

        private static string Resolve(string configured, string testId, bool forceTestAds)
        {
            if (forceTestAds || string.IsNullOrWhiteSpace(configured))
                return testId;

            return configured.Trim();
        }
    }
}
#endif
