using _Core.Feedback;
using _Core.Settings;
using _Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace _Project.UI
{
    /// <summary>
    /// Music, sound and vibration on/off toggles and the language button. The vibration row shows only where
    /// the device can vibrate (<see cref="Haptics.IsSupported"/>). Settings are written to storage when the popup closes.
    /// </summary>
    public class SettingsPopup : AnimatedUIPopup
    {
        [SerializeField] private Button musicButton;
        [SerializeField] private TMP_Text musicLabel;
        [SerializeField] private LocalizedString musicOnText;
        [SerializeField] private LocalizedString musicOffText;

        [SerializeField] private Button sfxButton;
        [SerializeField] private TMP_Text sfxLabel;
        [SerializeField] private LocalizedString sfxOnText;
        [SerializeField] private LocalizedString sfxOffText;

        [SerializeField] private Button vibrationButton;
        [SerializeField] private TMP_Text vibrationLabel;
        [SerializeField] private LocalizedString vibrationOnText = new("UI", "settings.vibration_on");
        [SerializeField] private LocalizedString vibrationOffText = new("UI", "settings.vibration_off");

        [SerializeField] private Button languageButton;
        [SerializeField] private Button closeButton;

        // The LocalizedString currently driving each label; it re-fires when the language changes.
        private LocalizedString _musicText;
        private LocalizedString _sfxText;
        private LocalizedString _vibrationText;

        protected override void Awake()
        {
            base.Awake();
            musicButton.onClick.AddListener(OnMusicClicked);
            sfxButton.onClick.AddListener(OnSfxClicked);
            languageButton.onClick.AddListener(OnLanguageClicked);
            closeButton.onClick.AddListener(OnCloseClicked);

            if (vibrationButton != null)
            {
                vibrationButton.onClick.AddListener(OnVibrationClicked);
                vibrationButton.gameObject.SetActive(Haptics.IsSupported);
            }
        }

        private void OnDestroy()
        {
            musicButton.onClick.RemoveListener(OnMusicClicked);
            sfxButton.onClick.RemoveListener(OnSfxClicked);
            languageButton.onClick.RemoveListener(OnLanguageClicked);
            closeButton.onClick.RemoveListener(OnCloseClicked);

            if (vibrationButton != null)
                vibrationButton.onClick.RemoveListener(OnVibrationClicked);

            BindLabel(ref _musicText, null, SetMusicLabel);
            BindLabel(ref _sfxText, null, SetSfxLabel);
            BindLabel(ref _vibrationText, null, SetVibrationLabel);
        }

        protected override void OnShown()
        {
            RefreshLabels();
        }

        protected override void OnHidden()
        {
            // One storage write per visit instead of one per click.
            SettingsManager.Instance.Commit();
        }

        private void OnMusicClicked()
        {
            SettingsManager.Instance.SetMusicEnabled(!SettingsManager.Instance.IsMusicEnabled);
            RefreshLabels();
        }

        private void OnSfxClicked()
        {
            SettingsManager.Instance.SetSfxEnabled(!SettingsManager.Instance.IsSfxEnabled);
            RefreshLabels();
        }

        private void OnVibrationClicked()
        {
            bool isEnabled = !SettingsManager.Instance.IsVibrationEnabled;
            SettingsManager.Instance.SetVibrationEnabled(isEnabled);

            // Let the player feel what was just turned on.
            if (isEnabled)
                Haptics.Vibrate();

            RefreshLabels();
        }

        private void OnLanguageClicked()
        {
            UIManager.Instance.ShowPopup<LanguagePopup>();
        }

        private void OnCloseClicked()
        {
            Close();
        }

        private void RefreshLabels()
        {
            BindLabel(ref _musicText, SettingsManager.Instance.IsMusicEnabled ? musicOnText : musicOffText, SetMusicLabel);
            BindLabel(ref _sfxText, SettingsManager.Instance.IsSfxEnabled ? sfxOnText : sfxOffText, SetSfxLabel);

            if (vibrationLabel != null)
                BindLabel(ref _vibrationText, SettingsManager.Instance.IsVibrationEnabled ? vibrationOnText : vibrationOffText, SetVibrationLabel);
        }

        // Swaps the LocalizedString a label listens to; subscribing fires the handler with the current text.
        private static void BindLabel(ref LocalizedString current, LocalizedString next, LocalizedString.ChangeHandler handler)
        {
            if (current == next)
                return;

            if (current != null)
                current.StringChanged -= handler;

            current = next;

            if (current != null)
                current.StringChanged += handler;
        }

        private void SetMusicLabel(string value)
        {
            musicLabel.text = value;
        }

        private void SetSfxLabel(string value)
        {
            sfxLabel.text = value;
        }

        private void SetVibrationLabel(string value)
        {
            vibrationLabel.text = value;
        }
    }
}
