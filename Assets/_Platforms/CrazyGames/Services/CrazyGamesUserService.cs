// Compiled only when CrazyGames is the selected platform (Tools/Template/Platform).
#if PLATFORM_CRAZYGAMES
using System;
using _Core.Platform.Core;
using CrazyGames;
using _Core.Platform.Services.User;
using UnityEngine;

namespace _Platforms.CrazyGames.Services
{
    /// <summary>
    /// Handles user operations through the CrazyGames SDK.
    /// </summary>
    public class CrazyGamesUserService : IUserService
    {
        private Action<PlatformUser> _userChanged;
        private bool _authListenerRegistered;

        public bool IsAvailable
        {
            get
            {
                try
                {
                    return CrazySDK.IsAvailable &&
                           CrazySDK.IsInitialized &&
                           CrazySDK.User.IsUserAccountAvailable;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    return false;
                }
            }
        }

        public PlatformUser CurrentUser { get; private set; }

        public event Action<PlatformUser> UserChanged
        {
            add
            {
                _userChanged += value;

                // CrazyGames QA reports whether the game listens for sign-ins,
                // so the SDK listener is registered only once someone actually subscribes.
                RegisterAuthListener();
            }
            remove
            {
                _userChanged -= value;
            }
        }

        public void GetCurrentUser(Action<PlatformUser> callback)
        {
            if (!IsAvailable)
            {
                callback?.Invoke(null);
                return;
            }

            CrazySDK.User.GetUser(user =>
            {
                SetCurrentUser(ToPlatformUser(user));
                callback?.Invoke(CurrentUser);
            });
        }

        public void ShowLogin(Action<PlatformUser> callback)
        {
            if (!IsAvailable)
            {
                callback?.Invoke(null);
                return;
            }

            CrazySDK.User.ShowAuthPrompt((error, user) =>
            {
                if (error != null)
                {
                    Debug.LogWarning($"CrazyGames: Login failed: {error}");
                    callback?.Invoke(null);
                    return;
                }

                SetCurrentUser(ToPlatformUser(user));
                callback?.Invoke(CurrentUser);
            });
        }

        public void ShowAccountLinkPrompt(Action<bool> callback)
        {
            if (!IsAvailable)
            {
                callback?.Invoke(false);
                return;
            }

            CrazySDK.User.ShowAccountLinkPrompt((error, linked) =>
            {
                if (error != null)
                {
                    Debug.LogWarning($"CrazyGames: Account link prompt failed: {error}");
                    callback?.Invoke(false);
                    return;
                }

                callback?.Invoke(linked);
            });
        }

        public void GetUserToken(Action<string> callback)
        {
            if (!IsAvailable)
            {
                callback?.Invoke(null);
                return;
            }

            CrazySDK.User.GetUserToken((error, token) =>
            {
                if (error != null)
                {
                    Debug.LogWarning($"CrazyGames: User token request failed: {error}");
                    callback?.Invoke(null);
                    return;
                }

                callback?.Invoke(token);
            });
        }

        private void RegisterAuthListener()
        {
            if (_authListenerRegistered || !IsAvailable)
                return;

            _authListenerRegistered = true;
            CrazySDK.User.AddAuthListener(OnAuthChanged);
        }

        /// <summary>
        /// Called by the SDK when the player signs in on the portal. Signing out reloads the page,
        /// so there is no sign-out callback. Public so tests can simulate a sign-in in the Editor.
        /// </summary>
        public void OnAuthChanged(PortalUser user)
        {
            SetCurrentUser(ToPlatformUser(user));
        }

        private void SetCurrentUser(PlatformUser user)
        {
            bool changed = !Equals(CurrentUser?.Id, user?.Id) ||
                           !Equals(CurrentUser?.Username, user?.Username);
            CurrentUser = user;

            if (!changed)
                return;

            if (_userChanged == null)
                return;

            // Isolate listeners so one throwing handler does not starve the others.
            foreach (Delegate handler in _userChanged.GetInvocationList())
            {
                try
                {
                    ((Action<PlatformUser>)handler).Invoke(user);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static PlatformUser ToPlatformUser(PortalUser user)
        {
            if (user == null)
                return null;

            return new PlatformUser(
                user.__dangerousUserId,
                user.username,
                user.profilePictureUrl);
        }
    }
}
#endif
