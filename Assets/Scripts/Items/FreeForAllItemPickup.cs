
using Escape4Now.Arena;
using UnityEngine;

namespace Escape4Now.Items
{
    // Allows arena players to collect items by walking over them.
    public sealed class FreeForAllItemPickup : MonoBehaviour
    {
        [SerializeField] private FreeForAllItem item;
        [SerializeField, Min(0.1f)] private float pickupRadius = 0.6f;

        private bool collected;
        public bool Collected => collected;

        private void Awake()
        {
            if (item == null)
                item = GetComponent<FreeForAllItem>();
        }

        private void Update()
        {
            if (collected || item == null)
                return;

            ArenaMatch match = FindFirstObjectByType<KingOfTheHillController>();

            if (match == null || !match.Playing || match.Paused)
                return;

            foreach (ArenaParticipant participant in match.Players)
            {
                if (!participant.Alive)
                    continue;

                float distance = Vector2.Distance(
                    participant.transform.position,
                    transform.position
                );

                if (distance > pickupRadius)
                    continue;

                PlayerInventory inventory =
                    participant.GetComponent<PlayerInventory>();

                if (inventory == null)
                    continue;

                // Only collect an item when the inventory accepts it.
                bool canCollect = !inventory.IsFull || 
                (inventory.Items[0] is FreeForAllItem heldItem 
                && heldItem.ItemType == item.ItemType 
                && heldItem.UsesRemaining < heldItem.MaxUses);

                if (!canCollect)
                    continue;

                item.PickUp(inventory);
                collected = true;
                return;
            }
        }
    }
}
