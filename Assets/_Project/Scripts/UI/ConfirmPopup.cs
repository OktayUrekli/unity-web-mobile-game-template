using System;
using _Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace _Project.UI
{
    /// <summary>
    /// Yes/no question such as "Quit the game?". Open it with <see cref="Open"/>; the action runs only when
    /// the player confirms. The cancel button and Back close it without running the action.
    /// </summary>
    public class ConfirmPopup : AnimatedUIPopup
    {
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        // The LocalizedString currently driving the message; it re-fires when the language changes.
        private LocalizedString _message;
        private Action _onConfirm;

        /// <summary>
        /// Shows the popup with <paramref name="message"/>. <paramref name="onConfirm"/> runs after the popup
        /// closes through the confirm button. Returns null when the popup is not registered in UIConfig.
        /// </summary>
        public static ConfirmPopup Open(LocalizedString message, Action onConfirm)
        {
            UIManager ui = UIManager.Instance;
            if (ui == null)
                return null;

            ConfirmPopup popup = ui.ShowPopup<ConfirmPopup>();
            if (popup != null)
                popup.Setup(message, onConfirm);

            return popup;
        }

        protected override void Awake()
        {
            base.Awake();
            confirmButton.onClick.AddListener(OnConfirmClicked);
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        private void OnDestroy()
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
            cancelButton.onClick.RemoveListener(OnCancelClicked);
            BindMessage(null);
        }

        protected override void OnHidden()
        {
            // Closed by Back or by cancel: the pending action must never run later.
            _onConfirm = null;
        }

        private void Setup(LocalizedString message, Action onConfirm)
        {
            _onConfirm = onConfirm;
            BindMessage(message);
        }

        private void OnConfirmClicked()
        {
            // Hiding clears the action, so take it first.
            Action onConfirm = _onConfirm;
            Close();
            onConfirm?.Invoke();
        }

        private void OnCancelClicked()
        {
            Close();
        }

        // Swaps the LocalizedString the message listens to; subscribing fires the handler with the current text.
        private void BindMessage(LocalizedString next)
        {
            if (_message == next)
                return;

            if (_message != null)
                _message.StringChanged -= SetMessage;

            _message = next;

            if (_message != null)
                _message.StringChanged += SetMessage;
        }

        private void SetMessage(string value)
        {
            messageLabel.text = value;
        }
    }
}
