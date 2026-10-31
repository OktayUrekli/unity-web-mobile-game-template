using System;

namespace _Core.Platform.Services.Authentication
{
    /// <summary>
    /// Provides authentication functionality.
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// True while the player is signed in to the platform; false for a guest.
        /// </summary>
        bool IsSignedIn { get; }

        /// <summary>
        /// Asks the player to sign in. The callback receives true once signed in (at once when already signed in), and
        /// false when the player cancels or sign-in fails or is unavailable.
        /// </summary>
        void SignIn(Action<bool> callback);

        /// <summary>
        /// Signs the player out where the platform lets the game do so; otherwise does nothing.
        /// </summary>
        void SignOut();
    }
}