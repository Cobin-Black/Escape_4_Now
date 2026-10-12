using UnityEngine;

namespace Escape4Now.Items
{
    public class KeyItem : Item
    {
        // Adds the key to the player's inventory.
        public override bool Collect(PlayerInventory inventory)
        {
            return inventory.AddKey(this);
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

        // Collects the key and moves it off-screen.
        public override void PickUp(PlayerInventory inventory)
        {
            if (Collect(inventory))
            {
                transform.position = new Vector3(0f, -100f, 0f);
            }
        }
    }
}