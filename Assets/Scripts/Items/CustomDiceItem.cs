using UnityEngine;
using UnityEngine.InputSystem;
using Escape4Now.Map;
using Escape4Now.Player;

namespace Escape4Now.Items
{
    //Allows the player to choose a movement value from 1 to 6.
    public sealed class CustomDiceItem : Item
    {
        //Map used to convert the item's grid position into a world position.
        [SerializeField] private IsometricMapTemplate mapTemplate;

        //Tile where the Custom Dice item is placed.
        [SerializeField] private Vector2Int gridPosition = new Vector2Int(6, 5);

        //Currently selected movement value.
        [SerializeField] private int selectedNumber = 1;

        //Whether the player is currently choosing a number.
        private bool isSelecting;

        //Inventory that owns this item while it is being selected.
        private PlayerInventory currentInventory;

        private bool hasBeenCollected;

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
            return !hasBeenCollected && gridPosition == position;
        }

        //Collects this item and removes it from the map.
        public override void PickUp(PlayerInventory inventory)
        {
            if (inventory == null)
            {
                Debug.LogWarning("[Item System] Custom Dice could not be picked up because inventory is null.");
                return;
            }

            if (Collect(inventory))
            {
                Debug.Log("[Item System] Custom Dice successfully added to inventory.");

                hasBeenCollected = true;

                // Hide all visuals.
                Renderer[] renderers = GetComponentsInChildren<Renderer>();

                foreach (Renderer renderer in renderers)
                {
                    renderer.enabled = false;
                }

                // Disable all colliders.
                Collider2D[] colliders = GetComponentsInChildren<Collider2D>();

                foreach (Collider2D collider in colliders)
                {
                    collider.enabled = false;
                }
            }
            else
            {
                Debug.Log("[Item System] Custom Dice pickup failed. Inventory may already be full.");
            }
        }

        //Adds the Custom Dice item to the player's inventory.
        public override bool Collect(PlayerInventory inventory)
        {
            if (inventory == null)
                return false;

            return inventory.AddItem(this);
        }

        //Starts the number-selection process.
        public override void Use(PlayerInventory inventory)
        {
            if (inventory == null || !inventory.HasItem(this))
                return;

            if (inventory.Player == null)
            {
                Debug.LogWarning("Custom Dice could not find the player.");
                return;
            }

            if (isSelecting)
                return;

            currentInventory = inventory;
            selectedNumber = 1;
            isSelecting = true;

            inventory.Player.SetUsingItem(true);

            Debug.Log("Custom Dice: Choose a number from 1 to 6.");
            Debug.Log($"Custom Dice selection: {selectedNumber}");
        }

        //Handles keyboard input while the player is choosing a number.
        private void Update()
        {
            if (!isSelecting)
                return;

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
                return;

            if (keyboard.dKey.wasPressedThisFrame ||
                keyboard.rightArrowKey.wasPressedThisFrame)
            {
                selectedNumber++;

                if (selectedNumber > 6)
                {
                    selectedNumber = 1;
                }

                Debug.Log($"Custom Dice selection: {selectedNumber}");
            }

            if (keyboard.aKey.wasPressedThisFrame ||
                keyboard.leftArrowKey.wasPressedThisFrame)
            {
                selectedNumber--;

                if (selectedNumber < 1)
                {
                    selectedNumber = 6;
                }

                Debug.Log($"Custom Dice selection: {selectedNumber}");
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                ConfirmSelection();
            }
        }

        //Confirms the selected number and gives the player that many moves.
        private void ConfirmSelection()
        {
            if (currentInventory == null || currentInventory.Player == null)
            {
                CancelSelection();
                return;
            }

            PlayerCharacter player = currentInventory.Player;

            player.SetMovesFromItem(selectedNumber);

            currentInventory.RemoveItem(this);

            Debug.Log($"Custom Dice selected {selectedNumber}.");

            // Restore normal player controls.
            player.SetUsingItem(false);

            isSelecting = false;
            currentInventory = null;

            Debug.Log("Custom Dice: Player controls restored.");
        }

        //Ends the current number-selection process.
        private void CancelSelection()
        {
            if (currentInventory != null && currentInventory.Player != null)
            {
                currentInventory.Player.SetUsingItem(false);
                Debug.Log("Custom Dice: Player controls restored.");
            }

            isSelecting = false;
            currentInventory = null;
        }
    }
}