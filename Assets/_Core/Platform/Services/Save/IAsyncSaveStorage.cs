using System.Threading.Tasks;

namespace _Core.Platform.Services.Save
{
    /// <summary>
    /// Optional interface for platform storages whose backend is asynchronous
    /// (cloud saves, SDK promises). Implement it next to <see cref="ISaveStorage"/>:
    /// <see cref="_Core.Save.SaveManager"/> then writes through these methods and
    /// reads keys warmed with <see cref="_Core.Save.SaveManager.PreloadAsync"/>.
    /// The synchronous <see cref="ISaveStorage"/> methods may answer from the
    /// storage's own cache and return null for keys that were not loaded yet.
    /// </summary>
    public interface IAsyncSaveStorage
    {
        /// <summary>
        /// Loads the value stored under <paramref name="key"/>; null when nothing is stored.
        /// </summary>
        Task<string> LoadAsync(string key);

        /// <summary>
        /// Stores <paramref name="data"/> under <paramref name="key"/>.
        /// </summary>
        Task SaveAsync(string key, string data);

        /// <summary>
        /// Removes the value stored under <paramref name="key"/>.
        /// </summary>
        Task DeleteAsync(string key);
    }
}
