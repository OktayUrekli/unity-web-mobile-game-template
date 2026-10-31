using UnityEngine;
namespace _Core.UI
{
    /// <summary>
    /// Holds references to the UI canvas hierarchy.
    /// </summary>
    public class UICanvasReferences : MonoBehaviour
    {
        [SerializeField] private Transform screensRoot;
        [SerializeField] private Transform popupsRoot;

        /// <summary>
        /// Parent that <see cref="UIManager"/> instantiates screens under.
        /// </summary>
        public Transform ScreensRoot => screensRoot;

        /// <summary>
        /// Parent that <see cref="UIManager"/> instantiates popups under.
        /// </summary>
        public Transform PopupsRoot => popupsRoot;
    }
}