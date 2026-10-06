namespace _Core.Events.Settings
{
    /// <summary>
    /// Published by <see cref="_Core.Settings.SettingsManager"/> when the selected language changes.
    /// </summary>
    public readonly struct LanguageChangedEvent : IGameEvent
    {
        /// <summary>
        /// The new language code (for example "en" or "tr").
        /// </summary>
        public string Language { get; }

        public LanguageChangedEvent(string language)
        {
            Language = language;
        }
    }
}
