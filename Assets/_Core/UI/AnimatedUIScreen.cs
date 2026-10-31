using DG.Tweening;
using UnityEngine;

namespace _Core.UI
{
    /// <summary>
    /// Screen that fades in/out in unscaled time.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class AnimatedUIScreen : UIScreen
    {
        [SerializeField, Min(0f)] private float duration = 0.2f;

        private CanvasGroup _canvasGroup;
        private Tween _tween;

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
            KillTween();
            gameObject.SetActive(true);

            CanvasGroup group = Group;
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;

            _tween = UITween.Fade(group, 1f, duration)
                .SetUpdate(true)
                .SetLink(gameObject);

            OnShown();
        }

        public override void Hide()
        {
            if (!gameObject.activeSelf)
                return;

            KillTween();

            CanvasGroup group = Group;
            group.interactable = false;
            group.blocksRaycasts = false;

            _tween = UITween.Fade(group, 0f, duration * 0.8f)
                .OnComplete(() => gameObject.SetActive(false))
                .SetUpdate(true)
                .SetLink(gameObject);

            OnHidden();
        }

        /// <summary>
        /// Called by <see cref="Show"/> once the screen is active and its fade-in has started; refresh displayed
        /// values here.
        /// </summary>
        protected virtual void OnShown()
        {
        }

        /// <summary>
        /// Called by <see cref="Hide"/> when the fade-out starts; the screen stays active until it ends.
        /// </summary>
        protected virtual void OnHidden()
        {
        }

        protected virtual void OnDisable()
        {
            KillTween();
        }

        private void KillTween()
        {
            if (_tween != null && _tween.IsActive())
                _tween.Kill();
            _tween = null;
        }
    }
}
