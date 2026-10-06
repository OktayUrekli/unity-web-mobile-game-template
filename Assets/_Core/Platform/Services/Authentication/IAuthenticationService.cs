using System;

namespace _Core.Platform.Services.Authentication
{
    /// <summary>
    /// Provides authentication functionality.
    /// </summary>
    public interface IAuthenticationService
    {
        bool IsSignedIn { get; }

        void SignIn(Action<bool> callback);

        void SignOut();
    }
}