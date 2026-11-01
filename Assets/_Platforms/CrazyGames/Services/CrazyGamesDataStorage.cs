// Compiled only when CrazyGames is the selected platform (Tools/Template/Platform).
#if PLATFORM_CRAZYGAMES
using System;
using _Core.Platform.Services.Save;
using CrazyGames;

namespace _Platforms.CrazyGames.Services
{
    /// <summary>
    /// Stores save data using the CrazyGames Data API, and reports its 1 MB limit (<see cref="ILimitedSaveStorage"/>).
    /// </summary>
    public class CrazyGamesDataStorage : ISaveStorage, ILimitedSaveStorage
    {
        // CrazyGames stops backing up data past 1 MB; rounded down.
        private const int DataLimitBytes = 1000 * 1000;

        /// <summary>
        /// CrazyGames' data limit: 1 MB.
        /// </summary>
        public int LimitBytes => DataLimitBytes;

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
