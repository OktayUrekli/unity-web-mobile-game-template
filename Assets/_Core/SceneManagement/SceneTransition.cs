using System;
using System.Threading.Tasks;
using _Core.Configuration;
using _Core.Managers;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace _Core.SceneManagement
{
    /// <summary>
    /// Changes scenes behind a full-screen fade: the screen fades out, the scene loads asynchronously (the page
    /// keeps responding on WebGL), then it fades in. Input is blocked for the whole transition and a second request
    /// while one runs is ignored. Created by the Bootstrapper; look and timing come from <see cref="SceneConfig"/>.
    /// Use it for every scene change made by the player: <c>_ = SceneTransition.LoadAsync(name)</c>.
    /// </summary>
    public class SceneTransition : Singleton<SceneTransition>
    {
        // Above every UI canvas, below the ad blocker overlay (30000).
        private const int SortingOrder = 29000;

        private CanvasGroup _canvasGroup;
        private Image _image;
        private float _fadeSeconds = 0.25f;

        /// <summary>
        /// True from the start of the fade-out until the fade-in has finished.
        /// </summary>
        public bool IsTransitioning { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            if (IsDuplicate)
                return;

            BuildOverlay();
            ApplyConfig();
            SetAlpha(0f);
        }

        /// <summary>
        /// Fades out, loads <paramref name="sceneName"/> and fades in. Completes when the new scene is visible.
        /// Ignored (with a warning) while another transition runs. Without a <see cref="SceneTransition"/> (boot failed)
        /// the scene loads without the fade.
        /// </summary>
        public static Task LoadAsync(string sceneName)
        {
            SceneTransition transition = Instance;
            return transition != null ? transition.TransitionAsync(sceneName) : SceneLoader.LoadAsync(sceneName);
        }

        private async Task TransitionAsync(string sceneName)
        {
            if (IsTransitioning)
            {
                Debug.LogWarning($"SceneTransition: already changing scenes; '{sceneName}' ignored.");
                return;
            }

            IsTransitioning = true;

            try
            {
                await FadeAsync(1f);
                await SceneLoader.LoadAsync(sceneName);

                // One frame so the new scene's Start methods run (screens shown, music started) before it appears.
                await Task.Yield();
            }
            catch (Exception exception)
            {
                // Callers fire and forget this task; log here so a failure is never silent.
                Debug.LogException(exception);
            }
            finally
            {
                // Destroyed with the app while waiting: nothing left to fade.
                if (this != null)
                {
                    await FadeAsync(0f);
                    IsTransitioning = false;
                }
            }
        }

        private Task FadeAsync(float alpha)
        {
            _canvasGroup.blocksRaycasts = true;

            // Continuations run on a later frame, not inside the DOTween callback.
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // DOTween.To instead of the CanvasGroup.DOFade module shortcut: the DOTween modules compile
            // into Assembly-CSharp, which the _Core assembly cannot reference.
            DOTween.To(() => _canvasGroup.alpha, value => _canvasGroup.alpha = value, alpha, _fadeSeconds)
                .SetUpdate(true)
                .SetTarget(_canvasGroup)
                .SetLink(gameObject)
                .OnKill(() =>
                {
                    if (_canvasGroup != null)
                        _canvasGroup.blocksRaycasts = _canvasGroup.alpha > 0f;

                    completion.TrySetResult(true);
                });

            return completion.Task;
        }

        private void SetAlpha(float alpha)
        {
            _canvasGroup.alpha = alpha;
            _canvasGroup.blocksRaycasts = alpha > 0f;
        }

        private void ApplyConfig()
        {
            GameConfig gameConfig = ConfigurationManager.GameConfig;
            SceneConfig config = gameConfig != null ? gameConfig.SceneConfig : null;
            if (config == null)
                return;

            _fadeSeconds = config.TransitionFadeSeconds;
            _image.color = config.TransitionColor;
        }

        /// <summary>
        /// Builds a screen-space canvas with one full-screen image that blocks input while it is visible.
        /// </summary>
        private void BuildOverlay()
        {
            var overlay = new GameObject("SceneTransitionCanvas", typeof(RectTransform));
            overlay.transform.SetParent(transform, false);

            Canvas canvas = overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            overlay.AddComponent<GraphicRaycaster>();
            _canvasGroup = overlay.AddComponent<CanvasGroup>();

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(overlay.transform, false);

            var rect = (RectTransform)fill.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _image = fill.AddComponent<Image>();
            _image.color = Color.black;
            _image.raycastTarget = true;
        }
    }
}
