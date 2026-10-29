// Compiled only when Yandex Games is the selected platform (Tools/Template/Platform).
#if PLATFORM_YANDEX
using _Core.Platform.Services.Leaderboard;
using _Platforms.YandexGames.Core;
using UnityEngine;

namespace _Platforms.YandexGames.Services
{
    /// <summary>
    /// Submits scores to a Yandex Games leaderboard (<c>ysdk.leaderboards.setScore</c>). The leaderboard ID is the
    /// technical name set in the Developer Console; a null ID uses <c>PlatformConfig.DefaultLeaderboardId</c>.
    /// Yandex accepts scores only from signed-in players and at most one per second; refused scores are logged.
    /// </summary>
    public class YandexGamesLeaderboardService : ILeaderboardService
    {
        private readonly string _defaultLeaderboardId;
        private bool _missingIdLogged;

        /// <param name="defaultLeaderboardId">Technical name used when game code passes no leaderboard ID.</param>
        public YandexGamesLeaderboardService(string defaultLeaderboardId)
        {
            _defaultLeaderboardId = defaultLeaderboardId;
        }

        public void SubmitScore(string leaderboardID, long score)
        {
            string name = string.IsNullOrWhiteSpace(leaderboardID) ? _defaultLeaderboardId : leaderboardID;

            if (string.IsNullOrWhiteSpace(name))
            {
                if (!_missingIdLogged)
                {
                    _missingIdLogged = true;
                    Debug.LogWarning("Yandex: No leaderboard ID passed and PlatformConfig.DefaultLeaderboardId is empty; score not submitted.");
                }

                return;
            }

            if (score < 0)
            {
                Debug.LogWarning($"Yandex: Leaderboard scores cannot be negative ({score}); score not submitted.");
                return;
            }

            YandexGamesSdk.SetLeaderboardScore(name, score, (ok, error) =>
            {
                if (ok)
                    Debug.Log($"Yandex: Score {score} submitted to leaderboard '{name}'.");
                else
                    Debug.Log($"Yandex: Score not submitted to leaderboard '{name}': {error}");
            });
        }

        public void ShowLeaderboard(string leaderboardID)
        {
            // Yandex Games has no leaderboard window of its own; a game shows entries in its own UI.
            Debug.Log("Yandex: ShowLeaderboard is not supported; Yandex has no built-in leaderboard window.");
        }
    }
}
#endif
