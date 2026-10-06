using _Core.Events;
using _Core.Events.Settings;
using _Core.Managers;
using _Core.Save;

namespace _Core.Settings
{
    /// <summary>
    /// Manages user settings.
    /// Setters store the value, hand the data to <see cref="SaveManager"/>, which debounces the storage write, and
    /// publish an event the owning system applies (<see cref="AudioSettingsChangedEvent"/>,
    /// <see cref="LanguageChangedEvent"/>), so this class depends on no other manager than the save system.
    /// Call <see cref="Commit"/> when the settings UI closes to write to storage right away.
    /// </summary>
    public class SettingsManager : Singleton<SettingsManager>
    {
        private const string FileName = "settings";

        private SettingsData _settingsData;

        /// <summary>
        /// True when music is on.
        /// </summary>
        public bool IsMusicEnabled => _settingsData.musicEnabled;

        /// <summary>
        /// True when sound effects are on.
        /// </summary>
        public bool IsSfxEnabled => _settingsData.sfxEnabled;

        /// <summary>
        /// True when vibration is on; <c>Haptics.Vibrate</c> does nothing while it is off.
        /// </summary>
        public bool IsVibrationEnabled => _settingsData.vibrationEnabled;

        /// <summary>
        /// The saved language code; empty until the player picks one (the platform or device language is used then).
        /// </summary>
        public string Language => _settingsData.language;

        /// <summary>
        /// True until <see cref="CompleteFirstLaunch"/> is called.
        /// </summary>
        public bool IsFirstLaunch => _settingsData.isFirstLaunch;

        /// <summary>
        /// Loads the settings and publishes them. Called by the Bootstrapper after the systems that apply them
        /// (<c>AudioManager</c>) have subscribed.
        /// </summary>
        public void Init()
        {
            Load();
            PublishAudioSettings();
        }

        /// <summary>
        /// Turns music on or off and saves.
        /// </summary>
        public void SetMusicEnabled(bool isEnabled)
        {
            _settingsData.musicEnabled = isEnabled;
            Save();
            PublishAudioSettings();
        }

        /// <summary>
        /// Turns sound effects on or off and saves.
        /// </summary>
        public void SetSfxEnabled(bool isEnabled)
        {
            _settingsData.sfxEnabled = isEnabled;
            Save();
            PublishAudioSettings();
        }

        /// <summary>
        /// Enables or disables vibration and saves.
        /// </summary>
        public void SetVibrationEnabled(bool isEnabled)
        {
            _settingsData.vibrationEnabled = isEnabled;
            Save();
        }

        /// <summary>
        /// Sets the language code (for example "en" or "tr"), saves and publishes
        /// <see cref="LanguageChangedEvent"/>, which <c>LocalizationManager</c> applies.
        /// </summary>
        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode) || languageCode == _settingsData.language)
                return;

            _settingsData.language = languageCode;
            Save();

            EventBus.Publish(new LanguageChangedEvent(languageCode));
        }

        /// <summary>
        /// Marks the first launch as done (e.g. after the tutorial) and saves.
        /// </summary>
        public void CompleteFirstLaunch()
        {
            if (!_settingsData.isFirstLaunch)
                return;

            _settingsData.isFirstLaunch = false;
            Save();
        }

        /// <summary>
        /// Writes pending settings to storage now. Call when the settings UI closes.
        /// </summary>
        public void Commit()
        {
            SaveManager.Instance.Flush();
        }

        private void PublishAudioSettings()
        {
            EventBus.Publish(new AudioSettingsChangedEvent(_settingsData.musicEnabled, _settingsData.sfxEnabled));
        }

        private void Load()
        {
            if (SaveManager.Instance.Load(FileName, out SettingsData data))
            {
                _settingsData = data;
                Sanitize();
            }
            else
            {
                // Defaults stay in memory and nothing is written until a setting changes. After a failed read
                // SaveManager reads the key again before writing it and keeps stored settings it finds then.
                _settingsData = new SettingsData();
            }
        }

        // Repairs values that may come from old or edited saves.
        private void Sanitize()
        {
            if (_settingsData.language == null)
                _settingsData.language = string.Empty;
        }

        private void Save()
        {
            SaveManager.Instance.Save(FileName, _settingsData);
        }
    }
}
