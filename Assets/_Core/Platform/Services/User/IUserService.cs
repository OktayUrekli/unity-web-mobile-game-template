using System;
using _Core.Platform.Core;

namespace _Core.Platform.Services.User
{
    /// <summary>
    /// Provides platform-independent user operations.
    /// A null <see cref="PlatformUser"/> means the player is a guest (or the platform has no accounts).
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// True when the platform supports user accounts right now.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// The last known signed-in user, or null for a guest or when not fetched yet.
        /// </summary>
        PlatformUser CurrentUser { get; }

        /// <summary>
        /// Raised when the signed-in user changes (for example the player signs in on the portal).
        /// Subscribing may register a platform listener; unsubscribe when done.
        /// </summary>
        event Action<PlatformUser> UserChanged;

        /// <summary>
        /// Fetches the signed-in user; the callback receives null for a guest.
        /// </summary>
        void GetCurrentUser(Action<PlatformUser> callback);

        /// <summary>
        /// Shows the platform sign-in prompt; the callback receives null when cancelled or failed.
        /// </summary>
        void ShowLogin(Action<PlatformUser> callback);

        /// <summary>
        /// Asks a signed-in player to link their in-game account; the callback receives true when linked.
        /// </summary>
        void ShowAccountLinkPrompt(Action<bool> callback);

        /// <summary>
        /// Gets a token a game server can verify to identify the player; the callback receives null when unavailable.
        /// </summary>
        void GetUserToken(Action<string> callback);
    }
}
