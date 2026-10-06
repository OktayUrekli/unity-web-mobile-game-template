namespace _Core.Platform.Services.Game.Null
{
    /// <summary>
    /// Safe fallback game service for unsupported platforms.
    /// </summary>
    public class NullGameService : IGameService
    {
        public bool IsAudioMutedByPlatform => false;

        public bool IsPausedByPlatform => false;

        public string Language => null;

        public void GameplayStart()
        {
            // No platform-specific gameplay start operation is required.
        }

        public void GameplayStop()
        {
            // No platform-specific gameplay stop operation is required.
        }

        public void HappyTime()
        {
            // No platform-specific celebration is available.
        }

        public void GameReady()
        {
            // No platform-specific loading signal is required.
        }
    }
}
