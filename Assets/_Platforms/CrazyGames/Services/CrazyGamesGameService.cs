// Compiled only when CrazyGames is the selected platform (Tools/Template/Platform).
#if PLATFORM_CRAZYGAMES
using System;
using CrazyGames;
using _Core.Events;
using _Core.Events.Platform;
using _Core.Platform.Services.Game;
using UnityEngine;

namespace _Platforms.CrazyGames.Services
{
    /// <summary>
    /// Handles CrazyGames-specific game lifecycle operations and portal settings.
    /// Registered only after a successful SDK initialization.
    /// </summary>
    public class CrazyGamesGameService : IGameService
    {
        private bool _isAudioMutedByPlatform;
        private string _language;
        private bool _languageRead;

        public bool IsAudioMutedByPlatform => _isAudioMutedByPlatform;

        // CrazyGames has no pause request; the portal mutes the game through muteAudio instead.
        public bool IsPausedByPlatform => false;

        /// <summary>
        /// The browser locale CrazyGames reports (for example "en-US"), read once. Null in the Editor, where the
        /// SDK returns a fixed demo value.
        /// </summary>
        public string Language
        {
            get
            {
                if (!_languageRead)
                {
                    _languageRead = true;
                    _language = ReadLanguage();
                }

                return _language;
            }
        }

        public CrazyGamesGameService()
        {
            try
            {
                GameSettings settings = CrazySDK.Game.Settings;
                _isAudioMutedByPlatform = settings != null && settings.muteAudio;

                // The portal can mute the game at any time (for example while a video plays on the page).
                CrazySDK.Game.AddSettingsChangeListener(OnSettingsChanged);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public void GameplayStart()
        {
            Run(() => CrazySDK.Game.GameplayStart());
        }

        public void GameplayStop()
        {
            Run(() => CrazySDK.Game.GameplayStop());
        }

        public void HappyTime()
        {
            Run(() => CrazySDK.Game.HappyTime());
        }

        public void GameReady()
        {
            // SDK 5.31 has no loading-finished API; gameplayStart ends CrazyGames' load measurement.
        }

        /// <summary>
        /// Applies a portal settings change. Public so tests can simulate the portal in the Editor,
        /// where the SDK never calls the settings listener.
        /// </summary>
        public void OnSettingsChanged(GameSettings settings)
        {
            SetAudioMutedByPlatform(settings != null && settings.muteAudio);
        }

        private void SetAudioMutedByPlatform(bool muted)
        {
            if (muted == _isAudioMutedByPlatform)
                return;

            _isAudioMutedByPlatform = muted;
            Debug.Log($"CrazyGames: Portal audio mute = {muted}");
            EventBus.Publish(new PlatformAudioMuteChangedEvent(muted));
        }

        private static string ReadLanguage()
        {
#if UNITY_EDITOR
            return null;
#else
            if (!CrazySDK.IsAvailable || !CrazySDK.IsInitialized)
                return null;

            try
            {
                var info = CrazySDK.User.SystemInfo;
                return info != null && !string.IsNullOrEmpty(info.locale) ? info.locale : null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
#endif
        }

        private static void Run(Action sdkCall)
        {
            if (!CrazySDK.IsAvailable || !CrazySDK.IsInitialized)
                return;

            try
            {
                sdkCall();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
#endif
