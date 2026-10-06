using _Core.UI;
using _Core.UI.Components;
using _Project.Data;
using _Project.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace _Project.UI
{
    /// <summary>
    /// End of round: score, best score, a rewarded "continue" (once per round), play again and main menu.
    /// </summary>
    public class GameOverPopup : AnimatedUIPopup
    {
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text bestText;
        [SerializeField] private LocalizedString bestFormat;
        [SerializeField] private LocalizedString newBestText;
        [SerializeField] private RewardedAdButton continueButton;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button menuButton;

        // The LocalizedString currently driving bestText; it re-fires when the language changes.
        private LocalizedString _shownBest;

        // The round is over: only the popup buttons lead somewhere, so Back must not close it.
        public override bool CanCloseWithBack => false;

        protected override void Awake()
        {
            base.Awake();
            continueButton.OnRewardGranted.AddListener(OnRewardGranted);
            playAgainButton.onClick.AddListener(OnPlayAgainClicked);
            menuButton.onClick.AddListener(OnMenuClicked);
        }

        private void OnDestroy()
        {
            continueButton.OnRewardGranted.RemoveListener(OnRewardGranted);
            playAgainButton.onClick.RemoveListener(OnPlayAgainClicked);
            menuButton.onClick.RemoveListener(OnMenuClicked);
            ShowBest(null);
        }

        protected override void OnShown()
        {
            GameplayController controller = GameplayController.Current;
            int score = controller != null ? controller.Score : 0;
            bool newBest = controller != null && controller.IsNewBest;

            scoreText.text = score.ToString();
            bestFormat.Arguments = new object[] { ProgressStore.BestScore };
            if (_shownBest == bestFormat)
                bestFormat.RefreshString();
            ShowBest(newBest ? newBestText : bestFormat);

            // This popup owns the active flag (once per round); RewardedAdButton only collapses
            // itself visually when rewarded ads are unavailable or blocked.
            continueButton.gameObject.SetActive(controller != null && !controller.HasContinued);
        }

        // Subscribing fires OnBestChanged with the current text.
        private void ShowBest(LocalizedString text)
        {
            if (_shownBest == text)
                return;

            if (_shownBest != null)
                _shownBest.StringChanged -= OnBestChanged;

            _shownBest = text;

            if (_shownBest != null)
                _shownBest.StringChanged += OnBestChanged;
        }

        private void OnBestChanged(string value)
        {
            bestText.text = value;
        }

        private void OnRewardGranted()
        {
            if (GameplayController.Current != null)
                GameplayController.Current.ContinueAfterReward();
        }

        private void OnPlayAgainClicked()
        {
            if (GameplayController.Current != null)
                GameplayController.Current.PlayAgain();
        }

        private void OnMenuClicked()
        {
            if (GameplayController.Current != null)
                GameplayController.Current.ExitToMenu(true);
        }
    }
}
