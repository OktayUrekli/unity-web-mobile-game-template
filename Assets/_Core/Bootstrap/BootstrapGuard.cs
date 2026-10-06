using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Core.Bootstrap
{
    /// <summary>
    /// Editor convenience: when Play Mode starts in a scene other than the bootstrap scene, loads the
    /// bootstrap scene first and returns to this scene afterwards, so managers and the platform exist.
    /// Place one in every scene that can be played directly (gameplay, test scenes). Does nothing in builds
    /// or when the game was already bootstrapped.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class BootstrapGuard : MonoBehaviour
    {
        private void Awake()
        {
            if (FindAnyObjectByType<PersistentRoot>() != null)
                return;

#if UNITY_EDITOR
            string scenePath = gameObject.scene.path;
            Debug.Log($"BootstrapGuard: '{gameObject.scene.name}' was played without bootstrapping; loading the bootstrap scene first.");

            Bootstrapper.ReturnScenePath = scenePath;

            // Disable this scene's objects so nothing runs against missing managers before the reload.
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                root.SetActive(false);

            SceneManager.LoadScene(0);
#endif
        }
    }
}
