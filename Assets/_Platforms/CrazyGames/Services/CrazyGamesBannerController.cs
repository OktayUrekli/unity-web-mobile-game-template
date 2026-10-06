using _Core.Managers;
#if PLATFORM_CRAZYGAMES
using CrazyGames;
#endif
using UnityEngine;

namespace _Platforms.CrazyGames.Services
{
    /// <summary>
    /// Controls the visibility and refresh state of the CrazyGames banner. Always compiled because scenes
    /// reference it; without PLATFORM_CRAZYGAMES it only keeps the banner hidden.
    /// </summary>
    public class CrazyGamesBannerController : Singleton<CrazyGamesBannerController>
    {
        protected override void Awake()
        {
            base.Awake();

            if (IsDuplicate)
                return;

            // Hidden until IAdsService.ShowBanner is called, so the banner (and its Editor placeholder)
            // never appears on platforms or screens that did not ask for it.
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows the banner GameObject.
        /// </summary>
        public void Show()
        {
            if (!CanUseBanner())
                return;

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the banner GameObject.
        /// </summary>
        public void Hide()
        {
            if (!CanUseBanner())
                return;

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Refreshes the CrazyGames banner overlay.
        /// </summary>
        public void Refresh()
        {
            if (!CanUseBanner())
                return;

#if PLATFORM_CRAZYGAMES
            CrazySDK.Banner.RefreshBanners();
#endif
        }

        /// <summary>
        /// Checks whether the CrazyGames banner system is available.
        /// </summary>
        private bool CanUseBanner()
        {
#if PLATFORM_CRAZYGAMES
            return CrazySDK.IsAvailable && CrazySDK.IsInitialized;
#else
            return false;
#endif
        }
    }
}