namespace _Core.Platform.Services.Ads
{
    /// <summary>
    /// Outcome of a full-screen advertisement request.
    /// Only <see cref="Completed"/> means a rewarded ad may grant its reward.
    /// </summary>
    public enum AdResult
    {
        /// <summary>
        /// The ad was shown and finished.
        /// </summary>
        Completed,

        /// <summary>
        /// The ad was requested but the platform reported an error (no fill, closed early, SDK error).
        /// </summary>
        Failed,

        /// <summary>
        /// Ads are not supported or the platform SDK is not ready; nothing was requested.
        /// </summary>
        NotAvailable,

        /// <summary>
        /// Another ad request is still in progress; this request was ignored.
        /// </summary>
        InProgress,

        /// <summary>
        /// The interstitial cooldown has not elapsed yet; nothing was requested.
        /// </summary>
        Cooldown,

        /// <summary>
        /// The platform did not answer in time (see <c>PlatformConfig</c> ad timeouts); the request was abandoned.
        /// </summary>
        TimedOut
    }
}
