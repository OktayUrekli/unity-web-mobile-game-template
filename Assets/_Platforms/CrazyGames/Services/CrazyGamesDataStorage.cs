// Compiled only when CrazyGames is the selected platform (Tools/Template/Platform).
#if PLATFORM_CRAZYGAMES
using System;
using _Core.Platform.Services.Save;
using CrazyGames;

namespace _Platforms.CrazyGames.Services
{
    /// <summary>
    /// Stores save data using the CrazyGames Data API.
    /// </summary>
    public class CrazyGamesDataStorage : ISaveStorage
    {
        public bool HasSave(string key)
        {
            EnsureSdkReady();

            return CrazySDK.Data.HasKey(key);
        }

        public void Save(string key, string data)
        {
            EnsureSdkReady();

            CrazySDK.Data.SetString(key, data);
        }

        public string Load(string key)
        {
            EnsureSdkReady();

            if (!CrazySDK.Data.HasKey(key))
                return null;

            return CrazySDK.Data.GetString(key, null);
        }

        public void Delete(string key)
        {
            EnsureSdkReady();

            CrazySDK.Data.DeleteKey(key);
        }

        private void EnsureSdkReady()
        {
            if (!CrazySDK.IsAvailable)
            {
                throw new InvalidOperationException(
                    "CrazyGames SDK is not available.");
            }

            if (!CrazySDK.IsInitialized)
            {
                throw new InvalidOperationException(
                    "CrazyGames SDK is not initialized.");
            }
        }
    }
}
#endif
