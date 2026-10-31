namespace _Core.Platform.Core
{
    /// <summary>
    /// Represents the current initialization state of a platform.
    /// </summary>
    public enum PlatformState
    {
        /// <summary>
        /// <see cref="PlatformManager.InitializeAsync"/> has not run yet; the services are null.
        /// </summary>
        None,

        /// <summary>
        /// <see cref="PlatformManager.InitializeAsync"/> is running.
        /// </summary>
        Initializing,

        /// <summary>
        /// The platform initialized; a service it does not provide is a Null implementation.
        /// </summary>
        Ready,

        /// <summary>
        /// Initialization failed: every service is a Null implementation (storage: PlayerPrefs), and they still work.
        /// </summary>
        Failed
    }
}