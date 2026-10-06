namespace _Core.Platform.Services.Leaderboard.Null
{
    /// <summary>
    /// Provides a safe fallback when leaderboards are not supported.
    /// </summary>
    public class NullLeaderboardService : ILeaderboardService
    {
        public void SubmitScore(string leaderboardID, long score)
        {
            // Leaderboards are not supported on this platform.
        }

        public void ShowLeaderboard(string leaderboardID)
        {
            // Leaderboards are not supported on this platform.
        }
    }
}