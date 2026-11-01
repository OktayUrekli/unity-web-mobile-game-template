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
    /// Saves are checked against the storage's own size limit (<see cref="ILimitedSaveStorage"/>) and
    /// <see cref="SaveConfig.StorageLimitBytes"/>, the game's save budget.
    /// Generic and reusable across projects.
    /// </summary>
    public class SaveManager : Singleton<SaveManager>
    {
        private SaveConfig _saveConfig;
        private ISaveStorage _storage;
        private SaveCache _cache;

        // Back-off for retrying writes that failed (storage or SDK error), reset once a flush leaves nothing pending
        // and no asynchronous write running.
        private const float MinRetryDelaySeconds = 2f;
        private const float MaxRetryDelaySeconds = 60f;

        private bool _flushScheduled;
        private float _flushAt;
        private float _flushDeadline;
        private bool _retryScheduled;
        private float _retryDelay = MinRetryDelaySeconds;
        private int _sizeWarningLevel;

        // The storage's own limit, 0 for none; saves past it are refused.
        private int _storageLimitBytes;

        // Keys whose last save was refused; the error is logged once until a save of the key is accepted.
        private readonly HashSet<string> _refusedKeys = new();

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

        // The lower of the storage's limit and SaveConfig's budget; read on each use, so Inspector edits of SaveConfig
        // apply in Play Mode.
        private int SizeLimitBytes => _storageLimitBytes > 0
            ? Mathf.Min(_storageLimitBytes, _saveConfig.StorageLimitBytes)
            : _saveConfig.StorageLimitBytes;

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

            _storageLimitBytes = _storage is ILimitedSaveStorage limited ? Mathf.Max(0, limited.LimitBytes) : 0;
            _sizeWarningLevel = 0;
            _refusedKeys.Clear();

            string storageLimit = _storageLimitBytes > 0 ? $"{_storageLimitBytes} bytes" : "none";
            Debug.Log(
                $"SaveManager: using {_storage.GetType().Name}; storage limit {storageLimit}, " +
                $"save budget {_saveConfig.StorageLimitBytes} bytes.");
        }

        /// <summary>
        /// Saves any SaveData object as compact JSON. The write reaches storage on the next flush.
        /// A save that would take the data past the storage's own size limit (<see cref="ILimitedSaveStorage"/>) is
        /// refused with an error, and the last saved data of the key stays.
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

            if (!FitsStorageLimit(key, json))
                return;

            _cache.Set(key, json);
            CheckSize();
            ScheduleFlush();
        }

        /// <summary>
        /// Loads a SaveData object. Always returns a usable object in <paramref name="data"/>:
        /// a fresh <typeparamref name="T"/> when nothing is stored, the stored JSON is corrupt or storage
        /// could not be read (the method then returns false). After a failed read, a later <see cref="Save{T}"/>
        /// to the key is written only once the key can be read: if it then holds data, that data is kept. Data written with an older <see cref="SaveData.saveVersion"/>
        /// is upgraded through <see cref="SaveData.Migrate"/> before it is returned. <see cref="IsUnreadable"/> tells
        /// a failed read apart from a missing save.
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

            _refusedKeys.Remove(key);
            _cache.Remove(key);
            ScheduleFlush();
        }

        /// <summary>
        /// Writes all pending changes to storage now. Call it at important moments
        /// (level end, purchase, settings closed) so nothing is lost if the tab closes.
        /// </summary>
        public void Flush()
        {
            FlushNow(false);
        }

        // includeKeysInFlight: also write keys whose asynchronous write is still running (page hidden or closing).
        private void FlushNow(bool includeKeysInFlight)
        {
            if (_cache == null)
                return;

            _flushScheduled = false;
            _retryScheduled = false;
            _cache.Flush(includeKeysInFlight);

            // Keys whose async write is still running stay dirty; try again shortly. After a page hide, this is the
            // flush that writes such a key once more, after its writes have ended.
            if (_cache.HasPendingChanges && _cache.HasWritesInFlight)
                ScheduleFlushAt(Time.unscaledTime + Mathf.Max(_saveConfig.FlushDelaySeconds, 0.1f));

            // Everything reached storage: the storage works again. A running async write (Yandex) may still fail and
            // must not reset the back-off, or a failing storage would be retried every ~2 s for ever.
            if (!_retryScheduled && !_cache.HasPendingChanges && !_cache.HasWritesInFlight)
                _retryDelay = MinRetryDelaySeconds;
        }

        /// <summary>
        /// Loads the given keys into memory ahead of time. Required before <see cref="Load{T}"/>
        /// for storages that implement <see cref="IAsyncSaveStorage"/>; harmless for others.
        /// A key whose earlier read failed is read again, unless this session changed it since.
        /// </summary>
        public Task PreloadAsync(params string[] keys)
        {
            if (!EnsureInitialized() || keys == null)
                return Task.CompletedTask;

            return _cache.PreloadAsync((IEnumerable<string>)keys);
        }

        /// <summary>
        /// True while the last read of <paramref name="key"/> from storage failed and no later read was kept, so what
        /// is stored there is unknown and <see cref="Load{T}"/> may not have returned it. <see cref="PreloadAsync"/>
        /// reads the key again unless this session changed it since. False before <see cref="Init()"/>.
        /// </summary>
        public bool IsUnreadable(string key)
        {
            return _cache != null && _cache.IsUnreadable(key);
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
        /// Called by SaveManager.jslib through SendMessage when the WebGL page is hidden or unloaded. Writes every
        /// pending change at once, including keys whose earlier asynchronous write is still running, because the page
        /// may close before a later flush. Those keys stay pending, so a later flush writes them once more after their
        /// writes end. Internal for EditMode tests.
        /// </summary>
        [Preserve]
        internal void OnPageHidden()
        {
            FlushNow(true);
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

        // False for a save that would take the data past the storage's own limit: the storage would reject that write
        // (Yandex possibly every later one too). A save that does not grow its key is always taken, so a game can get
        // back under the limit. Only keys read or written this session are counted.
        private bool FitsStorageLimit(string key, string json)
        {
            if (_storageLimitBytes <= 0)
            {
                _refusedKeys.Remove(key);
                return true;
            }

            int newBytes = SaveCache.MeasureBytes(key, json);
            int currentBytes = _cache.GetBytes(key);
            int newTotal = _cache.TotalBytes - currentBytes + newBytes;

            if (newBytes <= currentBytes || newTotal <= _storageLimitBytes)
            {
                _refusedKeys.Remove(key);
                return true;
            }

            if (_refusedKeys.Add(key))
            {
                Debug.LogError(
                    $"SaveManager: '{key}' was not saved: the save data would be {newTotal} bytes, over the " +
                    $"{_storageLimitBytes} byte limit of {_storage.GetType().Name}. The last saved '{key}' is kept.");
            }

            return false;
        }

        private void CheckSize()
        {
            int limit = SizeLimitBytes;
            int total = _cache.TotalBytes;

            // 0 = fine, 1 = near the limit, 2 = over it. Log only when the level rises.
            int level = total > limit ? 2 : total >= limit * _saveConfig.SizeWarningThreshold ? 1 : 0;

            if (level > _sizeWarningLevel)
            {
                if (level == 2)
                {
                    Debug.LogError(
                        $"SaveManager: save data is {total} bytes, over the {limit} byte storage limit. " +
                        "Make the save data smaller.");
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
