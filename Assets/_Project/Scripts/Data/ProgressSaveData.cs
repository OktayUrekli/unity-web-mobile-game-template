using System;
using _Core.Save;

namespace _Project.Data
{
    /// <summary>
    /// Player progress stored through <see cref="SaveManager"/> under <see cref="Key"/>.
    /// </summary>
    [Serializable]
    public class ProgressSaveData : SaveData
    {
        /// <summary>
        /// Save key of this data.
        /// </summary>
        public const string Key = "progress";

        public int bestScore;
    }
}
