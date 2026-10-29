//Shows the task deadline and plays feedback. PuzzleSession and the task controller still own progress and turns.
using Escape4Now.Puzzles;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class SurvivalPuzzleFeedback : MonoBehaviour
    {
        [Header("Saved UI and Audio")]
        [SerializeField] private Text timeLabel;
        [SerializeField] private Image timeFill;
        [SerializeField] private Image stateLamp;
        [SerializeField] private Text resultLabel;
        [SerializeField] private Image completionGlow;
        [SerializeField] private AudioSource sound;
        [SerializeField] private AudioClip hoverSound, pressSound, connectionSound, successSound, failureSound;
        [Header("Timing and Colors")]
        [SerializeField, Min(0)] private float successSeconds = .7f;
        [SerializeField, Min(0)] private float failureSeconds = 1f;
        [SerializeField, Min(0)] private float warningSeconds = 5f;
        [SerializeField, Min(.01f)] private float circuitStepSeconds = .09f;
        [SerializeField, Range(0,1)] private float volume = .5f;
        [SerializeField, Range(0,1)] private float glowStrength = .2f;
        [SerializeField, Min(.1f)] private float glowSpeed = 12f;
        [SerializeField] private Color readyColor = new Color(.15f,.8f,1,1);
        [SerializeField] private Color successColor = new Color(.2f,1,.5f,1);
        [SerializeField] private Color failureColor = new Color(1,.25f,.2f,1);
        [SerializeField] private string timeFormat = "TASK {0:0.0}s";
        [SerializeField] private string successText = "TASK COMPLETE";
        [SerializeField] private string failureText = "Incorrect — turn ends";
        [SerializeField] private string timeoutText = "TIME'S UP — turn ends";
        private PuzzleSession attempt;
        private float resultElapsed, circuitElapsed;
        private bool resultPlayed;
        public bool CanInteract => attempt != null && !attempt.Solved && !attempt.Failed
            && !Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused;
        public bool ReadyToClose => attempt != null && (attempt.Solved || attempt.Failed)
            && resultElapsed >= (attempt.Solved ? successSeconds : failureSeconds);
        public float ResultWait => Mathf.Max(successSeconds,failureSeconds);
        public void Hover() { if (CanInteract) Play(hoverSound); }
        public void Press() { if (CanInteract) Play(pressSound); }
        public void Connected() { Play(connectionSound); }
        private void Play(AudioClip clip) { if (sound != null && clip != null) sound.PlayOneShot(clip,volume); }
        public void CircuitChanged() { circuitElapsed = 0; }
        public bool PowerReached(int distance) => distance >= 0 && circuitElapsed >= distance*circuitStepSeconds;

        public void Present(PuzzleSession current)
        {
            if (attempt != current)
            { attempt = current; resultElapsed = circuitElapsed = 0; resultPlayed = false; }
            if (attempt == null) return;
            circuitElapsed += Time.deltaTime;
            bool ended = attempt.Solved || attempt.Failed;
            if (ended)
            {
                resultElapsed += Time.deltaTime;
                if (!resultPlayed) { resultPlayed = true; Play(attempt.Solved ? successSound : failureSound); }
            }
            var tint = ended ? attempt.Solved ? successColor : failureColor
                : attempt.SecondsRemaining <= warningSeconds ? failureColor : readyColor;
            timeLabel.text = string.Format(timeFormat,attempt.SecondsRemaining);
            timeFill.fillAmount = Mathf.Clamp01(attempt.SecondsRemaining/attempt.Definition.timeLimitSeconds);
            timeFill.color = tint; stateLamp.color = tint;
            resultLabel.gameObject.SetActive(ended);
            if (ended) { resultLabel.text = attempt.Solved ? successText : attempt.TimedOut ? timeoutText : failureText; resultLabel.color = tint; }
            var glow = tint; glow.a = ended ? glowStrength*.5f*(1+Mathf.Sin(resultElapsed*glowSpeed)) : 0;
            completionGlow.color = glow;
        }
        private void OnDisable() { if (sound != null) sound.Pause(); }
        private void OnEnable() { if (sound != null) sound.UnPause(); }
    }
}
