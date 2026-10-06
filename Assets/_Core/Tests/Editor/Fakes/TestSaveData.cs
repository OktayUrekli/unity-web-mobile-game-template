using System;
using System.Collections.Generic;
using _Core.Save;

namespace _Core.Tests.Fakes
{
    /// <summary>
    /// Save data used by the save tests; fields follow the public-field style of real <see cref="SaveData"/> types.
    /// </summary>
    [Serializable]
    public class TestSaveData : SaveData
    {
        /// <summary>
        /// Default of <see cref="playerName"/>, used to detect "fresh object" results.
        /// </summary>
        public const string DefaultPlayerName = "default";

        public int level;

        public string playerName = DefaultPlayerName;

        public bool soundEnabled = true;

        public List<int> unlockedLevels = new();
    }
}
