// Compiled only when CrazyGames is the selected platform (Tools/Template/Platform).
#if PLATFORM_CRAZYGAMES
using System;
using _Core.Platform.Services.Authentication;
using _Core.Platform.Services.User;
using UnityEngine;

namespace _Platforms.CrazyGames.Services
{
    /// <summary>
    /// Sign-in state on CrazyGames, built on the user service: a player is signed in when the portal
    /// returns a user, otherwise they play as a guest.
    /// </summary>
    public class CrazyGamesAuthenticationService : IAuthenticationService
    {
        private readonly IUserService _userService;

        public CrazyGamesAuthenticationService(IUserService userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        public bool IsSignedIn => _userService.CurrentUser != null;

        public void SignIn(Action<bool> callback)
        {
            if (IsSignedIn)
            {
                callback?.Invoke(true);
                return;
            }

            _userService.ShowLogin(user => callback?.Invoke(user != null));
        }

        public void SignOut()
        {
            // CrazyGames signs out on the portal and reloads the page; games cannot sign out.
            Debug.Log("CrazyGames: Sign out is handled by the portal.");
        }
    }
}
#endif
