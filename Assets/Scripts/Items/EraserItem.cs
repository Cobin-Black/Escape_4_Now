
using Escape4Now.Arena;
using UnityEngine;

namespace Escape4Now.Items
{
    public sealed class EraserItem : FreeForAllItem
    {
        // Activate a shield that blocks two incoming attacks.
        public override void Use(PlayerInventory inventory)
        {
            if (inventory == null || !inventory.HasItem(this) ||
                UsesRemaining <= 0)
                return;

            ArenaParticipant player = inventory.GetComponent<ArenaParticipant>();

            if (player == null || !player.Alive ||
                player.Match == null || !player.Match.Playing ||
                player.Match.Paused)
                return;

            // Prevent activating another shield while one is active.
            if (player.HasEraserShield)
                return;

            player.ActivateEraserShield(UsesRemaining);

            // Remove the Eraser from inventory after activation.
            inventory.RemoveItem(this);
            Destroy(gameObject);
        }
    }
}
