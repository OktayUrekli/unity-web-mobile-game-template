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
    /// Handles user operations through the CrazyGames SDK. Every callback is answered exactly once: CrazySDK keeps one
    /// slot per prompt (a second call overwrites the first caller's) and calls game callbacks inside its own loops,
    /// so this service answers overlapping prompt calls itself and never lets a game callback throw into the SDK.
    /// </summary>
    public class CrazyGamesUserService : IUserService
    {
        // CrazySDK's error code when the auth prompt is opened for a player who is already signed in.
        private const string UserAlreadySignedInError = "userAlreadySignedIn";

        private Action<PlatformUser> _userChanged;
        private bool _authListenerRegistered;
        private bool _isLoginPromptOpen;
        private bool _isLinkPromptOpen;

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
                SafeInvoke(callback, CurrentUser);
            });
        }

        public void ShowLogin(Action<PlatformUser> callback)
        {
            if (!IsAvailable)
            {
                callback?.Invoke(null);
                return;
            }

            // CrazySDK keeps one auth prompt callback: a second call would take the first caller's answer.
            if (_isLoginPromptOpen)
            {
                Debug.LogWarning("CrazyGames: Login prompt is already open; ignoring the second request.");
                callback?.Invoke(null);
                return;
            }

            _isLoginPromptOpen = true;
            CrazySDK.User.ShowAuthPrompt((error, user) =>
            {
                _isLoginPromptOpen = false;

                if (error != null)
                {
                    // Signed in on the portal without this service hearing about it (the auth listener is
                    // registered only when the game subscribes to UserChanged): answer with that user.
                    if (error.code == UserAlreadySignedInError)
                    {
                        GetCurrentUser(callback);
                        return;
                    }

                    Debug.LogWarning($"CrazyGames: Login failed: {error}");
                    SafeInvoke(callback, null);
                    return;
                }

                SetCurrentUser(ToPlatformUser(user));
                SafeInvoke(callback, CurrentUser);
            });
        }

        public void ShowAccountLinkPrompt(Action<bool> callback)
        {
            if (!IsAvailable)
            {
                callback?.Invoke(false);
                return;
            }

            // CrazySDK keeps one account link callback: a second call would take the first caller's answer.
            if (_isLinkPromptOpen)
            {
                Debug.LogWarning("CrazyGames: Account link prompt is already open; ignoring the second request.");
                callback?.Invoke(false);
                return;
            }

            _isLinkPromptOpen = true;
            CrazySDK.User.ShowAccountLinkPrompt((error, linked) =>
            {
                _isLinkPromptOpen = false;

                if (error != null)
                {
                    Debug.LogWarning($"CrazyGames: Account link prompt failed: {error}");
                    SafeInvoke(callback, false);
                    return;
                }

                SafeInvoke(callback, linked);
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
                    SafeInvoke(callback, null);
                    return;
                }

                SafeInvoke(callback, token);
            });
        }

        // Game callbacks run inside CrazySDK's callback code: an exception escaping there skips the SDK's own
        // cleanup (a prompt slot stays taken) and the callbacks queued after this one.
        private static void SafeInvoke<T>(Action<T> callback, T value)
        {
            try
            {
                callback?.Invoke(value);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
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
