using System.Collections.Generic;
using UnityEngine;
using Escape4Now.Player;
using UnityEngine.InputSystem;


namespace Escape4Now.Items
{
    //Stores the items currently collected by the player.
    public sealed class PlayerInventory : MonoBehaviour
    {
        //Player who owns this inventory.
        [SerializeField] private PlayerCharacter player;

        //List of items currently owned by this player.
        private readonly List<Item> items = new List<Item>();

        //Use Item action from the project-wide Input System asset.
        private InputAction useItemAction;

        //Read-only access to the player's current items.
        public IReadOnlyList<Item> Items => items;

        //Provides access to the player who owns this inventory.
        public PlayerCharacter Player => player;

        //Player can only carry one item at a time.
        public bool IsFull => items.Count >= 1;

        //Adds an item to the player's inventory.
        public bool AddItem(Item item)
        {
            if (item == null)
                return false;

            if (IsFull)
                return false;

            items.Add(item);
            Debug.Log($"Picked up {item.ItemName}.");
            return true;
        }

        //Removes an item from the player's inventory.
        public bool RemoveItem(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return items.Remove(item);
        }

        //Checks whether the player currently has a specific item.
        public bool HasItem(Item item)
        {
            return item != null && items.Contains(item);
        }

        //Finds the Use Item action in the project-wide Input System asset (Assets/Settings/InputSystem_Actions).
        private void Awake()
        {
            useItemAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Use Item") : null;

            if (useItemAction == null)
            {
                Debug.LogWarning($"{name}: Input action 'Player/Use Item' was not found.");
            }
        }

        //Checks for the Use Item action each frame.
        private void Update()
        {
            //The shared keyboard belongs only to the player taking this turn.
            if (player == null || !player.CanReadTurnInput) return;
            if (useItemAction == null)
            {
                return;
            }

            if (useItemAction.WasPressedThisFrame())
            {
                UseFirstItem();
            }
        }

        //Uses the first item currently stored in the inventory.
        private void UseFirstItem()
        {
            if (items.Count == 0)
            {
                Debug.Log("Inventory is empty.");
                return;
            }

            Item item = items[0];

            if (item == null)
            {
                items.RemoveAt(0);
                return;
            }

            if (!player.CanUseItem())
            {
                Debug.Log("Finish your current movement before using an item.");
                return;
            }

            Debug.Log($"Using {item.ItemName}.");
            item.Use(this);

        }

    }
}
