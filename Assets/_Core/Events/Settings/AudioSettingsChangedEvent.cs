namespace _Core.Events.Settings
{
    /// <summary>
    /// Published by <c>SettingsManager</c> when its settings are loaded and whenever music or sound effects are
    /// turned on or off. <c>AudioManager</c> applies it.
    /// </summary>
    public readonly struct AudioSettingsChangedEvent : IGameEvent
    {
        /// <summary>
        /// True when music is on.
        /// </summary>
        public bool IsMusicEnabled { get; }

        /// <summary>
        /// True when sound effects are on.
        /// </summary>
        public bool IsSfxEnabled { get; }

        public AudioSettingsChangedEvent(bool isMusicEnabled, bool isSfxEnabled)
        {
            IsMusicEnabled = isMusicEnabled;
            IsSfxEnabled = isSfxEnabled;
        }
    }
}
