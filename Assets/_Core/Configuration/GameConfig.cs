using UnityEngine;
using UnityEngine.Serialization;
using _Core.Audio;
using _Core.Platform.Config;
using _Core.Pooling;
using _Core.Save;
using _Core.SceneManagement;
using _Core.UI;

namespace _Core.Configuration
{
    /// <summary>
    /// Root of the framework configuration: one slot per core system. Assigned on the Bootstrapper and reached
    /// through <see cref="ConfigurationManager.GameConfig"/>. Game tuning does not belong here; it lives in
    /// <c>_Project</c> ScriptableObjects.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Configuration/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Core Systems")]
        [SerializeField] private SceneConfig sceneConfig;
        [SerializeField] private AudioConfig audioConfig;
        [SerializeField] private SaveConfig saveConfig;
        [SerializeField] private PoolConfig poolConfig;
        [FormerlySerializedAs("UIConfig")]
        [SerializeField] private UIConfig uiConfig;
        [SerializeField] private PlatformConfig platformConfig;

        /// <summary>
        /// Scenes the framework loads by name (first scene after boot, menu).
        /// </summary>
        public SceneConfig SceneConfig => sceneConfig;

        /// <summary>
        /// Audio mixer, its groups and the sounds the framework itself plays.
        /// </summary>
        public AudioConfig AudioConfig => audioConfig;

        /// <summary>
        /// Save debounce and storage limits.
        /// </summary>
        public SaveConfig SaveConfig => saveConfig;

        /// <summary>
        /// Pools created at boot.
        /// </summary>
        public PoolConfig PoolConfig => poolConfig;

        /// <summary>
        /// UI canvas and the screen/popup prefabs.
        /// </summary>
        public UIConfig UIConfig => uiConfig;

        /// <summary>
        /// Target platform, SDK and ad settings. Settable from tests only.
        /// </summary>
        public PlatformConfig PlatformConfig
        {
            get => platformConfig;
            internal set => platformConfig = value;
        }
    }
}
