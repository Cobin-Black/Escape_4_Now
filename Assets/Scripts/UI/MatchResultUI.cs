// Shows the result supplied by the mode and lets the player return to setup.

using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class MatchResultUI : MonoBehaviour
    {
        [Header("Unity UI References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text title;
        [SerializeField] private Text message;
        [SerializeField] private Text time;
        [SerializeField] private Image accent;
        [Header("Result Presentation")]
        [SerializeField] private Color winColor = new Color(0.3f, 0.9f, 0.73f);
        [SerializeField] private Color loseColor = new Color(1f, 0.43f, 0.4f);
        [SerializeField] private string timeFormat = "TIME LEFT {0}";

        private void LateUpdate()
        {
            bool finished = context.Session != null && context.Session.TryGetResult(out _);
            panel.SetActive(finished);
            if (!finished || !context.Session.TryGetResult(out var result)) return;
            title.text = result.Title;
            message.text = result.Message;
            title.color = accent.color = result.Won ? winColor : loseColor;
            time.gameObject.SetActive(context.Timer != null);
            if (context.Timer != null)
                time.text = string.Format(timeFormat, MatchUIContext.FormatTime(context.Timer.TimeRemaining));
        }

        public void BackToLobby() { context.Session?.ReturnToSetup(); }
    }
}
