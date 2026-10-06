using System.Collections.Generic;
using System.Text.RegularExpressions;
using _Core.Pooling;
using _Core.Tests.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _Core.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="ObjectPool"/>. The "prefab" is a hidden scene GameObject with a
    /// <see cref="FakePoolable"/>; Edit Mode runs no Awake/OnEnable, so only the pool's own callbacks are counted.
    /// Pooled objects live under the pool parent (or another created object) and are destroyed with it.
    /// </summary>
    public class ObjectPoolTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();
        private GameObject _prefab;
        private Transform _parent;

        [SetUp]
        public void SetUp()
        {
            _prefab = CreateGameObject("PoolablePrefab");
            _prefab.AddComponent<FakePoolable>();
            _parent = CreateGameObject("PoolParent").transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object created in _createdObjects)
            {
                if (created != null)
                    UnityEngine.Object.DestroyImmediate(created);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void Constructor_PrewarmsInactiveObjectsUnderTheParent()
        {
            _ = new ObjectPool(_prefab, 3, _parent);

            Assert.AreEqual(3, _parent.childCount);

            foreach (Transform child in _parent)
            {
                Assert.IsFalse(child.gameObject.activeSelf, $"{child.name} should wait inactive in the pool.");
                Assert.AreEqual(0, GetPoolable(child.gameObject).SpawnCount, "Prewarming must not spawn.");
            }
        }

        [Test]
        public void Get_ReturnsThePooledObject_ActivatedBeforeOnSpawn()
        {
            var pool = new ObjectPool(_prefab, 1, _parent);
            GameObject pooled = _parent.GetChild(0).gameObject;

            GameObject spawned = pool.Get();

            Assert.AreEqual(pooled, spawned, "Get must reuse the prewarmed object.");
            Assert.AreEqual(1, _parent.childCount, "Get must not create an object while one is pooled.");
            Assert.IsTrue(spawned.activeSelf);

            FakePoolable poolable = GetPoolable(spawned);
            Assert.AreEqual(1, poolable.SpawnCount);
            Assert.AreEqual(0, poolable.DespawnCount);
            Assert.IsTrue(poolable.WasActiveOnLastSpawn, "OnSpawn must run after the object is activated.");
        }

        [Test]
        public void Release_DeactivatesReparentsAndCallsOnDespawn()
        {
            var pool = new ObjectPool(_prefab, 1, _parent);
            GameObject spawned = pool.Get();
            Transform elsewhere = CreateGameObject("Elsewhere").transform;
            spawned.transform.SetParent(elsewhere);

            pool.Release(spawned);

            Assert.IsFalse(spawned.activeSelf);
            Assert.IsTrue(spawned.transform.parent == _parent, "Release must move the object back under the pool parent.");

            FakePoolable poolable = GetPoolable(spawned);
            Assert.AreEqual(1, poolable.DespawnCount);
            Assert.IsTrue(poolable.WasActiveOnLastDespawn, "OnDespawn must run before the object is deactivated.");
        }

        [Test]
        public void Release_SameObjectTwice_IsIgnoredWithWarning()
        {
            var pool = new ObjectPool(_prefab, 1, _parent);
            GameObject spawned = pool.Get();
            pool.Release(spawned);

            LogAssert.Expect(LogType.Warning, new Regex("released twice"));
            pool.Release(spawned);

            Assert.AreEqual(1, GetPoolable(spawned).DespawnCount, "The ignored release must not call OnDespawn again.");

            // Queued once: the second Get must create a new object instead of handing out the same one twice.
            GameObject first = pool.Get();
            GameObject second = pool.Get();
            Assert.AreEqual(spawned, first);
            Assert.AreNotEqual(spawned, second);
            Assert.AreEqual(2, _parent.childCount);
        }

        [Test]
        public void Get_WhenPoolIsEmpty_CreatesAnotherObject()
        {
            var pool = new ObjectPool(_prefab, 1, _parent);
            GameObject first = pool.Get();

            GameObject second = pool.Get();

            Assert.IsTrue(second != null);
            Assert.AreNotEqual(first, second);
            Assert.IsTrue(second.activeSelf);
            Assert.AreEqual(1, GetPoolable(second).SpawnCount);
            Assert.AreEqual(2, _parent.childCount, "The new object must be created under the pool parent.");
        }

        [Test]
        public void Get_SkipsObjectDestroyedWhilePooled()
        {
            var pool = new ObjectPool(_prefab, 2, _parent);
            GameObject destroyed = _parent.GetChild(0).gameObject;
            GameObject survivor = _parent.GetChild(1).gameObject;

            UnityEngine.Object.DestroyImmediate(destroyed);
            GameObject spawned = pool.Get();

            Assert.IsTrue(spawned != null, "Get must not return a destroyed object.");
            Assert.AreEqual(survivor, spawned);
            Assert.IsTrue(spawned.activeSelf);
        }

        [Test]
        public void Get_WhenEveryPooledObjectWasDestroyed_CreatesNewObject()
        {
            var pool = new ObjectPool(_prefab, 1, _parent);
            UnityEngine.Object.DestroyImmediate(_parent.GetChild(0).gameObject);

            GameObject spawned = pool.Get();

            Assert.IsTrue(spawned != null, "Get must create a new object when every pooled one was destroyed.");
            Assert.IsTrue(spawned.activeSelf);
            Assert.AreEqual(1, _parent.childCount);
        }

        [Test]
        public void ReleaseAll_ReturnsEveryActiveObject_AndSkipsDestroyedOnes()
        {
            var pool = new ObjectPool(_prefab, 0, _parent);
            Transform sceneParent = CreateGameObject("SceneParent").transform;
            GameObject first = pool.Get();
            GameObject second = pool.Get(Vector3.zero, Quaternion.identity, sceneParent);
            GameObject destroyed = pool.Get();
            UnityEngine.Object.DestroyImmediate(destroyed);

            Assert.AreEqual(3, pool.ActiveCount);

            pool.ReleaseAll();

            Assert.AreEqual(0, pool.ActiveCount);
            Assert.IsFalse(first.activeSelf);
            Assert.IsFalse(second.activeSelf);
            Assert.AreEqual(_parent, second.transform.parent, "Released objects go back under the pool parent.");
            Assert.AreEqual(1, GetPoolable(first).DespawnCount);

            // Released objects are reused before new ones are created.
            pool.Get();
            pool.Get();
            Assert.AreEqual(2, _parent.childCount);
        }

        [Test]
        public void Get_WithPlacement_PositionsAndParentsBeforeActivation()
        {
            var pool = new ObjectPool(_prefab, 1, _parent);
            Transform sceneParent = CreateGameObject("SceneParent").transform;
            var position = new Vector3(1f, 2f, 3f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, 90f);

            GameObject spawned = pool.Get(position, rotation, sceneParent);

            Assert.AreEqual(sceneParent, spawned.transform.parent);
            Assert.That(Vector3.Distance(position, spawned.transform.position), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(rotation, spawned.transform.rotation), Is.LessThan(0.01f));
            Assert.IsTrue(spawned.activeSelf);
            Assert.AreEqual(1, GetPoolable(spawned).SpawnCount);
            Assert.IsTrue(pool.IsActive(spawned));
        }

        // ---------- Helpers ----------

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        // TryGetComponent returns a real null when missing; GetComponent returns a fake-null object in the Editor.
        private static FakePoolable GetPoolable(GameObject gameObject)
        {
            Assert.IsTrue(gameObject.TryGetComponent(out FakePoolable poolable), $"{gameObject.name} has no FakePoolable.");
            return poolable;
        }
    }
}
