using System.Collections.Generic;
using _Core.Platform.Services.Game;

namespace _Core.Tests.Fakes
{
    /// <summary>
    /// <see cref="IGameService"/> test double that records the gameplay calls in order.
    /// </summary>
    public class FakeGameService : IGameService
    {
        /// <summary>
        /// "Start" for each <see cref="GameplayStart"/> and "Stop" for each <see cref="GameplayStop"/>, in order.
        /// </summary>
        public List<string> Calls { get; } = new();

        public bool IsAudioMutedByPlatform => false;

        public bool IsPausedByPlatform => false;

        /// <summary>
        /// Settable, so a test can play a platform that handles focus loss itself (CrazyGames) or one that does not.
        /// </summary>
        public bool HandlesFocusLoss { get; set; }

        public string Language => null;

        public void GameplayStart()
        {
            Calls.Add("Start");
        }

        public void GameplayStop()
        {
            Calls.Add("Stop");
        }

        public void HappyTime()
        {
        }

        public void GameReady()
        {
        }
    }
}
