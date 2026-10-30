namespace _Core.Events.Platform
{
    /// <summary>
    /// Published by the platform's privacy service when <c>IPrivacyService.IsPrivacyOptionsRequired</c> changes,
    /// for example after the start-up consent check. UI that offers the privacy options refreshes on it.
    /// </summary>
    public readonly struct PrivacyOptionsChangedEvent : IGameEvent
    {
        public bool IsRequired { get; }

        public PrivacyOptionsChangedEvent(bool isRequired)
        {
            IsRequired = isRequired;
        }
    }
}
