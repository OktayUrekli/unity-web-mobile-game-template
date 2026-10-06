using _Core.Platform.Services.Save;
using UnityEngine;

namespace _Core.Save.Local
{
    /// <summary>
    /// Stores save data in Unity <see cref="PlayerPrefs"/>.
    /// Used by <see cref="SaveManager"/> when the platform has no working storage
    /// (SDK unavailable, <c>NullPlatform</c>). On WebGL PlayerPrefs live in the
    /// browser's IndexedDB, so data is per-domain and per-browser.
    /// </summary>
    public class PlayerPrefsSaveStorage : ISaveStorage
    {
        private const string KeyPrefix = "save_";

        /// <inheritdoc />
        public bool HasSave(string key)
        {
            return PlayerPrefs.HasKey(GetKey(key));
        }

        /// <inheritdoc />
        public void Save(string key, string data)
        {
            PlayerPrefs.SetString(GetKey(key), data);

            // WebGL only persists PlayerPrefs to IndexedDB on Save().
            PlayerPrefs.Save();
        }

        /// <inheritdoc />
        public string Load(string key)
        {
            string prefsKey = GetKey(key);

            return PlayerPrefs.HasKey(prefsKey)
                ? PlayerPrefs.GetString(prefsKey)
                : null;
        }

        /// <inheritdoc />
        public void Delete(string key)
        {
            PlayerPrefs.DeleteKey(GetKey(key));
            PlayerPrefs.Save();
        }

        private static string GetKey(string key)
        {
            return KeyPrefix + key;
        }
    }
}
