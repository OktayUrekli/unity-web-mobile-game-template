using _Core.Events;
using _Core.Events.Ads;
using _Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace _Core.UI
{
    /// <summary>
    /// Full-screen overlay that blocks all UI input while an advertisement request is in progress
    /// (from <see cref="AdRequestedEvent"/> until <see cref="AdRequestCompletedEvent"/>).
    /// Created in code by the Bootstrapper and drawn above every other canvas.
    /// </summary>
    public class AdBlockerOverlay : MonoBehaviour
    {
        private const int SortingOrder = 30000;

        [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.6f);
        [SerializeField] private LocalizedString labelText = new LocalizedString(LocalizationManager.CoreTable, "ad.loading");

        private GameObject _overlay;
        private TextMeshProUGUI _label;

        /// <summary>
        /// True while the overlay is blocking input.
        /// </summary>
        public bool IsBlocking => _overlay != null && _overlay.activeSelf;

        private void Awake()
        {
            BuildOverlay();
            SetBlocking(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<AdRequestedEvent>(OnAdRequested);
            EventBus.Subscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AdRequestedEvent>(OnAdRequested);
            EventBus.Unsubscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);
        }

        private void OnDestroy()
        {
            labelText.StringChanged -= OnLabelChanged;
        }

        // Called on subscribe and again whenever the language changes.
        private void OnLabelChanged(string value)
        {
            if (_label != null)
                _label.text = value;
        }

        private void OnAdRequested(AdRequestedEvent gameEvent)
        {
            SetBlocking(true);
        }

        private void OnAdRequestCompleted(AdRequestCompletedEvent gameEvent)
        {
            SetBlocking(false);
        }

        private void SetBlocking(bool blocking)
        {
            if (_overlay != null)
                _overlay.SetActive(blocking);
        }

        /// <summary>
        /// Builds a screen-space canvas with one raycast-target image covering the whole screen.
        /// </summary>
        private void BuildOverlay()
        {
            _overlay = new GameObject("AdBlockerCanvas", typeof(RectTransform));
            _overlay.transform.SetParent(transform, false);

            Canvas canvas = _overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            _overlay.AddComponent<GraphicRaycaster>();

            GameObject blocker = new GameObject("Blocker", typeof(RectTransform));
            blocker.transform.SetParent(_overlay.transform, false);

            RectTransform rect = (RectTransform)blocker.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = blocker.AddComponent<Image>();
            image.color = dimColor;
            image.raycastTarget = true;

            // Shown until the ad starts (or instead of it when none is available).
            GameObject label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(blocker.transform, false);

            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 0.5f);
            labelRect.sizeDelta = new Vector2(0f, 80f);

            _label = label.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 36f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = Color.white;
            _label.raycastTarget = false;
            labelText.StringChanged += OnLabelChanged;
        }
    }
}
