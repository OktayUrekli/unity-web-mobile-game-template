namespace _Core.Platform.Services.Review.Null
{
    /// <summary>
    /// Provides a safe fallback when in-app reviews are not supported.
    /// </summary>
    public class NullReviewService : IReviewService
    {
        public void RequestReview()
        {
            // In-app reviews are not supported on this platform.
        }
    }
}
