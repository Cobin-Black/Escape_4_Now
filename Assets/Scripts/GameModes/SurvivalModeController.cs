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
        [SerializeField] private string survivedTitle = "YOU SURVIVED";
        [SerializeField, TextArea] private string survivedMessage = "All players survived until time ran out.";
        [SerializeField] private string survivorsLabel = "SURVIVORS";

        public override IPlayerGridMap GridMap => map;
        public override GameModeType Type => GameModeType.Monster;
        public override bool TimerExpirationIsWin => true;
        public override string GetObjective(PlayerCharacter player) => objective;
        public override bool CanUseExit(PlayerCharacter player) => false;

        public override bool TryGetResult(IReadOnlyList<PlayerCharacter> participants, out MatchResult result)
        {
            if (Timer == null || !Timer.HasExpired) { result = default; return false; }
            //No monster/elimination system exists yet; every current participant survives.
            var names = new List<string>();
            if (participants != null)
                foreach (PlayerCharacter player in participants)
                    if (player != null) names.Add(player.name);
            result = new MatchResult(true, survivedTitle,
                survivedMessage + "\n\n" + survivorsLabel + "\n" + string.Join("\n", names));
            return true;
        }
    }
}
