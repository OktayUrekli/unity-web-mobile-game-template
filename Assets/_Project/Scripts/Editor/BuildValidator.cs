using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// WebGL template, and the CrazyGames SDK compiled into another platform's WebGL build.
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

            List<string> errors = Validate(report.summary.platform, out List<string> warnings);

            foreach (string warning in warnings)
                Debug.LogWarning($"[BuildValidator] {warning}");

            if (errors.Count > 0)
                throw new BuildFailedException("[BuildValidator] Build blocked:\n- " + string.Join("\n- ", errors));

            if (leftOutPlugins.Count > 0)
                Debug.Log("[BuildValidator] Plug-ins of other platforms left out: " + string.Join(", ", leftOutPlugins));

            Debug.Log("[BuildValidator] All pre-build checks passed.");
        }

        /// <summary>
        /// Called by Unity after a player build. Warns (never fails) when a GAME_2D build includes the
        /// 3D Physics module or a GAME_3D build includes Physics2D, naming what pulled the module in.
        /// </summary>
        public void OnPostprocessBuild(BuildReport report)
        {
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

        [MenuItem("Tools/Build/Validate Build Settings")]
        private static void ValidateFromMenu()
        {
            List<string> errors = Validate(EditorUserBuildSettings.activeBuildTarget, out List<string> warnings);

            foreach (string warning in warnings)
                Debug.LogWarning($"[BuildValidator] {warning}");
            foreach (string error in errors)
                Debug.LogError($"[BuildValidator] {error}");

            if (errors.Count == 0)
                Debug.Log("[BuildValidator] All pre-build checks passed.");
        }

        /// <summary>
        /// Runs every check for the given build target and returns the blocking errors.
        /// </summary>
        public static List<string> Validate(BuildTarget target, out List<string> warnings)
        {
            var errors = new List<string>();
            warnings = new List<string>();

            ValidateScenes(errors);
            ValidatePlatformConfig(target, errors, warnings);
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

        private static void ValidatePlatformConfig(BuildTarget target, List<string> errors, List<string> warnings)
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

            if (config.LeaderboardEnabled && string.IsNullOrWhiteSpace(config.LeaderboardEncryptionKey))
                warnings.Add("PlatformConfig.LeaderboardEnabled is on but LeaderboardEncryptionKey is empty: the leaderboard will stay disabled.");
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
