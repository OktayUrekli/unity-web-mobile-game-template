using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using _Core.Events;
using _Core.Events.Settings;
using _Core.Managers;
using _Core.Save;
using UnityEngine;

namespace _Core.Settings
{
    /// <summary>
    /// Manages user settings.
    /// Setters store the value, hand the data to <see cref="SaveManager"/>, which debounces the storage write, and
    /// publish an event the owning system applies (<see cref="AudioSettingsChangedEvent"/>,
    /// <see cref="LanguageChangedEvent"/>), so this class depends on no other manager than the save system.
    /// Call <see cref="Commit"/> when the settings UI closes to write to storage right away.
    /// When the saved settings can't be read at start, nothing is written: changes work in memory, the read is retried
    /// (and on <see cref="Commit"/>), and once it succeeds the saved settings are applied with those changes on top.
    /// </summary>
    public class SettingsManager : Singleton<SettingsManager>
    {
        private const string FileName = "settings";

        // Back-off for reading the saved settings again while they can't be read.
        private const float MinReadRetrySeconds = 2f;
        private const float MaxReadRetrySeconds = 60f;

        private SettingsData _settingsData;

        // Changes made while the saved settings are unknown, replayed in order on top of them once they can be read.
        private readonly List<Action<SettingsData>> _changesWhileUnknown = new();
        private bool _storedSettingsUnknown;
        private bool _readRunning;
        private float _nextReadAt;
        private float _readRetryDelay = MinReadRetrySeconds;

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
            Change(data => data.musicEnabled = isEnabled);
            PublishAudioSettings();
        }

        /// <summary>
        /// Turns sound effects on or off and saves.
        /// </summary>
        public void SetSfxEnabled(bool isEnabled)
        {
            Change(data => data.sfxEnabled = isEnabled);
            PublishAudioSettings();
        }

        /// <summary>
        /// Enables or disables vibration and saves.
        /// </summary>
        public void SetVibrationEnabled(bool isEnabled)
        {
            Change(data => data.vibrationEnabled = isEnabled);
        }

        /// <summary>
        /// Sets the language code (for example "en" or "tr"), saves and publishes
        /// <see cref="LanguageChangedEvent"/>, which <c>LocalizationManager</c> applies.
        /// </summary>
        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode) || languageCode == _settingsData.language)
                return;

            Change(data => data.language = languageCode);

            EventBus.Publish(new LanguageChangedEvent(languageCode));
        }

        /// <summary>
        /// Marks the first launch as done (e.g. after the tutorial) and saves.
        /// </summary>
        public void CompleteFirstLaunch()
        {
            if (!_settingsData.isFirstLaunch)
                return;

            Change(data => data.isFirstLaunch = false);
        }

        /// <summary>
        /// Writes pending settings to storage now. Call when the settings UI closes.
        /// While the saved settings could not be read yet, it first tries to read them again; nothing is written until
        /// they can be.
        /// </summary>
        public void Commit()
        {
            // Closing the settings is a good moment to try again. With a synchronous storage, the settings are applied
            // and saved before this flush.
            if (_storedSettingsUnknown)
                _ = ReadStoredSettingsAsync();

            SaveManager.Instance.Flush();
        }

        /// <summary>
        /// Reads the saved settings again while they could not be read. Once they can, applies them with this
        /// session's changes on top. Called from <c>Update</c> with a back-off and from <see cref="Commit"/>; completes
        /// at once for synchronous storages. Internal for EditMode tests.
        /// </summary>
        internal async Task ReadStoredSettingsAsync()
        {
            if (!_storedSettingsUnknown || _readRunning)
                return;

            _readRunning = true;
            _nextReadAt = Time.unscaledTime + _readRetryDelay;
            _readRetryDelay = Mathf.Min(_readRetryDelay * 2f, MaxReadRetrySeconds);

            try
            {
                SaveManager saveManager = SaveManager.Instance;
                if (saveManager == null)
                    return;

                // Nothing saved the key meanwhile (see Change), so this reads storage again.
                await saveManager.PreloadAsync(FileName);

                if (this == null || !_storedSettingsUnknown || saveManager.IsUnreadable(FileName))
                    return;

                saveManager.Load(FileName, out SettingsData stored);
                ApplyStoredSettings(stored);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _readRunning = false;
            }
        }

        private void Update()
        {
            if (_storedSettingsUnknown && !_readRunning && Time.unscaledTime >= _nextReadAt)
                _ = ReadStoredSettingsAsync();
        }

        private void PublishAudioSettings()
        {
            EventBus.Publish(new AudioSettingsChangedEvent(_settingsData.musicEnabled, _settingsData.sfxEnabled));
        }

        private void Load()
        {
            SaveManager saveManager = SaveManager.Instance;
            saveManager.Load(FileName, out SettingsData data);
            _settingsData = data;
            Sanitize(_settingsData);

            _changesWhileUnknown.Clear();
            _readRetryDelay = MinReadRetrySeconds;

            // A failed read leaves the saved settings unknown. Writing now would replace them, so nothing is written
            // until a read succeeds (see Change), and ReadStoredSettingsAsync then applies them.
            _storedSettingsUnknown = saveManager.IsUnreadable(FileName);

            if (_storedSettingsUnknown)
            {
                Debug.LogWarning(
                    "SettingsManager: the saved settings could not be read. Changes stay in memory and are applied " +
                    "on top of the saved settings once those can be read.");
                _nextReadAt = Time.unscaledTime + MinReadRetrySeconds;
            }
        }

        // Applies a change to the current settings and saves them, or, while the saved settings are unknown, records
        // the change for ApplyStoredSettings instead of saving.
        private void Change(Action<SettingsData> change)
        {
            change(_settingsData);

            if (_storedSettingsUnknown)
                _changesWhileUnknown.Add(change);
            else
                Save();
        }

        // The saved settings could be read: they become the current settings, with this session's changes replayed on
        // top. Events go out only for values that differ from what the game used so far.
        private void ApplyStoredSettings(SettingsData stored)
        {
            Sanitize(stored);

            foreach (Action<SettingsData> change in _changesWhileUnknown)
                change(stored);

            bool hadChanges = _changesWhileUnknown.Count > 0;
            SettingsData previous = _settingsData;

            _changesWhileUnknown.Clear();
            _storedSettingsUnknown = false;
            _readRetryDelay = MinReadRetrySeconds;
            _settingsData = stored;

            Debug.Log("SettingsManager: the saved settings could be read and are applied.");

            if (hadChanges)
                Save();

            if (stored.musicEnabled != previous.musicEnabled || stored.sfxEnabled != previous.sfxEnabled)
                PublishAudioSettings();

            if (!string.IsNullOrEmpty(stored.language) && stored.language != previous.language)
                EventBus.Publish(new LanguageChangedEvent(stored.language));
        }

        // Repairs values that may come from old or edited saves.
        private static void Sanitize(SettingsData data)
        {
            if (data.language == null)
                data.language = string.Empty;
        }

        private void Save()
        {
            SaveManager.Instance.Save(FileName, _settingsData);
        }
    }
}
