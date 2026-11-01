// Compiled only when Yandex Games is the selected platform (Tools/Template/Platform).
#if PLATFORM_YANDEX
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using _Core.Platform.Services.Save;
using _Core.Save;
using _Platforms.YandexGames.Core;

namespace _Platforms.YandexGames.Services
{
    /// <summary>
    /// Saves to the player's Yandex cloud data (<c>player.getData/setData</c>), for guests and signed-in players
    /// alike. All data is loaded once while the SDK initializes, so reads answer from memory; writes update memory
    /// and go to Yandex asynchronously (<see cref="IAsyncSaveStorage"/>), and <c>SaveManager</c> retries a write that
    /// failed. Reports Yandex's 200 KB limit and all its keys (<see cref="ILimitedSaveStorage"/>), and refuses a save
    /// that would grow the data past that limit. A save that does not grow the data, and a delete, still go out, so
    /// data already past the limit (left by an older build) can shrink back under it.
    /// Registered only when the player data loaded.
    /// </summary>
    public class YandexGamesDataStorage : ISaveStorage, IAsyncSaveStorage, ILimitedSaveStorage
    {
        // Yandex allows 200 KB per player; rounded down for the object's own braces, quotes, colons and commas.
        // YandexGamesPlatform also gives it to the PlayerPrefs fallback.
        internal const int PlayerDataLimitBytes = 200 * 1000;

        private readonly Dictionary<string, string> _data;

        // Size of _data, counted as SaveCache.MeasureBytes counts it.
        private int _dataBytes;

        /// <param name="initialData">The player's data as loaded during initialization.</param>
        public YandexGamesDataStorage(Dictionary<string, string> initialData)
        {
            _data = initialData ?? new Dictionary<string, string>();

            foreach (KeyValuePair<string, string> pair in _data)
                _dataBytes += SaveCache.MeasureBytes(pair.Key, pair.Value);
        }

        /// <summary>
        /// Yandex's limit for one player's data: 200 KB.
        /// </summary>
        public int LimitBytes => PlayerDataLimitBytes;

        /// <summary>
        /// Every key of the player's data, loaded at start.
        /// </summary>
        public IEnumerable<string> StoredKeys => _data.Keys;

        public bool HasSave(string key)
        {
            return _data.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value);
        }

        public string Load(string key)
        {
            return _data.TryGetValue(key, out string value) ? value : null;
        }

        public void Save(string key, string data)
        {
            _ = SaveAsync(key, data);
        }

        public void Delete(string key)
        {
            _ = DeleteAsync(key);
        }

        public Task<string> LoadAsync(string key)
        {
            return Task.FromResult(Load(key));
        }

        public Task SaveAsync(string key, string data)
        {
            _data.TryGetValue(key, out string current);
            int newBytes = _dataBytes - SaveCache.MeasureBytes(key, current) + SaveCache.MeasureBytes(key, data);

            // setData sends the whole object. What Yandex does past its limit is undocumented, and one oversized key
            // could fail every later save, so this storage never grows the data past the limit; a save that does not
            // grow it still goes out, so data already over the limit can shrink back under it. Keys this session never
            // read count too. SaveManager counts them as well and refuses such a save before it is queued; this check
            // covers the rest, for example pending saves of several keys that reach this storage in another order.
            if (newBytes > PlayerDataLimitBytes && newBytes > _dataBytes)
            {
                return Task.FromException(new InvalidOperationException(
                    $"Yandex: '{key}' was not saved: the player data would be {newBytes} bytes, over Yandex's " +
                    $"{PlayerDataLimitBytes} byte limit."));
            }

            _data[key] = data;
            _dataBytes = newBytes;
            return YandexGamesSdk.SetDataAsync(key, data);
        }

        public Task DeleteAsync(string key)
        {
            if (_data.TryGetValue(key, out string current))
            {
                _dataBytes -= SaveCache.MeasureBytes(key, current);
                _data.Remove(key);
            }

            return YandexGamesSdk.SetDataAsync(key, null);
        }
    }
}
#endif
