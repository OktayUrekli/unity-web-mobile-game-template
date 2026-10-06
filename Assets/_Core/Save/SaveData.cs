using System;

namespace _Core.Save
{
    /// <summary>
    /// Base class for all save data objects. Carries a schema version so data written by an older
    /// build can be upgraded when it is loaded (see <see cref="Migrate"/>).
    /// </summary>
    [Serializable]
    public abstract class SaveData
    {
        /// <summary>
        /// Schema version the data was written with; 0 for data saved before versioning existed.
        /// <see cref="SaveManager"/> sets it on every save; game code does not write it.
        /// </summary>
        public int saveVersion;

        /// <summary>
        /// The schema version this class writes. Increase it when a field is renamed, removed or changes
        /// meaning, and handle the older versions in <see cref="Migrate"/>. Adding a field with a default
        /// value needs no new version.
        /// </summary>
        public virtual int CurrentVersion => 1;

        /// <summary>
        /// Upgrades data loaded from an older version: <paramref name="fromVersion"/> is lower than
        /// <see cref="CurrentVersion"/> (0 for data saved before versioning). Fields missing from the old
        /// JSON keep their defaults; keep a renamed field's old name declared until every player has
        /// migrated, and copy it here. Called by <see cref="SaveManager.Load{T}"/>; the next save writes
        /// <see cref="CurrentVersion"/>.
        /// </summary>
        public virtual void Migrate(int fromVersion)
        {
        }
    }
}
