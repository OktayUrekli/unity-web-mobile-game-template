namespace _Core.Platform.Services.Share.Null
{
    public class NullShareService : IShareService
    {
        public void ShareText(string text)
        {
            // Sharing is not supported on this platform.
        }

        public void ShareImage(string imagePath, string text)
        {
            // Image sharing is not supported on this platform.
        }
    }
}
