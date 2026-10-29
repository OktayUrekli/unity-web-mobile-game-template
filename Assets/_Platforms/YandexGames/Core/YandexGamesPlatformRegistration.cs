// Compiled only when Yandex Games is the selected platform (Tools/Template/Platform).
#if PLATFORM_YANDEX
using _Core.Platform.Core;
using UnityEngine;

namespace _Platforms.YandexGames.Core
{
    /// <summary>
    /// Registers <see cref="YandexGamesPlatform"/> with <see cref="PlatformFactory"/> before the first
    /// scene loads, so the bootstrap scene can create it without _Core referencing this bridge.
    /// </summary>
    public static class YandexGamesPlatformRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            PlatformFactory.Register(PlatformType.YandexGames, () => new YandexGamesPlatform());
        }
    }
}
#endif
