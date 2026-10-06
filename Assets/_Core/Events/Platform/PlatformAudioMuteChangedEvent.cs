namespace _Core.Events.Platform
{
    /// <summary>
    /// Published when the platform asks the game to mute or unmute all audio
    /// (for example CrazyGames "muteAudio"). <c>AudioManager</c> applies it.
    /// </summary>
    public readonly struct PlatformAudioMuteChangedEvent : IGameEvent
    {
        public bool IsMuted { get; }

        public PlatformAudioMuteChangedEvent(bool isMuted)
        {
            IsMuted = isMuted;
        }
    }
}
