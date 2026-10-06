using _Core.SceneManagement;
using _Core.UI;
using _Project.Data;
using _Project.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace _Project.UI
{
    /// <summary>
    /// Main menu: play, settings and the best score.
    /// </summary>
    public class MainMenuScreen : AnimatedUIScreen
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TMP_Text bestScoreText;
        [SerializeField] private LocalizedString bestScoreFormat;

        protected override void Awake()
        {
            base.Awake();

            // Listeners are added once; the instance is cached and reused across scenes.
            playButton.onClick.AddListener(OnPlayClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);

            // Fires now and again whenever the language changes.
            bestScoreFormat.Arguments = new object[] { ProgressStore.BestScore };
            bestScoreFormat.StringChanged += OnBestScoreChanged;
        }

        private void OnDestroy()
        {
            playButton.onClick.RemoveListener(OnPlayClicked);
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
            bestScoreFormat.StringChanged -= OnBestScoreChanged;
        }

        protected override void OnShown()
        {
            bestScoreFormat.Arguments = new object[] { ProgressStore.BestScore };
            bestScoreFormat.RefreshString();
        }

        private void OnBestScoreChanged(string value)
        {
            bestScoreText.text = ProgressStore.BestScore > 0 ? value : string.Empty;
        }

        private void OnPlayClicked()
        {
            _ = SceneTransition.LoadAsync(GameScenes.Gameplay);
        }

        private void OnSettingsClicked()
        {
            UIManager.Instance.ShowPopup<SettingsPopup>();
        }
    }
}
