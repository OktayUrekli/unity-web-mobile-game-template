// Compiled only when Yandex Games is the selected platform (Tools/Template/Platform).
#if PLATFORM_YANDEX
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace _Platforms.YandexGames.Core
{
    /// <summary>
    /// C# side of <c>YandexGames.jslib</c>: starts the Yandex Games SDK and forwards calls to it. The SDK answers
    /// through <see cref="YandexGamesCallbackReceiver"/>. Available only in a WebGL player whose page loaded the SDK
    /// script (the YandexGames WebGL template on Yandex or under <c>@yandex-games/sdk-dev-proxy</c>); everywhere else,
    /// the Editor included, <see cref="IsAvailable"/> is false and the platform uses fallback services.
    /// </summary>
    public static class YandexGamesSdk
    {
        /// <summary>
        /// Name of the receiver object; YandexGames.jslib sends its results to it with SendMessage.
        /// </summary>
        public const string ReceiverName = "YandexGamesSdk";

        // A request the SDK has not answered by then fails, so a lost promise cannot block a save forever.
        private const float RequestTimeoutSeconds = 30f;

        private enum InitState
        {
            NotStarted,
            Running,
            Succeeded,
            Failed
        }

        private sealed class PendingRequest
        {
            public Action<bool, string> Callback;
            public float Deadline;
        }

        [Serializable]
        private sealed class InitInfo
        {
            public string language;
            public string deviceType;
            public bool hasPlayerData;
            public bool isAuthorized;
            public string[] dataKeys;
            public string[] dataValues;
        }

        [Serializable]
        private sealed class RequestResult
        {
            public int id;
            public bool ok;
            public string value;
        }

        [Serializable]
        private sealed class AdEvent
        {
            public int id;
            public string type;
            public string value;
        }

        private static readonly Dictionary<int, PendingRequest> PendingRequests = new();
        private static readonly List<int> ExpiredRequests = new();

        private static InitState _initState;
        private static int _nextRequestId;
        private static int _adRequestId;
        private static Action<string, string> _adListener;
        private static Dictionary<string, string> _initialData;
        private static YandexGamesCallbackReceiver _receiver;

        /// <summary>
        /// True when the page loaded the Yandex Games SDK script. Always false in the Editor.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return YandexGames_IsSdkLoaded() == 1;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// True once <c>YaGames.init()</c> has resolved.
        /// </summary>
        public static bool IsInitialized => _initState == InitState.Succeeded;

        /// <summary>
        /// Yandex Games interface language (ISO 639-1, for example "ru" or "tr"), or null before initialization.
        /// </summary>
        public static string Language { get; private set; }

        /// <summary>
        /// Device type Yandex reports ("desktop", "mobile", "tablet", "tv"), or null before initialization.
        /// </summary>
        public static string DeviceType { get; private set; }

        /// <summary>
        /// True when the player object and its cloud data loaded during initialization.
        /// </summary>
        public static bool HasPlayerData { get; private set; }

        /// <summary>
        /// True when the player is signed in to Yandex (needed for leaderboards and reviews).
        /// </summary>
        public static bool IsAuthorized { get; private set; }

        /// <summary>
        /// True between the SDK's <c>game_api_pause</c> and <c>game_api_resume</c> events.
        /// </summary>
        public static bool IsPausedBySdk { get; private set; }

        /// <summary>
        /// Raised with the new state when the SDK sends <c>game_api_pause</c> or <c>game_api_resume</c>.
        /// </summary>
        public static event Action<bool> PauseChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Survives between Play sessions when domain reload is disabled.
            PendingRequests.Clear();
            _initState = InitState.NotStarted;
            _nextRequestId = 0;
            _adRequestId = 0;
            _adListener = null;
            _initialData = null;
            _receiver = null;
            Language = null;
            DeviceType = null;
            HasPlayerData = false;
            IsAuthorized = false;
            IsPausedBySdk = false;
            PauseChanged = null;
        }

        /// <summary>
        /// Calls <c>YaGames.init()</c>, loads the player and its cloud data, and waits at most
        /// <paramref name="timeoutSeconds"/> real seconds. Polls once per frame instead of <c>Task.Delay</c>,
        /// which relies on threads unavailable on WebGL.
        /// </summary>
        /// <returns>True when the SDK initialized before the timeout.</returns>
        public static async Task<bool> InitializeAsync(float timeoutSeconds)
        {
            if (_initState == InitState.Succeeded)
                return true;

            if (!IsAvailable)
                return false;

            if (_initState != InitState.Running)
            {
                EnsureReceiver();
                _initState = InitState.Running;
                CallInit(ReceiverName);
            }

            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (_initState == InitState.Running && Time.realtimeSinceStartup < deadline)
                await Task.Yield();

            return _initState == InitState.Succeeded;
        }

        /// <summary>
        /// The cloud data loaded during initialization, handed over once to the storage service. Empty when the
        /// player has no data yet.
        /// </summary>
        public static Dictionary<string, string> TakeInitialData()
        {
            Dictionary<string, string> data = _initialData ?? new Dictionary<string, string>();
            _initialData = null;
            return data;
        }

        /// <summary>
        /// Tells Yandex the game is loaded and playable (<c>LoadingAPI.ready</c>).
        /// </summary>
        public static void GameReady()
        {
            if (IsInitialized)
                CallGameReady();
        }

        /// <summary>
        /// Reports that gameplay started or resumed (<c>GameplayAPI.start</c>).
        /// </summary>
        public static void GameplayStart()
        {
            if (IsInitialized)
                CallGameplayStart();
        }

        /// <summary>
        /// Reports that gameplay paused or ended (<c>GameplayAPI.stop</c>).
        /// </summary>
        public static void GameplayStop()
        {
            if (IsInitialized)
                CallGameplayStop();
        }

        /// <summary>
        /// Shows a fullscreen (interstitial) ad. <paramref name="onEvent"/> receives the ad events
        /// ("open", "close" with "shown"/"notShown", "error" with a message) until a newer ad replaces it.
        /// </summary>
        public static void ShowFullscreenAd(Action<string, string> onEvent)
        {
            int id = BeginAd(onEvent);
            CallShowFullscreenAdv(id);
        }

        /// <summary>
        /// Shows a rewarded video. <paramref name="onEvent"/> receives "open", "rewarded", "close" and
        /// "error" (with a message).
        /// </summary>
        public static void ShowRewardedAd(Action<string, string> onEvent)
        {
            int id = BeginAd(onEvent);
            CallShowRewardedVideo(id);
        }

        /// <summary>
        /// Shows the sticky banner (needs "Use the API to display a sticky-banner" in the Developer Console).
        /// </summary>
        public static void ShowBanner()
        {
            if (IsInitialized)
                CallShowBanner();
        }

        /// <summary>
        /// Hides the sticky banner.
        /// </summary>
        public static void HideBanner()
        {
            if (IsInitialized)
                CallHideBanner();
        }

        /// <summary>
        /// Writes <paramref name="value"/> under <paramref name="key"/> in the player's cloud data (null deletes the
        /// key). Completes when Yandex accepted the write; throws <see cref="InvalidOperationException"/> when it
        /// failed or timed out.
        /// </summary>
        public static Task SetDataAsync(string key, string value)
        {
            var completion = new TaskCompletionSource<bool>();

            if (!IsInitialized || !HasPlayerData)
            {
                completion.SetException(new InvalidOperationException("Yandex player data is not available."));
                return completion.Task;
            }

            int id = BeginRequest((ok, error) =>
            {
                if (ok)
                    completion.TrySetResult(true);
                else
                    completion.TrySetException(new InvalidOperationException($"Yandex: setData failed: {error}"));
            });

            CallSetData(id, key, value);
            return completion.Task;
        }

        /// <summary>
        /// Submits <paramref name="score"/> to the leaderboard with the technical name
        /// <paramref name="leaderboardName"/>. The callback receives false and the reason when Yandex refused it
        /// (guest player, unknown name, more than one call per second).
        /// </summary>
        public static void SetLeaderboardScore(string leaderboardName, long score, Action<bool, string> callback)
        {
            if (!IsInitialized)
            {
                callback?.Invoke(false, "SDK not initialized");
                return;
            }

            int id = BeginRequest(callback);
            CallSetLeaderboardScore(id, leaderboardName, score);
        }

        /// <summary>
        /// Asks the player to rate the game when Yandex allows it. The callback receives true and "sent" or
        /// "closed", or false and the reason it was not shown (for example NO_AUTH or GAME_RATED).
        /// </summary>
        public static void RequestReview(Action<bool, string> callback)
        {
            if (!IsInitialized)
            {
                callback?.Invoke(false, "SDK not initialized");
                return;
            }

            int id = BeginRequest(callback);
            CallRequestReview(id);
        }

        internal static void HandleInitialized(string json)
        {
            if (_initState == InitState.Succeeded)
                return;

            InitInfo info;
            try
            {
                info = JsonUtility.FromJson<InitInfo>(json) ?? new InitInfo();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                info = new InitInfo();
            }

            Language = string.IsNullOrEmpty(info.language) ? null : info.language;
            DeviceType = string.IsNullOrEmpty(info.deviceType) ? null : info.deviceType;
            HasPlayerData = info.hasPlayerData;
            IsAuthorized = info.isAuthorized;
            _initialData = ToDictionary(info.dataKeys, info.dataValues);
            _initState = InitState.Succeeded;

            Debug.Log($"Yandex: SDK initialized (language {Language ?? "?"}, device {DeviceType ?? "?"}, " +
                      $"player data {(HasPlayerData ? "loaded" : "unavailable")}, signed in {IsAuthorized}).");
        }

        internal static void HandleInitFailed(string error)
        {
            if (_initState == InitState.Succeeded)
                return;

            _initState = InitState.Failed;
            Debug.LogWarning($"Yandex: SDK initialization failed: {error}");
        }

        internal static void HandleRequestCompleted(string json)
        {
            RequestResult result;
            try
            {
                result = JsonUtility.FromJson<RequestResult>(json);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return;
            }

            // An unknown id has already timed out.
            if (result == null || !PendingRequests.TryGetValue(result.id, out PendingRequest request))
                return;

            PendingRequests.Remove(result.id);
            Invoke(request.Callback, result.ok, result.value);
        }

        internal static void HandleAdEvent(string json)
        {
            AdEvent adEvent;
            try
            {
                adEvent = JsonUtility.FromJson<AdEvent>(json);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return;
            }

            // Events of an older ad (one the SDK answered after a newer request) are dropped.
            if (adEvent == null || adEvent.id != _adRequestId || _adListener == null)
                return;

            try
            {
                _adListener(adEvent.type, adEvent.value);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        internal static void HandlePauseChanged(bool paused)
        {
            if (paused == IsPausedBySdk)
                return;

            IsPausedBySdk = paused;

            if (PauseChanged == null)
                return;

            // Isolate listeners so one throwing handler does not starve the others.
            foreach (Delegate handler in PauseChanged.GetInvocationList())
            {
                try
                {
                    ((Action<bool>)handler).Invoke(paused);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        /// <summary>
        /// Fails requests the SDK did not answer in time. Called every frame by the receiver.
        /// </summary>
        internal static void ExpireRequests()
        {
            if (PendingRequests.Count == 0)
                return;

            float now = Time.realtimeSinceStartup;
            foreach (KeyValuePair<int, PendingRequest> pair in PendingRequests)
            {
                if (now > pair.Value.Deadline)
                    ExpiredRequests.Add(pair.Key);
            }

            foreach (int id in ExpiredRequests)
            {
                if (!PendingRequests.TryGetValue(id, out PendingRequest request))
                    continue;

                PendingRequests.Remove(id);
                Invoke(request.Callback, false, "timed out");
            }

            ExpiredRequests.Clear();
        }

        private static int BeginRequest(Action<bool, string> callback)
        {
            int id = ++_nextRequestId;
            PendingRequests[id] = new PendingRequest
            {
                Callback = callback,
                Deadline = Time.realtimeSinceStartup + RequestTimeoutSeconds
            };

            return id;
        }

        private static int BeginAd(Action<string, string> onEvent)
        {
            _adRequestId = ++_nextRequestId;
            _adListener = onEvent;
            return _adRequestId;
        }

        private static void Invoke(Action<bool, string> callback, bool ok, string value)
        {
            try
            {
                callback?.Invoke(ok, value);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static Dictionary<string, string> ToDictionary(string[] keys, string[] values)
        {
            var data = new Dictionary<string, string>();
            if (keys == null || values == null)
                return data;

            int count = Mathf.Min(keys.Length, values.Length);
            for (int i = 0; i < count; i++)
            {
                if (!string.IsNullOrEmpty(keys[i]))
                    data[keys[i]] = values[i];
            }

            return data;
        }

        private static void EnsureReceiver()
        {
            if (_receiver != null)
                return;

            var receiverObject = new GameObject(ReceiverName);
            UnityEngine.Object.DontDestroyOnLoad(receiverObject);
            _receiver = receiverObject.AddComponent<YandexGamesCallbackReceiver>();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int YandexGames_IsSdkLoaded();
        [DllImport("__Internal")] private static extern void YandexGames_Init(string receiverName);
        [DllImport("__Internal")] private static extern void YandexGames_GameReady();
        [DllImport("__Internal")] private static extern void YandexGames_GameplayStart();
        [DllImport("__Internal")] private static extern void YandexGames_GameplayStop();
        [DllImport("__Internal")] private static extern void YandexGames_ShowFullscreenAdv(int id);
        [DllImport("__Internal")] private static extern void YandexGames_ShowRewardedVideo(int id);
        [DllImport("__Internal")] private static extern void YandexGames_ShowBanner();
        [DllImport("__Internal")] private static extern void YandexGames_HideBanner();
        [DllImport("__Internal")] private static extern void YandexGames_SetData(int id, string key, string value);
        // The score goes as a double: Yandex scores are whole numbers well below 2^53.
        [DllImport("__Internal")] private static extern void YandexGames_SetLeaderboardScore(int id, string name, double score);
        [DllImport("__Internal")] private static extern void YandexGames_RequestReview(int id);

        private static void CallInit(string receiverName) => YandexGames_Init(receiverName);
        private static void CallGameReady() => YandexGames_GameReady();
        private static void CallGameplayStart() => YandexGames_GameplayStart();
        private static void CallGameplayStop() => YandexGames_GameplayStop();
        private static void CallShowFullscreenAdv(int id) => YandexGames_ShowFullscreenAdv(id);
        private static void CallShowRewardedVideo(int id) => YandexGames_ShowRewardedVideo(id);
        private static void CallShowBanner() => YandexGames_ShowBanner();
        private static void CallHideBanner() => YandexGames_HideBanner();
        private static void CallSetData(int id, string key, string value) => YandexGames_SetData(id, key, value);
        private static void CallSetLeaderboardScore(int id, string name, long score) => YandexGames_SetLeaderboardScore(id, name, score);
        private static void CallRequestReview(int id) => YandexGames_RequestReview(id);
#else
        // Outside a WebGL player IsAvailable is false, so initialization never succeeds and nothing below runs.
        private static void CallInit(string receiverName) { }
        private static void CallGameReady() { }
        private static void CallGameplayStart() { }
        private static void CallGameplayStop() { }
        private static void CallShowFullscreenAdv(int id) { }
        private static void CallShowRewardedVideo(int id) { }
        private static void CallShowBanner() { }
        private static void CallHideBanner() { }
        private static void CallSetData(int id, string key, string value) { }
        private static void CallSetLeaderboardScore(int id, string name, long score) { }
        private static void CallRequestReview(int id) { }
#endif
    }
}
#endif
