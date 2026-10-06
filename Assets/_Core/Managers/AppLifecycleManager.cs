using _Core.Configuration;
using _Core.Events;
using _Core.Events.Gameplay;
using _Core.Events.Platform;
using _Core.Platform.Core;
using UnityEngine;

namespace _Core.Managers
{
    /// <summary>
    /// App-level behaviour every game needs, mostly for mobile: the target frame rate on Android/iOS,
    /// keeping the screen awake during active play, and <see cref="ApplicationPauseChangedEvent"/> when
    /// the app is backgrounded, loses focus or the platform asks the game to pause. Created by the Bootstrapper.
    /// </summary>
    public class AppLifecycleManager : Singleton<AppLifecycleManager>
    {
        /// <summary>
        /// True while the app is in the background or unfocused, or the platform asks the game to pause.
        /// </summary>
        public bool IsApplicationPaused { get; private set; }

        // The two pause sources: the OS/browser (background, focus) and the platform (IGameService.IsPausedByPlatform).
        private bool _appPaused;
        private bool _platformPaused;

        /// <summary>
        /// Applies the start-up settings. Called by the Bootstrapper.
        /// </summary>
        public void Init()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            // Mobile players default to 30 FPS. WebGL is left alone: the browser paces frames.
            int frameRate = ConfigurationManager.GameConfig != null && ConfigurationManager.GameConfig.PlatformConfig != null
                ? ConfigurationManager.GameConfig.PlatformConfig.MobileTargetFrameRate
                : 60;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = frameRate;
#endif
            EventBus.Subscribe<GameplayStateChangedEvent>(OnGameplayStateChanged);

            PlatformManager platform = PlatformManager.Instance;
            _platformPaused = platform != null && platform.Game != null && platform.Game.IsPausedByPlatform;
            EventBus.Subscribe<PlatformPauseChangedEvent>(OnPlatformPauseChanged);
            PublishIfChanged();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            EventBus.Unsubscribe<GameplayStateChangedEvent>(OnGameplayStateChanged);
            EventBus.Unsubscribe<PlatformPauseChangedEvent>(OnPlatformPauseChanged);
        }

        private void OnGameplayStateChanged(GameplayStateChangedEvent gameEvent)
        {
#if UNITY_ANDROID || UNITY_IOS
            // Touch games get no input for long stretches (watching, waiting); don't let the screen dim mid-level.
            Screen.sleepTimeout = gameEvent.IsActive ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
#endif
        }

        private void OnApplicationPause(bool paused)
        {
            SetAppPaused(paused);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
#if UNITY_EDITOR
            // Clicking the Inspector or another window would pause the game on every edit.
            return;
#else
            SetAppPaused(!hasFocus);
#endif
        }

        private void OnPlatformPauseChanged(PlatformPauseChangedEvent gameEvent)
        {
            _platformPaused = gameEvent.IsPaused;
            PublishIfChanged();
        }

        private void SetAppPaused(bool paused)
        {
            if (paused == _appPaused)
                return;

            // Web ads and mobile ad SDKs take focus while they play; the ad flow already pauses the game.
            PlatformManager platform = PlatformManager.Instance;
            if (paused && platform != null && platform.Ads != null && platform.Ads.IsAdShowing)
                return;

            _appPaused = paused;
            PublishIfChanged();
        }

        // Publishes when the combined state changes, so a platform pause during a backgrounded app sends nothing new.
        private void PublishIfChanged()
        {
            bool paused = _appPaused || _platformPaused;
            if (paused == IsApplicationPaused)
                return;

            IsApplicationPaused = paused;
            EventBus.Publish(new ApplicationPauseChangedEvent(paused));
        }
    }
}
