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

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public override void Show()
        {
            KillTween();
            gameObject.SetActive(true);
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            _tween = FadeCanvas(1f, duration)
                .SetUpdate(true)
                .SetLink(gameObject);

            OnShown();
        }

        public override void Hide()
        {
            if (!gameObject.activeSelf)
                return;

            KillTween();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            _tween = FadeCanvas(0f, duration * 0.8f)
                .OnComplete(() => gameObject.SetActive(false))
                .SetUpdate(true)
                .SetLink(gameObject);

            OnHidden();
        }

        /// <summary>
        /// Called at the start of <see cref="Show"/>; refresh displayed values here.
        /// </summary>
        protected virtual void OnShown()
        {
        }

        /// <summary>
        /// Called at the start of <see cref="Hide"/>.
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

        // DOTween.To instead of the CanvasGroup.DOFade module shortcut: the DOTween modules compile
        // into Assembly-CSharp, which the _Core assembly cannot reference.
        private Tweener FadeCanvas(float endAlpha, float time) =>
            DOTween.To(() => _canvasGroup.alpha, alpha => _canvasGroup.alpha = alpha, endAlpha, time)
                .SetTarget(_canvasGroup);
    }
}
