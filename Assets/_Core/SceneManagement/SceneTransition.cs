using System;
using System.Threading.Tasks;
using _Core.Configuration;
using _Core.Gameplay;
using _Core.Managers;
using _Core.UI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace _Core.SceneManagement
{
    /// <summary>
    /// Changes scenes behind a full-screen fade: the screen fades out, the scene loads asynchronously (the page
    /// keeps responding on WebGL), then it fades in. The game is paused for the whole transition
    /// (<see cref="PauseSource.SceneTransition"/>): the old scene stops when the fade-out starts and the new one starts
    /// once it is visible; music keeps playing. Pointer input and Back are blocked meanwhile, and a second request is
    /// ignored. Created by the Bootstrapper; look and timing come from <see cref="SceneConfig"/>. Use it for every
    /// scene change made by the player: <c>_ = SceneTransition.LoadAsync(name)</c>.
    /// </summary>
    public class SceneTransition : Singleton<SceneTransition>
    {
        // Above every UI canvas, below the ad blocker overlay (30000).
        private const int SortingOrder = 29000;

        private CanvasGroup _canvasGroup;
        private Image _image;
        private float _fadeSeconds = 0.25f;

        /// <summary>
        /// True from the start of the fade-out until the fade-in has finished, the time
        /// <see cref="PauseSource.SceneTransition"/> pauses the game.
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
        /// Ignored (with a warning) while another transition runs: a scene that changes scenes again as soon as it
        /// starts must wait until <see cref="IsTransitioning"/> is false. Without a <see cref="SceneTransition"/> (boot
        /// failed) the scene loads without the fade or the pause.
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
                SetGamePaused(true);
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
                try
                {
                    // Also after a failed load, so the screen never stays covered. Destroyed with the app while
                    // waiting: nothing left to fade.
                    if (this != null)
                        await FadeAsync(0f);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }

                // Always, even after a failure: a flag left set would ignore every later scene change, and a pause left
                // held would freeze the game. The flag goes first, so code reacting to the resume sees the transition
                // finished.
                IsTransitioning = false;
                SetGamePaused(false);
            }
        }

        // The game is paused for the whole transition: the old scene stops when the player leaves it, and the new one
        // runs (and its gameplay reaches the platform) once it is visible. Music keeps playing: AudioManager ignores
        // pauses.
        private static void SetGamePaused(bool paused)
        {
            PauseManager pauseManager = PauseManager.Instance;
            if (pauseManager == null)
                return;

            if (paused)
                pauseManager.Pause(PauseSource.SceneTransition);
            else
                pauseManager.Resume(PauseSource.SceneTransition);
        }

        private Task FadeAsync(float alpha)
        {
            _canvasGroup.blocksRaycasts = true;

            // Continuations run on a later frame, not inside the DOTween callback.
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            UITween.Fade(_canvasGroup, alpha, _fadeSeconds)
                .SetUpdate(true)
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
