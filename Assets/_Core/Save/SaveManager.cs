using System;
using System.Collections.Generic;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using System.Threading.Tasks;
using _Core.Configuration;
using _Core.Managers;
using _Core.Platform.Core;
using _Core.Platform.Services.Save;
using _Core.Platform.Services.Save.Null;
using _Core.Save.Local;
using UnityEngine;
using UnityEngine.Scripting;

namespace _Core.Save
{
    /// <summary>
    /// Handles save and load operations.
    /// Reads and writes go through an in-memory <see cref="SaveCache"/>; changes are
    /// flushed to storage after a short debounce (see <see cref="SaveConfig"/>), when the
    /// app loses focus/pauses/quits, when a WebGL page is hidden or closed, or when <see cref="Flush"/> is called.
    /// Generic and reusable across projects.
    /// </summary>
    public class SaveManager : Singleton<SaveManager>
    {
        private SaveConfig _saveConfig;
        private ISaveStorage _storage;
        private SaveCache _cache;

        // Back-off for retrying writes that failed (storage or SDK error), reset once a flush leaves nothing pending.
        private const float MinRetryDelaySeconds = 2f;
        private const float MaxRetryDelaySeconds = 60f;

        private bool _flushScheduled;
        private float _flushAt;
        private float _flushDeadline;
        private bool _retryScheduled;
        private float _retryDelay = MinRetryDelaySeconds;
        private int _sizeWarningLevel;

#if UNITY_WEBGL && !UNITY_EDITOR
        private bool _listensForPageHide;

        [DllImport("__Internal")]
        private static extern void SaveManager_ListenForPageHide(string gameObjectName);
#endif

        /// <summary>
        /// True once <see cref="Init"/> has run.
        /// </summary>
        public bool IsInitialized => _cache != null;

        /// <summary>
        /// The storage backend in use (platform storage or the PlayerPrefs fallback).
        /// </summary>
        public ISaveStorage Storage => _storage;

        /// <summary>
        /// True while changes are waiting to be written to storage.
        /// </summary>
        public bool HasPendingChanges => _cache != null && _cache.HasPendingChanges;

        /// <summary>
        /// Test seam: true while a debounced flush or a retry is waiting for <c>Update</c>.
        /// </summary>
        internal bool IsFlushScheduled => _flushScheduled;

        /// <summary>
        /// Picks the storage backend and creates the cache. Called by the Bootstrapper
        /// after the platform is initialized.
        /// </summary>
        public void Init()
        {
            GameConfig gameConfig = ConfigurationManager.GameConfig;
            SaveConfig saveConfig = gameConfig != null ? gameConfig.SaveConfig : null;

            if (saveConfig == null)
            {
                Debug.LogWarning("SaveManager: SaveConfig is missing. Using default save settings.");
                saveConfig = ScriptableObject.CreateInstance<SaveConfig>();
            }

            Init(ResolveStorage(), saveConfig);
            ListenForPageHide();
        }

        /// <summary>
        /// Initializes with an explicit storage and config instead of resolving them from
        /// <see cref="ConfigurationManager"/> and <see cref="PlatformManager"/>. Used by <see cref="Init()"/>
        /// and by EditMode tests, which pass a fake storage.
        /// </summary>
        internal void Init(ISaveStorage storage, SaveConfig saveConfig)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _saveConfig = saveConfig != null ? saveConfig : ScriptableObject.CreateInstance<SaveConfig>();

            if (_cache != null)
                _cache.WriteFailed -= OnWriteFailed;

            _cache = new SaveCache(_storage);
            _cache.WriteFailed += OnWriteFailed;

            Debug.Log($"SaveManager: using {_storage.GetType().Name}.");
        }

        /// <summary>
        /// Saves any SaveData object as compact JSON. The write reaches storage on the next flush.
        /// </summary>
        public void Save<T>(string key, T data) where T : SaveData
        {
            if (!EnsureInitialized())
                return;

            if (data == null)
            {
                Debug.LogWarning($"SaveManager: tried to save null data for '{key}'. Use Delete instead.");
                return;
            }

            data.saveVersion = data.CurrentVersion;

            string json;

            try
            {
                json = JsonUtility.ToJson(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveManager: failed to serialize '{key}'.");
                Debug.LogException(e);
                return;
            }

            _cache.Set(key, json);
            CheckSize();
            ScheduleFlush();
        }

        /// <summary>
        /// Loads a SaveData object. Always returns a usable object in <paramref name="data"/>:
        /// a fresh <typeparamref name="T"/> when nothing is stored, the stored JSON is corrupt or storage
        /// could not be read (the method then returns false). After a failed read, a later <see cref="Save{T}"/>
        /// to the key is written only once the key can be read: if it then holds data, that data is kept. Data written with an older <see cref="SaveData.saveVersion"/>
        /// is upgraded through <see cref="SaveData.Migrate"/> before it is returned.
        /// </summary>
        public bool Load<T>(string key, out T data) where T : SaveData, new()
        {
            data = new T();

            if (!EnsureInitialized() || !_cache.TryGet(key, out string json))
                return false;

            try
            {
                JsonUtility.FromJsonOverwrite(json, data);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveManager: stored data for '{key}' is corrupt and was ignored. {e.Message}");
                data = new T();
                return false;
            }

            UpgradeVersion(key, data);
            return true;
        }

        /// <summary>
        /// True when data is stored under <paramref name="key"/>.
        /// </summary>
        public bool HasSave(string key)
        {
            return EnsureInitialized() && _cache.TryGet(key, out _);
        }

        /// <summary>
        /// Deletes the data stored under <paramref name="key"/>. The delete reaches storage on the next flush.
        /// </summary>
        public void Delete(string key)
        {
            if (!EnsureInitialized())
                return;

            _cache.Remove(key);
            ScheduleFlush();
        }

        /// <summary>
        /// Writes all pending changes to storage now. Call it at important moments
        /// (level end, purchase, settings closed) so nothing is lost if the tab closes.
        /// </summary>
        public void Flush()
        {
            if (_cache == null)
                return;

            _flushScheduled = false;
            _retryScheduled = false;
            _cache.Flush();

            // Keys whose async write is still running stay dirty; try again shortly.
            if (_cache.HasPendingChanges && _cache.HasWritesInFlight)
                ScheduleFlushAt(Time.unscaledTime + Mathf.Max(_saveConfig.FlushDelaySeconds, 0.1f));

            // Everything reached storage (or is being written): the storage works again.
            if (!_retryScheduled && !_cache.HasPendingChanges)
                _retryDelay = MinRetryDelaySeconds;
        }

        /// <summary>
        /// Loads the given keys into memory ahead of time. Required before <see cref="Load{T}"/>
        /// for storages that implement <see cref="IAsyncSaveStorage"/>; harmless for others.
        /// </summary>
        public Task PreloadAsync(params string[] keys)
        {
            if (!EnsureInitialized() || keys == null)
                return Task.CompletedTask;

            return _cache.PreloadAsync((IEnumerable<string>)keys);
        }

        private void Update()
        {
            if (!_flushScheduled)
                return;

            float now = Time.unscaledTime;

            if (now >= _flushAt || now >= _flushDeadline)
                Flush();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                Flush();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                Flush();
        }

        private void OnApplicationQuit()
        {
            Flush();
        }

        // WebGL: a tab closed or reloaded without losing focus first gets none of the events above, so the page
        // reports when it is hidden or unloaded (SaveManager.jslib calls OnPageHidden).
        private void ListenForPageHide()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_listensForPageHide)
                return;

            _listensForPageHide = true;
            SaveManager_ListenForPageHide(gameObject.name);
#endif
        }

        /// <summary>
        /// Called by SaveManager.jslib through SendMessage when the WebGL page is hidden or unloaded.
        /// </summary>
        [Preserve]
        private void OnPageHidden()
        {
            Flush();
        }

        protected override void OnDestroy()
        {
            if (_cache != null)
                _cache.WriteFailed -= OnWriteFailed;

            base.OnDestroy();
        }

        // A failed write stays dirty in the cache. Nothing else may trigger a flush soon (no new saves,
        // no focus change), so retry on a timer, backing off while the storage keeps failing.
        private void OnWriteFailed(string key)
        {
            if (_retryScheduled)
                return;

            _retryScheduled = true;
            ScheduleFlushAt(Time.unscaledTime + _retryDelay);
            _retryDelay = Mathf.Min(_retryDelay * 2f, MaxRetryDelaySeconds);
        }

        // Schedules a flush at 'time', or keeps an already scheduled earlier one.
        private void ScheduleFlushAt(float time)
        {
            if (_flushScheduled)
            {
                _flushAt = Mathf.Min(_flushAt, time);
                _flushDeadline = Mathf.Min(_flushDeadline, time);
                return;
            }

            _flushScheduled = true;
            _flushAt = time;
            _flushDeadline = time;
        }

        // Runs SaveData.Migrate for data written by an older schema version.
        private static void UpgradeVersion<T>(string key, T data) where T : SaveData
        {
            int storedVersion = data.saveVersion;
            int currentVersion = data.CurrentVersion;

            if (storedVersion == currentVersion)
                return;

            if (storedVersion > currentVersion)
            {
                Debug.LogWarning(
                    $"SaveManager: '{key}' was saved with version {storedVersion}, newer than this build's " +
                    $"{currentVersion}. Unknown fields are ignored.");
                return;
            }

            try
            {
                data.Migrate(storedVersion);
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveManager: migrating '{key}' from version {storedVersion} failed; using the data as loaded.");
                Debug.LogException(e);
            }

            data.saveVersion = currentVersion;
        }

        private void ScheduleFlush()
        {
            if (_saveConfig.FlushDelaySeconds <= 0f)
            {
                Flush();
                return;
            }

            float now = Time.unscaledTime;

            if (!_flushScheduled)
            {
                _flushScheduled = true;
                _flushDeadline = now + _saveConfig.MaxFlushDelaySeconds;
            }

            _flushAt = now + _saveConfig.FlushDelaySeconds;
        }

        private void CheckSize()
        {
            int limit = _saveConfig.StorageLimitBytes;
            int total = _cache.TotalBytes;

            // 0 = fine, 1 = near the limit, 2 = over it. Log only when the level rises.
            int level = total > limit ? 2 : total >= limit * _saveConfig.SizeWarningThreshold ? 1 : 0;

            if (level > _sizeWarningLevel)
            {
                if (level == 2)
                {
                    Debug.LogError(
                        $"SaveManager: save data is {total} bytes, over the {limit} byte storage limit. " +
                        "The platform may reject it.");
                }
                else
                {
                    Debug.LogWarning(
                        $"SaveManager: save data is {total} bytes, close to the {limit} byte storage limit.");
                }
            }

            _sizeWarningLevel = level;
        }

        private bool EnsureInitialized()
        {
            if (_cache != null)
                return true;

            Debug.LogError("SaveManager: used before Init(). Start Play Mode from the bootstrap scene.");
            return false;
        }

        private static ISaveStorage ResolveStorage()
        {
            PlatformManager platformManager = PlatformManager.Instance;
            ISaveStorage storage = platformManager != null ? platformManager.Storage : null;

            if (storage == null || storage is NullSaveStorage)
            {
                Debug.Log("SaveManager: platform storage is not available. Falling back to PlayerPrefs.");
                return new PlayerPrefsSaveStorage();
            }

            return storage;
        }
    }
}
