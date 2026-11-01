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
    /// <c>SaveManager</c> runs one write per key at a time, except when a WebGL page is hidden or closed: it then
    /// writes a key's newest value at once, even while an earlier write of that key is still running, and writes the
    /// key once more after both have ended. While the page stays open, that last write starts after the others ended.
    /// If the page closes first, a storage that applies an earlier request after a later one may keep the older
    /// value.
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
