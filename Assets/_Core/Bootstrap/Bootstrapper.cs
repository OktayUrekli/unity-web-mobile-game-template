using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using _Core.Audio;
using _Core.Configuration;
using _Core.Events;
using _Core.Gameplay;
using _Core.Localization;
using _Core.Managers;
using _Core.Platform.Core;
using _Core.Pooling;
using _Core.Save;
using _Core.SceneManagement;
using _Core.Settings;
using _Core.UI;

namespace _Core.Bootstrap
{
    /// <summary>
    /// Entry point of the game.
    /// Responsible for creating all persistent systems.
    /// </summary>
    public class Bootstrapper : MonoBehaviour
    {
        // Share of the loading bar for each boot step; they add up to 1. The bar is full before the first
        // scene loads (see LoadFirstSceneAsync).
        private const float PlatformWeight = 0.5f;
        private const float LocalizationWeight = 0.35f;
        private const float ManagersWeight = 0.15f;

        [SerializeField] private GameConfig gameConfig;
        [Tooltip("Optional loading screen in the bootstrap scene; boot works without it.")]
        [SerializeField] private LoadingScreen loadingScreen;

        private Transform _persistentRoot;
        private LocalizationManager _localizationManager;
        private float _progress;

        private void Awake()
        {
            ConfigurationManager.Initialize(gameConfig);
        }

        private async void Start()
        {
            // async void: any exception escaping here would be lost and the menu never loaded,
            // so everything is caught and the menu is always loaded.
            bool firstBoot = false;

            try
            {
                if (FindAnyObjectByType<PersistentRoot>() == null)
                {
                    firstBoot = true;

                    // Clear only on first boot; persistent managers keep their subscriptions
                    // if this scene is loaded again.
                    EventBus.Clear();

                    CreatePersistentRoot();

                    await InitializeManagersAsync();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            try
            {
                await LoadFirstSceneAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                // Still in the bootstrap scene: fall back to a plain synchronous load.
                if (this != null)
                    LoadFirstSceneFallback();
            }

            if (firstBoot)
            {
                Debug.Log($"Bootstrap: first scene shown after {Time.realtimeSinceStartup:F1} s.");
                ReportGameReady();
            }
        }

        // The game is loaded and interactive: tells the platform (Yandex LoadingAPI.ready). No-op where
        // the platform has no such call (CrazyGames, Null).
        private static void ReportGameReady()
        {
            try
            {
                PlatformManager platformManager = PlatformManager.Instance;
                if (platformManager != null)
                    platformManager.Game?.GameReady();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void AddProgress(float amount)
        {
            SetProgress(_progress + amount);
        }

        private void SetProgress(float progress)
        {
            _progress = Mathf.Clamp01(Mathf.Max(_progress, progress));

            if (loadingScreen != null)
                loadingScreen.SetProgress(_progress);
        }

        private async Task TrackAsync(Task task, float weight)
        {
            try
            {
                await task;
            }
            finally
            {
                AddProgress(weight);
            }
        }

        /// <summary>
        /// Editor only: scene to return to after bootstrapping, set by <see cref="BootstrapGuard"/>
        /// when Play Mode starts in another scene. Null loads the main menu.
        /// </summary>
        public static string ReturnScenePath { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Survives between Play sessions when domain reload is disabled.
            ReturnScenePath = null;
        }

        // Fills the loading bar, then loads SceneConfig.FirstScene (or, in the Editor, the scene Play Mode started in). The
        // bar stays on screen, full, until the new scene replaces the bootstrap scene. Scene activation is not
        // held back for the bar: that also holds Addressables (Localization) loads and can stall the boot.
        private async Task LoadFirstSceneAsync()
        {
            if (loadingScreen != null)
                await loadingScreen.CompleteAsync();

            Debug.Log($"Bootstrap: systems ready after {Time.realtimeSinceStartup:F1} s; loading the first scene.");

#if UNITY_EDITOR
            string returnScene = ReturnScenePath;
            ReturnScenePath = null;

            if (!string.IsNullOrEmpty(returnScene))
            {
                // Works for scenes outside Build Settings too (test scenes).
                AsyncOperation operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    returnScene,
                    new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                await SceneLoader.LoadOperationAsync(operation, null);
                return;
            }
#endif
            await SceneLoader.LoadAsync(ResolveFirstScene());
        }

        private static void LoadFirstSceneFallback()
        {
            SceneLoader.Load(ResolveFirstScene());
        }

        // SceneConfig.FirstScene, or the scene after the bootstrap scene in Build Settings when it is not set.
        private static string ResolveFirstScene()
        {
            string firstScene = SceneLoader.FirstScene;
            if (!string.IsNullOrEmpty(firstScene))
                return firstScene;

            Debug.LogError("Bootstrap: SceneConfig has no first scene; loading build index 1.");
            string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(1);
            return System.IO.Path.GetFileNameWithoutExtension(path);
        }

        private void CreatePersistentRoot()
        {
            GameObject persistentRootObject = new GameObject("PersistentRoot");
            persistentRootObject.AddComponent<PersistentRoot>();
            _persistentRoot = persistentRootObject.transform;
        }

        private async Task InitializeManagersAsync()
        {
            PlatformManager platformManager = CreatePlatformManager();

            // Localization (Addressables catalog, locales, string tables) loads while the platform SDK starts.
            // Picking the saved language still waits for SaveManager/SettingsManager below.
            RunStep(nameof(CreateLocalizationManager), CreateLocalizationManager);
            if (_localizationManager != null)
                _ = TrackAsync(_localizationManager.LoadTask, LocalizationWeight);
            else
                AddProgress(LocalizationWeight);

            // Wait until the platform and its services are fully initialized.
            // Never throws: on failure the platform registers Null services.
            await TrackAsync(platformManager.InitializeAsync(), PlatformWeight);

            // Each manager is created in isolation so one failing manager
            // does not prevent the others from being created.
            // AudioManager comes before SettingsManager: it subscribes to the audio settings SettingsManager.Init publishes.
            RunStep(nameof(CreateAudioManager), CreateAudioManager);
            RunStep(nameof(CreateSaveManager), CreateSaveManager);
            RunStep(nameof(CreateSettingsManager), CreateSettingsManager);

            // Awaited so the menu opens with its texts already in the player's language (no flicker).
            await RunStepAsync(nameof(InitLocalizationAsync), InitLocalizationAsync);
            SetProgress(PlatformWeight + LocalizationWeight);

            RunStep(nameof(CreatePoolManager), CreatePoolManager);
            RunStep(nameof(CreateUIManager), CreateUIManager);
            RunStep(nameof(CreateSceneTransition), CreateSceneTransition);
            RunStep(nameof(CreateAdBlockerOverlay), CreateAdBlockerOverlay);
            // PauseManager before AppLifecycleManager, which may report a pause while it initializes.
            RunStep(nameof(CreatePauseManager), CreatePauseManager);
            RunStep(nameof(CreateGameplayStateManager), CreateGameplayStateManager);
            RunStep(nameof(CreateAppLifecycleManager), CreateAppLifecycleManager);
            RunStep(nameof(CreateEventSystem), CreateEventSystem);
            AddProgress(ManagersWeight);
        }

        private static void RunStep(string stepName, Action step)
        {
            try
            {
                step();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Bootstrap step '{stepName}' failed.");
                Debug.LogException(exception);
            }
        }
        
        private static async Task RunStepAsync(string stepName, Func<Task> step)
        {
            try
            {
                await step();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Bootstrap step '{stepName}' failed.");
                Debug.LogException(exception);
            }
        }

        private void CreateAudioManager()
        {
            GameObject obj = new GameObject("AudioManager");
            obj.transform.SetParent(_persistentRoot);
            obj.AddComponent<AudioManager>().Init();
        }

        private void CreateSaveManager()
        {
            GameObject obj = new GameObject("SaveManager");
            obj.transform.SetParent(_persistentRoot);
            obj.AddComponent<SaveManager>().Init();
        }
        
        private void CreateSettingsManager()
        {
            GameObject obj = new GameObject("SettingsManager");
            obj.transform.SetParent(_persistentRoot);
            obj.AddComponent<SettingsManager>().Init();
        }

        private void CreateLocalizationManager()
        {
            GameObject obj = new GameObject("LocalizationManager");
            obj.transform.SetParent(_persistentRoot);
            _localizationManager = obj.AddComponent<LocalizationManager>();
            _localizationManager.BeginLoad();
        }

        private Task InitLocalizationAsync()
        {
            return _localizationManager != null ? _localizationManager.InitAsync() : Task.CompletedTask;
        }

        private void CreatePoolManager()
        {
            GameObject obj = new GameObject("PoolManager");
            obj.transform.SetParent(_persistentRoot);
            obj.AddComponent<PoolManager>();
        }
        
        private void CreateUIManager()
        {
            GameObject obj = new GameObject("UIManager");
            obj.transform.SetParent(_persistentRoot);
            obj.AddComponent<UIManager>();
        }

        private void CreatePauseManager()
        {
            GameObject obj = new GameObject("PauseManager");
            obj.transform.SetParent(_persistentRoot);
            obj.AddComponent<PauseManager>().Init();
        }

        private void CreateSceneTransition()
        {
            GameObject obj = new GameObject("SceneTransition");
            obj.transform.SetParent(_persistentRoot);
            obj.AddComponent<SceneTransition>();
        }

        private void CreateGameplayStateManager()
        {
            GameObject obj = new GameObject("GameplayStateManager");
            obj.transform.SetParent(_persistentRoot);
            obj.AddComponent<GameplayStateManager>().Init();
        }

        private void CreateAppLifecycleManager()
        {
            GameObject obj = new GameObject("AppLifecycleManager");
            obj.transform.SetParent(_persistentRoot);
            obj.AddComponent<AppLifecycleManager>().Init();
        }

        private void CreateAdBlockerOverlay()
        {
            GameObject obj = new GameObject("AdBlockerOverlay");
            obj.transform.SetParent(_persistentRoot);
            obj.AddComponent<AdBlockerOverlay>();
        }

        private PlatformManager  CreatePlatformManager()
        {
            GameObject obj = new GameObject("PlatformManager");
            obj.transform.SetParent(_persistentRoot);
            return  obj.AddComponent<PlatformManager>();
        }
        
        private void CreateEventSystem()
        {
            // Prevent creating multiple EventSystems.
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            GameObject eventSystemObject = new GameObject("EventSystem");

            eventSystemObject.transform.SetParent(_persistentRoot);

            eventSystemObject.AddComponent<EventSystem>();

            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
