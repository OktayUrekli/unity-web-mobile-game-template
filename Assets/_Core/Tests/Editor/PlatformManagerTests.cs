using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using _Core.Configuration;
using _Core.Platform.Config;
using _Core.Platform.Core;
using _Core.Platform.Services.Achievement.Null;
using _Core.Platform.Services.Ads;
using _Core.Platform.Services.Ads.Null;
using _Core.Platform.Services.Analytics.Null;
using _Core.Platform.Services.Authentication.Null;
using _Core.Platform.Services.Game.Null;
using _Core.Platform.Services.Leaderboard.Null;
using _Core.Platform.Services.Purchase.Null;
using _Core.Platform.Services.Review.Null;
using _Core.Platform.Services.Share.Null;
using _Core.Platform.Services.User.Null;
using _Core.Save.Local;
using _Core.Tests.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _Core.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="PlatformManager.InitializeAsync"/> and its "never throws" contract.
    /// Fake platforms are registered with <see cref="PlatformFactory"/> and answer with completed or faulted tasks,
    /// so InitializeAsync finishes within the call and its task is checked without awaiting. The manager is added
    /// to a hidden GameObject (Awake does not run in Edit Mode, so the singleton Instance is untouched) and the
    /// platform is selected through a GameConfig created in memory.
    /// </summary>
    public class PlatformManagerTests
    {
        // A value outside PlatformType, so no bridge ever registers it and removing the fake's factory in TearDown
        // leaves nothing missing.
        private const PlatformType FakePlatformType = (PlatformType)100;

        private const string FailureMessage = "FakePlatform init failed";

        private static readonly Regex MissingGameConfigError = new("PlatformManager: ConfigurationManager.GameConfig is null");
        private static readonly Regex MissingPlatformConfigError = new("PlatformManager: GameConfig '.*' has no PlatformConfig");
        private static readonly Regex FailureException = new($"InvalidOperationException: {FailureMessage}");

        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<PlatformType> _registeredPlatforms = new();
        private GameConfig _previousGameConfig;

        [SetUp]
        public void SetUp()
        {
            _previousGameConfig = ConfigurationManager.GameConfig;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (PlatformType platformType in _registeredPlatforms)
                PlatformFactory.Unregister(platformType);

            _registeredPlatforms.Clear();
            ConfigurationManager.Initialize(_previousGameConfig);

            foreach (UnityEngine.Object created in _createdObjects)
            {
                if (created != null)
                    UnityEngine.Object.DestroyImmediate(created);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void InitializeAsync_PlatformRegistersNoServices_FillsEveryServiceWithNullImplementation()
        {
            FakePlatform platform = RegisterFakePlatform(FakePlatformType);
            ConfigurationManager.Initialize(CreateGameConfig(FakePlatformType));
            PlatformManager manager = CreateManager();

            Task task = manager.InitializeAsync();

            AssertCompletedWithoutException(task);
            Assert.AreEqual(1, platform.InitializeCount, "The registered fake platform was not used.");
            Assert.AreEqual(PlatformState.Ready, manager.State);
            Assert.IsTrue(manager.IsReady);
            AssertNullServices(manager);
        }

        [Test]
        public void InitializeAsync_PlatformRegistersNoServices_ClearsCapabilitiesOfNullServices()
        {
            FakePlatform platform = RegisterFakePlatform(FakePlatformType);
            ConfigurationManager.Initialize(CreateGameConfig(FakePlatformType));
            PlatformManager manager = CreateManager();

            AssertCompletedWithoutException(manager.InitializeAsync());

            // The fake claims every capability; each flag whose service fell back to Null must be cleared.
            Assert.AreEqual(1, platform.InitializeCount, "The registered fake platform was not used.");
            AssertAllCapabilitiesCleared(manager.Capabilities);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void InitializeAsync_PlatformFails_FallsBackToNullServicesInFailedState(bool throwSynchronously)
        {
            var failure = new InvalidOperationException(FailureMessage);
            FakePlatform platform = RegisterFakePlatform(FakePlatformType);

            // Registered before the failure, so the fallback has to replace it.
            platform.AdsService = new FakeAdsService();

            if (throwSynchronously)
                platform.ThrowOnInitialize = failure;
            else
                platform.FaultOnInitialize = failure;

            ConfigurationManager.Initialize(CreateGameConfig(FakePlatformType));
            PlatformManager manager = CreateManager();

            LogAssert.Expect(LogType.Exception, FailureException);
            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape($"Platform initialization failed ({FakePlatformType})")));
            Task task = manager.InitializeAsync();

            AssertCompletedWithoutException(task);
            Assert.AreEqual(PlatformState.Failed, manager.State);
            Assert.IsFalse(manager.IsReady);
            AssertNullServices(manager);
            AssertAllCapabilitiesCleared(manager.Capabilities);
        }

        [Test]
        public void InitializeAsync_PlatformRegistersAds_KeepsAdsCapabilitiesAndWrapsThatService()
        {
            var ads = new FakeAdsService();
            FakePlatform platform = RegisterFakePlatform(FakePlatformType);
            platform.AdsService = ads;
            ConfigurationManager.Initialize(CreateGameConfig(FakePlatformType));
            PlatformManager manager = CreateManager();

            AssertCompletedWithoutException(manager.InitializeAsync());

            Assert.AreEqual(PlatformState.Ready, manager.State);
            AssertAdsWrappedInPolicy(manager);
            Assert.AreSame(ads, manager.AdPolicy.Inner, "The policy must wrap the service the platform registered.");
            Assert.IsTrue(manager.Capabilities.SupportsAds);
            Assert.IsTrue(manager.Capabilities.SupportsRewardedAds);
            Assert.IsTrue(manager.Capabilities.SupportsInterstitialAds);
            Assert.IsTrue(manager.Capabilities.SupportsBannerAds);

            // The services the platform left out still fall back and lose their flags.
            Assert.IsInstanceOf<PlayerPrefsSaveStorage>(manager.Storage);
            Assert.IsFalse(manager.Capabilities.SupportsSave);
            Assert.IsFalse(manager.Capabilities.SupportsLeaderboard);
        }

        [Test]
        public void InitializeAsync_MissingGameConfig_CompletesWithNullServicesAndLogsOneError()
        {
            ConfigurationManager.Initialize(null);
            PlatformManager manager = CreateManager();

            // Expected once: a second error from the later CurrentPlatform reads would fail the test.
            LogAssert.Expect(LogType.Error, MissingGameConfigError);
            Task task = manager.InitializeAsync();

            AssertCompletedWithoutException(task);
            Assert.AreEqual(PlatformType.None, manager.CurrentPlatform);
            Assert.AreEqual(PlatformState.Ready, manager.State, "A missing config behaves like PlatformType.None.");
            AssertNullServices(manager);
            AssertAllCapabilitiesCleared(manager.Capabilities);
        }

        [Test]
        public void InitializeAsync_GameConfigWithoutPlatformConfig_CompletesWithNullServicesAndLogsOneError()
        {
            ConfigurationManager.Initialize(CreateGameConfigWithoutPlatformConfig());
            PlatformManager manager = CreateManager();

            LogAssert.Expect(LogType.Error, MissingPlatformConfigError);
            Task task = manager.InitializeAsync();

            AssertCompletedWithoutException(task);
            Assert.AreEqual(PlatformType.None, manager.CurrentPlatform);
            Assert.AreEqual(PlatformState.Ready, manager.State);
            AssertNullServices(manager);
        }

        [Test]
        public void InitializeAsync_MissingGameConfigAndFailingPlatform_StillCompletesWithNullServices()
        {
            // Without a config the platform is None, so the failing fake is registered for None. Its failure reaches
            // the catch block, which reads CurrentPlatform again for its warning (the read that used to throw).
            FakePlatform platform = RegisterFakePlatform(PlatformType.None);
            platform.ThrowOnInitialize = new InvalidOperationException(FailureMessage);
            ConfigurationManager.Initialize(null);
            PlatformManager manager = CreateManager();

            LogAssert.Expect(LogType.Error, MissingGameConfigError);
            LogAssert.Expect(LogType.Exception, FailureException);
            Task task = manager.InitializeAsync();

            AssertCompletedWithoutException(task);
            Assert.AreEqual(1, platform.InitializeCount, "The fake registered for None was not used.");
            Assert.AreEqual(PlatformState.Failed, manager.State);
            AssertNullServices(manager);
            AssertAllCapabilitiesCleared(manager.Capabilities);
        }

        [Test]
        public void InitializeAsync_MissingGameConfigAndPlatformRegistersNothing_LogsTheConfigErrorOnce()
        {
            // Every missing-service warning reads CurrentPlatform again; the config error must still appear once.
            FakePlatform platform = RegisterFakePlatform(PlatformType.None);
            ConfigurationManager.Initialize(null);
            PlatformManager manager = CreateManager();

            LogAssert.Expect(LogType.Error, MissingGameConfigError);
            Task task = manager.InitializeAsync();

            AssertCompletedWithoutException(task);
            Assert.AreEqual(1, platform.InitializeCount, "The fake registered for None was not used.");
            Assert.AreEqual(PlatformState.Ready, manager.State);
            AssertNullServices(manager);
        }

        [Test]
        public void InitializeAsync_WhenAlreadyReady_DoesNotInitializeAgain()
        {
            FakePlatform platform = RegisterFakePlatform(FakePlatformType);
            ConfigurationManager.Initialize(CreateGameConfig(FakePlatformType));
            PlatformManager manager = CreateManager();
            AssertCompletedWithoutException(manager.InitializeAsync());
            IAdsService ads = manager.Ads;

            AssertCompletedWithoutException(manager.InitializeAsync());

            Assert.AreEqual(1, platform.InitializeCount);
            Assert.AreSame(ads, manager.Ads);
            Assert.AreEqual(PlatformState.Ready, manager.State);
        }

        [Test]
        public void CurrentPlatform_ReturnsTheConfiguredPlatform()
        {
            ConfigurationManager.Initialize(CreateGameConfig(FakePlatformType));
            PlatformManager manager = CreateManager();

            Assert.AreEqual(FakePlatformType, manager.CurrentPlatform);
            Assert.IsTrue(manager.IsPlatform(FakePlatformType));
            Assert.IsFalse(manager.IsPlatform(PlatformType.None));
        }

        [Test]
        public void CurrentPlatform_MissingGameConfig_ReturnsNoneAndLogsOneError()
        {
            ConfigurationManager.Initialize(null);
            PlatformManager manager = CreateManager();

            LogAssert.Expect(LogType.Error, MissingGameConfigError);

            Assert.AreEqual(PlatformType.None, manager.CurrentPlatform);
            Assert.AreEqual(PlatformType.None, manager.CurrentPlatform);
            Assert.IsTrue(manager.IsPlatform(PlatformType.None));
        }

        // ---------- Helpers ----------

        private FakePlatform RegisterFakePlatform(PlatformType platformType)
        {
            var platform = new FakePlatform();
            PlatformFactory.Register(platformType, () => platform);
            _registeredPlatforms.Add(platformType);
            return platform;
        }

        private PlatformManager CreateManager()
        {
            var gameObject = new GameObject(nameof(PlatformManagerTests)) { hideFlags = HideFlags.HideAndDontSave };
            _createdObjects.Add(gameObject);
            return gameObject.AddComponent<PlatformManager>();
        }

        private GameConfig CreateGameConfig(PlatformType platformType)
        {
            var platformConfig = ScriptableObject.CreateInstance<PlatformConfig>();
            platformConfig.hideFlags = HideFlags.HideAndDontSave;
            _createdObjects.Add(platformConfig);

            var serializedConfig = new UnityEditor.SerializedObject(platformConfig);
            UnityEditor.SerializedProperty platformProperty = serializedConfig.FindProperty("platform");
            Assert.IsNotNull(platformProperty, "PlatformConfig.platform was renamed; update PlatformManagerTests.");
            platformProperty.intValue = (int)platformType;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(platformType, platformConfig.Platform, "Could not set PlatformConfig.platform.");

            GameConfig gameConfig = CreateGameConfigWithoutPlatformConfig();
            gameConfig.PlatformConfig = platformConfig;
            return gameConfig;
        }

        private GameConfig CreateGameConfigWithoutPlatformConfig()
        {
            var gameConfig = ScriptableObject.CreateInstance<GameConfig>();
            gameConfig.name = "TestGameConfig";
            gameConfig.hideFlags = HideFlags.HideAndDontSave;
            _createdObjects.Add(gameConfig);
            return gameConfig;
        }

        private static void AssertCompletedWithoutException(Task task)
        {
            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status,
                $"InitializeAsync must finish within the call and never throw. {task.Exception}");
        }

        private static void AssertNullServices(PlatformManager manager)
        {
            Assert.IsInstanceOf<NullGameService>(manager.Game);
            Assert.IsInstanceOf<PlayerPrefsSaveStorage>(manager.Storage, "Storage falls back to PlayerPrefs, not NullSaveStorage.");
            Assert.IsInstanceOf<NullUserService>(manager.User);
            Assert.IsInstanceOf<NullAnalyticsService>(manager.Analytics);
            Assert.IsInstanceOf<NullAuthenticationService>(manager.Authentication);
            Assert.IsInstanceOf<NullLeaderboardService>(manager.Leaderboard);
            Assert.IsInstanceOf<NullAchievementService>(manager.Achievement);
            Assert.IsInstanceOf<NullReviewService>(manager.Review);
            Assert.IsInstanceOf<NullShareService>(manager.Share);
            Assert.IsInstanceOf<NullPurchaseService>(manager.Purchases);

            AssertAdsWrappedInPolicy(manager);
            Assert.IsInstanceOf<NullAdsService>(manager.AdPolicy.Inner);
        }

        private static void AssertAdsWrappedInPolicy(PlatformManager manager)
        {
            Assert.IsInstanceOf<AdPolicyService>(manager.Ads, "Ads must always be wrapped in AdPolicyService.");
            Assert.IsNotNull(manager.AdPolicy);
            Assert.AreSame(manager.Ads, manager.AdPolicy);
        }

        private static void AssertAllCapabilitiesCleared(PlatformCapabilities capabilities)
        {
            Assert.IsFalse(capabilities.SupportsAds, nameof(capabilities.SupportsAds));
            Assert.IsFalse(capabilities.SupportsRewardedAds, nameof(capabilities.SupportsRewardedAds));
            Assert.IsFalse(capabilities.SupportsInterstitialAds, nameof(capabilities.SupportsInterstitialAds));
            Assert.IsFalse(capabilities.SupportsBannerAds, nameof(capabilities.SupportsBannerAds));
            Assert.IsFalse(capabilities.SupportsAnalytics, nameof(capabilities.SupportsAnalytics));
            Assert.IsFalse(capabilities.SupportsAuthentication, nameof(capabilities.SupportsAuthentication));
            Assert.IsFalse(capabilities.SupportsLeaderboard, nameof(capabilities.SupportsLeaderboard));
            Assert.IsFalse(capabilities.SupportsAchievements, nameof(capabilities.SupportsAchievements));
            Assert.IsFalse(capabilities.SupportsSave, nameof(capabilities.SupportsSave));
            Assert.IsFalse(capabilities.SupportsReview, nameof(capabilities.SupportsReview));
            Assert.IsFalse(capabilities.SupportsShare, nameof(capabilities.SupportsShare));
            Assert.IsFalse(capabilities.SupportsPurchases, nameof(capabilities.SupportsPurchases));
        }
    }
}
