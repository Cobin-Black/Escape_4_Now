// Displays the existing custom-dice item's choices and forwards its Confirm or Cancel buttons.

using Escape4Now.Items;
using Escape4Now.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class DiceSelectionUI : MonoBehaviour
    {
        [SerializeField] private MatchUIContext context;
        [SerializeField] private GameObject panel;
        [SerializeField] private DiceDisplay dice;
        private CustomDiceItem selected;
        private LanMultiplayerSession Online => LanMultiplayerSession.Instance?.UsesRaceMatchUI == true ? LanMultiplayerSession.Instance : null;
        // Show the custom-dice choice for the current local or online selection.
        private void LateUpdate()
        {
            var network = Online;
            if (network != null)
            {
                panel.SetActive(network.ChoosingNumber && network.IsMyTurn && !context.Finished && !LocalMultiplayerSession.GameplayPaused);
                if (panel.activeSelf) dice.Show(network.SelectedDiceNumber, false);
                return;
            }
            selected = null;
            var inventory = context.ActivePlayer != null ? context.ActivePlayer.Inventory : null;
            if (inventory != null)
                foreach (var item in inventory.Items)
                    if (item is CustomDiceItem custom && custom.IsSelecting) { selected = custom; break; }
            panel.SetActive(selected != null && !context.Finished && !LocalMultiplayerSession.GameplayPaused);
            if (selected != null) dice.Show(selected.SelectedNumber, false);
        }
        public void Previous() { if (Online != null) Online.StepDiceNumber(-1); else if (selected != null) selected.SelectNumber((selected.SelectedNumber + 4) % 6 + 1); }
        public void Next() { if (Online != null) Online.StepDiceNumber(1); else if (selected != null) selected.SelectNumber(selected.SelectedNumber % 6 + 1); }
        public void Confirm() { if (Online != null) Online.ChooseNumber(Online.SelectedDiceNumber); else selected?.ConfirmSelection(); }
        public void Cancel() { if (Online != null) Online.CancelDiceNumber(); else selected?.CancelSelection(); }
    }
}
