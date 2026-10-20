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
        private void LateUpdate()
        {
            selected = null;
            var inventory = context.ActivePlayer != null ? context.ActivePlayer.Inventory : null;
            if (inventory != null)
                foreach (var item in inventory.Items)
                    if (item is CustomDiceItem custom && custom.IsSelecting) { selected = custom; break; }
            panel.SetActive(selected != null && !context.Finished && !LocalMultiplayerSession.GameplayPaused);
            if (selected != null) dice.Show(selected.SelectedNumber, false);
        }
        public void Previous() { if (selected != null) selected.SelectNumber((selected.SelectedNumber + 4) % 6 + 1); }
        public void Next() { if (selected != null) selected.SelectNumber(selected.SelectedNumber % 6 + 1); }
        public void Confirm() { selected?.ConfirmSelection(); }
        public void Cancel() { selected?.CancelSelection(); }
    }
}
