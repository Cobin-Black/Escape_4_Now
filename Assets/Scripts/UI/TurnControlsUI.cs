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

        private void LateUpdate()
        {
            var turns = context.Turns;
            var player = context.ActivePlayer;
            bool visible = context.Playing && !context.Finished && turns != null && turns.ShowEndTurnButton
                && (player == null || !player.IsInMenu())
                && !LocalMultiplayerSession.GameplayPaused && LocalMultiplayerSession.AllowTurnButtons;
            endTurn.gameObject.SetActive(visible);
            endTurn.interactable = visible && player != null && player.CanReadTurnInput && player.HasRolled
                && !player.IsMoving && !player.IsUsingItem() && !player.IsInMenu();
        }

        public void EndTurn()
        {
            if (!endTurn.gameObject.activeInHierarchy || !endTurn.interactable) return;
            if (LanMultiplayerSession.Active) LanMultiplayerSession.Instance.Submit("end");
            else context.Turns.AdvanceTurn();
        }
    }
}
