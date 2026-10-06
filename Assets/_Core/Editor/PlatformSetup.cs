using System.Collections.Generic;
using System.Linq;
using System.Text;
using _Core.Platform.Config;
using _Core.Platform.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace _Core.EditorTools
{
    /// <summary>
    /// Selects the platform the game ships to: sets <see cref="PlatformConfig.Platform"/>, exactly one
    /// PLATFORM_X scripting define on every profile build target, and the platform's WebGL template.
    /// Platform bridges in Assets/_Platforms compile only under their define, so inactive bridges are left out
    /// of the build and never register with <see cref="PlatformFactory"/>; their SDK plug-ins are left out of
    /// the build too (<see cref="PlatformBuildAssets"/>).
    /// </summary>
    public static class PlatformSetup
    {
        private const string MenuRoot = "Tools/Template/Platform/";

        // One define per platform. None has no define.
        private static readonly Dictionary<PlatformType, string> Defines = new()
        {
            { PlatformType.CrazyGames, "PLATFORM_CRAZYGAMES" },
            { PlatformType.YandexGames, "PLATFORM_YANDEX" },
            { PlatformType.GooglePlay, "PLATFORM_GOOGLEPLAY" }
        };

        [MenuItem(MenuRoot + "CrazyGames (WebGL)", priority = 40)]
        private static void SelectCrazyGames() => ApplyWithConfirmation(PlatformType.CrazyGames);

        [MenuItem(MenuRoot + "Yandex Games (WebGL)", priority = 41)]
        private static void SelectYandex() => ApplyWithConfirmation(PlatformType.YandexGames);

        [MenuItem(MenuRoot + "Google Play (Android)", priority = 42)]
        private static void SelectGooglePlay() => ApplyWithConfirmation(PlatformType.GooglePlay);

        [MenuItem(MenuRoot + "None (no platform SDK)", priority = 43)]
        private static void SelectNone() => ApplyWithConfirmation(PlatformType.None);

        [MenuItem(MenuRoot + "Show Current Platform", priority = 60)]
        private static void ShowCurrentPlatform()
        {
            string report = DescribeCurrentState();
            Debug.Log("[PlatformSetup] " + report);
            EditorUtility.DisplayDialog("Platform", report, "OK");
        }

        /// <summary>
        /// The scripting define for <paramref name="platform"/>, or null for <see cref="PlatformType.None"/>.
        /// </summary>
        public static string GetDefine(PlatformType platform) =>
            Defines.TryGetValue(platform, out string define) ? define : null;

        /// <summary>
        /// The platform defines set on <paramref name="target"/>. A correctly set up target has at most one.
        /// </summary>
        public static PlatformType[] GetDefinedPlatforms(NamedBuildTarget target)
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
            return Defines.Where(pair => defines.Contains(pair.Value)).Select(pair => pair.Key).ToArray();
        }

        /// <summary>
        /// The build target that <paramref name="platform"/> ships on.
        /// </summary>
        public static BuildTarget GetBuildTarget(PlatformType platform) =>
            platform == PlatformType.GooglePlay ? BuildTarget.Android : BuildTarget.WebGL;

        /// <summary>
        /// The single PlatformConfig asset, or null (with an error) when there is not exactly one.
        /// </summary>
        public static PlatformConfig FindConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(PlatformConfig));
            if (guids.Length != 1)
            {
                Debug.LogError($"[PlatformSetup] Expected exactly one PlatformConfig asset, found {guids.Length}.");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<PlatformConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static void ApplyWithConfirmation(PlatformType platform)
        {
            string define = GetDefine(platform);
            BuildTarget buildTarget = GetBuildTarget(platform);

            bool confirmed = EditorUtility.DisplayDialog(
                $"Select Platform: {platform}",
                $"This sets the project's platform to {platform}:\n\n" +
                $"- PlatformConfig.Platform = {platform}\n" +
                $"- Scripting define {(define ?? "(none)")} for {string.Join(", ", GameProfileSetup.ProfileTargets.Select(t => t.TargetName))}; " +
                "other PLATFORM_ defines are removed\n" +
                "- Bridges and SDK plug-ins of the other platforms are left out of builds\n" +
                $"- WebGL template: {PlatformBuildAssets.GetWebGLTemplate(platform)}\n\n" +
                $"Build target for this platform: {buildTarget} (switch it in Build Profiles; active now: {EditorUserBuildSettings.activeBuildTarget}).\n\n" +
                "Commit first so you can revert.",
                "Apply", "Cancel");

            if (confirmed)
                Apply(platform);
        }

        /// <summary>
        /// Applies <paramref name="platform"/> without asking. Changing the defines triggers a recompile.
        /// </summary>
        public static void Apply(PlatformType platform)
        {
            PlatformConfig config = FindConfig();
            if (config == null)
                return;

            var serialized = new SerializedObject(config);
            SerializedProperty platformProperty = serialized.FindProperty("platform");
            platformProperty.enumValueIndex = (int)platform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            string define = GetDefine(platform);
            foreach (NamedBuildTarget target in GameProfileSetup.ProfileTargets)
            {
                PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
                IEnumerable<string> updated = defines.Where(existing => !Defines.ContainsValue(existing));
                if (define != null)
                    updated = updated.Append(define);

                PlayerSettings.SetScriptingDefineSymbols(target, updated.ToArray());
            }

            string template = PlatformBuildAssets.ApplyWebGLTemplate(platform);

            AssetDatabase.SaveAssets();

            if (EditorUserBuildSettings.activeBuildTarget != GetBuildTarget(platform) && platform != PlatformType.None)
                Debug.LogWarning($"[PlatformSetup] {platform} builds for {GetBuildTarget(platform)}; " +
                                 $"the active build target is {EditorUserBuildSettings.activeBuildTarget}.");

            Debug.Log($"[PlatformSetup] Platform set to {platform} (define: {define ?? "none"}, WebGL template: {template}). Recompiling...");
        }

        private static string DescribeCurrentState()
        {
            var report = new StringBuilder();
            PlatformConfig config = FindConfig();
            report.AppendLine($"PlatformConfig.Platform: {(config != null ? config.Platform.ToString() : "(missing)")}");

            foreach (NamedBuildTarget target in GameProfileSetup.ProfileTargets)
            {
                PlatformType[] defined = GetDefinedPlatforms(target);
                report.AppendLine($"  {target.TargetName}: {(defined.Length == 0 ? "None" : string.Join(", ", defined))}");
            }

            report.AppendLine($"WebGL template: {PlayerSettings.WebGL.template}");
            report.Append($"Active build target: {EditorUserBuildSettings.activeBuildTarget}");
            return report.ToString();
        }
    }
}
