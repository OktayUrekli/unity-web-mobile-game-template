using System;

namespace _Core.Platform.Services.Ads.Null
{
    /// <summary>
    /// Provides a safe fallback when the current platform does not support advertisements.
    /// </summary>
    public class NullAdsService : IAdsService
    {
        public bool IsAdShowing => false;

        public void ShowRewardedAd(Action<AdResult> callback)
        {
            // No advertisement is available on this platform.
            callback?.Invoke(AdResult.NotAvailable);
        }

        public void ShowInterstitialAd(Action<AdResult> callback)
        {
            // No advertisement is available on this platform.
            callback?.Invoke(AdResult.NotAvailable);
        }

        public void PrefetchAd(AdType adType)
        {
            // Nothing to preload on this platform.
        }

        public void HasAdblock(Action<bool> callback)
        {
            // No ads means no ad blocker to detect.
            callback?.Invoke(false);
        }

        public void ShowBanner()
        {
            // No banner is available on this platform.
        }

        public void HideBanner()
        {
            // No banner is available on this platform.
        }
    }
}
