using UnityEngine;

namespace _Core.UI
{
    /// <summary>
    /// Base class for all popup windows. Opened with <c>UIManager.ShowPopup&lt;T&gt;()</c>.
    /// </summary>
    public abstract class UIPopup : MonoBehaviour
    {
        /// <summary>
        /// False for popups that must be closed through one of their buttons (e.g. game over),
        /// so Back/Escape cannot leave the game without a way forward.
        /// </summary>
        public virtual bool CanCloseWithBack => true;

        /// <summary>
        /// Called when the popup is opened.
        /// </summary>
        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Called when the popup is closed.
        /// </summary>
        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Closes this popup through <see cref="UIManager"/>, so the popup stack and
        /// <c>UIPopupClosedEvent</c> stay correct. Use it from close/cancel buttons.
        /// </summary>
        protected void Close()
        {
            UIManager ui = UIManager.Instance;
            if (ui != null)
                ui.HidePopup(this);
        }
    }
}
