using UnityEngine;
using UnityEngine.InputSystem;
using _Core.Platform.Core;
using _Core.Platform.Services.Ads;

namespace _Platforms.CrazyGames.Testing
{
    /// <summary>
    /// Manual test harness for the ads service.
    /// Keys: B/H show/hide banner, I interstitial, R rewarded, P prefetch rewarded, K adblock check.
    /// </summary>
    public class CrazyGamesAdsTest : MonoBehaviour
    {
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || PlatformManager.Instance == null)
                return;

            IAdsService ads = PlatformManager.Instance.Ads;

            if (keyboard.bKey.wasPressedThisFrame)
                ads.ShowBanner();

            if (keyboard.hKey.wasPressedThisFrame)
                ads.HideBanner();

            if (keyboard.iKey.wasPressedThisFrame)
                TestMidgameAd();

            if (keyboard.rKey.wasPressedThisFrame)
                TestRewardedAd();

            if (keyboard.pKey.wasPressedThisFrame)
            {
                ads.PrefetchAd(AdType.Rewarded);
                Debug.Log("Prefetch requested for a rewarded ad.");
            }

            if (keyboard.kKey.wasPressedThisFrame)
                ads.HasAdblock(hasAdblock => Debug.Log($"Adblock detected: {hasAdblock}"));
        }

        [ContextMenu("Test Midgame Ad")]
        private void TestMidgameAd()
        {
            Debug.Log("Requesting Midgame Ad...");

            PlatformManager.Instance.Ads.ShowInterstitialAd(result =>
            {
                Debug.Log($"Midgame Ad result: {result}");
            });
        }

        [ContextMenu("Test Rewarded Ad")]
        private void TestRewardedAd()
        {
            Debug.Log("Requesting Rewarded Ad...");

            PlatformManager.Instance.Ads.ShowRewardedAd(result =>
            {
                Debug.Log($"Rewarded Ad result: {result}");

                if (result == AdResult.Completed)
                    Debug.Log("Reward can be granted.");
            });
        }
    }
}
