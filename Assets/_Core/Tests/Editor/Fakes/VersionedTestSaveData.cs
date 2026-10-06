using System;
using _Core.Save;

namespace _Core.Tests.Fakes
{
    /// <summary>
    /// Save data at schema version 2 for the migration tests: version 1 stored the score as
    /// <see cref="points"/>, version 2 renamed it to <see cref="score"/>.
    /// </summary>
    [Serializable]
    public class VersionedTestSaveData : SaveData
    {
        /// <summary>
        /// Version 1 name of <see cref="score"/>; kept declared so <see cref="Migrate"/> can copy it.
        /// </summary>
        public int points;

        public int score;

        /// <summary>
        /// The version <see cref="Migrate"/> was called with, or -1 when it was not called. Not serialized.
        /// </summary>
        public int MigratedFromVersion { get; private set; } = -1;

        /// <inheritdoc />
        public override int CurrentVersion => 2;

        /// <inheritdoc />
        public override void Migrate(int fromVersion)
        {
            MigratedFromVersion = fromVersion;

            if (fromVersion < 2)
            {
                score = points;
                points = 0;
            }
        }
    }
}
