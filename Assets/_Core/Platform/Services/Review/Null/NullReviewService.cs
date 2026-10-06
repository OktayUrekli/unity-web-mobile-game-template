namespace _Core.Platform.Services.Review.Null
{
    public class NullReviewService : IReviewService
    {
        public void RequestReview()
        {
            // In-app reviews are not supported on this platform.
        }
    }
}
