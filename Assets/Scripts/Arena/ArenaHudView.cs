using Escape4Now.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.Arena
{
    // The shared HUD shows mode-specific information without introducing any new game rules.
    public sealed class ArenaHudView : MonoBehaviour
    {
        [SerializeField]
        private ArenaInterfaceStyle style;
        [SerializeField]
        private Text modeTitle, remaining, objective, round, timer, holder, notice;
        [SerializeField]
        private GameObject potatoSection;
        [SerializeField]
        private Image timerBar;
        [SerializeField]
        private ArenaHealthRow[] healthRows;
        [Header("Editable Wording")]
        [SerializeField]
        private string remainingFormat = "{0} PLAYERS REMAINING";
        [SerializeField]
        private string roundFormat = "ROUND {0}";
        [SerializeField]
        private string holderFormat = "POTATO: {0}";
        [Header("Show Events / Quiet Periods")]
        [SerializeField]
        private string nextEventFormat = "Next event in {0:0}s";
        [SerializeField]
        private string noEventsLabel = "No more events scheduled this match.";
        // Read the mode state and every participant health value into the saved HUD widgets.
        public void Present(ArenaMatch match)
        {
            modeTitle.text = match.Title;
            remaining.text = string.Format(remainingFormat, match.Remaining);
            objective.text = match.Objective;
            notice.text = match.Notice;
            if (string.IsNullOrEmpty(notice.text) && match.Events != null && match.Events.EventLocationsVisible)
            {
                var active = match.Events.Active;
                if (active.Count > 0)
                {
                    var state = active[0];
                    notice.text = state.Rule.label + " (" + Mathf.CeilToInt(state.Warning ? state.WarningRemaining : Mathf.Max(0, state.Rule.duration - state.Elapsed)) + "s)";
                }
                else
                    notice.text = match.Events.HasScheduledEvents ? string.Format(nextEventFormat, match.Events.NextEventSeconds) : noEventsLabel;
            }

            notice.gameObject.SetActive(!string.IsNullOrEmpty(notice.text));
            var potato = match as HotPotatoController;
            potatoSection.SetActive(potato != null);
            if (potato != null)
            {
                round.text = string.Format(roundFormat, potato.Round);
                timer.text = MatchUIContext.FormatTime(potato.RemainingSeconds);
                timerBar.fillAmount = Mathf.Clamp01(potato.RemainingSeconds / potato.RoundSeconds);
                timer.color = timerBar.fillAmount < style.urgentTimerFraction ? style.healthy : style.holder;
                holder.text = string.Format(holderFormat, potato.Holder != null ? potato.Holder.name : "");
            }

            for (int i = 0; i < healthRows.Length; i++)
            {
                var player = i < match.Players.Count ? match.Players[i] : null;
                healthRows[i].Show(player, potato != null && potato.Holder == player);
            }
        }
    }
}
