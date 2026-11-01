namespace _Core.Platform.Services.Game
{
    /// <summary>
    /// Provides common game lifecycle operations and portal settings for supported platforms.
    /// Prefer <c>GameplayStateManager</c> over calling <see cref="GameplayStart"/>/<see cref="GameplayStop"/> directly;
    /// it keeps the calls paired.
    /// </summary>
    public interface IGameService
    {
        /// <summary>
        /// True while the platform asks the game to be silent (for example CrazyGames "muteAudio").
        /// Changes are published as <c>PlatformAudioMuteChangedEvent</c>.
        /// </summary>
        bool IsAudioMutedByPlatform { get; }

        /// <summary>
        /// True while the platform asks the game to pause (for example Yandex Games game_api_pause). Changes are
        /// published as <c>PlatformPauseChangedEvent</c>; <c>AppLifecycleManager</c> treats it like the app going
        /// to the background and <c>AudioManager</c> silences the game.
        /// </summary>
        bool IsPausedByPlatform { get; }

        /// <summary>
        /// True when the platform tracks focus loss and hidden tabs itself and asks games not to report them as
        /// gameplay stops (CrazyGames). <c>GameplayStateManager</c> then tells it nothing while the app is away and
        /// reports the state once the app is back.
        /// </summary>
        bool HandlesFocusLoss { get; }

        /// <summary>
        /// Language the platform shows the game in (for example "ru" or "en-US"), or null when the platform
        /// does not tell. <c>LocalizationManager</c> uses it at start when the player has not picked a language.
        /// </summary>
        string Language { get; }

        /// <summary>
        /// Reports that the player is actively playing.
        /// </summary>
        void GameplayStart();

        /// <summary>
        /// Reports that active play stopped (pause, menu, game over).
        /// </summary>
        void GameplayStop();

        /// <summary>
        /// Reports a moment of achievement (level completed, boss beaten). Use sparingly.
        /// </summary>
        void HappyTime();

        /// <summary>
        /// Reports that loading finished and the game can be interacted with. No-op where unsupported.
        /// </summary>
        void GameReady();
    }
}
