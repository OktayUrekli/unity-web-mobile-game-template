// Compiled only when Google Play is the selected platform (Tools/Template/Platform).
#if PLATFORM_GOOGLEPLAY
using _Core.Platform.Core;
using UnityEngine;

namespace _Platforms.GooglePlay.Core
{
    /// <summary>
    /// Registers <see cref="GooglePlayPlatform"/> with <see cref="PlatformFactory"/> before the first
    /// scene loads, so the bootstrap scene can create it without _Core referencing this bridge.
    /// </summary>
    public static class GooglePlayPlatformRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            PlatformFactory.Register(PlatformType.GooglePlay, () => new GooglePlayPlatform());
        }
    }
}
#endif
