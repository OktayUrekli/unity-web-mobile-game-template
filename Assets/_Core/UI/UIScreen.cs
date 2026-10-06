using UnityEngine;

namespace _Core.UI
{
    /// <summary>
    /// Base class for all full-screen UI panels. Opened with <c>UIManager.ShowScreen&lt;T&gt;()</c>.
    /// </summary>
    public abstract class UIScreen : MonoBehaviour
    {
        /// <summary>
        /// Called when the screen is shown.
        /// </summary>
        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Called when the screen is hidden.
        /// </summary>
        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
