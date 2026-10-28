// Counts down the configured match time. The game mode decides what happens when it reaches zero.

using UnityEngine;

namespace Escape4Now.TurnSystem
{
    //Counts down the shared match clock for either game mode.
    public sealed class GameTimer : MonoBehaviour
    {
        [Header("Match Settings")]
        [SerializeField, Min(1)] private float timeLimit = 300f;
        [SerializeField] private Escape4Now.GameModes.GameModeController gameMode;

        private float timeRemaining;


        //Read-only access to the remaining time.
        public float TimeRemaining => timeRemaining;
        public bool HasExpired => hasStarted && timeRemaining <= 0f;
        public bool ExpirationIsWin => gameMode != null && gameMode.TimerExpirationIsWin;
        private bool timerRunning;
        private bool hasStarted;

        //Starts the timer at the selected time limit.
        private void Start()
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.IsClient) return;
            timeRemaining = timeLimit;
            timerRunning = true;
            hasStarted = true;
        }

        //Counts down the remaining game time.
        private void Update()
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.IsClient) return;
            if (!timerRunning || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused)
            {
                return;
            }

            timeRemaining -= Time.deltaTime;

            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                timerRunning = false;

                Debug.Log("Match timer expired.");
                Time.timeScale = 0f;
            }
        }

        //Guests display the host's clock instead of starting their own countdown.
        // A client displays the host's remaining time instead of deciding its own match expiry.
        public void ApplyNetworkTime(float seconds)
        {
            if (!Escape4Now.Multiplayer.LanMultiplayerSession.IsClient) return;
            timeRemaining = seconds;
            hasStarted = true;
            if (seconds <= 0f) Time.timeScale = 0f;
        }
    }
}

