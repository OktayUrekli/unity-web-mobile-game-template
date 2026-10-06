using System.Collections.Generic;
using _Core.Configuration;
using _Core.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Core.Pooling
{
    /// <summary>
    /// Manages all object pools. Pools come from <see cref="PoolConfig"/> at boot or from <see cref="CreatePool"/>
    /// at runtime. Every object still out of its pool is returned when a scene is unloaded, so nothing spawned
    /// for one scene survives into the next.
    /// </summary>
    public class PoolManager : Singleton<PoolManager>
    {
        private readonly Dictionary<string, ObjectPool> _pools = new();

        // Which pool an object handed out by Get belongs to, so Release needs only the object.
        private readonly Dictionary<GameObject, ObjectPool> _owners = new();
        private Transform _root;

        protected override void Awake()
        {
            base.Awake();

            if (IsDuplicate)
                return;

            _root = new GameObject("Pools").transform;
            _root.SetParent(transform);

            InitializePools();
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        protected override void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            base.OnDestroy();
        }

        /// <summary>
        /// True when a pool with this id exists.
        /// </summary>
        public bool HasPool(string poolId)
        {
            return !string.IsNullOrEmpty(poolId) && _pools.ContainsKey(poolId);
        }

        /// <summary>
        /// Creates a pool at runtime (for content loaded later or pools a scene only needs). Returns false and logs
        /// when the id is empty, already used, or the prefab is missing.
        /// </summary>
        public bool CreatePool(string poolId, GameObject prefab, int initialSize)
        {
            if (string.IsNullOrEmpty(poolId) || prefab == null)
            {
                Debug.LogError("PoolManager: a pool needs an id and a prefab.");
                return false;
            }

            if (_pools.ContainsKey(poolId))
            {
                Debug.LogError($"PoolManager: pool '{poolId}' already exists.");
                return false;
            }

            Transform parent = new GameObject(poolId).transform;
            parent.SetParent(_root);
            _pools.Add(poolId, new ObjectPool(prefab, Mathf.Max(0, initialSize), parent));
            return true;
        }

        /// <summary>
        /// Returns an active object from the requested pool, or null when the pool does not exist.
        /// </summary>
        public GameObject Get(string poolId)
        {
            if (!TryGetPool(poolId, out ObjectPool pool))
                return null;

            return Track(pool.Get(), pool);
        }

        /// <summary>
        /// Returns an object from the requested pool placed before it is activated (see
        /// <see cref="ObjectPool.Get(Vector3, Quaternion, Transform)"/>), or null when the pool does not exist.
        /// </summary>
        public GameObject Get(string poolId, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!TryGetPool(poolId, out ObjectPool pool))
                return null;

            return Track(pool.Get(position, rotation, parent), pool);
        }

        /// <summary>
        /// Returns the <typeparamref name="T"/> component of an object from the requested pool placed before it
        /// is activated, or null when the pool does not exist or its prefab has no such component.
        /// </summary>
        public T Get<T>(string poolId, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            GameObject obj = Get(poolId, position, rotation, parent);
            if (obj == null)
                return null;

            if (obj.TryGetComponent(out T component))
                return component;

            Debug.LogError($"PoolManager: pool '{poolId}' prefab has no {typeof(T).Name} component.");
            Release(obj);
            return null;
        }

        /// <summary>
        /// Returns an object handed out by <see cref="Get(string)"/> to its pool. Releasing an object twice, or one
        /// that did not come from a pool, is ignored with a warning.
        /// </summary>
        public void Release(GameObject obj)
        {
            if (obj == null)
                return;

            if (!_owners.Remove(obj, out ObjectPool pool))
            {
                Debug.LogWarning($"PoolManager: '{obj.name}' is not a pooled object in use; ignored.", obj);
                return;
            }

            pool.Release(obj);
        }

        /// <summary>
        /// Returns every object still out of any pool. Called automatically when a scene is unloaded.
        /// </summary>
        public void ReleaseAll()
        {
            _owners.Clear();

            foreach (ObjectPool pool in _pools.Values)
                pool.ReleaseAll();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            ReleaseAll();
        }

        private GameObject Track(GameObject obj, ObjectPool pool)
        {
            _owners[obj] = pool;
            return obj;
        }

        private bool TryGetPool(string poolId, out ObjectPool pool)
        {
            if (!string.IsNullOrEmpty(poolId) && _pools.TryGetValue(poolId, out pool))
                return true;

            pool = null;
            Debug.LogError($"Pool '{poolId}' was not found.");
            return false;
        }

        /// <summary>
        /// Creates all pools defined in PoolConfig.
        /// </summary>
        private void InitializePools()
        {
            GameConfig gameConfig = ConfigurationManager.GameConfig;
            PoolConfig config = gameConfig != null ? gameConfig.PoolConfig : null;
            if (config == null || config.pools == null)
                return;

            // One bad entry is logged and skipped; it must not stop the other pools from being created.
            foreach (PoolData data in config.pools)
            {
                if (data == null || string.IsNullOrEmpty(data.poolId) || data.prefab == null)
                {
                    Debug.LogError("PoolManager: skipped a pool with an empty id or no prefab in PoolConfig.", config);
                    continue;
                }

                if (_pools.ContainsKey(data.poolId))
                {
                    Debug.LogError($"PoolManager: duplicate pool id '{data.poolId}' in PoolConfig; only the first is used.", config);
                    continue;
                }

                CreatePool(data.poolId, data.prefab, data.initialSize);
            }
        }
    }
}
