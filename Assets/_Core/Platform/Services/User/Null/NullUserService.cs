using System;
using _Core.Platform.Core;

namespace _Core.Platform.Services.User.Null
{
    /// <summary>
    /// Provides a safe fallback when the current platform does not support users.
    /// </summary>
    public class NullUserService : IUserService
    {
        public bool IsAvailable => false;

        public PlatformUser CurrentUser => null;

        public event Action<PlatformUser> UserChanged
        {
            // Users never change on this platform.
            add { }
            remove { }
        }

        public void GetCurrentUser(Action<PlatformUser> callback)
        {
            // No user is available on this platform.
            callback?.Invoke(null);
        }

        public void ShowLogin(Action<PlatformUser> callback)
        {
            // Login is not supported on this platform.
            callback?.Invoke(null);
        }

        public void ShowAccountLinkPrompt(Action<bool> callback)
        {
            // Account linking is not supported on this platform.
            callback?.Invoke(false);
        }

        public void GetUserToken(Action<string> callback)
        {
            // No token can be issued on this platform.
            callback?.Invoke(null);
        }
    }
}
