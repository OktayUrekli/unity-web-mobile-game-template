using System.Collections.Generic;
using System.Text.RegularExpressions;
using _Core.Save;
using _Core.Tests.Fakes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace _Core.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="SaveManager"/> and its <see cref="SaveCache"/>.
    /// The manager is added to a hidden GameObject (Awake does not run in Edit Mode, so the singleton
    /// Instance is untouched) and initialized through the internal Init(storage, config) seam with a fake
    /// storage. Update never runs, so debounced writes reach storage only through an explicit Flush.
    /// </summary>
    public class SaveManagerTests
    {
        private const string Key = "test_key";
        private const string NotInitializedError = "SaveManager: used before Init(). Start Play Mode from the bootstrap scene.";

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private FakeSaveStorage _storage;
        private FakeAsyncSaveStorage _asyncStorage;

        [SetUp]
        public void SetUp()
        {
            _storage = new FakeSaveStorage();
            _asyncStorage = null;
        }

        [TearDown]
        public void TearDown()
        {
            _asyncStorage?.CompleteAllWrites();

            foreach (UnityEngine.Object created in _createdObjects)
            {
                if (created != null)
                    UnityEngine.Object.DestroyImmediate(created);
            }

            _createdObjects.Clear();
        }

        // ---------- SaveManager ----------

        [Test]
        public void Save_ThenLoad_RoundTripsAllFields()
        {
            SaveManager manager = CreateManager(_storage, CreateConfig());
            TestSaveData saved = CreateSampleData();

            manager.Save(Key, saved);
            bool found = manager.Load(Key, out TestSaveData loaded);

            Assert.IsTrue(found);
            AssertSameData(saved, loaded);
            Assert.AreNotSame(saved, loaded, "Load must return a new object, not the saved instance.");
        }

        [Test]
        public void Save_Flush_ThenLoadInNewManager_RoundTripsThroughStorage()
        {
            SaveManager writer = CreateManager(_storage, CreateConfig());
            TestSaveData saved = CreateSampleData();

            writer.Save(Key, saved);
            writer.Flush();

            Assert.IsTrue(_storage.Values.ContainsKey(Key));

            SaveManager reader = CreateManager(_storage, CreateConfig());
            Assert.IsTrue(reader.HasSave(Key));
            Assert.IsTrue(reader.Load(Key, out TestSaveData loaded));
            AssertSameData(saved, loaded);
        }

        [Test]
        public void Save_IsDebounced_UntilFlush()
        {
            SaveManager manager = CreateManager(_storage, CreateConfig(flushDelaySeconds: 1f));

            manager.Save(Key, CreateSampleData());

            Assert.AreEqual(0, _storage.SaveCount, "With a flush delay the write must wait for a flush.");
            Assert.IsTrue(manager.HasPendingChanges);

            manager.Flush();

            Assert.AreEqual(1, _storage.SaveCount);
            Assert.IsFalse(manager.HasPendingChanges);
        }

        [Test]
        public void Save_WithZeroFlushDelay_WritesImmediately()
        {
            SaveManager manager = CreateManager(_storage, CreateConfig(flushDelaySeconds: 0f));

            manager.Save(Key, CreateSampleData());

            Assert.AreEqual(1, _storage.SaveCount);
            Assert.IsFalse(manager.HasPendingChanges);
        }

        [Test]
        public void Save_SameKeyTwiceBeforeFlush_WritesLatestValueOnce()
        {
            SaveManager manager = CreateManager(_storage, CreateConfig());

            manager.Save(Key, new TestSaveData { level = 1 });
            manager.Save(Key, new TestSaveData { level = 2 });
            manager.Flush();

            Assert.AreEqual(1, _storage.SaveCount);
            Assert.AreEqual(2, JsonUtility.FromJson<TestSaveData>(_storage.Values[Key]).level);
        }

        [Test]
        public void Load_MissingKey_ReturnsFalseAndFreshData()
        {
            SaveManager manager = CreateManager(_storage, CreateConfig());

            bool found = manager.Load(Key, out TestSaveData data);

            Assert.IsFalse(found);
            AssertIsFresh(data);
            Assert.IsFalse(manager.HasSave(Key));
        }

        [Test]
        public void Load_CorruptJson_ReturnsFalseAndFreshData()
        {
            _storage.Values[Key] = "{this is not json";
            SaveManager manager = CreateManager(_storage, CreateConfig());

            LogAssert.Expect(LogType.Warning, new Regex($"SaveManager: stored data for '{Key}' is corrupt"));
            bool found = manager.Load(Key, out TestSaveData data);

            Assert.IsFalse(found);
            AssertIsFresh(data);
        }

        [Test]
        public void Load_EmptyStoredString_IsTreatedAsNoSave()
        {
            _storage.Values[Key] = "";
            SaveManager manager = CreateManager(_storage, CreateConfig());

            Assert.IsFalse(manager.HasSave(Key));
            Assert.IsFalse(manager.Load(Key, out TestSaveData data));
            AssertIsFresh(data);
        }

        [Test]
        public void Load_OlderOrNewerJson_KeepsDefaultsForMissingFieldsAndIgnoresUnknownOnes()
        {
            // An older save without playerName/soundEnabled and with a field that no longer exists.
            _storage.Values[Key] = "{\"level\":7,\"removedField\":123}";
            SaveManager manager = CreateManager(_storage, CreateConfig());

            Assert.IsTrue(manager.Load(Key, out TestSaveData data));
            Assert.AreEqual(7, data.level);
            Assert.AreEqual(TestSaveData.DefaultPlayerName, data.playerName);
            Assert.IsTrue(data.soundEnabled);
        }

        [Test]
        public void Load_ReadsStorageOnlyOncePerKey()
        {
            _storage.Values[Key] = JsonUtility.ToJson(CreateSampleData());
            SaveManager manager = CreateManager(_storage, CreateConfig());

            manager.Load(Key, out TestSaveData _);
            manager.Load(Key, out TestSaveData _);
            manager.HasSave(Key);

            Assert.AreEqual(1, _storage.LoadCount);
        }

        [Test]
        public void Delete_RemovesDataNow_AndFromStorageOnFlush()
        {
            _storage.Values[Key] = JsonUtility.ToJson(CreateSampleData());
            SaveManager manager = CreateManager(_storage, CreateConfig());

            manager.Delete(Key);

            Assert.IsFalse(manager.HasSave(Key));
            Assert.IsFalse(manager.Load(Key, out TestSaveData data));
            AssertIsFresh(data);
            Assert.IsTrue(_storage.Values.ContainsKey(Key), "The delete must wait for a flush.");

            manager.Flush();

            Assert.AreEqual(1, _storage.DeleteCount);
            Assert.IsFalse(_storage.Values.ContainsKey(Key));
        }

        [Test]
        public void Save_NullData_LogsWarningAndStoresNothing()
        {
            SaveManager manager = CreateManager(_storage, CreateConfig());

            LogAssert.Expect(LogType.Warning, new Regex("tried to save null data"));
            manager.Save<TestSaveData>(Key, null);

            Assert.IsFalse(manager.HasPendingChanges);
            Assert.IsFalse(manager.HasSave(Key));
        }

        [Test]
        public void UseBeforeInit_LogsErrorAndReturnsFreshData()
        {
            SaveManager manager = CreateUninitializedManager();

            Assert.IsFalse(manager.IsInitialized);

            LogAssert.Expect(LogType.Error, NotInitializedError);
            bool found = manager.Load(Key, out TestSaveData data);
            Assert.IsFalse(found);
            AssertIsFresh(data);

            LogAssert.Expect(LogType.Error, NotInitializedError);
            manager.Save(Key, CreateSampleData());
            Assert.IsFalse(manager.HasPendingChanges);

            // Flush before Init is a silent no-op.
            Assert.DoesNotThrow(() => manager.Flush());
        }

        [Test]
        public void Save_OverStorageLimit_LogsErrorOnlyWhenTheLevelRises()
        {
            SaveManager manager = CreateManager(_storage, CreateConfig(storageLimitBytes: 10));

            LogAssert.Expect(LogType.Error, new Regex("over the 10 byte storage limit"));
            manager.Save(Key, CreateSampleData());

            // Still over the limit: no second error (an unexpected error would fail the test).
            manager.Save(Key, CreateSampleData());
        }

        [Test]
        public void Save_NearStorageLimit_LogsWarning()
        {
            // Sample data is well over 10% and under 100% of 2000 bytes.
            SaveManager manager = CreateManager(_storage, CreateConfig(storageLimitBytes: 2000, sizeWarningThreshold: 0.1f));
            TestSaveData data = CreateSampleData();
            data.playerName = new string('x', 300);

            LogAssert.Expect(LogType.Warning, new Regex("close to the 2000 byte storage limit"));
            manager.Save(Key, data);
        }

        [Test]
        public void Flush_StorageThrows_KeepsChangePending_AndRetriesOnNextFlush()
        {
            SaveManager manager = CreateManager(_storage, CreateConfig());
            manager.Save(Key, CreateSampleData());
            _storage.ThrowOnSave = true;

            LogAssert.Expect(LogType.Error, new Regex($"SaveCache: failed to write '{Key}'"));
            LogAssert.Expect(LogType.Exception, new Regex("FakeSaveStorage save failed"));
            manager.Flush();

            Assert.IsTrue(manager.HasPendingChanges);
            Assert.IsFalse(_storage.Values.ContainsKey(Key));

            _storage.ThrowOnSave = false;
            manager.Flush();

            Assert.IsFalse(manager.HasPendingChanges);
            Assert.IsTrue(_storage.Values.ContainsKey(Key));
        }

        [Test]
        public void Flush_StorageThrows_SchedulesRetryWithoutNewSaves()
        {
            SaveManager manager = CreateManager(_storage, CreateConfig());
            manager.Save(Key, CreateSampleData());
            _storage.ThrowOnSave = true;

            LogAssert.Expect(LogType.Error, new Regex($"SaveCache: failed to write '{Key}'"));
            LogAssert.Expect(LogType.Exception, new Regex("FakeSaveStorage save failed"));
            manager.Flush();

            Assert.IsTrue(manager.IsFlushScheduled, "A failed write must schedule a retry even when nothing else saves.");

            _storage.ThrowOnSave = false;
            manager.Flush();

            Assert.IsFalse(manager.IsFlushScheduled);
            Assert.IsFalse(manager.HasPendingChanges);
        }

        [Test]
        public void Load_StorageReadThrows_ReturnsFalseAndFreshData()
        {
            _storage.Values[Key] = JsonUtility.ToJson(CreateSampleData());
            _storage.ThrowOnRead = true;
            SaveManager manager = CreateManager(_storage, CreateConfig());

            ExpectReadFailure();
            bool found = manager.Load(Key, out TestSaveData data);

            Assert.IsFalse(found);
            AssertIsFresh(data);
        }

        [Test]
        public void Save_AfterFailedRead_KeepsStoredDataOnceReadable()
        {
            TestSaveData stored = CreateSampleData();
            string storedJson = JsonUtility.ToJson(stored);
            _storage.Values[Key] = storedJson;
            _storage.ThrowOnRead = true;
            SaveManager manager = CreateManager(_storage, CreateConfig());

            ExpectReadFailure();
            manager.Load(Key, out TestSaveData _);
            manager.Save(Key, new TestSaveData { level = 1 });

            ExpectReadFailure();
            manager.Flush();

            Assert.AreEqual(storedJson, _storage.Values[Key], "A key that could not be read must not be overwritten.");
            Assert.IsTrue(manager.HasPendingChanges);
            Assert.IsTrue(manager.IsFlushScheduled, "The held-back change must be retried.");

            _storage.ThrowOnRead = false;
            manager.Flush();

            Assert.AreEqual(storedJson, _storage.Values[Key], "Saved data this session never saw wins over the change.");
            Assert.AreEqual(0, _storage.SaveCount);
            Assert.IsFalse(manager.HasPendingChanges);
            Assert.IsTrue(manager.Load(Key, out TestSaveData loaded));
            AssertSameData(stored, loaded);
        }

        [Test]
        public void Save_AfterFailedRead_IsWrittenWhenNothingIsStored()
        {
            _storage.ThrowOnRead = true;
            SaveManager manager = CreateManager(_storage, CreateConfig());

            ExpectReadFailure();
            manager.Load(Key, out TestSaveData _);
            TestSaveData saved = CreateSampleData();
            manager.Save(Key, saved);

            ExpectReadFailure();
            manager.Flush();
            Assert.IsFalse(_storage.Values.ContainsKey(Key));

            _storage.ThrowOnRead = false;
            manager.Flush();

            Assert.IsTrue(_storage.Values.ContainsKey(Key));
            Assert.IsFalse(manager.HasPendingChanges);
            AssertSameData(saved, JsonUtility.FromJson<TestSaveData>(_storage.Values[Key]));
        }

        // ---------- Versioning ----------

        [Test]
        public void Save_WritesCurrentSaveVersion()
        {
            SaveManager manager = CreateManager(_storage, CreateConfig());

            manager.Save(Key, new VersionedTestSaveData { score = 3 });
            manager.Flush();

            VersionedTestSaveData stored = JsonUtility.FromJson<VersionedTestSaveData>(_storage.Values[Key]);
            Assert.AreEqual(2, stored.saveVersion);
            Assert.AreEqual(3, stored.score);
        }

        [Test]
        public void Load_OlderVersion_MigratesAndUpgradesVersion()
        {
            _storage.Values[Key] = "{\"saveVersion\":1,\"points\":40}";
            SaveManager manager = CreateManager(_storage, CreateConfig());

            Assert.IsTrue(manager.Load(Key, out VersionedTestSaveData data));

            Assert.AreEqual(1, data.MigratedFromVersion);
            Assert.AreEqual(40, data.score);
            Assert.AreEqual(0, data.points);
            Assert.AreEqual(2, data.saveVersion);
        }

        [Test]
        public void Load_SaveWithoutVersion_MigratesFromZero()
        {
            _storage.Values[Key] = "{\"points\":5}";
            SaveManager manager = CreateManager(_storage, CreateConfig());

            Assert.IsTrue(manager.Load(Key, out VersionedTestSaveData data));

            Assert.AreEqual(0, data.MigratedFromVersion);
            Assert.AreEqual(5, data.score);
            Assert.AreEqual(2, data.saveVersion);
        }

        [Test]
        public void Load_CurrentVersion_DoesNotMigrate()
        {
            _storage.Values[Key] = "{\"saveVersion\":2,\"score\":9}";
            SaveManager manager = CreateManager(_storage, CreateConfig());

            Assert.IsTrue(manager.Load(Key, out VersionedTestSaveData data));

            Assert.AreEqual(-1, data.MigratedFromVersion);
            Assert.AreEqual(9, data.score);
        }

        [Test]
        public void Load_NewerVersion_LogsWarningAndKeepsData()
        {
            _storage.Values[Key] = "{\"saveVersion\":3,\"score\":9}";
            SaveManager manager = CreateManager(_storage, CreateConfig());

            LogAssert.Expect(LogType.Warning, new Regex("saved with version 3, newer than this build's 2"));
            Assert.IsTrue(manager.Load(Key, out VersionedTestSaveData data));

            Assert.AreEqual(-1, data.MigratedFromVersion);
            Assert.AreEqual(9, data.score);
        }

        // ---------- SaveCache ----------

        [Test]
        public void SaveCache_TotalBytes_TracksSetAndRemove()
        {
            var cache = new SaveCache(_storage);

            cache.Set("a", "1234");
            Assert.AreEqual(5, cache.TotalBytes, "Key and value bytes are both counted.");

            cache.Set("a", "12");
            Assert.AreEqual(3, cache.TotalBytes, "Overwriting replaces the old size.");

            cache.Remove("a");
            Assert.AreEqual(0, cache.TotalBytes);
        }

        [Test]
        public void SaveCache_AsyncWriteInFlight_KeepsNewChangeDirty()
        {
            _asyncStorage = new FakeAsyncSaveStorage();
            var cache = new SaveCache(_asyncStorage);

            cache.Set(Key, "first");
            cache.Flush();

            Assert.AreEqual(1, _asyncStorage.SaveAsyncCount);
            Assert.IsTrue(cache.HasWritesInFlight);
            Assert.IsFalse(cache.HasPendingChanges);

            cache.Set(Key, "second");
            cache.Flush();

            Assert.AreEqual(1, _asyncStorage.SaveAsyncCount, "A key whose write is in flight must not be written again yet.");
            Assert.IsTrue(cache.HasPendingChanges);
        }

        // ---------- Helpers ----------

        private static void ExpectReadFailure()
        {
            LogAssert.Expect(LogType.Error, new Regex($"SaveCache: failed to read '{Key}'"));
            LogAssert.Expect(LogType.Exception, new Regex("FakeSaveStorage read failed"));
        }

        private SaveManager CreateManager(FakeSaveStorage storage, SaveConfig config)
        {
            SaveManager manager = CreateUninitializedManager();
            manager.Init(storage, config);
            return manager;
        }

        private SaveManager CreateUninitializedManager()
        {
            var gameObject = new GameObject(nameof(SaveManagerTests)) { hideFlags = HideFlags.HideAndDontSave };
            _createdObjects.Add(gameObject);
            return gameObject.AddComponent<SaveManager>();
        }

        private SaveConfig CreateConfig(
            float flushDelaySeconds = 1f,
            int storageLimitBytes = 1024 * 1024,
            float sizeWarningThreshold = 0.8f)
        {
            var config = ScriptableObject.CreateInstance<SaveConfig>();
            config.hideFlags = HideFlags.HideAndDontSave;
            _createdObjects.Add(config);

            var serializedConfig = new SerializedObject(config);
            SetFloat(serializedConfig, "flushDelaySeconds", flushDelaySeconds);
            SetFloat(serializedConfig, "maxFlushDelaySeconds", Mathf.Max(5f, flushDelaySeconds));
            SetFloat(serializedConfig, "sizeWarningThreshold", sizeWarningThreshold);

            SerializedProperty limit = serializedConfig.FindProperty("storageLimitBytes");
            Assert.IsNotNull(limit, "SaveConfig.storageLimitBytes was renamed; update SaveManagerTests.");
            limit.intValue = storageLimitBytes;

            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.IsNotNull(property, $"SaveConfig.{propertyName} was renamed; update SaveManagerTests.");
            property.floatValue = value;
        }

        private static TestSaveData CreateSampleData()
        {
            return new TestSaveData
            {
                level = 12,
                playerName = "Player One",
                soundEnabled = false,
                unlockedLevels = new List<int> { 1, 2, 3 }
            };
        }

        private static void AssertSameData(TestSaveData expected, TestSaveData actual)
        {
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.level, actual.level);
            Assert.AreEqual(expected.playerName, actual.playerName);
            Assert.AreEqual(expected.soundEnabled, actual.soundEnabled);
            CollectionAssert.AreEqual(expected.unlockedLevels, actual.unlockedLevels);
        }

        private static void AssertIsFresh(TestSaveData data)
        {
            Assert.IsNotNull(data, "Load must always return a usable object.");
            Assert.AreEqual(0, data.level);
            Assert.AreEqual(TestSaveData.DefaultPlayerName, data.playerName);
            Assert.IsTrue(data.soundEnabled);
            CollectionAssert.IsEmpty(data.unlockedLevels);
        }
    }
}
