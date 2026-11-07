
using UnityEngine;

namespace Escape4Now.Items
{
    // Base class for FreeForAll items with multiple uses.
    public abstract class FreeForAllItem : Item
    {
        [SerializeField] private FreeForAllItemType itemType;

        [SerializeField, Min(1)] private int maxUses = 3;

        public FreeForAllItemType ItemType => itemType;
        public int MaxUses => maxUses;
        public int UsesRemaining { get; private set; }

        private void Awake()
        {
            UsesRemaining = maxUses;
        }

        // Collect the item or refill a matching item.
        public override bool Collect(PlayerInventory inventory)
        {
            if (inventory == null)
                return false;

            if (!inventory.IsFull)
                return inventory.AddItem(this);

            if (inventory.Items[0] is FreeForAllItem heldItem 
            && heldItem.ItemType == itemType 
            && heldItem.UsesRemaining < heldItem.MaxUses)
            {
                heldItem.Refill();
                return true;
            }

            return false;
        }

        // Restore all item uses.
        public void Refill()
        {
            UsesRemaining = maxUses;
        }

        // Consume one use and remove the item when empty.
        protected void ConsumeUse(PlayerInventory inventory)
        {
            if (inventory == null || !inventory.HasItem(this))
                return;

            if (UsesRemaining <= 0)
                return;

            UsesRemaining--;

            if (UsesRemaining == 0)
            {
                inventory.RemoveItem(this);
                Destroy(gameObject);
            }
        }

        // Arena pickups will use continuous positions instead of grid tiles.
        public override bool IsAtPosition(Vector2Int position)
        {
            return false;
        }

        // Collect the item and hide its world appearance.
        public override void PickUp(PlayerInventory inventory)
        {
            if (!Collect(inventory))
                return;

            SpriteRenderer sprite = GetComponent<SpriteRenderer>();

            if (sprite != null)
                sprite.enabled = false;

            Collider2D itemCollider = GetComponent<Collider2D>();

            if (itemCollider != null)
                itemCollider.enabled = false;
        }
    }
}
