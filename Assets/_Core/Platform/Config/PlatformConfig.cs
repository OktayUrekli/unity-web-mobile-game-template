using _Core.Platform.Core;
using UnityEngine;

namespace _Core.Platform.Config
{
    /// <summary>
    /// Stores the target platform configuration.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PlatformConfig",
        menuName = "Configuration/Platform Config")]
    public class PlatformConfig : ScriptableObject
    {
        [SerializeField] private PlatformType platform;

        [Tooltip("Seconds to wait for the platform SDK to initialize before falling back to Null services.")]
        [Min(1f)]
        [SerializeField] private float sdkInitTimeoutSeconds = 10f;

        [Header("Ads")]
        [Tooltip("Minimum real seconds between two completed interstitial (midgame) ads. CrazyGames allows at most one every 3 minutes.")]
        [Min(0f)]
        [SerializeField] private float interstitialCooldownSeconds = 180f;

        [Tooltip("Seconds to wait for a requested ad to start before giving up. Protects against an SDK that never calls back.")]
        [Min(1f)]
        [SerializeField] private float adStartTimeoutSeconds = 30f;

        [Tooltip("Seconds a started ad may run before it is abandoned. Must be longer than the longest ad.")]
        [Min(1f)]
        [SerializeField] private float adPlayTimeoutSeconds = 180f;

        /// <summary>
        /// Real seconds a requested ad may take to start (<c>AdStartedEvent</c>) before the request times out.
        /// </summary>
        public float AdStartTimeoutSeconds => adStartTimeoutSeconds;

        /// <summary>
        /// Real seconds a started ad may run before the request times out.
        /// </summary>
        public float AdPlayTimeoutSeconds => adPlayTimeoutSeconds;

        /// <summary>
        /// Minimum real time between two completed interstitial ads, enforced by <c>AdPolicyService</c>.
        /// </summary>
        public float InterstitialCooldownSeconds => interstitialCooldownSeconds;

        [Header("Leaderboard")]
        [Tooltip("Off by default in the template. Turn on only after the leaderboard is set up in the platform's developer portal.")]
        [SerializeField] private bool leaderboardEnabled;

        /// <summary>
        /// True when the game submits scores to the platform leaderboard. Off by default.
        /// </summary>
        public bool LeaderboardEnabled => leaderboardEnabled;

        [Tooltip("Base64 score encryption key from the platform developer portal (CrazyGames: Leaderboard settings). " +
                 "Required when the leaderboard is enabled. The key ships inside the game build by design.")]
        [SerializeField] private string leaderboardEncryptionKey = "";

        /// <summary>
        /// Score encryption key for platforms that require encrypted leaderboard submissions.
        /// </summary>
        public string LeaderboardEncryptionKey => leaderboardEncryptionKey;

        [Tooltip("Leaderboard used when game code submits a score without a leaderboard ID. Yandex Games: the " +
                 "leaderboard's technical name in the Developer Console. CrazyGames has one leaderboard and ignores it.")]
        [SerializeField] private string defaultLeaderboardId = "";

        /// <summary>
        /// Leaderboard ID used when game code passes none (Yandex Games: the technical name).
        /// </summary>
        public string DefaultLeaderboardId => defaultLeaderboardId;

        [Header("Google Play (AdMob)")]
        [Tooltip("AdMob interstitial ad unit ID (ca-app-pub-...). Empty shows Google's test ads; the Editor and " +
                 "Development builds always do. The AdMob app ID goes in Assets > Google Mobile Ads > Settings.")]
        [SerializeField] private string adMobInterstitialAdUnitId = "";

        [Tooltip("AdMob rewarded ad unit ID (ca-app-pub-...). Empty shows Google's test ads; the Editor and " +
                 "Development builds always do.")]
        [SerializeField] private string adMobRewardedAdUnitId = "";

        [Tooltip("AdMob banner ad unit ID (ca-app-pub-...), created as an anchored adaptive banner. Empty shows " +
                 "Google's test ads; the Editor and Development builds always do.")]
        [SerializeField] private string adMobBannerAdUnitId = "";

        /// <summary>
        /// AdMob interstitial ad unit ID used by Google Play release builds; empty means Google's test ad unit.
        /// </summary>
        public string AdMobInterstitialAdUnitId => adMobInterstitialAdUnitId;

        /// <summary>
        /// AdMob rewarded ad unit ID used by Google Play release builds; empty means Google's test ad unit.
        /// </summary>
        public string AdMobRewardedAdUnitId => adMobRewardedAdUnitId;

        /// <summary>
        /// AdMob banner ad unit ID used by Google Play release builds; empty means Google's test ad unit.
        /// </summary>
        public string AdMobBannerAdUnitId => adMobBannerAdUnitId;

        [Header("Mobile")]
        [Tooltip("Application.targetFrameRate on Android and iOS, which otherwise run at 30 FPS. Ignored on WebGL and desktop.")]
        [Min(15)]
        [SerializeField] private int mobileTargetFrameRate = 60;

        /// <summary>
        /// Frame rate applied on Android and iOS at startup.
        /// </summary>
        public int MobileTargetFrameRate => mobileTargetFrameRate;

        /// <summary>
        /// The platform the game is built for.
        /// </summary>
        public PlatformType Platform => platform;

        /// <summary>
        /// Maximum time, in real seconds, to wait for the platform SDK initialization callback.
        /// </summary>
        public float SdkInitTimeoutSeconds => sdkInitTimeoutSeconds;
    }
}
