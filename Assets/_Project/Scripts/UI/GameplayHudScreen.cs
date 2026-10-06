using _Core.UI;
using _Project.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.UI
{
    /// <summary>
    /// In-game HUD: score, remaining time, pause button and the example "tap" button.
    /// Reads its values from <see cref="GameplayController.Current"/>.
    /// </summary>
    public class GameplayHudScreen : AnimatedUIScreen
    {
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button tapButton;

        private int _shownScore = -1;
        private int _shownSeconds = -1;

        protected override void Awake()
        {
            base.Awake();
            pauseButton.onClick.AddListener(OnPauseClicked);
            tapButton.onClick.AddListener(OnTapClicked);
        }

        private void OnDestroy()
        {
            pauseButton.onClick.RemoveListener(OnPauseClicked);
            tapButton.onClick.RemoveListener(OnTapClicked);
        }

        protected override void OnShown()
        {
            _shownScore = -1;
            _shownSeconds = -1;
        }

        private void Update()
        {
            GameplayController controller = GameplayController.Current;
            if (controller == null)
                return;

            // Only touch the text when the value changes, to avoid rebuilding the canvas every frame.
            if (controller.Score != _shownScore)
            {
                _shownScore = controller.Score;
                scoreText.text = _shownScore.ToString();
            }

            int seconds = Mathf.CeilToInt(controller.TimeLeft);
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                timerText.text = seconds.ToString();
            }
        }

        private void OnPauseClicked()
        {
            if (GameplayController.Current != null)
                GameplayController.Current.Pause();
        }

        private void OnTapClicked()
        {
            if (GameplayController.Current != null)
                GameplayController.Current.AddPoint();
        }
    }
}
