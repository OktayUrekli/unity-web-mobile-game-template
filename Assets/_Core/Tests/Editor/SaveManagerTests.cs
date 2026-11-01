using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using _Core.Platform.Services.Save;
using _Core.Save;
using _Core.Save.Local;
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
    /// Each test runs without a SynchronizationContext, so the cache's await continuations run inside the fake
    /// storage's CompleteWrite, FailWrite and CompleteLoads, and a test can check what happens after a write or read
    /// ends.
    /// </summary>
    public class SaveManagerTests
    {
        private const string Key = "test_key";
        private const string NotInitializedError = "SaveManager: used before Init(). Start Play Mode from the bootstrap scene.";

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private FakeSaveStorage _storage;
        private FakeAsyncSaveStorage _asyncStorage;
        private SynchronizationContext _previousSyncContext;

        [SetUp]
        public void SetUp()
        {
            _storage = new FakeSaveStorage();
            _asyncStorage = null;

            // See the class summary; TearDown restores the context.
            _previousSyncContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                _asyncStorage?.CompleteLoads();
                _asyncStorage?.CompleteAllWrites();

                foreach (UnityEngine.Object created in _createdObjects)
                {
                    if (created != null)
                        UnityEngine.Object.DestroyImmediate(created);
                }

                _createdObjects.Clear();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(_previousSyncContext);
            }
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
        public void Save_OverSaveConfigLimit_LogsErrorOnceButStillSaves()
        {
            SaveManager manager = CreateManager(_storage, CreateConfig(storageLimitBytes: 10));

            LogAssert.Expect(LogType.Error, new Regex("over the 10 byte storage limit"));
            manager.Save(Key, CreateSampleData());

            // Still over the limit: no second error (an unexpected error would fail the test).
            manager.Save(Key, CreateSampleData());

            // SaveConfig's budget warns but never refuses a save.
            Assert.IsTrue(manager.HasPendingChanges);
            manager.Flush();
            Assert.AreEqual(1, _storage.SaveCount);
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
        public void Save_PastTheStoragesOwnLimit_IsRefusedAndKeepsTheLastSavedData()
        {
            _storage.LimitBytes = 200;
            SaveManager manager = CreateManager(_storage, CreateConfig());

            // About 110 bytes.
            manager.Save(Key, new TestSaveData { level = 1 });
            manager.Flush();
            Assert.AreEqual(1, _storage.SaveCount);

            var tooLarge = new TestSaveData { level = 1, playerName = new string('x', 300) };

            LogAssert.Expect(LogType.Error, new Regex($"SaveManager: '{Key}' was not saved"));
            manager.Save(Key, tooLarge);

            Assert.IsFalse(manager.HasPendingChanges, "A refused save must not reach storage.");
            Assert.IsTrue(manager.Load(Key, out TestSaveData loaded));
            Assert.AreEqual(1, loaded.level);
            Assert.AreEqual(TestSaveData.DefaultPlayerName, loaded.playerName, "The last accepted save is kept.");

            // Refused again: no second error (an unexpected error would fail the test).
            manager.Save(Key, tooLarge);

            manager.Flush();
            Assert.AreEqual(1, _storage.SaveCount);
        }

        [Test]
        public void Save_ThatShrinksTheData_IsTakenEvenPastTheStoragesLimit()
        {
            _storage.Values[Key] = JsonUtility.ToJson(new TestSaveData { playerName = new string('x', 300) });
            _storage.LimitBytes = 200;
            SaveManager manager = CreateManager(_storage, CreateConfig());

            // Read, so the stored data is counted.
            manager.Load(Key, out TestSaveData _);

            LogAssert.Expect(LogType.Error, new Regex("over the 200 byte storage limit"));
            manager.Save(Key, new TestSaveData { playerName = new string('x', 250) });

            Assert.IsTrue(manager.HasPendingChanges, "A save that does not grow the data is always taken.");
            manager.Flush();
            Assert.AreEqual(1, _storage.SaveCount);
        }

        [TestCase(300, 1024 * 1024)]
        [TestCase(1024 * 1024, 300)]
        public void SizeWarnings_UseTheLowerOfTheStorageLimitAndSaveConfig(int storageLimitBytes, int saveBudgetBytes)
        {
            _storage.LimitBytes = storageLimitBytes;
            SaveManager manager = CreateManager(
                _storage, CreateConfig(storageLimitBytes: saveBudgetBytes, sizeWarningThreshold: 0.5f));

            // About 200 bytes: over half of 300, under 300.
            LogAssert.Expect(LogType.Warning, new Regex("close to the 300 byte storage limit"));
            manager.Save(Key, new TestSaveData { playerName = new string('x', 100) });

            Assert.IsTrue(manager.HasPendingChanges, "A save under both limits is taken.");
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

        [Test]
        public void OnPageHidden_WritesAKeyWhoseEarlierWriteIsStillRunning()
        {
            _asyncStorage = new FakeAsyncSaveStorage();
            SaveManager manager = CreateManager(_asyncStorage, CreateConfig());

            manager.Save(Key, new TestSaveData { level = 1 });
            manager.Flush();
            Assert.AreEqual(1, _asyncStorage.SaveAsyncCount);

            manager.Save(Key, new TestSaveData { level = 2 });
            manager.Flush();
            Assert.AreEqual(1, _asyncStorage.SaveAsyncCount, "A normal flush waits for the running write.");
            Assert.IsTrue(manager.HasPendingChanges);

            manager.OnPageHidden();

            Assert.AreEqual(2, _asyncStorage.SaveAsyncCount, "The page may close before a later flush.");
            Assert.AreEqual(2, JsonUtility.FromJson<TestSaveData>(_asyncStorage.SavedData[1]).level);
            Assert.IsTrue(manager.HasPendingChanges, "The key is written once more after its writes end.");
            Assert.IsTrue(manager.IsFlushScheduled, "That last write must be scheduled.");
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

        [TestCase("k", null, 0)]
        [TestCase("k", "", 1)]
        [TestCase("k", "abc", 4)]
        [TestCase("k", "\"", 3)]
        [TestCase("k", "\\", 3)]
        [TestCase("k", "\n", 3)]
        [TestCase("k", "\u0001", 7)]
        [TestCase("k", "\u00e7", 3)]
        [TestCase("k", "\u20ac", 4)]
        [TestCase("k", "\U0001F600", 5)]
        [TestCase("\"", "x", 3)]
        public void SaveCache_MeasureBytes_CountsUtf8AndJsonEscapes(string key, string value, int expected)
        {
            Assert.AreEqual(expected, SaveCache.MeasureBytes(key, value));
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

        [Test]
        public void SaveCache_FlushIncludingKeysInFlight_WritesTheNewestValueAtOnceAndKeepsItPending()
        {
            _asyncStorage = new FakeAsyncSaveStorage();
            var cache = new SaveCache(_asyncStorage);

            cache.Set(Key, "first");
            cache.Flush();
            Assert.AreEqual(1, _asyncStorage.SaveAsyncCount);

            cache.Set(Key, "second");
            cache.Flush();
            Assert.AreEqual(1, _asyncStorage.SaveAsyncCount);
            Assert.IsTrue(cache.HasPendingChanges);

            cache.Flush(true);

            Assert.AreEqual(2, _asyncStorage.SaveAsyncCount);
            Assert.AreEqual("second", _asyncStorage.SavedData[1]);
            Assert.AreEqual(2, _asyncStorage.PendingWriteCount);
            Assert.IsTrue(cache.HasWritesInFlight);
            Assert.IsTrue(cache.HasPendingChanges, "The key must be written once more after both writes end.");
        }

        [Test]
        public void SaveCache_AfterAPageHideWrite_WritesTheKeyOnceMoreWhenItsWritesEnd()
        {
            SaveCache cache = CreateCacheWithOverlappingWrites();

            // The newer write lands first.
            _asyncStorage.CompleteWrite(1);
            Assert.AreEqual("second", _asyncStorage.Values[Key]);
            Assert.IsTrue(cache.HasWritesInFlight, "The older write is still running.");

            cache.Flush();
            Assert.AreEqual(2, _asyncStorage.SaveAsyncCount, "No write while an earlier one still runs.");

            // The older write lands last and leaves the older value in storage.
            _asyncStorage.CompleteWrite(0);
            Assert.AreEqual("first", _asyncStorage.Values[Key]);
            Assert.IsFalse(cache.HasWritesInFlight);
            Assert.IsTrue(cache.HasPendingChanges);

            cache.Flush();

            Assert.AreEqual(3, _asyncStorage.SaveAsyncCount);
            Assert.AreEqual("second", _asyncStorage.SavedData[2]);
            Assert.IsFalse(cache.HasPendingChanges);

            _asyncStorage.CompleteWrite(0);
            Assert.AreEqual("second", _asyncStorage.Values[Key], "The newest value must land last.");
        }

        [Test]
        public void SaveCache_OlderWriteFailingAfterAPageHideWrite_IsRetriedWithTheNewestValue()
        {
            SaveCache cache = CreateCacheWithOverlappingWrites();
            int failures = 0;
            cache.WriteFailed += _ => failures++;

            LogAssert.Expect(LogType.Error, new Regex($"SaveCache: failed to write '{Key}'"));
            LogAssert.Expect(LogType.Exception, new Regex("FakeAsyncSaveStorage write failed"));
            _asyncStorage.FailWrite(0);

            Assert.AreEqual(1, failures);
            Assert.IsTrue(cache.HasPendingChanges);
            Assert.IsTrue(cache.HasWritesInFlight, "The newer write is still running.");

            cache.Flush();
            Assert.AreEqual(2, _asyncStorage.SaveAsyncCount);

            _asyncStorage.CompleteWrite(0);
            Assert.IsFalse(cache.HasWritesInFlight);

            cache.Flush();

            Assert.AreEqual(3, _asyncStorage.SaveAsyncCount);
            Assert.AreEqual("second", _asyncStorage.SavedData[2]);
        }

        [Test]
        public void SaveCache_FlushIncludingKeysInFlight_NeverWritesAKeyBeingReadAgain()
        {
            _asyncStorage = new FakeAsyncSaveStorage { ThrowOnLoad = true };
            var cache = new SaveCache(_asyncStorage);

            LogAssert.Expect(LogType.Error, new Regex($"SaveCache: failed to preload '{Key}'"));
            LogAssert.Expect(LogType.Exception, new Regex("FakeAsyncSaveStorage load failed"));
            Assert.IsTrue(cache.PreloadAsync(new[] { Key }).IsCompleted);

            _asyncStorage.ThrowOnLoad = false;
            _asyncStorage.HoldLoads = true;
            cache.Set(Key, "mine");
            cache.Flush();

            Assert.IsTrue(cache.HasWritesInFlight, "The key is read again before it is written.");
            Assert.AreEqual(0, _asyncStorage.SaveAsyncCount);

            cache.Flush(true);
            Assert.AreEqual(0, _asyncStorage.SaveAsyncCount, "A key being read again must not be written early.");

            // Nothing is stored, so the change is written.
            _asyncStorage.HoldLoads = false;
            _asyncStorage.CompleteLoads();

            Assert.AreEqual(1, _asyncStorage.SaveAsyncCount);
            Assert.AreEqual("mine", _asyncStorage.SavedData[0]);
            Assert.IsFalse(cache.HasPendingChanges);
        }

        // ---------- PlayerPrefsSaveStorage ----------

        [Test]
        public void PlayerPrefsSaveStorage_LimitBytes_IsTheLowerOfItsOwnAndTheGivenLimit()
        {
            Assert.AreEqual(200000, new PlayerPrefsSaveStorage(200000).LimitBytes);
#if UNITY_WEBGL
            Assert.AreEqual(1000000, new PlayerPrefsSaveStorage().LimitBytes, "1 MB of PlayerPrefs on the web.");
            Assert.AreEqual(1000000, new PlayerPrefsSaveStorage(5000000).LimitBytes);
#else
            Assert.AreEqual(0, new PlayerPrefsSaveStorage().LimitBytes, "PlayerPrefs have no limit outside WebGL.");
            Assert.AreEqual(5000000, new PlayerPrefsSaveStorage(5000000).LimitBytes);
#endif
        }

        // ---------- Helpers ----------

        private static void ExpectReadFailure()
        {
            LogAssert.Expect(LogType.Error, new Regex($"SaveCache: failed to read '{Key}'"));
            LogAssert.Expect(LogType.Exception, new Regex("FakeSaveStorage read failed"));
        }

        private SaveManager CreateManager(ISaveStorage storage, SaveConfig config)
        {
            SaveManager manager = CreateUninitializedManager();
            manager.Init(storage, config);
            return manager;
        }

        // A cache over a new async fake whose key has two writes running: "first" from a normal flush, then "second"
        // from a page-hide flush made while the first was still running.
        private SaveCache CreateCacheWithOverlappingWrites()
        {
            _asyncStorage = new FakeAsyncSaveStorage();
            var cache = new SaveCache(_asyncStorage);

            cache.Set(Key, "first");
            cache.Flush();
            cache.Set(Key, "second");
            cache.Flush(true);

            Assert.AreEqual(2, _asyncStorage.PendingWriteCount);
            return cache;
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
