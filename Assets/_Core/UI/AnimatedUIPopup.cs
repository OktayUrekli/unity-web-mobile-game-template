using DG.Tweening;
using UnityEngine;

namespace _Core.UI
{
    /// <summary>
    /// Popup that fades and scales in/out. Plays in unscaled time so it works while the game is paused.
    /// The root is the full-screen dim background; <see cref="panel"/> is the animated window.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class AnimatedUIPopup : UIPopup
    {
        [SerializeField] private RectTransform panel;
        [SerializeField, Min(0f)] private float duration = 0.2f;

        private CanvasGroup _canvasGroup;
        private Sequence _sequence;

        // Also read on first use: a subclass Awake that doesn't call base.Awake() leaves the field unset, and
        // Show/Hide would otherwise throw.
        private CanvasGroup Group
        {
            get
            {
                if (_canvasGroup == null)
                    _canvasGroup = GetComponent<CanvasGroup>();
                return _canvasGroup;
            }
        }

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public override void Show()
        {
            KillSequence();
            gameObject.SetActive(true);

            CanvasGroup group = Group;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = true;

            if (panel != null)
                panel.localScale = Vector3.one * 0.85f;

            _sequence = DOTween.Sequence()
                .Append(UITween.Fade(group, 1f, duration))
                .OnComplete(() => group.interactable = true)
                .SetUpdate(true)
                .SetLink(gameObject);

            if (panel != null)
                _sequence.Join(panel.DOScale(1f, duration).SetEase(Ease.OutBack));

            OnShown();
        }

        public override void Hide()
        {
            if (!gameObject.activeSelf)
                return;

            KillSequence();

            CanvasGroup group = Group;
            group.interactable = false;
            group.blocksRaycasts = false;

            _sequence = DOTween.Sequence()
                .Append(UITween.Fade(group, 0f, duration * 0.8f))
                .OnComplete(() => gameObject.SetActive(false))
                .SetUpdate(true)
                .SetLink(gameObject);

            if (panel != null)
                _sequence.Join(panel.DOScale(0.9f, duration * 0.8f).SetEase(Ease.InQuad));

            OnHidden();
        }

        /// <summary>
        /// Called by <see cref="Show"/> once the popup is active and its show animation has started; refresh displayed
        /// values here.
        /// </summary>
        protected virtual void OnShown()
        {
        }

        /// <summary>
        /// Called by <see cref="Hide"/> when the hide animation starts; the popup stays active until it ends.
        /// </summary>
        protected virtual void OnHidden()
        {
        }

        protected virtual void OnDisable()
        {
            KillSequence();
        }

        private void KillSequence()
        {
            if (_sequence != null && _sequence.IsActive())
                _sequence.Kill();
            _sequence = null;
        }
    }
}
