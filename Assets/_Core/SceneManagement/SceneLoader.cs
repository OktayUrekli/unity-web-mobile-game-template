using System;
using System.Threading.Tasks;
using _Core.Configuration;
using _Core.Events;
using _Core.Events.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Core.SceneManagement
{
    /// <summary>
    /// Loads scenes by name. Names must be in Build Settings; keep the game's scene names as constants in one
    /// <c>_Project</c> class.
    /// </summary>
    public static class SceneLoader
    {
        /// <summary>
        /// Progress (0..1) of the scene load running through <see cref="LoadAsync"/>; 0 when none runs.
        /// </summary>
        public static float Progress { get; private set; }

        /// <summary>
        /// The scene the Bootstrapper loads after boot (<see cref="SceneConfig.FirstScene"/>), or null when the
        /// config is missing.
        /// </summary>
        public static string FirstScene
        {
            get
            {
                GameConfig gameConfig = ConfigurationManager.GameConfig;
                SceneConfig sceneConfig = gameConfig != null ? gameConfig.SceneConfig : null;
                return sceneConfig != null ? sceneConfig.FirstScene : null;
            }
        }

        /// <summary>
        /// Loads the scene synchronously. Prefer <see cref="LoadAsync"/> (or a scene transition) on WebGL, where a
        /// synchronous load freezes the page until it finishes.
        /// </summary>
        public static void Load(string sceneName)
        {
            if (!CanLoad(sceneName))
                return;

            EventBus.Publish(new SceneLoadStartedEvent(sceneName));
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// Reloads the active scene.
        /// </summary>
        public static void ReloadCurrentScene()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            EventBus.Publish(new SceneLoadStartedEvent(sceneName));
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// Loads the next scene by build index.
        /// </summary>
        public static void LoadNextScene()
        {
            int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;

            if (nextIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogWarning("Next scene does not exist.");
                return;
            }

            EventBus.Publish(new SceneLoadStartedEvent(System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(nextIndex))));
            SceneManager.LoadScene(nextIndex);
        }

        /// <summary>
        /// Loads a scene asynchronously, reporting progress (0..1) to <paramref name="onProgress"/> and to
        /// <see cref="Progress"/>. The scene is shown as soon as it is loaded. Frame-polled, so it works on
        /// WebGL; it does not need a MonoBehaviour that survives the load.
        /// </summary>
        public static Task LoadAsync(string sceneName, Action<float> onProgress = null)
        {
            if (!CanLoad(sceneName))
                return Task.CompletedTask;

            EventBus.Publish(new SceneLoadStartedEvent(sceneName));
            return LoadOperationAsync(SceneManager.LoadSceneAsync(sceneName), onProgress);
        }

        /// <summary>
        /// Drives an already started scene load (shared with the editor-only return-scene path of the Bootstrapper).
        /// </summary>
        internal static async Task LoadOperationAsync(AsyncOperation operation, Action<float> onProgress)
        {
            if (operation == null)
                return;

            // Activation is never held back (allowSceneActivation = false): while a scene waits for activation,
            // Unity also holds every async load queued after it, Addressables and so Localization included, which
            // can stall the WebGL boot on a full loading bar.
            while (!operation.isDone)
            {
                Report(Mathf.Clamp01(operation.progress / 0.9f), onProgress);
                await Task.Yield();
            }

            Report(1f, onProgress);
            Progress = 0f;
        }

        private static bool CanLoad(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("SceneLoader: no scene name given.");
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"SceneLoader: scene '{sceneName}' is not in Build Settings.");
                return false;
            }

            return true;
        }

        private static void Report(float progress, Action<float> onProgress)
        {
            Progress = progress;
            onProgress?.Invoke(progress);
        }
    }
}
