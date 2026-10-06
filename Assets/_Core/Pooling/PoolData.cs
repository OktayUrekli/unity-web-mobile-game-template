using System;
using UnityEngine;

namespace _Core.Pooling
{
    /// <summary>
    /// Stores a pooled prefab and its initial size.
    /// </summary>
    [Serializable]
    public class PoolData
    {
        [Tooltip("Unique identifier of the pool.")]
        public string poolId;

        [Tooltip("Prefab that will be pooled.")]
        public GameObject prefab;

        [Min(1)]
        [Tooltip("Number of objects created on initialization.")]
        public int initialSize = 10;
    }
}