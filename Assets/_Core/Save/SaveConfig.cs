using UnityEngine;

namespace _Core.Save
{
    /// <summary>
    /// Tunables for <see cref="SaveManager"/>: write debouncing and storage size limits.
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

        [Tooltip("Storage quota in bytes (CrazyGames Data module: 1 MB).")]
        [Min(1)]
        [SerializeField] private int storageLimitBytes = 1024 * 1024;

        [Tooltip("Fraction of the quota at which a warning is logged.")]
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
        /// Storage quota in bytes.
        /// </summary>
        public int StorageLimitBytes => storageLimitBytes;

        /// <summary>
        /// Fraction of <see cref="StorageLimitBytes"/> at which a warning is logged.
        /// </summary>
        public float SizeWarningThreshold => sizeWarningThreshold;
    }
}
