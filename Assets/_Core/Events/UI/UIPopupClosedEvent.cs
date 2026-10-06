using _Core.UI;

namespace _Core.Events.UI
{
    /// <summary>
    /// Published by <c>UIManager</c> when a popup is closed (button, Back/Escape or code).
    /// Check the popup's type: <c>if (gameEvent.Popup is PausePopup)</c>.
    /// </summary>
    public readonly struct UIPopupClosedEvent : IGameEvent
    {
        /// <summary>
        /// The popup that was closed.
        /// </summary>
        public UIPopup Popup { get; }

        public UIPopupClosedEvent(UIPopup popup)
        {
            Popup = popup;
        }
    }
}
