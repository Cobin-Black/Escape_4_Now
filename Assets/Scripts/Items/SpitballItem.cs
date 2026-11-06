
using Escape4Now.Arena;
using UnityEngine;

namespace Escape4Now.Items
{
    // Fires spitballs using the player's remaining item charges.
    public sealed class SpitballItem : FreeForAllItem
    {
        [SerializeField] private SpitballProjectile projectilePrefab;

        // Fire a spitball when the player uses this item.
        public override void Use(PlayerInventory inventory)
        {
            if (inventory == null || !inventory.HasItem(this))
                return;

            if (UsesRemaining <= 0 || projectilePrefab == null)
                return;

            ArenaParticipant shooter = inventory.GetComponent<ArenaParticipant>();

            if (shooter == null || !shooter.Alive ||
                shooter.Match == null || !shooter.Match.Playing ||
                shooter.Match.Paused)
                return;

            SpitballProjectile projectile = Instantiate(projectilePrefab);
            projectile.Initialize(shooter);

            ConsumeUse(inventory);
        }
    }
}
