using _Core.Events;
using _Core.Events.Platform;
using _Core.Platform.Core;
using UnityEngine;
using UnityEngine.UI;

namespace _Core.UI.Components
{
    /// <summary>
    /// Button that opens the platform's privacy options form, where the player changes their ad consent choice
    /// (<c>PlatformManager.Privacy</c>). Google Play requires one in GDPR regions; put it in the settings screen and
    /// give it a localized label. It collapses (invisible and out of the layout) while the platform does not
    /// require it, which is always the case on platforms without such a form.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PrivacyOptionsButton : MonoBehaviour
    {
        private Button _button;
        private CanvasGroup _canvasGroup;
        private LayoutElement _layoutElement;
        private bool _isFormOpen;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClicked);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PrivacyOptionsChangedEvent>(OnPrivacyOptionsChanged);
            Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PrivacyOptionsChangedEvent>(OnPrivacyOptionsChanged);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnClicked);
        }

        /// <summary>
        /// True while the platform requires the privacy options entry point.
        /// </summary>
        public bool IsOffered
        {
            get
            {
                PlatformManager platform = PlatformManager.Instance;
                return platform != null && platform.Privacy != null && platform.Privacy.IsPrivacyOptionsRequired;
            }
        }

        /// <summary>
        /// Re-evaluates whether the button is shown and can be pressed.
        /// </summary>
        public void Refresh()
        {
            bool visible = IsOffered;
            SetVisible(visible);

            if (_button != null)
                _button.interactable = visible && !_isFormOpen;
        }

        private void SetVisible(bool visible)
        {
            // Collapse instead of SetActive(false), so the button stays subscribed and can appear later.
            // TryGetComponent instead of '??': the Editor returns a fake-null for missing components.
            if (_canvasGroup == null && !TryGetComponent(out _canvasGroup))
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            // A default LayoutElement changes nothing while the button is visible.
            if (_layoutElement == null && !TryGetComponent(out _layoutElement))
                _layoutElement = gameObject.AddComponent<LayoutElement>();

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _layoutElement.ignoreLayout = !visible;
        }

        private void OnPrivacyOptionsChanged(PrivacyOptionsChangedEvent gameEvent)
        {
            Refresh();
        }

        private void OnClicked()
        {
            if (!IsOffered || _isFormOpen)
            {
                Refresh();
                return;
            }

            _isFormOpen = true;
            Refresh();
            PlatformManager.Instance.Privacy.ShowPrivacyOptions(OnFormClosed);
        }

        private void OnFormClosed()
        {
            // The form can answer after this object was destroyed.
            if (this == null)
                return;

            _isFormOpen = false;
            Refresh();
        }
    }
}
