using _Core.Events;
using _Core.Events.Ads;
using _Core.Platform.Core;
using _Core.Platform.Services.Ads;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace _Core.UI.Components
{
    /// <summary>
    /// Button that requests a rewarded ad and reports the outcome.
    /// Disables itself when rewarded ads are unsupported, when an ad blocker is detected
    /// and while any ad request is in progress, so players never press a button that cannot work.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RewardedAdButton : MonoBehaviour
    {
        [Tooltip("Hide the button instead of disabling it when rewarded ads cannot be shown.")]
        [SerializeField] private bool hideWhenUnavailable;

        [Tooltip("Invoked only when the ad completed; grant the reward here.")]
        [SerializeField] private UnityEvent onRewardGranted = new();

        [Tooltip("Invoked when the ad did not complete (failed, not available, in progress).")]
        [SerializeField] private UnityEvent onRewardFailed = new();

        private Button _button;
        private CanvasGroup _canvasGroup;
        private LayoutElement _layoutElement;
        private bool _hasAdblock;
        private bool _adblockRequested;

        /// <summary>
        /// Invoked only when the ad completed.
        /// </summary>
        public UnityEvent OnRewardGranted => onRewardGranted;

        /// <summary>
        /// Invoked when the ad did not complete.
        /// </summary>
        public UnityEvent OnRewardFailed => onRewardFailed;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClicked);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<AdRequestedEvent>(OnAdRequested);
            EventBus.Subscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);

            // Ask once per component; the SDK keeps every pending callback until detection finishes.
            PlatformManager platform = PlatformManager.Instance;
            if (!_adblockRequested && platform != null && platform.Ads != null)
            {
                _adblockRequested = true;
                platform.Ads.HasAdblock(OnAdblockDetected);
            }

            Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AdRequestedEvent>(OnAdRequested);
            EventBus.Unsubscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnClicked);
        }

        /// <summary>
        /// True when a rewarded ad can be requested right now.
        /// </summary>
        public bool CanShowAd
        {
            get
            {
                PlatformManager platform = PlatformManager.Instance;
                return platform != null &&
                       platform.Ads != null &&
                       platform.Capabilities.SupportsRewardedAds &&
                       !_hasAdblock &&
                       !platform.Ads.IsAdShowing;
            }
        }

        /// <summary>
        /// Re-evaluates availability and updates the button state.
        /// </summary>
        public void Refresh()
        {
            bool available = CanShowAd;

            if (_button != null)
                _button.interactable = available;

            // Hiding only reacts to permanent unavailability, never to a running ad.
            // The active flag is left to the owner (e.g. a popup), so the button collapses
            // visually and in layout instead of toggling its own GameObject.
            if (hideWhenUnavailable)
                SetVisible(IsOffered);
        }

        /// <summary>
        /// True when rewarded ads are supported and no ad blocker was detected.
        /// </summary>
        public bool IsOffered
        {
            get
            {
                PlatformManager platform = PlatformManager.Instance;
                return platform != null && platform.Capabilities.SupportsRewardedAds && !_hasAdblock;
            }
        }

        private void SetVisible(bool visible)
        {
            // TryGetComponent instead of '??': the Editor returns a fake-null for missing components.
            if (_canvasGroup == null && !TryGetComponent(out _canvasGroup))
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (_layoutElement == null)
                TryGetComponent(out _layoutElement);

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;

            if (_layoutElement != null)
                _layoutElement.ignoreLayout = !visible;
        }

        private void OnAdblockDetected(bool hasAdblock)
        {
            // Detection may answer later, after this object was destroyed or hidden.
            if (this == null)
                return;

            _hasAdblock = hasAdblock;

            if (isActiveAndEnabled)
                Refresh();
        }

        private void OnAdRequested(AdRequestedEvent gameEvent)
        {
            Refresh();
        }

        private void OnAdRequestCompleted(AdRequestCompletedEvent gameEvent)
        {
            Refresh();
        }

        private void OnClicked()
        {
            if (!CanShowAd)
            {
                Refresh();
                return;
            }

            PlatformManager.Instance.Ads.ShowRewardedAd(OnAdResult);
        }

        private void OnAdResult(AdResult result)
        {
            if (this == null)
                return;

            if (result == AdResult.Completed)
                onRewardGranted.Invoke();
            else
                onRewardFailed.Invoke();

            Refresh();
        }
    }
}
