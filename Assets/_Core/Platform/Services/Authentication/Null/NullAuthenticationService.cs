using System;

namespace _Core.Platform.Services.Authentication.Null
{
    /// <summary>
    /// Provides a safe fallback when authentication is not supported.
    /// </summary>
    public class NullAuthenticationService : IAuthenticationService
    {
        public bool IsSignedIn => false;

        public void SignIn(Action<bool> callback)
        {
            // Authentication is not supported on this platform.
            callback?.Invoke(false);
        }

        public void SignOut()
        {
            // Authentication is not supported on this platform.
        }
    }
}