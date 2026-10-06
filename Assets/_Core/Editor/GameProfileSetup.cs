using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Core.EditorTools
{
    /// <summary>
    /// The game profile the template is configured for.
    /// </summary>
    public enum GameProfile
    {
        Unknown,
        Game2D,
        Game3D
    }

    /// <summary>
    /// Switches the project between the 2D and 3D game profiles: render pipeline, editor behavior mode,
    /// the unused physics engine's simulation mode, the GAME_2D/GAME_3D scripting define and the
    /// profile-specific packages. Physics settings are edited as serialized project settings so this
    /// compiles whichever physics module is installed.
    /// </summary>
    public static class GameProfileSetup
    {
        /// <summary>
        /// Scripting define set by the 2D profile.
        /// </summary>
        public const string Define2D = "GAME_2D";

        /// <summary>
        /// Scripting define set by the 3D profile.
        /// </summary>
        public const string Define3D = "GAME_3D";

        // DOTween's own switches for its Physics/Physics2D modules (Assets/ThirdParty/.../Modules).
        // The 3D profile must drop the Physics2D module, which no longer compiles without physics2d.
        private const string DOTweenNoPhysics = "DOTWEEN_NOPHYSICS";
        private const string DOTweenNoPhysics2D = "DOTWEEN_NOPHYSICS2D";

        private const string MenuRoot = "Tools/Template/";
        private const string Pipeline2DPath = "Assets/Settings/URP_2D.asset";
        private const string Pipeline3DPath = "Assets/Settings/URP_3D.asset";
        private const string ManifestPath = "Packages/manifest.json";

        // Values of UnityEngine.SimulationMode and SimulationMode2D (same order in both enums).
        private const int SimulationFixedUpdate = 0;
        private const int SimulationScript = 2;

        /// <summary>
        /// The build targets whose scripting defines carry the profile.
        /// </summary>
        public static readonly NamedBuildTarget[] ProfileTargets =
        {
            NamedBuildTarget.WebGL,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS,
            NamedBuildTarget.Standalone
        };

        // Only the 2D profile lists these. com.unity.2d.sprite stays in both (Sprite Editor for UI sprites).
        private static readonly Dictionary<string, string> Packages2D = new()
        {
            { "com.unity.2d.animation", "13.0.6" },
            { "com.unity.2d.aseprite", "3.0.2" },
            { "com.unity.2d.psdimporter", "12.0.2" },
            { "com.unity.2d.spriteshape", "13.0.0" },
            { "com.unity.2d.tilemap", "1.0.0" },
            { "com.unity.2d.tilemap.extras", "6.0.3" },
            { "com.unity.2d.tooling", "1.0.4" },
            { "com.unity.modules.physics2d", "1.0.0" },
            { "com.unity.modules.tilemap", "1.0.0" }
        };

        // Only the 3D profile lists these. In the 2D profile 3D physics is still a transitive dependency
        // of URP and UI Toolkit; engine stripping removes it from builds that don't use it.
        private static readonly Dictionary<string, string> Packages3D = new()
        {
            { "com.unity.modules.physics", "1.0.0" }
        };

        private static AddAndRemoveRequest _packageRequest;

        [MenuItem(MenuRoot + "Set Up as 2D Game", priority = 1)]
        private static void SetUp2DFromMenu() => ApplyWithConfirmation(GameProfile.Game2D);

        [MenuItem(MenuRoot + "Set Up as 3D Game", priority = 2)]
        private static void SetUp3DFromMenu() => ApplyWithConfirmation(GameProfile.Game3D);

        [MenuItem(MenuRoot + "Show Current Profile", priority = 20)]
        private static void ShowCurrentProfile()
        {
            string report = DescribeCurrentState();
            Debug.Log("[GameProfile] " + report);
            EditorUtility.DisplayDialog("Game Profile", report, "OK");
        }

        /// <summary>
        /// The profile set by the scripting defines of the active build target.
        /// Unknown when neither or both defines are set.
        /// </summary>
        public static GameProfile Current => GetProfile(NamedBuildTarget.FromBuildTargetGroup(
            BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget)));

        /// <summary>
        /// The profile set by the scripting defines of <paramref name="target"/>.
        /// </summary>
        public static GameProfile GetProfile(NamedBuildTarget target)
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
            bool is2D = defines.Contains(Define2D);
            bool is3D = defines.Contains(Define3D);

            if (is2D == is3D)
                return GameProfile.Unknown;

            return is2D ? GameProfile.Game2D : GameProfile.Game3D;
        }

        private static void ApplyWithConfirmation(GameProfile profile)
        {
            string name = profile == GameProfile.Game2D ? "2D" : "3D";
            string packages = profile == GameProfile.Game2D
                ? "adds the 2D packages (animation, tilemap, sprite shape, Physics 2D...)"
                : "removes the 2D packages (animation, tilemap, sprite shape, Physics 2D...) and adds 3D Physics";

            bool confirmed = EditorUtility.DisplayDialog(
                $"Set Up as {name} Game",
                $"This switches the project to the {name} profile:\n\n" +
                $"- Render pipeline: {(profile == GameProfile.Game2D ? Pipeline2DPath : Pipeline3DPath)} (default and every quality level)\n" +
                $"- Editor default behavior mode: {name}\n" +
                $"- Unused physics engine set to Script simulation\n" +
                $"- Scripting defines {(profile == GameProfile.Game2D ? $"{Define2D}, {DOTweenNoPhysics}" : $"{Define3D}, {DOTweenNoPhysics2D}")} for WebGL, Android, iOS, Standalone\n" +
                $"- Package Manager {packages}\n\n" +
                "Existing scenes, cameras and assets are not changed. Commit first so you can revert.",
                "Apply", "Cancel");

            if (confirmed)
                Apply(profile);
        }

        /// <summary>
        /// Applies <paramref name="profile"/> without asking. Packages change last; the Package Manager
        /// request finishes asynchronously and triggers a recompile.
        /// </summary>
        public static void Apply(GameProfile profile)
        {
            if (profile == GameProfile.Unknown)
            {
                Debug.LogError("[GameProfile] Cannot apply the Unknown profile.");
                return;
            }

            if (_packageRequest != null && !_packageRequest.IsCompleted)
            {
                Debug.LogError("[GameProfile] A profile package change is still running; try again when it finishes.");
                return;
            }

            bool is2D = profile == GameProfile.Game2D;
            string pipelinePath = is2D ? Pipeline2DPath : Pipeline3DPath;
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                Debug.LogError($"[GameProfile] Render pipeline asset not found at {pipelinePath}.");
                return;
            }

            SetRenderPipeline(pipeline);
            EditorSettings.defaultBehaviorMode = is2D ? EditorBehaviorMode.Mode2D : EditorBehaviorMode.Mode3D;
            SetSimulationMode("ProjectSettings/DynamicsManager.asset", is2D ? SimulationScript : SimulationFixedUpdate);
            SetSimulationMode("ProjectSettings/Physics2DSettings.asset", is2D ? SimulationFixedUpdate : SimulationScript);
            SetDefines(
                is2D ? new[] { Define2D, DOTweenNoPhysics } : new[] { Define3D, DOTweenNoPhysics2D },
                is2D ? new[] { Define3D, DOTweenNoPhysics2D } : new[] { Define2D, DOTweenNoPhysics });
            AssetDatabase.SaveAssets();

            Debug.Log($"[GameProfile] Settings switched to {profile}. Updating packages...");
            ChangePackages(is2D ? Packages2D : Packages3D, is2D ? Packages3D : Packages2D);
        }

        private static void SetRenderPipeline(RenderPipelineAsset pipeline)
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;

            Object qualitySettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")
                .FirstOrDefault();
            if (qualitySettings == null)
            {
                Debug.LogError("[GameProfile] Could not load QualitySettings; set the quality levels' pipeline by hand.");
                return;
            }

            var serialized = new SerializedObject(qualitySettings);
            SerializedProperty levels = serialized.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The active level's pipeline is cached at runtime; refresh it so the Editor renders with the new one.
            QualitySettings.renderPipeline = pipeline;
        }

        private static void SetSimulationMode(string settingsPath, int mode)
        {
            Object settings = AssetDatabase.LoadAllAssetsAtPath(settingsPath).FirstOrDefault();
            SerializedProperty property = settings != null
                ? new SerializedObject(settings).FindProperty("m_SimulationMode")
                : null;

            if (property == null)
            {
                Debug.LogWarning($"[GameProfile] No m_SimulationMode in {settingsPath} (module not installed?); skipped.");
                return;
            }

            property.intValue = mode;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetDefines(string[] add, string[] remove)
        {
            foreach (NamedBuildTarget target in ProfileTargets)
            {
                PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
                string[] updated = defines.Where(define => !add.Contains(define) && !remove.Contains(define))
                    .Concat(add)
                    .ToArray();
                PlayerSettings.SetScriptingDefineSymbols(target, updated);
            }
        }

        private static void ChangePackages(Dictionary<string, string> add, Dictionary<string, string> remove)
        {
            string manifest = File.ReadAllText(ManifestPath);

            string[] toAdd = add.Where(package => !IsListed(manifest, package.Key))
                .Select(package => $"{package.Key}@{package.Value}")
                .ToArray();
            string[] toRemove = remove.Keys.Where(name => IsListed(manifest, name)).ToArray();

            if (toAdd.Length == 0 && toRemove.Length == 0)
            {
                Debug.Log("[GameProfile] Packages already match the profile.");
                return;
            }

            _packageRequest = Client.AddAndRemove(toAdd, toRemove);
            EditorApplication.update += WaitForPackageRequest;
        }

        private static bool IsListed(string manifest, string packageName) =>
            manifest.Contains($"\"{packageName}\"");

        private static void WaitForPackageRequest()
        {
            if (_packageRequest == null || !_packageRequest.IsCompleted)
                return;

            EditorApplication.update -= WaitForPackageRequest;

            if (_packageRequest.Status == StatusCode.Success)
                Debug.Log("[GameProfile] Packages updated.");
            else
                Debug.LogError($"[GameProfile] Package change failed: {_packageRequest.Error?.message}");

            _packageRequest = null;
        }

        private static string DescribeCurrentState()
        {
            var report = new StringBuilder();
            report.AppendLine($"Profile (active build target defines): {Current}");

            foreach (NamedBuildTarget target in ProfileTargets)
                report.AppendLine($"  {target.TargetName}: {GetProfile(target)}");

            report.AppendLine($"Default render pipeline: {AssetPathOrNone(GraphicsSettings.defaultRenderPipeline)}");
            for (int i = 0; i < QualitySettings.names.Length; i++)
                report.AppendLine($"  Quality '{QualitySettings.names[i]}': {AssetPathOrNone(QualitySettings.GetRenderPipelineAssetAt(i))}");

            report.AppendLine($"Editor behavior mode: {EditorSettings.defaultBehaviorMode}");

            string manifest = File.ReadAllText(ManifestPath);
            report.AppendLine($"2D packages listed: {Packages2D.Keys.Count(name => IsListed(manifest, name))}/{Packages2D.Count}");
            report.Append($"3D Physics listed explicitly: {IsListed(manifest, Packages3D.Keys.First())}");

            return report.ToString();
        }

        private static string AssetPathOrNone(Object asset) =>
            asset != null ? AssetDatabase.GetAssetPath(asset) : "(none)";
    }
}
