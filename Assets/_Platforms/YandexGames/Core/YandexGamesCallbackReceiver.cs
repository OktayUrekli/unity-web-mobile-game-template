// Compiled only when Yandex Games is the selected platform (Tools/Template/Platform).
#if PLATFORM_YANDEX
using UnityEngine;

namespace _Platforms.YandexGames.Core
{
    /// <summary>
    /// Receives the Yandex Games SDK results that YandexGames.jslib sends with SendMessage and hands them to
    /// <see cref="YandexGamesSdk"/>. Created by <see cref="YandexGamesSdk"/> on a DontDestroyOnLoad object named
    /// <see cref="YandexGamesSdk.ReceiverName"/>; never add it to a scene.
    /// </summary>
    public sealed class YandexGamesCallbackReceiver : MonoBehaviour
    {
        private void Update()
        {
            YandexGamesSdk.ExpireRequests();
        }

        /// <summary>
        /// Called by YandexGames.jslib when <c>YaGames.init()</c> and the player data load finished.
        /// </summary>
        public void OnInitialized(string json)
        {
            YandexGamesSdk.HandleInitialized(json);
        }

        /// <summary>
        /// Called by YandexGames.jslib when <c>YaGames.init()</c> failed.
        /// </summary>
        public void OnInitFailed(string error)
        {
            YandexGamesSdk.HandleInitFailed(error);
        }

        /// <summary>
        /// Called by YandexGames.jslib with the result of a request (save, leaderboard, review).
        /// </summary>
        public void OnRequestCompleted(string json)
        {
            YandexGamesSdk.HandleRequestCompleted(json);
        }

        /// <summary>
        /// Called by YandexGames.jslib for every ad callback.
        /// </summary>
        public void OnAdEvent(string json)
        {
            YandexGamesSdk.HandleAdEvent(json);
        }

        /// <summary>
        /// Called by YandexGames.jslib on the SDK's <c>game_api_pause</c> event.
        /// </summary>
        public void OnGamePaused(string unused)
        {
            YandexGamesSdk.HandlePauseChanged(true);
        }

        /// <summary>
        /// Called by YandexGames.jslib on the SDK's <c>game_api_resume</c> event.
        /// </summary>
        public void OnGameResumed(string unused)
        {
            YandexGamesSdk.HandlePauseChanged(false);
        }
    }
}
#endif
