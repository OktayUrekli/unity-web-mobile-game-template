using _Core.Save;

namespace _Core.Platform.Services.Save.Null
{
    /// <summary>
    /// Safe fallback save storage for unsupported platforms.
    /// </summary>
    public class NullSaveStorage : ISaveStorage
    {
        public bool HasSave(string key)
        {
            // No save data exists in the null storage.
            return false;
        }

        public void Save(string key, string data)
        {
            // Ignore save requests on unsupported platforms.
        }

        public string Load(string key)
        {
            // No save data is available.
            return null;
        }

        public void Delete(string key)
        {
            // Nothing to delete.
        }
    }
}