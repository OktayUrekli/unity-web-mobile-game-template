namespace _Core.Platform.Services.Share
{
    /// <summary>
    /// Provides sharing functionality.
    /// </summary>
    public interface IShareService
    {
        /// <summary>
        /// Shares <paramref name="text"/> through the platform.
        /// </summary>
        void ShareText(string text);

        /// <summary>
        /// Shares the image file at <paramref name="imagePath"/> together with <paramref name="text"/>.
        /// </summary>
        void ShareImage(string imagePath, string text);
    }
}