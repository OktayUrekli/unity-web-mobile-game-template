// Compiled only when Google Play is the selected platform (Tools/Template/Platform).
#if PLATFORM_GOOGLEPLAY
using System;
using _Core.Platform.Services.Privacy;
using _Platforms.GooglePlay.Core;

namespace _Platforms.GooglePlay.Services
{
    /// <summary>
    /// Google's UMP privacy options form, where the player changes their AdMob consent choice. It is required in the
    /// regions where <see cref="GooglePlayMobileAds.IsPrivacyOptionsRequired"/> is true (GDPR).
    /// </summary>
    public class GooglePlayPrivacyService : IPrivacyService
    {
        public bool IsPrivacyOptionsRequired => GooglePlayMobileAds.IsPrivacyOptionsRequired;

        public void ShowPrivacyOptions(Action onClosed)
        {
            GooglePlayMobileAds.ShowPrivacyOptionsForm(onClosed);
        }
    }
}
#endif
