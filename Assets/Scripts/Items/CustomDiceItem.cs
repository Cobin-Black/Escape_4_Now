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
        private bool ignoreConfirmInput;

        //Actions from the project-wide Input System asset used while choosing a number.
        private InputAction optionSelectAction;
        private InputAction useItemAction;

        //Finds the Item Option Select and Use Item actions in the project-wide Input System asset.
        private void Awake()
        {
            InputActionAsset actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogWarning($"{name}: No project-wide Input Actions asset is set, so Custom Dice cannot be controlled.");
                return;
            }

            optionSelectAction = actions.FindAction("Player/Item Option Select");
            useItemAction = actions.FindAction("Player/Use Item");
            if (optionSelectAction == null) Debug.LogWarning($"{name}: Input action 'Player/Item Option Select' was not found.");
            if (useItemAction == null) Debug.LogWarning($"{name}: Input action 'Player/Use Item' was not found.");
        }

        //Places the item on its assigned map tile.
        private void Start()
        {
            if (mapTemplate == null)
            {
                return;
            }

            transform.position = mapTemplate.GridToWorld(gridPosition);
        }

        public void SetSpawnPosition(IsometricMapTemplate map, Vector2Int position)
        {
            mapTemplate = map;
            gridPosition = position;

            if (mapTemplate != null)
            {
                transform.position = mapTemplate.GridToWorld(gridPosition);
            }
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
            ignoreConfirmInput = true;

            inventory.Player.SetUsingItem(true);

            Debug.Log("Custom Dice: Choose a number from 1 to 6.");
            Debug.Log($"Custom Dice selection: {selectedNumber}");
        }


        // Lets a bot use Custom Dice without opening the human selection controls.
        public bool UseForBot(PlayerInventory inventory)
        {
            if (inventory == null
                || !inventory.HasItem(this)
                || inventory.Player == null
                || !inventory.Player.CanUseItem()
                || isSelecting)
            {
                return false;
            }

            int selectedNumber = 6;

            inventory.Player.SetMovesFromItem(selectedNumber);
            inventory.RemoveItem(this);

            Debug.Log($"Bot used Custom Dice and selected {selectedNumber}.");

            return true;
        }


        // Displays the number-selection screen while using Custom Dice.
        private void OnGUI()
        {
            if (!isSelecting)
                return;

            float width = 320f;
            float height = 220f;

            Rect panel = new Rect(
                (Screen.width - width) / 2f,
                (Screen.height - height) / 2f,
                width,
                height);

            GUI.Box(panel, "");

            GUILayout.BeginArea(new Rect(
                panel.x + 20f,
                panel.y + 20f,
                panel.width - 40f,
                panel.height - 40f));

            GUILayout.Label("CUSTOM DICE");
            GUILayout.Space(15f);
            GUILayout.Label("Choose your number:");

            GUIStyle numberStyle = new GUIStyle(GUI.skin.label);
            numberStyle.fontSize = 48;
            numberStyle.alignment = TextAnchor.MiddleCenter;
            numberStyle.fontStyle = FontStyle.Bold;

            GUILayout.Label(selectedNumber.ToString(), numberStyle);

            GUILayout.Space(10f);
            GUILayout.Label("Use Left/Right to change the number.");
            GUILayout.Label("Press E to confirm.");

            GUILayout.EndArea();
        }

        //Handles Item Option Select (left/right) and Use Item input while the player is choosing a number.
        private void Update()
        {
            if (!isSelecting)
                return;

            if (ignoreConfirmInput)
            {
                ignoreConfirmInput = false;
                return;
            }

            int step = ReadHorizontalPressedThisFrame();

            if (step > 0)
            {
                selectedNumber++;

                if (selectedNumber > 6)
                {
                    selectedNumber = 1;
                }

                Debug.Log($"Custom Dice selection: {selectedNumber}");
            }

            if (step < 0)
            {
                selectedNumber--;

                if (selectedNumber < 1)
                {
                    selectedNumber = 6;
                }

                Debug.Log($"Custom Dice selection: {selectedNumber}");
            }

            if (useItemAction != null && useItemAction.WasPressedThisFrame())
            {
                ConfirmSelection();
            }
        }

        //Returns 1 for right, -1 for left, or 0 when Item Option Select wasn't pressed this frame.
        private int ReadHorizontalPressedThisFrame()
        {
            if (optionSelectAction == null || !optionSelectAction.WasPressedThisFrame())
                return 0;

            float x = optionSelectAction.ReadValue<Vector2>().x;

            if (Mathf.Approximately(x, 0f))
                return 0;

            return x > 0f ? 1 : -1;
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