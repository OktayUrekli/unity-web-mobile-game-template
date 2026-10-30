using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using _Core.EditorTools;
using _Core.Platform.Config;
using _Core.Platform.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace _Project.EditorTools
{
    /// <summary>
    /// Validates build settings before every player build (including the CrazySDK
    /// Development/Release Build menus) and fails the build on blocking problems:
    /// scene order, test scenes/harnesses in the build, the PlatformConfig target, its PLATFORM_X define and
    /// WebGL template, the CrazyGames SDK compiled into another platform's WebGL build, and the AdMob IDs of a
    /// Google Play build.
    /// Before the build it also leaves the SDK plug-ins of unselected platforms out (<see cref="PlatformBuildAssets"/>).
    /// After the build it warns when the engine modules don't match the 2D/3D game profile.
    /// </summary>
    public class BuildValidator : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string BootstrapSceneName = "00_Bootstrap";
        private const string TestSceneFolder = "Assets/_Project/Scenes/Test/";
        private const string Physics3DModule = "Physics Module";
        private const string Physics2DModule = "Physics2D Module";

        // Assets/CrazySDK compiles only under PLATFORM_CRAZYGAMES (defineConstraints in its asmdefs);
        // the type exists in the loaded assemblies exactly when the SDK is compiled.
        private const string CrazySdkTypeName = "CrazyGames.CrazySDK";

        // Google Mobile Ads keeps the AdMob app ID in its own settings asset (Assets > Google Mobile Ads > Settings).
        private const string AdMobSettingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
        private const string AdMobAndroidAppIdProperty = "adMobAndroidAppId";

        // Google's sample AdMob account: its app ID and ad unit IDs only ever show test ads.
        private const string AdMobSamplePublisher = "ca-app-pub-3940256099942544";

        // An app ID has '~' where an ad unit ID has '/'; the SDK stops the app at launch on a malformed app ID.
        private static readonly Regex AdMobAppIdPattern = new(@"^ca-app-pub-\d+~\d+$");

        private static readonly string[] TestHarnessFolders =
        {
            "Assets/_Project/Testing/",
            "Assets/_Platforms/CrazyGames/Testing/"
        };

        /// <summary>
        /// Runs before other build preprocessors.
        /// </summary>
        public int callbackOrder => -100;

        /// <summary>
        /// Called by Unity before a player build starts.
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            List<string> leftOutPlugins = PlatformBuildAssets.RegisterPluginFilters();

            bool developmentBuild = (report.summary.options & BuildOptions.Development) != 0;
            List<string> errors = Validate(report.summary.platform, out List<string> warnings, developmentBuild);

            foreach (string warning in warnings)
                Debug.LogWarning($"[BuildValidator] {warning}");

            if (errors.Count > 0)
                throw new BuildFailedException("[BuildValidator] Build blocked:\n- " + string.Join("\n- ", errors));

            if (leftOutPlugins.Count > 0)
                Debug.Log("[BuildValidator] Plug-ins of other platforms left out: " + string.Join(", ", leftOutPlugins));

            Debug.Log("[BuildValidator] All pre-build checks passed.");
        }

        /// <summary>
        /// Called by Unity after a player build. Logs an error when a Yandex Games build has file names Yandex
        /// rejects, and warns (never fails) when a GAME_2D build includes the 3D Physics module or a GAME_3D build
        /// includes Physics2D, naming what pulled the module in.
        /// </summary>
        public void OnPostprocessBuild(BuildReport report)
        {
            ValidateYandexFileNames(report);

            GameProfile profile = GameProfileSetup.GetProfile(
                NamedBuildTarget.FromBuildTargetGroup(report.summary.platformGroup));
            if (profile == GameProfile.Unknown)
                return;

            // Only targets with engine code stripping report modules (e.g. WebGL, Android, iOS).
            StrippingInfo stripping = report.strippingInfo;
            if (stripping == null)
            {
                Debug.Log("[BuildValidator] No engine module info for this build; profile module check skipped.");
                return;
            }

            string unexpectedModule = profile == GameProfile.Game2D ? Physics3DModule : Physics2DModule;
            if (!stripping.includedModules.Contains(unexpectedModule))
            {
                Debug.Log($"[BuildValidator] {profile} build does not include the {unexpectedModule}.");
                return;
            }

            string reasons = string.Join(", ", stripping.GetReasonsForIncluding(unexpectedModule));
            Debug.LogWarning($"[BuildValidator] {profile} build includes the {unexpectedModule}" +
                             (string.IsNullOrEmpty(reasons) ? "." : $", pulled in by: {reasons}."));
        }

        // Yandex Games rejects an upload whose file or folder names contain spaces or non-ASCII characters.
        // Addressables bundles can carry their group's name, and Localization names its groups after each Locale
        // ("Русский"), so a new group needs Bundle Naming "Use Hash of Filename" like the string-table groups.
        private static void ValidateYandexFileNames(BuildReport report)
        {
            string root = report.summary.outputPath;
            if (report.summary.platform != BuildTarget.WebGL || !Directory.Exists(root))
                return;

            PlatformConfig config = PlatformSetup.FindConfig();
            if (config == null || config.Platform != PlatformType.YandexGames)
                return;

            string[] invalid = Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path).Any(c => c == ' ' || c > 127))
                .Select(path => path.Substring(root.Length).TrimStart('/', '\\'))
                .ToArray();

            if (invalid.Length > 0)
                Debug.LogError("[BuildValidator] Yandex Games rejects file names with spaces or non-ASCII characters. " +
                               "Rename these (for Addressables bundles: Bundle Naming \"Use Hash of Filename\") before uploading:\n- " +
                               string.Join("\n- ", invalid));
            else
                Debug.Log("[BuildValidator] Yandex Games file names OK (no spaces or non-ASCII characters).");
        }

        [MenuItem("Tools/Build/Validate Build Settings")]
        private static void ValidateFromMenu()
        {
            List<string> errors = Validate(EditorUserBuildSettings.activeBuildTarget, out List<string> warnings,
                EditorUserBuildSettings.development);

            foreach (string warning in warnings)
                Debug.LogWarning($"[BuildValidator] {warning}");
            foreach (string error in errors)
                Debug.LogError($"[BuildValidator] {error}");

            if (errors.Count == 0)
                Debug.Log("[BuildValidator] All pre-build checks passed.");
        }

        /// <summary>
        /// Runs every check for the given build target and returns the blocking errors.
        /// <paramref name="developmentBuild"/> skips the checks that only matter for a release (AdMob IDs).
        /// </summary>
        public static List<string> Validate(BuildTarget target, out List<string> warnings, bool developmentBuild = false)
        {
            var errors = new List<string>();
            warnings = new List<string>();

            ValidateScenes(errors);
            ValidatePlatformConfig(target, developmentBuild, errors, warnings);
            ValidateGameProfile(target, warnings);

            return errors;
        }

        private static void ValidateGameProfile(BuildTarget target, List<string> warnings)
        {
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            if (GameProfileSetup.GetProfile(NamedBuildTarget.FromBuildTargetGroup(group)) == GameProfile.Unknown)
                warnings.Add($"Set exactly one of {GameProfileSetup.Define2D} / {GameProfileSetup.Define3D} for {group}: " +
                             "Tools/Template/Set Up as 2D Game or Set Up as 3D Game.");
        }

        private static void ValidateScenes(List<string> errors)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                errors.Add("No enabled scenes in Build Settings.");
                return;
            }

            if (Path.GetFileNameWithoutExtension(scenes[0]) != BootstrapSceneName)
                errors.Add($"Build index 0 is '{scenes[0]}', expected '{BootstrapSceneName}' (it initializes all systems).");

            foreach (string scene in scenes)
            {
                if (!File.Exists(scene))
                {
                    errors.Add($"Build scene missing on disk: {scene}");
                    continue;
                }

                if (scene.StartsWith(TestSceneFolder))
                    errors.Add($"Test scene in build: {scene}");

                // Scene dependencies include the MonoScripts of its components.
                foreach (string dependency in AssetDatabase.GetDependencies(scene, false))
                {
                    if (dependency.EndsWith(".cs") && TestHarnessFolders.Any(dependency.StartsWith))
                        errors.Add($"Test harness {Path.GetFileName(dependency)} is used in build scene {scene}.");
                }
            }
        }

        private static void ValidatePlatformConfig(BuildTarget target, bool developmentBuild, List<string> errors,
            List<string> warnings)
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(PlatformConfig));
            if (guids.Length != 1)
            {
                errors.Add($"Expected exactly one PlatformConfig asset, found {guids.Length}.");
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<PlatformConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            PlatformType platform = config.Platform;

            bool matchesTarget = target switch
            {
                BuildTarget.WebGL => platform is PlatformType.CrazyGames or PlatformType.YandexGames,
                BuildTarget.Android => platform == PlatformType.GooglePlay,
                _ => platform != PlatformType.None
            };

            if (!matchesTarget)
                errors.Add($"PlatformConfig.Platform is {platform}, which does not match build target {target}.");

            // Bridges compile only under their PLATFORM_X define; a mismatch silently ships NullPlatform.
            PlatformType[] defined = PlatformSetup.GetDefinedPlatforms(
                NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target)));
            bool definesMatch = platform == PlatformType.None
                ? defined.Length == 0
                : defined.Length == 1 && defined[0] == platform;

            if (!definesMatch)
                errors.Add($"PlatformConfig.Platform is {platform} but the platform defines for {target} are " +
                           $"[{string.Join(", ", defined.Select(PlatformSetup.GetDefine))}]. " +
                           "Select the platform with Tools/Template/Platform.");

            if (target == BuildTarget.WebGL)
            {
                string templateError = PlatformBuildAssets.ValidateWebGLTemplate(platform);
                if (templateError != null)
                    errors.Add(templateError);
            }

            ValidateCrazyGamesSdk(target, platform, defined, errors, warnings);

            if (config.LeaderboardEnabled && platform == PlatformType.CrazyGames &&
                string.IsNullOrWhiteSpace(config.LeaderboardEncryptionKey))
                warnings.Add("PlatformConfig.LeaderboardEnabled is on but LeaderboardEncryptionKey is empty: the leaderboard will stay disabled.");

            if (config.LeaderboardEnabled && platform == PlatformType.YandexGames &&
                string.IsNullOrWhiteSpace(config.DefaultLeaderboardId))
                warnings.Add("PlatformConfig.LeaderboardEnabled is on but DefaultLeaderboardId is empty: scores submitted " +
                             "without a leaderboard ID are dropped. Enter the leaderboard's technical name from the Yandex Developer Console.");

            if (platform == PlatformType.GooglePlay && target == BuildTarget.Android)
                ValidateAdMob(config, developmentBuild, errors, warnings);
        }

        // Google Mobile Ads fails the build itself when the app ID is empty; this says where it goes. Development
        // builds always use Google's test ad units, which work with any app ID, so the ID checks are for releases.
        private static void ValidateAdMob(PlatformConfig config, bool developmentBuild, List<string> errors,
            List<string> warnings)
        {
            Object settings = AssetDatabase.LoadMainAssetAtPath(AdMobSettingsPath);
            string appId = settings != null
                ? new SerializedObject(settings).FindProperty(AdMobAndroidAppIdProperty)?.stringValue?.Trim()
                : null;

            if (string.IsNullOrEmpty(appId))
            {
                errors.Add($"The AdMob Android app ID is empty ({AdMobSettingsPath}). Enter it in Assets > Google " +
                           $"Mobile Ads > Settings; Google's sample app ID {AdMobSamplePublisher}~3347511713 shows test ads.");
                return;
            }

            if (!AdMobAppIdPattern.IsMatch(appId))
            {
                errors.Add($"The AdMob Android app ID ({appId}) is not an app ID (ca-app-pub-...~...). Enter the app ID " +
                           "from AdMob > Apps > App settings in Assets > Google Mobile Ads > Settings; ad unit IDs " +
                           "(ca-app-pub-.../...) go in PlatformConfig.");
                return;
            }

            if (developmentBuild)
                return;

            string appPublisher = GetAdMobPublisher(appId);
            if (appPublisher == AdMobSamplePublisher)
                warnings.Add("This release build uses Google's sample AdMob app ID, so it shows test ads and earns " +
                             "nothing. Enter your AdMob app ID in Assets > Google Mobile Ads > Settings before publishing.");

            var adUnits = new (string Name, string Id)[]
            {
                ("AdMobInterstitialAdUnitId", config.AdMobInterstitialAdUnitId),
                ("AdMobRewardedAdUnitId", config.AdMobRewardedAdUnitId),
                ("AdMobBannerAdUnitId", config.AdMobBannerAdUnitId)
            };

            foreach ((string name, string id) in adUnits)
            {
                string adUnitId = id?.Trim();
                if (string.IsNullOrEmpty(adUnitId) || GetAdMobPublisher(adUnitId) == AdMobSamplePublisher)
                {
                    warnings.Add($"PlatformConfig.{name} is empty or Google's test ad unit, so this release build " +
                                 "shows test ads for that format and earns nothing from it.");
                    continue;
                }

                // An app ID has '~' where an ad unit ID has '/'.
                if (adUnitId.Contains('~'))
                    errors.Add($"PlatformConfig.{name} is an app ID ({adUnitId}); enter the ad unit ID (ca-app-pub-.../...) " +
                               "from AdMob > Apps > Ad units.");
                else if (GetAdMobPublisher(adUnitId) != appPublisher)
                    errors.Add($"PlatformConfig.{name} ({adUnitId}) belongs to another AdMob account than the app ID " +
                               $"({appId}), so it never fills. Use the app ID and ad units of the same AdMob app.");
            }
        }

        // "ca-app-pub-1234~5678" (app ID) and "ca-app-pub-1234/5678" (ad unit ID) both give "ca-app-pub-1234".
        private static string GetAdMobPublisher(string id)
        {
            int end = id.IndexOfAny(new[] { '~', '/' });
            return end < 0 ? id : id.Substring(0, end);
        }

        // CrazySDK runs its SiteLock before the first scene on WebGL and crashes the game on any domain other
        // than CrazyGames or localhost, so it must not ship in another portal's WebGL build.
        private static void ValidateCrazyGamesSdk(BuildTarget target, PlatformType platform, PlatformType[] defined,
            List<string> errors, List<string> warnings)
        {
            if (platform == PlatformType.CrazyGames || !IsCrazySdkCompiled())
                return;

            string cause = defined.Contains(PlatformType.CrazyGames)
                ? $"PLATFORM_CRAZYGAMES is defined for {target}; select the platform with Tools/Template/Platform."
                : "its define constraint is missing (PLATFORM_CRAZYGAMES is not defined). Restore " +
                  "Assets/CrazySDK/Scripts/CrazyGames.SDK.asmdef, Scripts/Editor/CrazyGames.SDK.Editor.asmdef and " +
                  "Demo/CrazyGames.SDK.Demo.asmdef with defineConstraints [\"PLATFORM_CRAZYGAMES\"]; " +
                  "deleting and re-importing Assets/CrazySDK removes them.";

            if (target == BuildTarget.WebGL)
                errors.Add($"The CrazyGames SDK is compiled into this {platform} WebGL build. Its SiteLock runs before " +
                           $"the first scene and crashes the game on any domain other than CrazyGames or localhost. Cause: {cause}");
            else
                warnings.Add($"The CrazyGames SDK is compiled into this {target} build for {platform}; it does nothing " +
                             $"off WebGL but adds code. Cause: {cause}");
        }

        /// <summary>
        /// True when the CrazyGames SDK is compiled for the active build target, whichever assembly holds it.
        /// The Editor compiles with the active target's defines, the same ones the player build uses.
        /// </summary>
        private static bool IsCrazySdkCompiled()
        {
            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && assembly.GetType(CrazySdkTypeName, false) != null)
                    return true;
            }

            return false;
        }
    }
}
