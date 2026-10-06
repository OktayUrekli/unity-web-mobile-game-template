using System.Collections.Generic;
using UnityEngine;

namespace _Core.Pooling
{
    /// <summary>
    /// Manages a pool for a single prefab and keeps track of the objects it handed out, so they can all be
    /// returned at once (<see cref="ReleaseAll"/>).
    /// </summary>
    public class ObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Queue<GameObject> _pool = new();

        // Objects currently waiting in the pool, so a second Release of the same object is caught.
        private readonly HashSet<GameObject> _inPool = new();

        // Objects handed out by Get and not released yet.
        private readonly HashSet<GameObject> _active = new();
        private readonly List<GameObject> _releaseBuffer = new();
        private readonly Transform _parent;

        public ObjectPool(GameObject prefab, int initialSize, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;

            for (int i = 0; i < initialSize; i++)
            {
                CreateObject();
            }
        }

        /// <summary>
        /// The pooled prefab.
        /// </summary>
        public GameObject Prefab => _prefab;

        /// <summary>
        /// Number of objects handed out and not released yet (objects destroyed meanwhile included until the
        /// next <see cref="ReleaseAll"/>).
        /// </summary>
        public int ActiveCount => _active.Count;

        /// <summary>
        /// Returns an object from the pool, activated, then calls <see cref="IPoolable.OnSpawn"/>.
        /// It stays under the pool's parent at its last position.
        /// </summary>
        public GameObject Get()
        {
            GameObject obj = Take();
            Activate(obj);
            return obj;
        }

        /// <summary>
        /// Returns an object from the pool placed at <paramref name="position"/>/<paramref name="rotation"/>
        /// (under <paramref name="parent"/> when given) before it is activated, so <c>OnEnable</c> and
        /// <see cref="IPoolable.OnSpawn"/> already see the new placement.
        /// </summary>
        public GameObject Get(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            GameObject obj = Take();

            Transform objTransform = obj.transform;
            if (parent != null)
                objTransform.SetParent(parent, false);

            objTransform.SetPositionAndRotation(position, rotation);
            Activate(obj);
            return obj;
        }

        /// <summary>
        /// Returns an object back to the pool.
        /// </summary>
        public void Release(GameObject obj)
        {
            if (obj == null)
                return;

            if (_inPool.Contains(obj))
            {
                Debug.LogWarning($"ObjectPool: '{obj.name}' was released twice; ignored.", obj);
                return;
            }

            _active.Remove(obj);

            if (obj.TryGetComponent(out IPoolable poolable))
            {
                poolable.OnDespawn();
            }

            obj.SetActive(false);
            obj.transform.SetParent(_parent);

            _pool.Enqueue(obj);
            _inPool.Add(obj);
        }

        /// <summary>
        /// Returns every object handed out and not released yet. Objects destroyed meanwhile (for example with
        /// the scene they were parented to) are forgotten.
        /// </summary>
        public void ReleaseAll()
        {
            if (_active.Count == 0)
                return;

            _releaseBuffer.Clear();
            _releaseBuffer.AddRange(_active);
            _active.Clear();

            foreach (GameObject obj in _releaseBuffer)
            {
                if (obj != null)
                    Release(obj);
            }

            _releaseBuffer.Clear();
        }

        /// <summary>
        /// True when <paramref name="obj"/> was handed out by this pool and is not released yet.
        /// </summary>
        public bool IsActive(GameObject obj)
        {
            return obj != null && _active.Contains(obj);
        }

        // Dequeues a pooled object, skipping objects destroyed while they were in the pool.
        private GameObject Take()
        {
            GameObject obj = null;

            while (obj == null)
            {
                if (_pool.Count == 0)
                    CreateObject();

                obj = _pool.Dequeue();
                _inPool.Remove(obj);
            }

            _active.Add(obj);
            return obj;
        }

        private static void Activate(GameObject obj)
        {
            obj.SetActive(true);

            if (obj.TryGetComponent(out IPoolable poolable))
            {
                poolable.OnSpawn();
            }
        }

        /// <summary>
        /// Creates a new pooled object.
        /// </summary>
        private void CreateObject()
        {
            GameObject obj = Object.Instantiate(_prefab, _parent);

            obj.SetActive(false);

            _pool.Enqueue(obj);
            _inPool.Add(obj);
        }
    }
}
