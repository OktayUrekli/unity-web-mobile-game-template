using _Core.Gameplay;
using _Core.UI;
using _Project.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.UI
{
    /// <summary>
    /// Pause menu: resume, settings, main menu. The game is paused (<see cref="PauseSource.Menu"/>) while it is open,
    /// so closing it any way (button, Back, leaving the scene) resumes the game.
    /// </summary>
    public class PausePopup : AnimatedUIPopup
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button menuButton;

        protected override void Awake()
        {
            base.Awake();
            resumeButton.onClick.AddListener(OnResumeClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
            menuButton.onClick.AddListener(OnMenuClicked);
        }

        private void OnDestroy()
        {
            resumeButton.onClick.RemoveListener(OnResumeClicked);
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
            menuButton.onClick.RemoveListener(OnMenuClicked);
        }

        protected override void OnShown()
        {
            if (PauseManager.Instance != null)
                PauseManager.Instance.Pause(PauseSource.Menu);
        }

        protected override void OnHidden()
        {
            if (PauseManager.Instance != null)
                PauseManager.Instance.Resume(PauseSource.Menu);
        }

        private void OnResumeClicked()
        {
            Close();
        }

        private void OnSettingsClicked()
        {
            UIManager.Instance.ShowPopup<SettingsPopup>();
        }

        private void OnMenuClicked()
        {
            if (GameplayController.Current != null)
                GameplayController.Current.ExitToMenu(false);
        }
    }
}
