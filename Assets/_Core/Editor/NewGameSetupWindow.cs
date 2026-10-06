using System.Collections.Generic;
using System.Text.RegularExpressions;
using _Core.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace _Core.EditorTools
{
    /// <summary>
    /// First step when starting a new game from the template: sets the identity Player Settings
    /// (company, product, bundle id, version) and the screen orientation for every target in one place.
    /// The orientation also sets the CanvasScaler of the UI canvas and boot loading screen prefabs, the
    /// product name is written to the UI string table as the localized launcher name, and an optional
    /// default app icon and loading screen logo are applied.
    /// Menu: Tools/Template/New Game Setup.
    /// </summary>
    public class NewGameSetupWindow : EditorWindow
    {
        /// <summary>
        /// Screen orientation choices offered by the window.
        /// </summary>
        public enum GameOrientation
        {
            Landscape,
            Portrait,
            AutoRotation
        }

        // Localized launcher name: Localization Settings > Android App Info points at this entry.
        private const string AppNameTable = "UI";
        private const string AppNameKey = "app.name";

        // Id of app.name in the template's UI table. Reused when the entry has to be created again,
        // so references to it by id (the App Info metadata) keep working.
        private const long AppNameKeyId = 1230000153;

        // Private serialized field of LoadingScreen that holds the logo Image.
        private const string LogoImageField = "logoImage";

        // CanvasScaler presets. Landscape and AutoRotation keep the template's 1920x1080 with a balanced
        // match; Portrait uses 1080x1920 and matches the width.
        private const float LandscapeMatch = 0.5f;
        private const float PortraitMatch = 0f;
        private static readonly Vector2 LandscapeReference = new(1920f, 1080f);
        private static readonly Vector2 PortraitReference = new(1080f, 1920f);

        private static readonly string[] ProjectFolders = { "Assets" };

        private static readonly NamedBuildTarget[] IdentifierTargets =
        {
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS,
            NamedBuildTarget.Standalone
        };

        // Reverse-domain id accepted by both Google Play and the App Store: at least two segments,
        // each starting with a letter.
        private static readonly Regex BundleIdPattern = new(@"^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$");

        private static readonly GUIContent OrientationLabel = new("Orientation",
            "Also sets the CanvasScaler of the UI canvas and loading screen prefabs: Portrait 1080x1920 " +
            "matching width, Landscape and Auto Rotation 1920x1080 with match 0.5.");

        private static readonly GUIContent AppIconLabel = new("App Icon",
            "Default icon for every platform (Player Settings > Icon). Empty keeps the current icons.");

        private static readonly GUIContent LoadingLogoLabel = new("Loading Logo",
            "Logo sprite on the boot loading screen prefab. Empty keeps the current logo.");

        private string _companyName;
        private string _productName;
        private string _bundleId;
        private string _version;
        private int _buildNumber;
        private GameOrientation _orientation;
        private Texture2D _appIcon;
        private Sprite _loadingLogo;
        private bool _loadingScreenFound;

        [MenuItem("Tools/Template/New Game Setup", priority = 0)]
        private static void Open()
        {
            GetWindow<NewGameSetupWindow>(true, "New Game Setup").minSize = new Vector2(420f, 470f);
        }

        private void OnEnable()
        {
            _companyName = PlayerSettings.companyName;
            _productName = PlayerSettings.productName;
            _bundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            _version = PlayerSettings.bundleVersion;
            _buildNumber = PlayerSettings.Android.bundleVersionCode;
            _orientation = PlayerSettings.defaultInterfaceOrientation switch
            {
                UIOrientation.Portrait or UIOrientation.PortraitUpsideDown => GameOrientation.Portrait,
                UIOrientation.AutoRotation => GameOrientation.AutoRotation,
                _ => GameOrientation.Landscape
            };

            _appIcon = GetDefaultIcon();

            LoadingScreen loadingScreen = FindLoadingScreenPrefab();
            _loadingScreenFound = loadingScreen != null;
            Image logoImage = GetLogoImage(loadingScreen);
            _loadingLogo = logoImage != null ? logoImage.sprite : null;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Sets Player Settings for Android, iOS and Standalone; Orientation also sets the UI canvas and " +
                "loading screen CanvasScaler. Product Name is also the localized launcher name " +
                "(UI table, app.name, every locale).\n" +
                "Next: Tools/Template/Set Up as 2D/3D Game, then pick the platform in Tools/Template/Platform.",
                MessageType.Info);

            _companyName = EditorGUILayout.TextField("Company Name", _companyName);
            _productName = EditorGUILayout.TextField("Product Name", _productName);

            EditorGUILayout.BeginHorizontal();
            _bundleId = EditorGUILayout.TextField("Bundle ID", _bundleId);
            if (GUILayout.Button("Suggest", GUILayout.Width(70f)))
                _bundleId = SuggestBundleId(_companyName, _productName);
            EditorGUILayout.EndHorizontal();

            _version = EditorGUILayout.TextField("Version", _version);
            _buildNumber = Mathf.Max(1, EditorGUILayout.IntField("Build Number", _buildNumber));
            _orientation = (GameOrientation)EditorGUILayout.EnumPopup(OrientationLabel, _orientation);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Optional", EditorStyles.boldLabel);
            _appIcon = (Texture2D)EditorGUILayout.ObjectField(AppIconLabel, _appIcon, typeof(Texture2D), false);
            using (new EditorGUI.DisabledScope(!_loadingScreenFound))
            {
                _loadingLogo = (Sprite)EditorGUILayout.ObjectField(LoadingLogoLabel, _loadingLogo, typeof(Sprite), false);
            }

            string error = Validate();
            if (error != null)
                EditorGUILayout.HelpBox(error, MessageType.Warning);

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(error != null))
            {
                if (GUILayout.Button("Apply", GUILayout.Height(30f)))
                {
                    Apply();
                    // Prefab saves and asset imports ran inside this GUI event; end it cleanly.
                    GUIUtility.ExitGUI();
                }
            }
        }

        private string Validate()
        {
            if (string.IsNullOrWhiteSpace(_companyName) || string.IsNullOrWhiteSpace(_productName))
                return "Company and product name are required.";

            if (string.IsNullOrEmpty(_bundleId) || !BundleIdPattern.IsMatch(_bundleId))
                return "Bundle ID must look like com.company.game (letters, digits, underscores, at least two parts).";

            if (string.IsNullOrWhiteSpace(_version))
                return "Version is required (for example 1.0.0).";

            return null;
        }

        private void Apply()
        {
            string productName = _productName.Trim();
            PlayerSettings.companyName = _companyName.Trim();
            PlayerSettings.productName = productName;

            foreach (NamedBuildTarget target in IdentifierTargets)
                PlayerSettings.SetApplicationIdentifier(target, _bundleId);

            PlayerSettings.bundleVersion = _version.Trim();
            PlayerSettings.Android.bundleVersionCode = _buildNumber;
            PlayerSettings.iOS.buildNumber = _buildNumber.ToString();

            ApplyOrientation(_orientation);

            var changes = new List<string>();
            if (_appIcon != null && ApplyDefaultIcon(_appIcon))
                changes.Add("default icon");

            ApplyUICanvasScaling(_orientation, changes);
            ApplyLoadingScreen(_orientation, _loadingLogo, changes);

            if (ApplyAppName(productName))
                changes.Add($"{AppNameTable}/{AppNameKey}");

            AssetDatabase.SaveAssets();

            Debug.Log($"[NewGameSetup] {PlayerSettings.productName} ({_bundleId}) {PlayerSettings.bundleVersion} " +
                      $"build {_buildNumber}, {_orientation}. Assets updated: " +
                      $"{(changes.Count > 0 ? string.Join(", ", changes) : "none")}.");
        }

        private static void ApplyOrientation(GameOrientation orientation)
        {
            bool landscape = orientation != GameOrientation.Portrait;
            bool portrait = orientation != GameOrientation.Landscape;

            PlayerSettings.allowedAutorotateToLandscapeLeft = landscape;
            PlayerSettings.allowedAutorotateToLandscapeRight = landscape;
            PlayerSettings.allowedAutorotateToPortrait = portrait;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // Landscape and Portrait still auto-rotate between their two (or one) allowed sides.
            PlayerSettings.defaultInterfaceOrientation = orientation == GameOrientation.Portrait
                ? UIOrientation.Portrait
                : UIOrientation.AutoRotation;
        }

        // The Default Icon of Player Settings > Icon (used by every platform without its own icons), or null.
        private static Texture2D GetDefaultIcon()
        {
            Texture2D[] icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            return icons != null && icons.Length > 0 ? icons[0] : null;
        }

        // Sets the Default Icon. Like the Player Settings inspector, only the first slot is written.
        private static bool ApplyDefaultIcon(Texture2D icon)
        {
            Texture2D[] icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (icons == null || icons.Length == 0)
                icons = new Texture2D[1];
            else if (icons[0] == icon)
                return false;

            icons[0] = icon;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, icons, IconKind.Any);
            return true;
        }

        // Scales the canvas prefab of every UIConfig asset (normally exactly one) for the orientation.
        private static void ApplyUICanvasScaling(GameOrientation orientation, List<string> changes)
        {
            var canvasPaths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(UIConfig)))
            {
                var config = AssetDatabase.LoadAssetAtPath<UIConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (config == null || config.UICanvasPrefab == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(config.UICanvasPrefab);
                if (!canvasPaths.Contains(path))
                    canvasPaths.Add(path);
            }

            if (canvasPaths.Count == 0)
            {
                Debug.LogWarning("[NewGameSetup] No UIConfig asset with a UI Canvas Prefab found; UI scaling not applied.");
                return;
            }

            foreach (string path in canvasPaths)
            {
                bool saved = EditPrefab(path, root =>
                {
                    CanvasScaler scaler = root.GetComponentInChildren<CanvasScaler>(true);
                    if (scaler != null)
                        return ApplyScaling(scaler, orientation);

                    Debug.LogWarning($"[NewGameSetup] {path} has no CanvasScaler; UI scaling not applied to it.");
                    return false;
                });

                if (saved)
                    changes.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)} scaling");
            }
        }

        // Scales the boot loading screen prefab's own canvas, if it has one, and sets its logo when one is given.
        private static void ApplyLoadingScreen(GameOrientation orientation, Sprite logo, List<string> changes)
        {
            LoadingScreen prefabLoadingScreen = FindLoadingScreenPrefab();
            if (prefabLoadingScreen == null)
            {
                Debug.LogWarning("[NewGameSetup] No prefab with a LoadingScreen component found; loading screen not updated.");
                return;
            }

            string path = AssetDatabase.GetAssetPath(prefabLoadingScreen);
            string prefabName = System.IO.Path.GetFileNameWithoutExtension(path);
            bool scaled = false;
            bool logoSet = false;

            bool saved = EditPrefab(path, root =>
            {
                // Without its own CanvasScaler the loading screen follows the canvas it is placed under.
                CanvasScaler scaler = root.GetComponentInChildren<CanvasScaler>(true);
                scaled = scaler != null && ApplyScaling(scaler, orientation);

                if (logo != null)
                {
                    Image logoImage = GetLogoImage(root.GetComponentInChildren<LoadingScreen>(true));
                    if (logoImage != null)
                        logoSet = SetLogo(logoImage, logo);
                    else
                        Debug.LogWarning($"[NewGameSetup] {path}: LoadingScreen has no Logo Image assigned; logo not set.");
                }

                return scaled || logoSet;
            });

            if (saved && scaled)
                changes.Add($"{prefabName} scaling");
            if (saved && logoSet)
                changes.Add($"{prefabName} logo");
        }

        // Sets the orientation's reference resolution and match; true when a value changed.
        private static bool ApplyScaling(CanvasScaler scaler, GameOrientation orientation)
        {
            bool portrait = orientation == GameOrientation.Portrait;
            Vector2 reference = portrait ? PortraitReference : LandscapeReference;
            float match = portrait ? PortraitMatch : LandscapeMatch;

            if (scaler.referenceResolution == reference && Mathf.Approximately(scaler.matchWidthOrHeight, match))
                return false;

            scaler.referenceResolution = reference;
            scaler.matchWidthOrHeight = match;
            return true;
        }

        // Puts the logo on the Image and makes sure it is shown; true when anything changed.
        private static bool SetLogo(Image image, Sprite logo)
        {
            if (image.sprite == logo && image.enabled && image.gameObject.activeSelf)
                return false;

            image.sprite = logo;
            image.enabled = true;
            image.gameObject.SetActive(true);
            return true;
        }

        // Opens the prefab in an isolated scene, applies modify and saves only when modify reports a change.
        // Returns whether the prefab was saved.
        private static bool EditPrefab(string path, System.Func<GameObject, bool> modify)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!modify(root))
                    return false;

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved)
                    Debug.LogError($"[NewGameSetup] Could not save {path}.");

                return saved;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The LoadingScreen component of the boot loading screen prefab: prefabs named LoadingScreen are
        // checked first, then every prefab under Assets. Null when there is none.
        private static LoadingScreen FindLoadingScreenPrefab()
        {
            LoadingScreen byName = FindInPrefabs<LoadingScreen>("t:Prefab " + nameof(LoadingScreen));
            return byName != null ? byName : FindInPrefabs<LoadingScreen>("t:Prefab");
        }

        private static T FindInPrefabs<T>(string filter) where T : Component
        {
            foreach (string guid in AssetDatabase.FindAssets(filter, ProjectFolders))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null)
                    continue;

                T component = prefab.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }

            return null;
        }

        // LoadingScreen keeps its logo Image in a private serialized field.
        private static Image GetLogoImage(LoadingScreen loadingScreen)
        {
            if (loadingScreen == null)
                return null;

            using var serialized = new SerializedObject(loadingScreen);
            SerializedProperty property = serialized.FindProperty(LogoImageField);
            return property != null ? property.objectReferenceValue as Image : null;
        }

        // Writes the product name to UI/app.name in every locale of the collection, creating the entry when
        // it is missing. True when a table changed.
        private static bool ApplyAppName(string productName)
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(AppNameTable);
            if (collection == null || collection.SharedData == null)
            {
                Debug.LogWarning($"[NewGameSetup] String table collection '{AppNameTable}' not found; " +
                                 $"{AppNameKey} not written.");
                return false;
            }

            SharedTableData sharedData = collection.SharedData;
            SharedTableData.SharedTableEntry entry = sharedData.GetEntry(AppNameKey);
            bool changed = false;

            if (entry == null)
            {
                // AddKey(key, id) returns null when the id is taken; fall back to a generated id.
                entry = sharedData.AddKey(AppNameKey, AppNameKeyId) ?? sharedData.AddKey(AppNameKey);
                if (entry == null)
                {
                    Debug.LogWarning($"[NewGameSetup] Could not add {AppNameKey} to the {AppNameTable} table.");
                    return false;
                }

                EditorUtility.SetDirty(sharedData);
                changed = true;
            }

            foreach (StringTable table in collection.StringTables)
            {
                if (table == null)
                    continue;

                StringTableEntry tableEntry = table.GetEntry(entry.Id);
                if (tableEntry != null && tableEntry.Value == productName)
                    continue;

                table.AddEntry(entry.Id, productName);
                EditorUtility.SetDirty(table);
                changed = true;
            }

            // Refreshes an open Localization Tables window.
            if (changed)
                LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);

            return changed;
        }

        private static string SuggestBundleId(string company, string product)
        {
            return $"com.{ToIdSegment(company)}.{ToIdSegment(product)}";
        }

        private static string ToIdSegment(string value)
        {
            string segment = Regex.Replace((value ?? string.Empty).ToLowerInvariant(), "[^a-z0-9_]", "");
            if (segment.Length == 0)
                return "game";

            return char.IsLetter(segment[0]) ? segment : "g" + segment;
        }
    }
}
