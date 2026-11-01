using UnityEngine;

namespace _Core.Save
{
    /// <summary>
    /// Tunables for <see cref="SaveManager"/>: write debouncing and the save size budget.
    /// </summary>
    [CreateAssetMenu(fileName = "SaveConfig", menuName = "Configuration/Save Config")]
    public class SaveConfig : ScriptableObject
    {
        [Tooltip("Seconds without a new Save/Delete before pending changes are written to storage. 0 writes immediately.")]
        [Min(0f)]
        [SerializeField] private float flushDelaySeconds = 1f;

        [Tooltip("Upper bound in seconds a pending change may wait while saves keep arriving.")]
        [Min(0f)]
        [SerializeField] private float maxFlushDelaySeconds = 5f;

        [Tooltip("This game's own save budget in bytes: a warning near it and an error over it; nothing is refused. " +
                 "The storage's own limit (Yandex 200 KB, CrazyGames and WebGL PlayerPrefs 1 MB) applies when it is " +
                 "lower, and a save that would pass that limit is refused.")]
        [Min(1)]
        [SerializeField] private int storageLimitBytes = 1024 * 1024;

        [Tooltip("Fraction of the size limit (the lower of the storage's own limit and the save budget) at which a " +
                 "warning is logged.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float sizeWarningThreshold = 0.8f;

        /// <summary>
        /// Seconds without a new write before pending changes are flushed.
        /// </summary>
        public float FlushDelaySeconds => flushDelaySeconds;

        /// <summary>
        /// Maximum seconds a pending change may wait before it is flushed.
        /// </summary>
        public float MaxFlushDelaySeconds => Mathf.Max(maxFlushDelaySeconds, flushDelaySeconds);

        /// <summary>
        /// This game's own save budget in bytes. <see cref="SaveManager"/> warns against the lower of this and the
        /// storage's own limit (<see cref="_Core.Platform.Services.Save.ILimitedSaveStorage"/>); only the storage's
        /// limit refuses saves.
        /// </summary>
        public int StorageLimitBytes => storageLimitBytes;

        /// <summary>
        /// Fraction of the size limit (the lower of the storage's own limit and <see cref="StorageLimitBytes"/>) at
        /// which a warning is logged.
        /// </summary>
        public float SizeWarningThreshold => sizeWarningThreshold;
    }
}
