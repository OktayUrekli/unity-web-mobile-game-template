namespace _Core.Platform.Services.Achievement.Null
{
    /// <summary>
    /// Provides a safe fallback when achievements are not supported.
    /// </summary>
    public class NullAchievementService : IAchievementService
    {
        public void Unlock(string achievementID)
        {
            // Achievements are not supported on this platform.
        }

        public void Increment(string achievementID, int amount)
        {
            // Achievements are not supported on this platform.
        }

        public void ShowAchievements()
        {
            // Achievements are not supported on this platform.
        }
    }
}