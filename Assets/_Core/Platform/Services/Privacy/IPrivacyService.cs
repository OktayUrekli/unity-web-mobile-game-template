using System;

namespace _Core.Platform.Services.Privacy
{
    /// <summary>
    /// The platform's privacy (ad consent) options. Some platforms require a way back to them inside the game
    /// (Google Play: AdMob in GDPR regions); <c>PrivacyOptionsButton</c> shows one only while
    /// <see cref="IsPrivacyOptionsRequired"/> is true.
    /// </summary>
    public interface IPrivacyService
    {
        /// <summary>
        /// True while the game must offer a way to open the privacy options. It can change at run time, for example
        /// after the start-up consent check; <c>PrivacyOptionsChangedEvent</c> is published then.
        /// </summary>
        bool IsPrivacyOptionsRequired { get; }

        /// <summary>
        /// Opens the platform's privacy options form. <paramref name="onClosed"/> is always invoked on the main
        /// thread, when the form closes or when it cannot open.
        /// </summary>
        void ShowPrivacyOptions(Action onClosed);
    }
}
