// Stores the Race key in a player's inventory; the Race mode decides whether that player may escape.

using UnityEngine;

namespace Escape4Now.Items
{
    public class KeyItem : Item
    {
        // Adds the key to the player's inventory.
        public override bool Collect(PlayerInventory inventory)
        {
            return inventory != null && inventory.AddKey(this);
        }

        // Does nothing when the key is used.
        public override void Use(PlayerInventory inventory)
        {
        }

        // Returns false because the key is stored in a locker.
        public override bool IsAtPosition(Vector2Int position)
        {
            return false;
        }

        //Hides the collected key so it is not sent as a floor pickup.
        public override void PickUp(PlayerInventory inventory)
        {
            if (Collect(inventory))
            {
                gameObject.SetActive(false);
            }
        }
    }
}
