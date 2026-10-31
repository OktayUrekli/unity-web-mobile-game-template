namespace _Core.Configuration
{
    /// <summary>
    /// Global access to the <c>GameConfig</c> assigned on the Bootstrapper.
    /// </summary>
    public static class ConfigurationManager
    {
        /// <summary>
        /// The framework configuration; null before the Bootstrapper's <c>Awake</c> has run or when none is assigned
        /// there.
        /// </summary>
        public static GameConfig GameConfig { get; private set; }

        /// <summary>
        /// Sets <see cref="GameConfig"/>. The Bootstrapper calls it in <c>Awake</c>.
        /// </summary>
        public static void Initialize(GameConfig config)
        {
            GameConfig = config;
        }
    }
}