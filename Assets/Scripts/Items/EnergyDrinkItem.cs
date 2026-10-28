// Uses the existing inventory to collect a drink and save its movement bonus for the survivor's next turn.

using Escape4Now.GameModes;
using UnityEngine;

namespace Escape4Now.Items
{
    public sealed class EnergyDrinkItem : Item
    {
        [SerializeField, Min(0)] private int nextTurnBonus = 2;
        [SerializeField] private string useFeedbackFormat = "Energy Drink: +{0} movement next turn.";
        private Vector2Int cell;
        private SurvivalModeController mode;
        private bool collected;
        public void Configure(SurvivalModeController owner, Vector2Int position, int bonus)
        { mode = owner; cell = position; nextTurnBonus = bonus; }
        public override bool IsAtPosition(Vector2Int position) => !collected && cell == position;
        // Use an ordinary inventory slot and hide the world object only after pickup succeeds.
        public override bool Collect(PlayerInventory inventory)
        {
            if (collected || inventory == null || mode == null || !mode.IsActiveSurvivor(inventory.Player) || !inventory.AddItem(this)) return false;
            collected = true;
            gameObject.SetActive(false);
            return true;
        }
        public override void PickUp(PlayerInventory inventory) => Collect(inventory);
        // Save the bonus on the player before removing the consumed drink from inventory.
        public override void Use(PlayerInventory inventory)
        {
            if (!collected || inventory == null || !inventory.HasItem(this) || !inventory.Player.CanUseItem()) return;
            inventory.Player.EventState.GiveMovementBonus(nextTurnBonus);
            mode.Events.ShowEffectMessage(inventory.Player, Escape4Now.Map.SurvivalEventType.EnergyDrink, string.Format(useFeedbackFormat, nextTurnBonus));
            inventory.RemoveItem(this);
            Destroy(gameObject);
        }
    }
}
