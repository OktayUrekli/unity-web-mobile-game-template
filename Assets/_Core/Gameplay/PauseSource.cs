namespace _Core.Gameplay
{
    /// <summary>
    /// Something that can pause the game through <see cref="PauseManager"/>. The game stays paused while at least one
    /// source holds a pause. The framework defines <see cref="Menu"/>, <see cref="Ad"/>, <see cref="Application"/> and
    /// <see cref="SceneTransition"/>; a game adds its own (tutorial, cutscene) as <c>static readonly</c> instances.
    /// </summary>
    public sealed class PauseSource
    {
        /// <summary>
        /// The pause menu. Released automatically when the scene changes. A source has one holder at a time (pausing
        /// twice holds it once, one resume releases it), so a menu that can open over another one uses its own
        /// <c>static readonly</c> source instead of sharing this one.
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

        /// <summary>
        /// A scene change through <c>SceneTransition</c>, held from the start of its fade-out until its fade-in ends:
        /// the old scene stops when the player leaves it and the new one starts once it is visible. It persists across
        /// the scene change it covers and stops platform gameplay (loading is not play on CrazyGames or Yandex). Driven
        /// by <c>SceneTransition</c> only; plain <c>SceneLoader</c> loads don't hold it.
        /// </summary>
        public static readonly PauseSource SceneTransition = new("SceneTransition", persistsAcrossScenes: true);

        /// <param name="name">Shown in the pause logs of the Editor and development builds.</param>
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
        /// Name shown in the pause logs of the Editor and development builds.
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
