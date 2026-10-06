namespace _Core.Platform.Services.Share
{
    /// <summary>
    /// Provides sharing functionality.
    /// </summary>
    public interface IShareService
    {
        void ShareText(string text);

        void ShareImage(string imagePath, string text);
    }
}