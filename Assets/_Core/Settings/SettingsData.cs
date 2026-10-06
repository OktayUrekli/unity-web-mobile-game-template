using _Core.Save;

namespace _Core.Settings
{
    /// <summary>
    /// Global settings shared across most games.
    /// </summary>
    [System.Serializable]
    public class SettingsData : SaveData
    {
        // Older saves stored musicVolume/sfxVolume floats; JsonUtility skips those keys and both start enabled.
        public bool musicEnabled = true;

        public bool sfxEnabled = true;

        public bool vibrationEnabled = true;

        // Language code ("en", "tr", "es"...). Empty follows the device/browser language.
        public string language = "";

        // Replaces the old "ısFirstLaunch" key. That field was never written,
        // so it was always true and nothing needs migrating.
        public bool isFirstLaunch = true;
    }
}
