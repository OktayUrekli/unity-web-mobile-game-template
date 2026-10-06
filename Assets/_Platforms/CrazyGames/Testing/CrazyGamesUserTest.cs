using _Core.Platform.Core;
using UnityEngine;

namespace _Platforms.CrazyGames.Testing
{
    /// <summary>
    /// Manual test harness for the platform user, authentication and leaderboard services.
    /// Run the context menu items in Play Mode.
    /// </summary>
    public class CrazyGamesUserTest : MonoBehaviour
    {
        [SerializeField] private long testScore = 100;

        [ContextMenu("Get Current User")]
        public void GetCurrentUser()
        {
            PlatformManager.Instance.User.GetCurrentUser(user =>
            {
                if (user == null)
                {
                    Debug.Log("No user signed in. Playing as Guest.");
                    return;
                }

                Debug.Log($"User: {user.Username} (id {user.Id}), picture: {user.ProfilePictureUrl}");
            });
        }

        [ContextMenu("Show Login")]
        public void ShowLogin()
        {
            PlatformManager.Instance.User.ShowLogin(user =>
            {
                Debug.Log(user == null
                    ? "Login cancelled or failed."
                    : $"Logged in as: {user.Username}");
            });
        }

        [ContextMenu("Show Account Link Prompt")]
        public void ShowAccountLinkPrompt()
        {
            PlatformManager.Instance.User.ShowAccountLinkPrompt(linked => Debug.Log($"Account linked: {linked}"));
        }

        [ContextMenu("Get User Token")]
        public void GetUserToken()
        {
            PlatformManager.Instance.User.GetUserToken(token =>
                Debug.Log(token == null ? "No user token." : $"User token length: {token.Length}"));
        }

        [ContextMenu("Submit Test Score")]
        public void SubmitScore()
        {
            Debug.Log($"Leaderboard supported: {PlatformManager.Instance.Capabilities.SupportsLeaderboard}");
            PlatformManager.Instance.Leaderboard.SubmitScore(null, testScore);
        }

        [ContextMenu("Happy Time")]
        public void HappyTime()
        {
            PlatformManager.Instance.Game.HappyTime();
        }
    }
}
