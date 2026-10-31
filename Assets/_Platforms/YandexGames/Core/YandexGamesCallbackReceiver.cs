// Compiled only when Yandex Games is the selected platform (Tools/Template/Platform).
#if PLATFORM_YANDEX
using UnityEngine;
using UnityEngine.Scripting;

namespace _Platforms.YandexGames.Core
{
    /// <summary>
    /// Receives the Yandex Games SDK results that YandexGames.jslib sends with SendMessage and hands them to
    /// <see cref="YandexGamesSdk"/>. Created by <see cref="YandexGamesSdk"/> on a DontDestroyOnLoad object named
    /// <see cref="YandexGamesSdk.ReceiverName"/>; never add it to a scene. Only SendMessage calls the On* methods,
    /// so they carry [Preserve] against managed code stripping.
    /// </summary>
    public sealed class YandexGamesCallbackReceiver : MonoBehaviour
    {
        private void Update()
        {
            YandexGamesSdk.ExpireRequests();
        }

        /// <summary>
        /// Called by YandexGames.jslib when <c>YaGames.init()</c> finished and the player data loaded, failed or ran
        /// out of its share of the init timeout.
        /// </summary>
        [Preserve]
        public void OnInitialized(string json)
        {
            YandexGamesSdk.HandleInitialized(json);
        }

        /// <summary>
        /// Called by YandexGames.jslib when <c>YaGames.init()</c> failed.
        /// </summary>
        [Preserve]
        public void OnInitFailed(string error)
        {
            YandexGamesSdk.HandleInitFailed(error);
        }

        /// <summary>
        /// Called by YandexGames.jslib with the result of a request (save, leaderboard, review).
        /// </summary>
        [Preserve]
        public void OnRequestCompleted(string json)
        {
            YandexGamesSdk.HandleRequestCompleted(json);
        }

        /// <summary>
        /// Called by YandexGames.jslib for every ad callback.
        /// </summary>
        [Preserve]
        public void OnAdEvent(string json)
        {
            YandexGamesSdk.HandleAdEvent(json);
        }

        /// <summary>
        /// Called by YandexGames.jslib on the SDK's <c>game_api_pause</c> event.
        /// </summary>
        [Preserve]
        public void OnGamePaused(string unused)
        {
            YandexGamesSdk.HandlePauseChanged(true);
        }

        /// <summary>
        /// Called by YandexGames.jslib on the SDK's <c>game_api_resume</c> event.
        /// </summary>
        [Preserve]
        public void OnGameResumed(string unused)
        {
            YandexGamesSdk.HandlePauseChanged(false);
        }
    }
}
#endif
