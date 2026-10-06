using _Core.Events;
using _Core.Events.UI;
using _Core.Platform.Core;
using _Core.UI;
using UnityEngine;
using UnityEngine.Localization;

namespace _Project.UI
{
    /// <summary>
    /// Shows the main menu screen when the menu scene starts, and the banner ad while the menu is open (banners
    /// must not be visible during gameplay). On Android, Back on the menu asks whether to quit. The menu music
    /// comes from the scene's <c>SceneMusic</c>.
    /// </summary>
    public class MainMenuSceneEntry : MonoBehaviour
    {
        [SerializeField] private LocalizedString quitConfirmText = new("UI", "menu.quit_confirm");

        private void OnEnable()
        {
            EventBus.Subscribe<UIBackRequestedEvent>(OnBackRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<UIBackRequestedEvent>(OnBackRequested);
        }

        private void Start()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowScreen<MainMenuScreen>();

            PlatformManager platform = PlatformManager.Instance;
            if (platform != null && platform.Capabilities.SupportsBannerAds)
                platform.Ads.ShowBanner();
        }

        private void OnDestroy()
        {
            PlatformManager platform = PlatformManager.Instance;
            if (platform != null && platform.Ads != null)
                platform.Ads.HideBanner();
        }

        // Back with no popup open. Android players expect it to leave the app, so ask first; a web game
        // cannot close its tab and iOS has no Back, so elsewhere it does nothing.
        private void OnBackRequested(UIBackRequestedEvent gameEvent)
        {
            if (QuitsOnBack)
                ConfirmPopup.Open(quitConfirmText, Application.Quit);
        }

        // Android builds, and the Editor while Android is the build target.
        private static bool QuitsOnBack
        {
            get
            {
#if UNITY_ANDROID
                return true;
#else
                return false;
#endif
            }
        }
    }
}
