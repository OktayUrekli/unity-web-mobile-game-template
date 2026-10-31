namespace _Core.Platform.Services.Achievement
{
    /// <summary>
    /// Provides achievement functionality.
    /// </summary>
    public interface IAchievementService
    {
        /// <summary>
        /// Unlocks the achievement <paramref name="achievementID"/>.
        /// </summary>
        void Unlock(string achievementID);

        /// <summary>
        /// Adds <paramref name="amount"/> to the progress of an incremental achievement.
        /// </summary>
        void Increment(string achievementID, int amount);

        /// <summary>
        /// Opens the platform's own achievements window where it has one; otherwise does nothing.
        /// </summary>
        void ShowAchievements();
    }
}