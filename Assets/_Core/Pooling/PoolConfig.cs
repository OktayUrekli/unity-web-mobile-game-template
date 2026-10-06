using System.Collections.Generic;
using UnityEngine;

namespace _Core.Pooling
{
    /// <summary>
    /// Stores all pool definitions.
    /// </summary>
    [CreateAssetMenu(fileName = "PoolConfig", menuName = "Configuration/Pool Config")]
    public class PoolConfig : ScriptableObject
    {
        public List<PoolData> pools = new();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (pools == null)
                return;

            HashSet<string> ids = new();

            foreach (PoolData data in pools)
            {
                if (data == null || string.IsNullOrEmpty(data.poolId))
                    continue;

                if (!ids.Add(data.poolId))
                    Debug.LogError($"PoolConfig: duplicate pool id '{data.poolId}'.", this);
            }
        }
#endif
    }
}