// Shows the current turn, player, dice, time and mode-specific objective in the shared HUD.

using Escape4Now.Multiplayer;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Escape4Now.UI
{
    public sealed class MatchHudUI : MonoBehaviour
    {
        [Header("Unity UI References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text turn;
        [SerializeField] private Text playerName;
        [SerializeField] private Text action;
        [SerializeField] private Text time;
        [SerializeField] private Text objective;
        [SerializeField] private GameObject objectiveSection;
        [SerializeField] private GameObject timerSection;
        [SerializeField] private GameObject timerDivider;
        [SerializeField] private DiceDisplay dice;
        [SerializeField] private RectTransform attachedNotice;
        [Header("Panel Layout")]
        [SerializeField, Min(1f)] private float heightWithObjective = 210f;
        [SerializeField, Min(1f)] private float heightWithoutObjective = 121f;
        [SerializeField, Min(1f)] private float heightWithoutObjectiveWithNotice = 168f;
        [Header("Copy")]
        [SerializeField] private string turnFormat = "TURN  {0}";
        [SerializeField] private string movesFormat = "MOVES {0}";
        [SerializeField] private string rollingText = "ROLLING";
        [SerializeField] private string rollFormat = "{0}";
        [SerializeField] private string rollFallback = "ROLL";
        public Rect PanelRect => panel.activeInHierarchy
            ? MatchUIContext.ScreenRect((RectTransform)panel.transform) : default;
        public Rect OccupiedRect
        {
            get
            {
                Rect bounds = PanelRect;
                if (attachedNotice == null || !attachedNotice.gameObject.activeInHierarchy) return bounds;
                Rect notice = MatchUIContext.ScreenRect(attachedNotice);
                return Rect.MinMaxRect(Mathf.Min(bounds.xMin, notice.xMin), Mathf.Min(bounds.yMin, notice.yMin),
                    Mathf.Max(bounds.xMax, notice.xMax), Mathf.Max(bounds.yMax, notice.yMax));
            }
        }


        //Shared by every gamemode. Monster health and abilities live in MonsterHudUI so this HUD
        //never depends on Monster-only references.
        private void LateUpdate()
        {
            var player = context.ActivePlayer;
            var monster = context.Monster;
            bool monsterTurn = monster != null && context.Turns != null
                && context.Turns.CurrentTurnOwner == Escape4Now.TurnSystem.TurnOwner.Enemy;
            panel.SetActive((player != null || monsterTurn) && !context.Finished);
            if (!panel.activeSelf) return;
            turn.text = string.Format(turnFormat, context.Turns != null ? context.Turns.TurnNumber : 1);
            bool hasTimer = context.Timer != null;
            timerSection.SetActive(hasTimer);
            timerDivider.SetActive(hasTimer);
            if (hasTimer) time.text = MatchUIContext.FormatTime(context.Timer.TimeRemaining);

            string text = "";
            if (monsterTurn)
            {
                playerName.text = "MONSTER";
                var monsterRoll = monster.RollDiceAction;
                action.text = monster.HasRolled ? string.Format(movesFormat, monster.MovesRemaining)
                    : monster.IsRolling || monster.IsAIControlled ? rollingText
                    : string.Format(rollFormat, monsterRoll != null ? monsterRoll.GetBindingDisplayString(0).ToUpperInvariant() : rollFallback);
                dice.Show(monster.HasRolled ? monster.LastRoll : 1, monster.IsRolling);
            }
            else
            {
                playerName.text = (context.Turns != null ? context.Turns.CurrentTurnName : player.name).ToUpperInvariant();
                var roll = player.InputBindings != null ? player.InputBindings.RollDice : null;
                action.text = player.IsRollAnimating ? rollingText : player.HasRolled
                    ? string.Format(movesFormat, player.MovesRemaining)
                    : string.Format(rollFormat, roll != null ? roll.GetBindingDisplayString(0).ToUpperInvariant() : rollFallback);
                //Double Dice shows both dice it rolled; every other roll shows one die.
                if (player.HasRolled && player.SecondDie > 0)
                    dice.Show(player.FirstDie, player.SecondDie, player.IsRollAnimating);
                else
                    dice.Show(player.HasRolled ? player.LastRoll : 1, player.IsRollAnimating);
                text = player.Mode != null ? player.Mode.GetObjective(player) : "";
            }
            bool hasObjective = !string.IsNullOrEmpty(text);
            objectiveSection.SetActive(hasObjective);
            objective.text = text;
            ((RectTransform)panel.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                hasObjective ? heightWithObjective : attachedNotice != null && attachedNotice.gameObject.activeSelf
                    ? heightWithoutObjectiveWithNotice : heightWithoutObjective);
        }
    }
}
