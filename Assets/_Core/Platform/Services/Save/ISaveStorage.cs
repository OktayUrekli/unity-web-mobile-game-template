namespace _Core.Platform.Services.Save
{
    /// <summary>
    /// Defines platform-independent save storage operations.
    /// </summary>
    public interface ISaveStorage
    {
        /// <summary>
        /// True when a value is stored under <paramref name="key"/>. Throws when storage cannot be read.
        /// </summary>
        bool HasSave(string key);

        /// <summary>
        /// Stores <paramref name="data"/> under <paramref name="key"/>, or throws so <c>SaveManager</c> retries the
        /// write. A storage whose writes finish later also implements <see cref="IAsyncSaveStorage"/>.
        /// </summary>
        void Save(string key, string data);

        /// <summary>
        /// The value stored under <paramref name="key"/>, or null when nothing is stored. Throws when storage cannot
        /// be read, so the failure is not taken for an empty save.
        /// </summary>
        string Load(string key);

        /// <summary>
        /// Removes the value stored under <paramref name="key"/>, or throws so <c>SaveManager</c> retries.
        /// </summary>
        void Delete(string key);
    }
}