using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using _Core.Events;
using _Core.Events.Settings;
using _Core.Managers;
using _Core.Platform.Core;
using _Core.Settings;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace _Core.Localization
{
    /// <summary>
    /// Bridges <see cref="SettingsManager"/> and Unity Localization: applies the saved language at boot
    /// and switches <see cref="LocalizationSettings.SelectedLocale"/> on <see cref="LanguageChangedEvent"/>.
    /// The language itself is stored in <see cref="SettingsData.language"/>; an empty value means
    /// "follow the platform, otherwise the device/browser language".
    /// </summary>
    public class LocalizationManager : Singleton<LocalizationManager>
    {
        /// <summary>
        /// String table owned by _Core (texts created in code, e.g. the ad overlay label).
        /// </summary>
        public const string CoreTable = "Core";

        // Longest the boot waits for localization. WebGL has no threads, so waits poll once per frame
        // (Task.Yield) instead of blocking or using Task.Delay.
        private const float InitTimeoutSeconds = 10f;

        /// <summary>
        /// True once the localization system finished loading the selected language's tables.
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// Code of the language currently shown (for example "en", "tr", "es").
        /// </summary>
        public string CurrentLanguage
        {
            get
            {
                Locale locale = LocalizationSettings.SelectedLocale;
                return locale != null ? locale.Identifier.Code : string.Empty;
            }
        }

        /// <summary>
        /// Languages the project ships (the Locales in the Localization settings).
        /// </summary>
        public IReadOnlyList<Locale> AvailableLocales
        {
            get
            {
                ILocalesProvider provider = LocalizationSettings.AvailableLocales;
                return provider != null ? (IReadOnlyList<Locale>)provider.Locales : Array.Empty<Locale>();
            }
        }

        /// <summary>
        /// Completes when Unity Localization finished its first load (locales and the startup tables),
        /// true on success. Started by <see cref="BeginLoad"/>; completed when it was never started.
        /// </summary>
        public Task<bool> LoadTask => _loadTask ?? Task.FromResult(false);

        private Task<bool> _loadTask;

        /// <summary>
        /// Starts loading Unity Localization (Addressables catalog, locales, string tables) without waiting,
        /// so it runs in parallel with the platform SDK. The Bootstrapper calls it before platform init;
        /// <see cref="InitAsync"/> calls it too if it was not started. Safe to call more than once.
        /// </summary>
        public void BeginLoad()
        {
            _loadTask ??= LoadLocalizationAsync();
        }

        /// <summary>
        /// Waits for the load started by <see cref="BeginLoad"/>, then selects the saved (or detected) language
        /// and preloads its tables. Never throws; on failure the texts keep their prefab values. Called by the
        /// Bootstrapper after <see cref="SettingsManager"/> is initialized.
        /// </summary>
        public async Task InitAsync()
        {
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
            BeginLoad();

            // Boot waits at most InitTimeoutSeconds; a slow load keeps going and applies the language when done.
            Task initialization = ApplyStartupLanguageAsync();
            await WhenDoneOrTimeoutAsync(initialization);
        }

        private static async Task<bool> LoadLocalizationAsync()
        {
            try
            {
                return await WaitAsync(LocalizationSettings.InitializationOperation);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private async Task ApplyStartupLanguageAsync()
        {
            try
            {
                if (!await LoadTask)
                    return;

                await SelectLocaleAsync(ResolveStartupLanguage());
                IsReady = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static async Task WhenDoneOrTimeoutAsync(Task task)
        {
            float deadline = Time.realtimeSinceStartup + InitTimeoutSeconds;

            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Debug.LogWarning($"LocalizationManager: not ready after {InitTimeoutSeconds} s; continuing boot, texts update when it finishes.");
                    return;
                }

                await Task.Yield();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void OnLanguageChanged(LanguageChangedEvent gameEvent)
        {
            _ = SelectLocaleAsync(gameEvent.Language);
        }

        // Saved choice first; then the language the platform reports (portals such as Yandex Games expect the game
        // to follow it); then the device/browser language; each only when shipped. Otherwise the project default.
        private string ResolveStartupLanguage()
        {
            string saved = SettingsManager.Instance != null ? SettingsManager.Instance.Language : null;
            if (FindLocale(saved) != null)
                return saved;

            PlatformManager platform = PlatformManager.Instance;
            string platformLanguage = platform != null && platform.Game != null ? platform.Game.Language : null;
            Locale platformLocale = FindLocale(platformLanguage);
            if (platformLocale != null)
                return platformLocale.Identifier.Code;

            string system = ToLanguageCode(Application.systemLanguage);
            if (FindLocale(system) != null)
                return system;

            return null;
        }

        private async Task SelectLocaleAsync(string languageCode)
        {
            try
            {
                Locale locale = FindLocale(languageCode);
                if (locale != null && locale != LocalizationSettings.SelectedLocale)
                    LocalizationSettings.SelectedLocale = locale;

                // Changing the locale restarts the initialization operation, which preloads the new tables.
                await WaitAsync(LocalizationSettings.InitializationOperation);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private Locale FindLocale(string languageCode)
        {
            IReadOnlyList<Locale> locales = AvailableLocales;
            var codes = new List<string>(locales.Count);
            foreach (Locale locale in locales)
                codes.Add(locale != null ? locale.Identifier.Code : null);

            int index = MatchLanguage(languageCode, codes);
            return index >= 0 ? locales[index] : null;
        }

        /// <summary>
        /// Index of the shipped language code that matches <paramref name="languageCode"/>, or -1. An exact match
        /// wins (ignoring case and '_' versus '-'); otherwise the language part alone, so "en-US" picks "en" and
        /// "pt" picks "pt-BR".
        /// </summary>
        internal static int MatchLanguage(string languageCode, IReadOnlyList<string> availableCodes)
        {
            if (string.IsNullOrWhiteSpace(languageCode) || availableCodes == null)
                return -1;

            string requested = Normalize(languageCode);
            for (int i = 0; i < availableCodes.Count; i++)
            {
                if (availableCodes[i] != null && Normalize(availableCodes[i]) == requested)
                    return i;
            }

            string requestedLanguage = LanguagePart(requested);
            for (int i = 0; i < availableCodes.Count; i++)
            {
                if (availableCodes[i] != null && LanguagePart(Normalize(availableCodes[i])) == requestedLanguage)
                    return i;
            }

            return -1;
        }

        private static string Normalize(string code) => code.Trim().Replace('_', '-').ToLowerInvariant();

        private static string LanguagePart(string normalizedCode)
        {
            int dash = normalizedCode.IndexOf('-');
            return dash > 0 ? normalizedCode.Substring(0, dash) : normalizedCode;
        }

        /// <summary>
        /// Maps Unity's system language (the browser language on WebGL) to a language code.
        /// Extend this when you add a Locale whose language is missing here.
        /// </summary>
        private static string ToLanguageCode(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.English: return "en";
                case SystemLanguage.German: return "de";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified: return "zh-Hans";
                case SystemLanguage.ChineseTraditional: return "zh-Hant";
                default: return null;
            }
        }

        // Returns false when the operation failed.
        private static async Task<bool> WaitAsync<T>(AsyncOperationHandle<T> handle)
        {
            while (handle.IsValid() && !handle.IsDone)
                await Task.Yield();

            if (handle.IsValid() && handle.Status == AsyncOperationStatus.Failed)
            {
                Debug.LogError($"LocalizationManager: initialization failed: {handle.OperationException}");
                return false;
            }

            return true;
        }
    }
}
