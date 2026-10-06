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

        public Transform ScreensRoot => screensRoot;
        public Transform PopupsRoot => popupsRoot;
    }
}