namespace _Core.Platform.Services.Save
{
    /// <summary>
    /// Defines platform-independent save storage operations.
    /// </summary>
    public interface ISaveStorage
    {
        bool HasSave(string key);

        void Save(string key, string data);

        string Load(string key);

        void Delete(string key);
    }
}