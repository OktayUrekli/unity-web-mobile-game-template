using _Core.Save;

namespace _Project.Data
{
    /// <summary>
    /// Reads and writes <see cref="ProgressSaveData"/>.
    /// </summary>
    public static class ProgressStore
    {
        /// <summary>
        /// The saved best score, or 0 when nothing is saved yet.
        /// </summary>
        public static int BestScore
        {
            get
            {
                if (SaveManager.Instance != null &&
                    SaveManager.Instance.Load(ProgressSaveData.Key, out ProgressSaveData data))
                {
                    return data.bestScore;
                }

                return 0;
            }
        }

        /// <summary>
        /// Stores <paramref name="score"/> when it beats the best score and writes it to storage right away.
        /// </summary>
        /// <returns>True when a new best score was saved.</returns>
        public static bool TrySaveBestScore(int score)
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
                return false;

            // Change the loaded record instead of saving a new one, so the other progress fields a game adds to
            // ProgressSaveData keep their values. Load always returns a usable object.
            saveManager.Load(ProgressSaveData.Key, out ProgressSaveData data);
            if (score <= data.bestScore)
                return false;

            data.bestScore = score;
            saveManager.Save(ProgressSaveData.Key, data);

            // A finished round is a key moment: don't wait for the save debounce.
            saveManager.Flush();
            return true;
        }
    }
}
