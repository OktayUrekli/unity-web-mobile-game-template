namespace _Core.Platform.Services.Review
{
    /// <summary>
    /// Provides in-app review functionality.
    /// </summary>
    public interface IReviewService
    {
        /// <summary>
        /// Asks the platform to show its rate-the-game dialog. The platform decides whether it appears, and the outcome
        /// is not reported back.
        /// </summary>
        void RequestReview();
    }
}