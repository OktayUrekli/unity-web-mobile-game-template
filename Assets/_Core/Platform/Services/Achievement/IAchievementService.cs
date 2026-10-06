namespace _Core.Platform.Services.Achievement
{
    /// <summary>
    /// Provides achievement functionality.
    /// </summary>
    public interface IAchievementService
    {
        void Unlock(string achievementID);

        void Increment(string achievementID, int amount);

        void ShowAchievements();
    }
}