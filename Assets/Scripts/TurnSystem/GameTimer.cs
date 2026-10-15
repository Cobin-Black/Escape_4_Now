using UnityEngine;

namespace Escape4Now.TurnSystem
{
    //Counts down the amount of time the player has to escape.
    public sealed class GameTimer : MonoBehaviour
    {
        [Header("Match Settings")]
        [SerializeField, Min(1)] private float timeLimit = 300f;

        private float timeRemaining;


        //Read-only access to the remaining time.
        public float TimeRemaining => timeRemaining;
        public bool HasExpired => hasStarted && timeRemaining <= 0f;
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

                Debug.Log("Time's up! You lose.");
                Time.timeScale = 0f;
            }
        }

        //Guests display the host's clock instead of starting their own countdown.
        public void ApplyNetworkTime(float seconds)
        {
            if (!Escape4Now.Multiplayer.LanMultiplayerSession.IsClient) return;
            timeRemaining = seconds;
            hasStarted = true;
            if (seconds <= 0f) Time.timeScale = 0f;
        }
    }
}

