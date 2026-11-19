// Shows the configured End Turn control and only forwards it when the current player is allowed to finish.

using Escape4Now.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class TurnControlsUI : MonoBehaviour
    {
        [Header("Unity UI References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField] private Button endTurn;

        // Show only the turn actions the current player is allowed to use.
        private void LateUpdate()
        {
            var turns = context.Turns;
            var player = context.ActivePlayer;
            var network = LanMultiplayerSession.Instance;
            bool online = network?.UsesRaceMatchUI == true;
            bool visible = context.Playing && !context.Finished && turns != null && turns.ShowEndTurnButton
                && (player == null || !player.IsInMenu())
                && !LocalMultiplayerSession.GameplayPaused && LocalMultiplayerSession.AllowTurnButtons
                && (!online || network.IsMyTurn && !network.ShowingRaceStorage && !network.ChoosingNumber
                    && network.RaceBattleTargets.Count == 0 && string.IsNullOrEmpty(network.LatestState?.battleMessage));
            endTurn.gameObject.SetActive(visible);
            endTurn.interactable = visible && player != null && player.CanReadTurnInput && player.HasRolled
                && !player.IsMoving && !player.IsUsingItem() && !player.IsInMenu();
        }

        // End the turn through the shared local or network action path.
        public void EndTurn()
        {
            if (!endTurn.gameObject.activeInHierarchy || !endTurn.interactable) return;
            if (LanMultiplayerSession.Active) LanMultiplayerSession.Instance.Submit("end");
            else context.Turns.AdvanceTurn();
        }
    }
}
