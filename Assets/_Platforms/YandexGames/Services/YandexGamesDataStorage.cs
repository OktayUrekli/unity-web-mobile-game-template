// Compiled only when Yandex Games is the selected platform (Tools/Template/Platform).
#if PLATFORM_YANDEX
using System.Collections.Generic;
using System.Threading.Tasks;
using _Core.Platform.Services.Save;
using _Platforms.YandexGames.Core;

namespace _Platforms.YandexGames.Services
{
    /// <summary>
    /// Saves to the player's Yandex cloud data (<c>player.getData/setData</c>), for guests and signed-in players
    /// alike. All data is loaded once while the SDK initializes, so reads answer from memory; writes update memory
    /// and go to Yandex asynchronously (<see cref="IAsyncSaveStorage"/>), and <c>SaveManager</c> retries a write that
    /// failed. Yandex allows 200 KB per player. Registered only when the player data loaded.
    /// </summary>
    public class YandexGamesDataStorage : ISaveStorage, IAsyncSaveStorage
    {
        private readonly Dictionary<string, string> _data;

        /// <param name="initialData">The player's data as loaded during initialization.</param>
        public YandexGamesDataStorage(Dictionary<string, string> initialData)
        {
            _data = initialData ?? new Dictionary<string, string>();
        }

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
            _data[key] = data;
            return YandexGamesSdk.SetDataAsync(key, data);
        }

        public Task DeleteAsync(string key)
        {
            _data.Remove(key);
            return YandexGamesSdk.SetDataAsync(key, null);
        }
    }
}
#endif
