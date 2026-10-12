using UnityEngine;

namespace Escape4Now.TurnSystem
{
    //Counts down the amount of time the player has to escape.
    public sealed class GameTimer : MonoBehaviour
    {
        [SerializeField, Min(1)] private float timeLimit = 300f;

        private float timeRemaining;


        //Read-only access to the remaining time.
        public float TimeRemaining => timeRemaining;
        private bool timerRunning;

        //Starts the timer at the selected time limit.
        private void Start()
        {
            timeRemaining = timeLimit;
            timerRunning = true;
        }

        //Counts down the remaining game time.
        private void Update()
        {
            if (!timerRunning)
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

        //Displays the remaining game time on screen.
        private void OnGUI()
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);

            GUI.Label(
                new Rect(16f, 95f, 200f, 40f),
                $"Time: {minutes:00}:{seconds:00}"
            );
        }
    }
}