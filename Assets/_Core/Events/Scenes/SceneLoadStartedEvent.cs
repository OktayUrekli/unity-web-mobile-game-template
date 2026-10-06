namespace _Core.Events.Scenes
{
    /// <summary>
    /// Published by <c>SceneLoader</c> right before it replaces the active scene (inside a <c>SceneTransition</c>,
    /// once the screen is covered). <c>UIManager</c> hides every screen and popup on it.
    /// </summary>
    public readonly struct SceneLoadStartedEvent : IGameEvent
    {
        /// <summary>
        /// Name of the scene being loaded.
        /// </summary>
        public string SceneName { get; }

        public SceneLoadStartedEvent(string sceneName)
        {
            SceneName = sceneName;
        }
    }
}
