using System;
using System.Collections.Generic;
using System.Linq;
using _Core.Platform.Core;
using UnityEditor;
using UnityEditor.Build;

namespace _Core.EditorTools
{
    /// <summary>
    /// The per-platform build files that scripting defines cannot switch: the WebGL template and native or
    /// precompiled SDK plug-ins. <see cref="PlatformSetup"/> applies the template when a platform is selected,
    /// and a build links a platform's plug-ins only while that platform's PLATFORM_X define is set.
    /// <para>
    /// Conventions: a WebGL template in <c>Assets/WebGLTemplates/&lt;PlatformType&gt;</c> (for example
    /// <c>Assets/WebGLTemplates/YandexGames</c>) is that platform's template, otherwise Unity's Default is used.
    /// Plug-ins under <c>Assets/_Platforms/&lt;PlatformType&gt;/</c> belong to that platform; SDK plug-ins that
    /// stay in a vendor folder are listed in <see cref="VendorPlugins"/>.
    /// </para>
    /// </summary>
    public static class PlatformBuildAssets
    {
        /// <summary>
        /// Folder holding the project's WebGL templates; Unity only looks here.
        /// </summary>
        public const string WebGLTemplatesFolder = "Assets/WebGLTemplates";

        /// <summary>
        /// Unity's built-in template, used by platforms without their own.
        /// </summary>
        public const string DefaultWebGLTemplate = "APPLICATION:Default";

        private const string ProjectTemplatePrefix = "PROJECT:";
        private const string BridgesFolder = "Assets/_Platforms/";

        // SDK plug-ins outside Assets/_Platforms/<PlatformType>/ (vendor folders are never moved). A path ending in
        // '/' covers the whole folder. Add a platform's SDK plug-ins here when its SDK is imported.
        private static readonly Dictionary<PlatformType, string[]> VendorPlugins = new()
        {
            { PlatformType.CrazyGames, new[] { "Assets/Plugins/crazySDK.jslib" } }
        };

        /// <summary>
        /// The WebGL template <paramref name="platform"/> builds with: <c>PROJECT:&lt;PlatformType&gt;</c> when
        /// <c>Assets/WebGLTemplates/&lt;PlatformType&gt;</c> exists, otherwise <see cref="DefaultWebGLTemplate"/>.
        /// </summary>
        public static string GetWebGLTemplate(PlatformType platform)
        {
            if (platform != PlatformType.None && AssetDatabase.IsValidFolder($"{WebGLTemplatesFolder}/{platform}"))
                return ProjectTemplatePrefix + platform;

            return DefaultWebGLTemplate;
        }

        /// <summary>
        /// Sets the WebGL template of <paramref name="platform"/> in Player Settings and returns it.
        /// </summary>
        public static string ApplyWebGLTemplate(PlatformType platform)
        {
            string template = GetWebGLTemplate(platform);
            if (PlayerSettings.WebGL.template != template)
                PlayerSettings.WebGL.template = template;

            return template;
        }

        /// <summary>
        /// Null when the WebGL template suits <paramref name="platform"/>; otherwise why it does not: the platform
        /// has its own template and another one is selected, or another platform's template is selected (it would
        /// load that portal's SDK script).
        /// </summary>
        public static string ValidateWebGLTemplate(PlatformType platform)
        {
            string current = PlayerSettings.WebGL.template;
            string expected = GetWebGLTemplate(platform);

            if (expected != DefaultWebGLTemplate && current != expected)
                return $"{platform} has its own WebGL template ({expected}) but {current} is selected. " +
                       "Select the platform again with Tools/Template/Platform.";

            PlatformType? owner = GetTemplateOwner(current);
            if (owner.HasValue && owner.Value != platform)
                return $"The WebGL template {current} belongs to {owner.Value}, not {platform}. " +
                       "Select the platform again with Tools/Template/Platform.";

            return null;
        }

        /// <summary>
        /// The platform a plug-in belongs to, or null for a plug-in every platform uses.
        /// </summary>
        public static PlatformType? GetPluginOwner(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            foreach (KeyValuePair<PlatformType, string[]> pair in VendorPlugins)
            {
                foreach (string path in pair.Value)
                {
                    bool matches = path.EndsWith("/")
                        ? assetPath.StartsWith(path, StringComparison.OrdinalIgnoreCase)
                        : string.Equals(assetPath, path, StringComparison.OrdinalIgnoreCase);

                    if (matches)
                        return pair.Key;
                }
            }

            if (!assetPath.StartsWith(BridgesFolder, StringComparison.Ordinal))
                return null;

            int folderEnd = assetPath.IndexOf('/', BridgesFolder.Length);
            if (folderEnd < 0)
                return null;

            return ParsePlatform(assetPath.Substring(BridgesFolder.Length, folderEnd - BridgesFolder.Length));
        }

        /// <summary>
        /// True when a build for the active build target links the plug-in at <paramref name="assetPath"/>:
        /// shared plug-ins always, platform plug-ins only while their platform's define is set.
        /// </summary>
        public static bool IsPluginIncludedInBuild(string assetPath)
        {
            PlatformType? owner = GetPluginOwner(assetPath);
            if (!owner.HasValue)
                return true;

            NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            return PlatformSetup.GetDefinedPlatforms(target).Contains(owner.Value);
        }

        /// <summary>
        /// Hooks every platform plug-in to <see cref="IsPluginIncludedInBuild"/>. Unity before 6000.6 ignores
        /// define constraints on native plug-ins (the one in crazySDK.jslib.meta only records the intent), and an
        /// unused SDK's global JS functions could override another portal SDK's. The hooks last until the next
        /// domain reload, so this runs after every load and again before each build. Returns the platform plug-ins
        /// the active build target leaves out.
        /// </summary>
        public static List<string> RegisterPluginFilters()
        {
            var leftOut = new List<string>();
            foreach (PluginImporter importer in PluginImporter.GetAllImporters())
            {
                if (!GetPluginOwner(importer.assetPath).HasValue)
                    continue;

                importer.SetIncludeInBuildDelegate(IsPluginIncludedInBuild);
                if (!IsPluginIncludedInBuild(importer.assetPath))
                    leftOut.Add(importer.assetPath);
            }

            return leftOut;
        }

        [InitializeOnLoadMethod]
        private static void RegisterPluginFiltersOnLoad()
        {
            // After the load finishes, so the asset database is ready.
            EditorApplication.delayCall += () => RegisterPluginFilters();
        }

        private static PlatformType? GetTemplateOwner(string template)
        {
            if (string.IsNullOrEmpty(template) || !template.StartsWith(ProjectTemplatePrefix, StringComparison.Ordinal))
                return null;

            return ParsePlatform(template.Substring(ProjectTemplatePrefix.Length));
        }

        // Exact PlatformType names only (Enum.TryParse would also accept numbers); None is not a platform.
        private static PlatformType? ParsePlatform(string name)
        {
            foreach (PlatformType platform in Enum.GetValues(typeof(PlatformType)))
            {
                if (platform != PlatformType.None && platform.ToString() == name)
                    return platform;
            }

            return null;
        }
    }
}
