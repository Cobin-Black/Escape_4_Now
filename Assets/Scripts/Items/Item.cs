using UnityEngine;

namespace Escape4Now.Items
{
    //Base class for all collectible and usable items in the game.
    public abstract class Item : MonoBehaviour
    {
        //Basic information displayed or used by the inventory.
        [SerializeField] private string itemName = "Item";
        [SerializeField] private string description = "";

        //Read-only item information for other scripts.
        public string ItemName => itemName;
        public string Description => description;

        //Adds this item to the player's inventory when collected.
        public abstract bool Collect(PlayerInventory inventory);

        //Uses this item when the player activates it from the inventory.
        public abstract void Use(PlayerInventory inventory);

        public abstract bool IsAtPosition(Vector2Int position);
        public abstract void PickUp(PlayerInventory inventory);
    }
}
