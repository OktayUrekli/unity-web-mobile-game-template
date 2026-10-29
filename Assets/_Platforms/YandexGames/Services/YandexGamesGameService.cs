// Compiled only when Yandex Games is the selected platform (Tools/Template/Platform).
#if PLATFORM_YANDEX
using _Core.Events;
using _Core.Events.Platform;
using _Core.Platform.Core;
using _Core.Platform.Services.Game;
using _Platforms.YandexGames.Core;
using UnityEngine;

namespace _Platforms.YandexGames.Services
{
    /// <summary>
    /// Yandex Games lifecycle: <c>LoadingAPI.ready</c>, <c>GameplayAPI.start/stop</c>, the interface language and the
    /// SDK's pause requests. Registered only after a successful SDK initialization.
    /// </summary>
    public class YandexGamesGameService : IGameService
    {
        private bool _isPausedByPlatform;

        public YandexGamesGameService()
        {
            // The platform's start-up ad may already have paused the game.
            _isPausedByPlatform = YandexGamesSdk.IsPausedBySdk;
            YandexGamesSdk.PauseChanged += OnSdkPauseChanged;
        }

        // Yandex has no mute request; it pauses the game instead (IsPausedByPlatform).
        public bool IsAudioMutedByPlatform => false;

        public bool IsPausedByPlatform => _isPausedByPlatform;

        /// <summary>
        /// The Yandex Games interface language (ISO 639-1, for example "ru" or "tr"). Yandex requires the game to
        /// start in it (requirement 2.14); <c>LocalizationManager</c> does when the player has not picked a language.
        /// </summary>
        public string Language => YandexGamesSdk.Language;

        public void GameplayStart()
        {
            YandexGamesSdk.GameplayStart();
        }

        public void GameplayStop()
        {
            YandexGamesSdk.GameplayStop();
        }

        public void HappyTime()
        {
            // Yandex Games has no celebration call.
        }

        public void GameReady()
        {
            // Required by moderation (1.19.2): sent once the first scene is playable.
            YandexGamesSdk.GameReady();
        }

        private void OnSdkPauseChanged(bool paused)
        {
            // Yandex also pauses the game around the game's own ads. The ad flow already pauses and mutes for the
            // whole request, and a platform pause would open the pause menu after every ad.
            if (paused && IsAdRequestRunning())
                return;

            if (paused == _isPausedByPlatform)
                return;

            _isPausedByPlatform = paused;
            Debug.Log($"Yandex: Platform pause = {paused}");
            EventBus.Publish(new PlatformPauseChangedEvent(paused));
        }

        private static bool IsAdRequestRunning()
        {
            PlatformManager platform = PlatformManager.Instance;
            return platform != null && platform.Ads != null && platform.Ads.IsAdShowing;
        }
    }
}
#endif
