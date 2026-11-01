using System.Collections.Generic;
using _Core.Platform.Services.Save;
using UnityEngine;

namespace _Core.Save.Local
{
    /// <summary>
    /// Stores save data in Unity <see cref="PlayerPrefs"/>.
    /// Used by <see cref="SaveManager"/> when the platform has no working storage
    /// (SDK unavailable, <c>NullPlatform</c>). On WebGL PlayerPrefs live in the
    /// browser's IndexedDB, so data is per-domain and per-browser, and they are limited to 1 MB
    /// (<see cref="LimitBytes"/>).
    /// </summary>
    public class PlayerPrefsSaveStorage : ISaveStorage, ILimitedSaveStorage
    {
        private const string KeyPrefix = "save_";

#if UNITY_WEBGL
        // Unity keeps at most 1 MB of PlayerPrefs on the web, shared with every other PlayerPrefs key; rounded down.
        private const int OwnLimitBytes = 1000 * 1000;
#else
        // No limit on Android, iOS and standalone.
        private const int OwnLimitBytes = 0;
#endif

        private readonly int _limitBytes;

        /// <summary>
        /// Creates a storage with PlayerPrefs' own limit (see <see cref="LimitBytes"/>).
        /// </summary>
        public PlayerPrefsSaveStorage() : this(0)
        {
        }

        /// <summary>
        /// Creates a storage that also reports <paramref name="limitBytes"/> when it is lower than PlayerPrefs' own
        /// limit. A platform whose own storage holds less passes its limit, so saves made on this fallback, which the
        /// Editor always uses for some platforms (Yandex Games), stay within what the platform accepts. 0 or less
        /// keeps PlayerPrefs' own limit.
        /// </summary>
        public PlayerPrefsSaveStorage(int limitBytes)
        {
            _limitBytes = limitBytes > 0 && (OwnLimitBytes <= 0 || limitBytes < OwnLimitBytes)
                ? limitBytes
                : OwnLimitBytes;
        }

        /// <summary>
        /// 1 MB in WebGL builds and in the Editor while WebGL is the build target, 0 (no limit) elsewhere, or the
        /// lower limit given to the constructor.
        /// </summary>
        public int LimitBytes => _limitBytes;

        /// <summary>
        /// Null: PlayerPrefs can't list their keys.
        /// </summary>
        public IEnumerable<string> StoredKeys => null;

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
