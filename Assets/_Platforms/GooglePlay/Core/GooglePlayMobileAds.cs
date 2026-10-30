// Compiled only when Google Play is the selected platform (Tools/Template/Platform).
#if PLATFORM_GOOGLEPLAY
using System;
using _Core.Events;
using _Core.Events.Platform;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace _Platforms.GooglePlay.Core
{
    /// <summary>
    /// Starts the Google Mobile Ads SDK (AdMob) the way Google requires: first the player's consent through the User
    /// Messaging Platform (UMP), then <c>MobileAds.Initialize</c>. Both run in the background, so the boot never waits
    /// for a consent form; ads answer NotAvailable until <see cref="IsReady"/>. Also opens the privacy options form
    /// for <c>GooglePlayPrivacyService</c>.
    /// <para>
    /// On Android the SDK calls back on threads other than Unity's; every callback goes through
    /// <see cref="RunOnMainThread"/> before it touches game state.
    /// </para>
    /// </summary>
    public static class GooglePlayMobileAds
    {
        private enum SdkState
        {
            NotStarted,
            Initializing,
            Ready,
            Failed
        }

        private static bool _consentStarted;
        // The plugin keeps one dismiss callback for all consent forms, so only one form may be in flight.
        private static bool _isFormInFlight;
        private static SdkState _sdkState;

        /// <summary>
        /// True where the Google Mobile Ads plugin runs: Android players, and the Editor with placeholder ads.
        /// </summary>
        public static bool IsSupported => Application.platform == RuntimePlatform.Android || Application.isEditor;

        /// <summary>
        /// True once consent allows ads and the SDK has initialized; ads can be loaded and shown.
        /// </summary>
        public static bool IsReady => _sdkState == SdkState.Ready;

        /// <summary>
        /// True when the player's region requires a way to change the consent choice (GDPR). Known once the
        /// start-up consent flow has finished; false before. Changes publish <c>PrivacyOptionsChangedEvent</c>.
        /// </summary>
        public static bool IsPrivacyOptionsRequired { get; private set; }

        /// <summary>
        /// Raised on the main thread once <see cref="IsReady"/> becomes true.
        /// </summary>
        public static event Action Ready;

        /// <summary>
        /// Gathers consent and initializes the SDK in the background. Call once, on the main thread.
        /// </summary>
        public static void Start()
        {
            if (_consentStarted)
                return;

            _consentStarted = true;

            // The start-up consent flow holds the form slot until it ends, so a privacy options request answers at
            // once meanwhile: the plugin keeps a single dismiss callback for all consent forms.
            _isFormInFlight = true;

            // The SDK's main-thread queue; created here because consent callbacks can arrive before MobileAds exists.
            MobileAdsEventExecutor.Initialize();

            Debug.Log("GooglePlay: Updating consent information (UMP).");

            try
            {
                ConsentInformation.Update(new ConsentRequestParameters(),
                    error => RunOnMainThread(() => OnConsentInfoUpdated(error)));
            }
            catch (Exception exception)
            {
                // Treated like a failed update. RunOnMainThread logs anything the consent check throws in turn.
                Debug.LogException(exception);
                _isFormInFlight = false;
                RunOnMainThread(() => StartAdsIfAllowed(consentUpdateFailed: true));
            }
        }

        /// <summary>
        /// Opens Google's privacy options form, where the player can change their consent choice. Answers on the main
        /// thread when the form closes or cannot open (before the start-up consent flow has finished, or while
        /// another consent form is open).
        /// </summary>
        public static void ShowPrivacyOptionsForm(Action onClosed = null)
        {
            if (!_consentStarted || _isFormInFlight)
            {
                onClosed?.Invoke();
                return;
            }

            _isFormInFlight = true;

            try
            {
                ConsentForm.ShowPrivacyOptionsForm(error => RunOnMainThread(() =>
                {
                    _isFormInFlight = false;

                    if (error != null)
                        Debug.LogWarning($"GooglePlay: Privacy options form failed: {error.Message}");

                    RefreshPrivacyOptions();

                    // Consent may only be gathered now, for example when the start-up form failed to show.
                    if (ConsentInformation.CanRequestAds())
                        InitializeSdk();

                    onClosed?.Invoke();
                }));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _isFormInFlight = false;
                onClosed?.Invoke();
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> on Unity's main thread: at once when called there (the Editor's placeholder
        /// ads), otherwise in the next frame. Exceptions are logged, so one failing callback does not drop the others
        /// queued with it.
        /// </summary>
        public static void RunOnMainThread(Action action)
        {
            if (action == null)
                return;

            // At once on the main thread: the Editor placeholders write Time.timeScale right after their close
            // callback, so the ad pause must already be released then.
            if (MobileAdsEventExecutor.IsOnMainThread())
            {
                Invoke(action);
                return;
            }

            // The lambda captures the action: the SDK's queue skips delegates without a target.
            MobileAdsEventExecutor.ExecuteInUpdate(() => Invoke(action));
        }

        private static void Invoke(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void OnConsentInfoUpdated(FormError error)
        {
            if (error != null)
            {
                // Google: on an error, CanRequestAds() still reflects the previous session's consent.
                Debug.LogWarning($"GooglePlay: Consent information update failed ({error.ErrorCode}): {error.Message}");
                _isFormInFlight = false;
                RefreshPrivacyOptions();
                StartAdsIfAllowed(consentUpdateFailed: true);
                return;
            }

            // The form slot taken in Start stays held until this form is done, so the player cannot open a second one.
            try
            {
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => RunOnMainThread(() =>
                {
                    _isFormInFlight = false;

                    if (formError != null)
                        Debug.LogWarning($"GooglePlay: Consent form failed ({formError.ErrorCode}): {formError.Message}");

                    RefreshPrivacyOptions();
                    StartAdsIfAllowed(consentUpdateFailed: false);
                }));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _isFormInFlight = false;
                RefreshPrivacyOptions();
                StartAdsIfAllowed(consentUpdateFailed: false);
            }
        }

        private static void StartAdsIfAllowed(bool consentUpdateFailed)
        {
            if (ConsentInformation.CanRequestAds())
            {
                Debug.Log($"GooglePlay: Consent allows ads ({ConsentInformation.ConsentStatus}).");
                InitializeSdk();
                return;
            }

            // Development builds always use Google's test ad units, so testing goes on without an AdMob privacy
            // message (the sample app ID has none). Release builds follow the consent result.
            if (consentUpdateFailed && Debug.isDebugBuild)
            {
                Debug.LogWarning("GooglePlay: Consent is unknown; starting test ads anyway because this is a Development build.");
                InitializeSdk();
                return;
            }

            Debug.LogWarning($"GooglePlay: Consent was not gathered ({ConsentInformation.ConsentStatus}), so ads stay " +
                             "off until the player makes a choice in the privacy options form. In a release build, " +
                             "check AdMob > Privacy & messaging for a published GDPR message.");
        }

        private static void InitializeSdk()
        {
            if (_sdkState == SdkState.Initializing || _sdkState == SdkState.Ready)
                return;

            _sdkState = SdkState.Initializing;
            Debug.Log("GooglePlay: Initializing Google Mobile Ads.");

            try
            {
                MobileAds.Initialize(status => RunOnMainThread(() => OnSdkInitialized(status)));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _sdkState = SdkState.Failed;
            }
        }

        private static void OnSdkInitialized(InitializationStatus status)
        {
            if (status == null)
            {
                Debug.LogError("GooglePlay: Google Mobile Ads failed to initialize. Ads stay off this session.");
                _sdkState = SdkState.Failed;
                return;
            }

            _sdkState = SdkState.Ready;
            Debug.Log("GooglePlay: Google Mobile Ads initialized.");
            Ready?.Invoke();
        }

        private static void RefreshPrivacyOptions()
        {
            bool required = ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
            if (required == IsPrivacyOptionsRequired)
                return;

            IsPrivacyOptionsRequired = required;
            EventBus.Publish(new PrivacyOptionsChangedEvent(required));
        }

        // Static state outlives a Play session when domain reload is off (Enter Play Mode Options).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _consentStarted = false;
            _isFormInFlight = false;
            _sdkState = SdkState.NotStarted;
            IsPrivacyOptionsRequired = false;
            Ready = null;
        }
    }
}
#endif
