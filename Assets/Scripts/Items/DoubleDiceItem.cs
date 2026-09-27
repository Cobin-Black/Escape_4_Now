using UnityEngine;
using Escape4Now.Map;

namespace Escape4Now.Items
{
    //Allows the player to roll two dice at once for a single turn.
    public sealed class DoubleDiceItem : Item
    {
        //Map used to convert the item's grid position into a world position.
        [SerializeField] private IsometricMapTemplate mapTemplate;

        //Tile where the Double Dice item is placed.
        [SerializeField] private Vector2Int gridPosition = new Vector2Int(5, 5);

        //Tracks whether this item has already been used.
        private bool hasBeenUsed;

        //Places the item on its assigned map tile.
        private void Start()
        {
            if (mapTemplate == null)
            {
                return;
            }

            transform.position = mapTemplate.GridToWorld(gridPosition);
        }

        //Checks whether the item is on the specified tile.
        public override bool IsAtPosition(Vector2Int position)
        {
            return gridPosition == position;
        }

        // Adds the Double Dice item to the player's inventory.
        public override bool Collect(PlayerInventory inventory)
        {
            if (inventory == null)
                return false;

            return inventory.AddItem(this);
        }

        //Collects this item and removes it from the map.
        public override void PickUp(PlayerInventory inventory)
        {
            if (inventory == null)
            {
                Debug.LogWarning("[Item System] Double Dice could not be picked up because inventory is null.");
                return;
            }

            if (Collect(inventory))
            {
                Debug.Log("[Item System] Double Dice successfully added to inventory.");
                gameObject.SetActive(false);
            }
            else
            {
                Debug.Log("[Item System] Double Dice pickup failed. Inventory may already be full.");
            }
        }

        //Rolls two six-sided dice and uses their combined result.
        //Rolls two six-sided dice and gives the player their combined movement.
        public override void Use(PlayerInventory inventory)
        {
            if (hasBeenUsed)
            {
                Debug.Log("Double Dice has already been used.");
                return;
            }

            if (inventory == null || !inventory.HasItem(this))
            {
                return;
            }

            if (inventory.Player == null)
            {
                Debug.LogWarning("Double Dice could not find the player.");
                return;
            }

            int firstRoll = Random.Range(1, 7);
            int secondRoll = Random.Range(1, 7);
            int totalRoll = firstRoll + secondRoll;

            hasBeenUsed = true;

            inventory.Player.SetMovesFromItem(totalRoll);

            Debug.Log($"Double Dice rolled {firstRoll} and {secondRoll}. Total: {totalRoll}");

            inventory.RemoveItem(this);
        }

    }
}
