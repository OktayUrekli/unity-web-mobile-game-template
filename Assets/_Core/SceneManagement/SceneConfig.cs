using UnityEngine;

namespace _Core.SceneManagement
{
    /// <summary>
    /// Scenes the framework itself loads, and how <see cref="SceneTransition"/> looks. Game code loads its own
    /// scenes by name and keeps those names as constants in <c>_Project</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "SceneConfig", menuName = "Configuration/Scene Config")]
    public class SceneConfig : ScriptableObject
    {
        [Tooltip("Scene the Bootstrapper loads once every system is ready (usually the main menu). Must be in Build Settings.")]
        [SerializeField] private string firstScene = "01_MainMenu";

        [Header("Transition")]
        [Tooltip("Seconds to fade the screen out before a scene load, and in again after it (real time).")]
        [Min(0f)]
        [SerializeField] private float transitionFadeSeconds = 0.25f;

        [SerializeField] private Color transitionColor = Color.black;

        /// <summary>
        /// Name of the scene loaded after boot.
        /// </summary>
        public string FirstScene => firstScene;

        /// <summary>
        /// Duration of each fade of <see cref="SceneTransition"/>.
        /// </summary>
        public float TransitionFadeSeconds => transitionFadeSeconds;

        /// <summary>
        /// Colour the screen fades to during a <see cref="SceneTransition"/>.
        /// </summary>
        public Color TransitionColor => transitionColor;
    }
}
