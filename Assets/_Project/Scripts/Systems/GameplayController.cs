using _Core.Audio;
using _Core.Events;
using _Core.Events.Platform;
using _Core.Events.UI;
using _Core.Feedback;
using _Core.Gameplay;
using _Core.Platform.Core;
using _Core.SceneManagement;
using _Core.UI;
using _Project.Data;
using _Project.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project.Systems
{
    /// <summary>
    /// Example game loop for the template: score points (Tap button or Space) before the timer runs out.
    /// Shows how a level uses the framework: HUD screen, pause/game-over popups, <see cref="PauseManager"/>,
    /// <see cref="GameplayStateManager"/>, saving the best score, leaderboard, HappyTime, a rewarded "continue" and
    /// interstitials between rounds. Replace the scoring rule with real gameplay; keep the flow.
    /// </summary>
    public class GameplayController : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float roundSeconds = 20f;
        [SerializeField, Min(1f)] private float continueBonusSeconds = 10f;
        [Tooltip("Played for every point scored.")]
        [SerializeField] private SoundData scoreSound;

        /// <summary>
        /// The controller of the loaded gameplay scene, or null outside it.
        /// </summary>
        public static GameplayController Current { get; private set; }

        public int Score { get; private set; }
        public float TimeLeft { get; private set; }
        public bool IsRunning { get; private set; }
        public bool HasContinued { get; private set; }
        public bool IsNewBest { get; private set; }

        /// <summary>
        /// True while anything pauses the game (pause menu, ad, app in the background).
        /// </summary>
        public bool IsPaused => PauseManager.Instance != null && PauseManager.Instance.IsPaused;

        private bool _isLeaving;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Survives between Play sessions when domain reload is disabled.
            Current = null;
        }

        private void Awake()
        {
            Current = this;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<UIBackRequestedEvent>(OnBackRequested);
            EventBus.Subscribe<ApplicationPauseChangedEvent>(OnApplicationPauseChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<UIBackRequestedEvent>(OnBackRequested);
            EventBus.Unsubscribe<ApplicationPauseChangedEvent>(OnApplicationPauseChanged);
        }

        private void Start()
        {
            UIManager.Instance.ShowScreen<GameplayHudScreen>();
            StartRound();
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        private void Update()
        {
            if (!IsRunning || IsPaused)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                AddPoint();

            // Scaled time: PauseManager sets the time scale to 0 while anything pauses the game.
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                GameOver();
            }
        }

        /// <summary>
        /// Starts a fresh round.
        /// </summary>
        public void StartRound()
        {
            Score = 0;
            TimeLeft = roundSeconds;
            HasContinued = false;
            IsNewBest = false;
            IsRunning = true;
            GameplayStateManager.Instance.BeginGameplay();
        }

        /// <summary>
        /// The example scoring action.
        /// </summary>
        public void AddPoint()
        {
            if (!IsRunning || IsPaused)
                return;

            Score++;
            AudioManager.Instance.PlaySfx(scoreSound);
        }

        /// <summary>
        /// Opens the pause popup and pauses the game until it is closed.
        /// </summary>
        public void Pause()
        {
            if (!IsRunning || _isLeaving || UIManager.Instance.IsPopupOpen<PausePopup>())
                return;

            // The popup holds the menu pause while it is open.
            UIManager.Instance.ShowPopup<PausePopup>();
        }

        /// <summary>
        /// Closes the pause popup; the game resumes when it is closed.
        /// </summary>
        public void Resume()
        {
            UIManager.Instance.HidePopup<PausePopup>();
        }

        /// <summary>
        /// Grants extra time after a completed rewarded ad. Allowed once per round.
        /// </summary>
        public void ContinueAfterReward()
        {
            if (IsRunning || HasContinued)
                return;

            HasContinued = true;
            TimeLeft = continueBonusSeconds;
            IsRunning = true;
            UIManager.Instance.HidePopup<GameOverPopup>();
            GameplayStateManager.Instance.BeginGameplay();
        }

        /// <summary>
        /// Starts a new round after an interstitial (shown only when the cooldown allows).
        /// </summary>
        public void PlayAgain()
        {
            UIManager.Instance.HidePopup<GameOverPopup>();

            // Game flow continues whatever the ad result is (Cooldown, NotAvailable, Failed...).
            PlatformManager.Instance.Ads.ShowInterstitialAd(_ =>
            {
                if (this != null)
                    StartRound();
            });
        }

        /// <summary>
        /// Leaves to the main menu; shows an interstitial first when coming from the game-over popup.
        /// </summary>
        public void ExitToMenu(bool showInterstitial)
        {
            if (_isLeaving)
                return;

            _isLeaving = true;
            IsRunning = false;
            GameplayStateManager.Instance.EndGameplay();

            // Loading the menu hides the pause popup, which releases its pause; the interstitial pauses on its own.
            if (showInterstitial)
                PlatformManager.Instance.Ads.ShowInterstitialAd(_ => LoadMenu());
            else
                LoadMenu();
        }

        private static void LoadMenu()
        {
            _ = SceneTransition.LoadAsync(GameScenes.MainMenu);
        }

        private void GameOver()
        {
            IsRunning = false;
            GameplayStateManager.Instance.EndGameplay();

            Haptics.Vibrate();

            IsNewBest = ProgressStore.TrySaveBestScore(Score);
            if (IsNewBest)
                PlatformManager.Instance.Game.HappyTime();

            // Ignored by platforms without a leaderboard (Null service).
            PlatformManager.Instance.Leaderboard.SubmitScore(null, Score);

            UIManager.Instance.ShowPopup<GameOverPopup>();
        }

        private void OnBackRequested(UIBackRequestedEvent gameEvent)
        {
            Pause();
        }

        private void OnApplicationPauseChanged(ApplicationPauseChangedEvent gameEvent)
        {
            // Backgrounded app or hidden tab: open the pause menu, so the player resumes it on return.
            if (gameEvent.IsPaused)
                Pause();
        }
    }
}
