namespace _Core.Platform.Services.Leaderboard
{
    /// <summary>
    /// Provides leaderboard functionality.
    /// </summary>
    public interface ILeaderboardService
    {
        void SubmitScore(string leaderboardID, long score);

        void ShowLeaderboard(string leaderboardID);
    }
}