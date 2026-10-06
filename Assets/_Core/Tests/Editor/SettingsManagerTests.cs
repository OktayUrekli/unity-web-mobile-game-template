using System;
using System.Collections.Generic;
using _Core.Events;
using _Core.Events.Settings;
using _Core.Save;
using _Core.Settings;
using _Core.Tests.Fakes;
using NUnit.Framework;
using UnityEngine;

namespace _Core.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="SettingsManager"/>. Awake does not run in Edit Mode, so the managers it reaches
    /// through <c>Instance</c> are registered with the internal <c>Singleton.SetInstanceForTests</c> seam and restored
    /// in TearDown: a <see cref="SaveManager"/> over a <see cref="FakeSaveStorage"/>. Update never runs, so storage is
    /// written only by Commit or an explicit Flush.
    /// </summary>
    public class SettingsManagerTests
    {
        // SettingsManager's storage key. Changing it would orphan every player's saved settings.
        private const string SettingsKey = "settings";

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<string> _languageEvents = new();
        private FakeSaveStorage _storage;
        private SaveManager _previousSaveManager;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _languageEvents.Clear();
            _storage = new FakeSaveStorage();

            _previousSaveManager = SaveManager.Instance;

            StartSession();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
            EventBus.Clear();

            SaveManager.SetInstanceForTests(_previousSaveManager);

            foreach (UnityEngine.Object created in _createdObjects)
            {
                if (created != null)
                    UnityEngine.Object.DestroyImmediate(created);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void Init_WithNoSave_KeepsDefaultsAndWritesNothing()
        {
            SettingsManager settings = CreateInitializedSettings();
            var defaults = new SettingsData();

            Assert.AreEqual(defaults.musicEnabled, settings.IsMusicEnabled);
            Assert.AreEqual(defaults.sfxEnabled, settings.IsSfxEnabled);
            Assert.AreEqual(defaults.vibrationEnabled, settings.IsVibrationEnabled);
            Assert.AreEqual(defaults.language, settings.Language);
            Assert.AreEqual(defaults.isFirstLaunch, settings.IsFirstLaunch);

            Assert.IsFalse(SaveManager.Instance.HasPendingChanges, "Loading defaults must not queue a write.");
            SaveManager.Instance.Flush();
            Assert.AreEqual(0, _storage.SaveCount, "Defaults must never overwrite stored settings.");
        }

        [Test]
        public void Init_WithStoredSettings_RestoresEveryValue()
        {
            _storage.Values[SettingsKey] = JsonUtility.ToJson(new SettingsData
            {
                musicEnabled = false,
                sfxEnabled = false,
                vibrationEnabled = false,
                language = "tr",
                isFirstLaunch = false
            });

            SettingsManager settings = CreateInitializedSettings();

            Assert.IsFalse(settings.IsMusicEnabled);
            Assert.IsFalse(settings.IsSfxEnabled);
            Assert.IsFalse(settings.IsVibrationEnabled);
            Assert.AreEqual("tr", settings.Language);
            Assert.IsFalse(settings.IsFirstLaunch);

            SaveManager.Instance.Flush();
            Assert.AreEqual(0, _storage.SaveCount, "Loading must not write the settings back.");
        }

        [Test]
        public void SetLanguage_PublishesLanguageChangedEvent_OnlyWhenTheLanguageChanges()
        {
            SettingsManager settings = CreateInitializedSettings();
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);

            settings.SetLanguage("tr");
            settings.Commit();

            // The same language, empty and null are ignored: no event and nothing new to write.
            settings.SetLanguage("tr");
            settings.SetLanguage("");
            settings.SetLanguage(null);
            settings.Commit();

            CollectionAssert.AreEqual(new[] { "tr" }, _languageEvents);
            Assert.AreEqual("tr", settings.Language);
            Assert.AreEqual(1, _storage.SaveCount);

            settings.SetLanguage("en");

            CollectionAssert.AreEqual(new[] { "tr", "en" }, _languageEvents);
            Assert.AreEqual("en", settings.Language);
        }

        [Test]
        public void AudioSettings_ArePublishedOnInit_AndOnEveryChange()
        {
            var events = new List<AudioSettingsChangedEvent>();
            Action<AudioSettingsChangedEvent> handler = events.Add;
            EventBus.Subscribe(handler);

            try
            {
                SettingsManager settings = CreateInitializedSettings();
                settings.SetMusicEnabled(false);
                settings.SetSfxEnabled(false);

                Assert.AreEqual(3, events.Count);
                Assert.IsTrue(events[0].IsMusicEnabled && events[0].IsSfxEnabled, "Init publishes the loaded values.");
                Assert.IsTrue(!events[1].IsMusicEnabled && events[1].IsSfxEnabled);
                Assert.IsTrue(!events[2].IsMusicEnabled && !events[2].IsSfxEnabled);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public void SetVibration_IsWrittenOnCommit_AndRestoredInTheNextSession()
        {
            SettingsManager settings = CreateInitializedSettings();

            settings.SetVibrationEnabled(false);
            settings.Commit();

            Assert.AreEqual(1, _storage.SaveCount);
            Assert.IsTrue(_storage.Values.TryGetValue(SettingsKey, out string json), $"Nothing stored under '{SettingsKey}'.");
            Assert.IsFalse(JsonUtility.FromJson<SettingsData>(json).vibrationEnabled);

            StartSession();
            SettingsManager nextSession = CreateInitializedSettings();

            Assert.IsFalse(nextSession.IsVibrationEnabled);
        }

        [Test]
        public void Setters_AfterCommit_AreRestoredInTheNextSession()
        {
            SettingsManager settings = CreateInitializedSettings();

            settings.SetMusicEnabled(false);
            settings.SetSfxEnabled(false);
            settings.SetLanguage("de");
            settings.CompleteFirstLaunch();
            settings.Commit();

            StartSession();
            SettingsManager nextSession = CreateInitializedSettings();

            Assert.IsFalse(nextSession.IsMusicEnabled);
            Assert.IsFalse(nextSession.IsSfxEnabled);
            Assert.AreEqual("de", nextSession.Language);
            Assert.IsFalse(nextSession.IsFirstLaunch);
            Assert.AreEqual(new SettingsData().vibrationEnabled, nextSession.IsVibrationEnabled, "Untouched settings keep their defaults.");
        }

        // ---------- Helpers ----------

        // A new SaveManager over the same storage, as after a restart: nothing cached, every read goes to storage.
        private void StartSession()
        {
            SaveManager saveManager = CreateComponent<SaveManager>();
            saveManager.Init(_storage, CreateSaveConfig());
            SaveManager.SetInstanceForTests(saveManager);
        }

        private SettingsManager CreateInitializedSettings()
        {
            SettingsManager settings = CreateComponent<SettingsManager>();
            settings.Init();
            return settings;
        }

        private T CreateComponent<T>() where T : Component
        {
            var gameObject = new GameObject(typeof(T).Name) { hideFlags = HideFlags.HideAndDontSave };
            _createdObjects.Add(gameObject);
            return gameObject.AddComponent<T>();
        }

        private SaveConfig CreateSaveConfig()
        {
            var config = ScriptableObject.CreateInstance<SaveConfig>();
            config.hideFlags = HideFlags.HideAndDontSave;
            _createdObjects.Add(config);
            return config;
        }

        private void OnLanguageChanged(LanguageChangedEvent gameEvent)
        {
            _languageEvents.Add(gameEvent.Language);
        }
    }
}
