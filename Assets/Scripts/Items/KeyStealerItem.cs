using UnityEngine;
using Escape4Now.Map;

namespace Escape4Now.Items
{
    public sealed class KeyStealerItem : Item
    {
        [SerializeField] private Vector2Int gridPosition = new Vector2Int(5, 5);

        // Adds the item to the player's inventory.
        public override bool Collect(PlayerInventory inventory)
        {
            if (inventory == null)
            {
                return false;
            }

            return inventory.AddItem(this);
        }

        // Tries to steal a key from another player.
        public override void Use(PlayerInventory inventory)
        {
            Debug.Log("Key Stealer Use() was called.");

            if (inventory == null)
            {
                return;
            }

            PlayerInventory[] inventories =
                FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);

            foreach (PlayerInventory otherInventory in inventories)
            {
                if (otherInventory == null || otherInventory == inventory)
                {
                    continue;
                }

                if (!otherInventory.HasKey())
                {
                    continue;
                }

                if (otherInventory.KeyItems.Count == 0)
                {
                    continue;
                }

                // Gets the other player's key.
                KeyItem stolenKey = otherInventory.KeyItems[0];

                if (stolenKey == null)
                {
                    Debug.Log("The other player's key is missing.");
                    return;
                }

                if (inventory.HasKey())
                {
                    Debug.Log("You already have a key.");
                    return;
                }

                // Removes the key from the other player's inventory.
                if (!otherInventory.RemoveKey())
                {
                    return;
                }

                // Gives the key to this player and returns it if unsuccessful.
                if (!inventory.GiveKey(stolenKey))
                {
                    otherInventory.GiveKey(stolenKey);
                    return;
                }

                // Removes the Key Stealer item after successful use.
                inventory.RemoveItem(this);

                string otherPlayerName = otherInventory.Player != null
                    ? otherInventory.Player.name
                    : otherInventory.name;

                string playerName = inventory.Player != null
                    ? inventory.Player.name
                    : inventory.name;

                Debug.Log(
                    $"{playerName} stole the key from {otherPlayerName}."
                );

                return;
            }

            Debug.Log("No other player currently has the key.");
        }

        // Checks whether the item is at the given grid position.
        public override bool IsAtPosition(Vector2Int position)
        {
            return gridPosition == position;
        }

        // Sets the item's spawn position on the map.
        public void SetSpawnPosition(IsometricMapTemplate map, Vector2Int position)
        {
            gridPosition = position;

            if (map != null)
            {
                transform.position = map.GridToWorld(gridPosition);
            }
        }

        // Adds the item to the inventory and hides it from the map.
        public override void PickUp(PlayerInventory inventory)
        {
            if (Collect(inventory))
            {
                gameObject.SetActive(false);
            }
        }
    }
}