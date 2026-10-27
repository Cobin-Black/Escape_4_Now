using System.Collections.Generic;
using Escape4Now.Map;
using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.GameModes
{
    [DisallowMultipleComponent, AddComponentMenu("Escape 4 Now/Game Modes/Survival Mode")]
    public sealed class SurvivalModeController : GameModeController
    {
        [Header("Outdoor Map")]
        [SerializeField] private SurvivalMap map;
        [Header("Objective")]
        [SerializeField] private string objective = "Survive the monster.";
        [Header("Results")]
        [SerializeField] private string monsterWinTitle = "MONSTER WINS";
        [SerializeField, TextArea]
        private string monsterWinMessage = "The Monster eliminated all survivors.";
        [SerializeField] private string eliminatedLabel = "ELIMINATED";
        [SerializeField] private string survivedTitle = "YOU SURVIVED";
        [SerializeField, TextArea] private string survivedMessage = "All players survived until time ran out.";
        [SerializeField] private string survivorsLabel = "SURVIVORS";

        public override IPlayerGridMap GridMap => map;
        public override GameModeType Type => GameModeType.Monster;
        public override bool TimerExpirationIsWin => true;
        public override string GetObjective(PlayerCharacter player) => objective;
        public override bool CanUseExit(PlayerCharacter player) => false;

        public override bool TryGetResult(
            IReadOnlyList<PlayerCharacter> participants,
            out MatchResult result)
        {
            var survivors = new List<string>();
            var eliminated = new List<string>();

            if (participants != null)
            {
                foreach (PlayerCharacter player in participants)
                {
                    if (player == null)
                        continue;

                    if (player.Health > 0)
                        survivors.Add(player.name);
                    else
                        eliminated.Add(player.name);
                }
            }

            // The Monster wins immediately when no survivors remain.
            if (survivors.Count == 0)
            {
                result = new MatchResult(
                    false,
                    monsterWinTitle,
                    monsterWinMessage + "\n\n"
                    + eliminatedLabel + "\n"
                    + string.Join("\n", eliminated));

                return true;
            }

            // Living players win if they survive until the timer expires.
            if (Timer == null || !Timer.HasExpired)
            {
                result = default;
                return false;
            }

            result = new MatchResult(
                true,
                survivedTitle,
                survivedMessage + "\n\n"
                + survivorsLabel + "\n"
                + string.Join("\n", survivors));

            return true;
        }
    }
}
