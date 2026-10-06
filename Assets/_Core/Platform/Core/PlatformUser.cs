namespace _Core.Platform.Core
{
    /// <summary>
    /// Represents common user information provided by a platform.
    /// </summary>
    public class PlatformUser
    {
        /// <summary>
        /// Platform user ID. Informational only; use a user token to identify players on a server.
        /// May be empty when the platform does not expose it.
        /// </summary>
        public string Id { get; }

        public string Username { get; }
        public string ProfilePictureUrl { get; }

        public PlatformUser(
            string id,
            string username,
            string profilePictureUrl)
        {
            Id = id ?? string.Empty;
            Username = username;
            ProfilePictureUrl = profilePictureUrl;
        }
    }
}
