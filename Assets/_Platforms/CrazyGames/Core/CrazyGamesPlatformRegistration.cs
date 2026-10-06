// Compiled only when CrazyGames is the selected platform (Tools/Template/Platform).
#if PLATFORM_CRAZYGAMES
using _Core.Platform.Core;
using UnityEngine;

namespace _Platforms.CrazyGames.Core
{
    /// <summary>
    /// Registers <see cref="CrazyGamesPlatform"/> with <see cref="PlatformFactory"/> before the first
    /// scene loads, so the bootstrap scene can create it without _Core referencing this bridge.
    /// </summary>
    public static class CrazyGamesPlatformRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            PlatformFactory.Register(PlatformType.CrazyGames, () => new CrazyGamesPlatform());
        }
    }
}
#endif
