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

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public override void Show()
        {
            KillSequence();
            gameObject.SetActive(true);
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = true;

            if (panel != null)
                panel.localScale = Vector3.one * 0.85f;

            _sequence = DOTween.Sequence()
                .Append(FadeCanvas(1f, duration))
                .OnComplete(() => _canvasGroup.interactable = true)
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
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            _sequence = DOTween.Sequence()
                .Append(FadeCanvas(0f, duration * 0.8f))
                .OnComplete(() => gameObject.SetActive(false))
                .SetUpdate(true)
                .SetLink(gameObject);

            if (panel != null)
                _sequence.Join(panel.DOScale(0.9f, duration * 0.8f).SetEase(Ease.InQuad));

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
            KillSequence();
        }

        private void KillSequence()
        {
            if (_sequence != null && _sequence.IsActive())
                _sequence.Kill();
            _sequence = null;
        }

        // DOTween.To instead of the CanvasGroup.DOFade module shortcut: the DOTween modules compile
        // into Assembly-CSharp, which the _Core assembly cannot reference.
        private Tweener FadeCanvas(float endAlpha, float time) =>
            DOTween.To(() => _canvasGroup.alpha, alpha => _canvasGroup.alpha = alpha, endAlpha, time)
                .SetTarget(_canvasGroup);
    }
}
