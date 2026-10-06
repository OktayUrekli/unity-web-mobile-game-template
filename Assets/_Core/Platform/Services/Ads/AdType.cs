namespace _Core.Platform.Services.Ads
{
    /// <summary>
    /// Platform-independent advertisement formats that can be requested full-screen.
    /// </summary>
    public enum AdType
    {
        /// <summary>
        /// A skippable ad shown between gameplay moments (CrazyGames "midgame").
        /// </summary>
        Interstitial,

        /// <summary>
        /// An opt-in ad that grants a reward when watched to the end.
        /// </summary>
        Rewarded
    }
}
