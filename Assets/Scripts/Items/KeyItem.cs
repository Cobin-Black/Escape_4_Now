using UnityEngine;

namespace Escape4Now.Items
{
    public class KeyItem : Item
    {
        public override bool Collect(PlayerInventory inventory)
        {
            
            return inventory.AddKey(this);
        }

        public override void Use(PlayerInventory inventory)
        {
        }

        public override bool IsAtPosition(Vector2Int position)
        {
            return false;
        }

        public override void PickUp(PlayerInventory inventory)
        {
            if (Collect(inventory))
            {
                Destroy(gameObject);
            }
        }
    }
}