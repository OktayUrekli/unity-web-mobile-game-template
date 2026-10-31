namespace _Core.Platform.Services.Leaderboard
{
    /// <summary>
    /// Provides leaderboard functionality.
    /// </summary>
    public interface ILeaderboardService
    {
        /// <summary>
        /// Submits <paramref name="score"/> without reporting back whether the platform accepted it. A null or empty
        /// <paramref name="leaderboardID"/> means <c>PlatformConfig.DefaultLeaderboardId</c>; a platform with a single
        /// leaderboard ignores the ID.
        /// </summary>
        void SubmitScore(string leaderboardID, long score);

        /// <summary>
        /// Opens the platform's own leaderboard window where it has one; otherwise does nothing.
        /// </summary>
        void ShowLeaderboard(string leaderboardID);
    }
}