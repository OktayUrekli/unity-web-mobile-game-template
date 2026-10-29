// Compiled only when Yandex Games is the selected platform (Tools/Template/Platform).
#if PLATFORM_YANDEX
using _Core.Platform.Services.Review;
using _Platforms.YandexGames.Core;
using UnityEngine;

namespace _Platforms.YandexGames.Services
{
    /// <summary>
    /// Asks the player to rate the game (<c>ysdk.feedback</c>). Yandex shows the dialog only to signed-in players who
    /// have not rated the game, once per session; otherwise nothing is shown and the reason is logged.
    /// </summary>
    public class YandexGamesReviewService : IReviewService
    {
        public void RequestReview()
        {
            YandexGamesSdk.RequestReview((shown, result) =>
            {
                Debug.Log(shown
                    ? $"Yandex: Review dialog {result}."
                    : $"Yandex: Review dialog not shown: {result}");
            });
        }
    }
}
#endif
