namespace _Core.Gameplay
{
    /// <summary>
    /// Something that can pause the game through <see cref="PauseManager"/>. The game stays paused while at least one
    /// source holds a pause. The framework defines <see cref="Menu"/>, <see cref="Ad"/> and <see cref="Application"/>;
    /// a game adds its own (tutorial, cutscene) as <c>static readonly</c> instances.
    /// </summary>
    public sealed class PauseSource
    {
        /// <summary>
        /// A pause menu or any other in-game menu that stops play. Released automatically when the scene changes.
        /// </summary>
        public static readonly PauseSource Menu = new("Menu");

        /// <summary>
        /// A full-screen ad request. Driven by <see cref="PauseManager"/> itself from the ad request events; the
        /// platform SDK reports gameplay around ads on its own, so it does not stop platform gameplay.
        /// </summary>
        public static readonly PauseSource Ad = new("Ad", persistsAcrossScenes: true, stopsPlatformGameplay: false);

        /// <summary>
        /// The app is in the background or unfocused, or the platform asks the game to pause. Driven by
        /// <see cref="PauseManager"/> itself from <c>ApplicationPauseChangedEvent</c>.
        /// </summary>
        public static readonly PauseSource Application = new("Application", persistsAcrossScenes: true);

        /// <param name="name">Shown in logs.</param>
        /// <param name="persistsAcrossScenes">False releases this source when the active scene changes.</param>
        /// <param name="stopsPlatformGameplay">
        /// True reports gameplay as stopped to the platform (CrazyGames <c>gameplayStop</c>) while this source pauses.
        /// </param>
        public PauseSource(string name, bool persistsAcrossScenes = false, bool stopsPlatformGameplay = true)
        {
            Name = name;
            PersistsAcrossScenes = persistsAcrossScenes;
            StopsPlatformGameplay = stopsPlatformGameplay;
        }

        /// <summary>
        /// Name shown in logs.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// False when a scene change releases this source.
        /// </summary>
        public bool PersistsAcrossScenes { get; }

        /// <summary>
        /// True when gameplay is reported to the platform as stopped while this source pauses.
        /// </summary>
        public bool StopsPlatformGameplay { get; }

        public override string ToString() => Name;
    }
}
