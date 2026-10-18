using Escape4Now.Items;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.Obstacles
{
    //Holds one item inside an interactable obstacle and shows a simple menu when a player next to it presses Interact.
    public sealed class ObstacleStorage : MonoBehaviour
    {
        //Item prefab stored inside. Empty means nothing is left in the obstacle.
        [SerializeField] private Item storedItemPrefab;
        [Header("Location Assist Markers")]
        [SerializeField] private Texture2D itemIndicatorIcon;
        [SerializeField] private Texture2D keyIndicatorIcon;
        [SerializeField] private Vector3 locationMarkerOffset = new Vector3(0f, 1.1f, 0f);
        [SerializeField] private Vector2 locationMarkerSize = new Vector2(104f, 26f);
        [SerializeField] private Color itemIndicatorColor = new Color(0.35f, 0.85f, 1f, 1f);
        [SerializeField] private Color keyIndicatorColor = new Color(1f, 0.78f, 0.2f, 1f);
        [SerializeField, Range(8, 24)] private int locationMarkerFontSize = 12;
        [Header("Obstacle Menu")]
        [SerializeField, Min(200f)] private float menuWidth = 360f;
        [SerializeField, Min(120f)] private float menuHeight = 180f;
        [SerializeField, Range(10, 36)] private int menuFontSize = 18;
        [SerializeField] private Color menuTextColor = Color.white;
        [SerializeField, Min(0f)] private float menuContentPadding = 16f;
        [SerializeField, Min(0f)] private float menuTitleOffset = 32f;

        //Only one obstacle menu can be open at a time, even if a player stands next to several.
        private static ObstacleStorage openStorage;
        private static int lastMenuChangeFrame = -1;

        private ObstacleGridControl gridControl;
        private Escape4Now.Map.SchoolFurniture furniture;
        private PlayerCharacter[] players;
        private PlayerCharacter viewingPlayer;
        private InputAction interactAction;
        private GUIStyle menuTextStyle;
        private GUIStyle debugMarkerStyle;
        private bool networkHasItem;
        private bool networkContainsKey;

        //Read-only contents for other scripts.
        public Item StoredItemPrefab => storedItemPrefab;
        public bool HasItem => Escape4Now.Multiplayer.LanMultiplayerSession.IsClient ? networkHasItem : storedItemPrefab != null;
        public bool ContainsKey => Escape4Now.Multiplayer.LanMultiplayerSession.IsClient
            ? networkContainsKey : storedItemPrefab is KeyItem;
        public bool IsMenuOpen => openStorage == this;
        public Vector2Int GridPosition => gridControl != null ? gridControl.GridPosition : furniture != null ? furniture.Cell : new Vector2Int(-1, -1);
        public bool CanInteract => gridControl != null ? gridControl.IsInteractable : furniture != null;

        // Tracks whether any player has searched this obstacle.
        private bool hasBeenSearched;

        // Read-only search status shared by all bots.
        public bool HasBeenSearched => hasBeenSearched;

        private void OnValidate()
        {
            locationMarkerSize.x = Mathf.Clamp(locationMarkerSize.x, 16f, 240f);
            locationMarkerSize.y = Mathf.Clamp(locationMarkerSize.y, 16f, 120f);
            locationMarkerFontSize = Mathf.Clamp(locationMarkerFontSize, 8, 24);
            menuWidth = Mathf.Clamp(menuWidth, 200f, 900f);
            menuHeight = Mathf.Clamp(menuHeight, 120f, 700f);
            menuFontSize = Mathf.Clamp(menuFontSize, 10, 36);
            menuContentPadding = Mathf.Clamp(menuContentPadding, 0f, 80f);
            menuTitleOffset = Mathf.Clamp(menuTitleOffset, 0f, 160f);
            menuTextStyle = null;
        }

        //Clears the shared menu state when Play mode starts, even if the editor skips reloading scripts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedState()
        {
            openStorage = null;
            lastMenuChangeFrame = -1;
        }

        //Finds the obstacle controller and the Interact action in the project-wide Input System asset.
        private void Awake()
        {
            gridControl = GetComponent<ObstacleGridControl>();
            furniture = GetComponent<Escape4Now.Map.SchoolFurniture>();
            interactAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Interact") : null;

            if (interactAction == null)
            {
                Debug.LogWarning($"{name}: Input action 'Player/Interact' was not found.");
            }
        }

        //Finds the players that can open this obstacle.
        private void Start()
        {
            players = FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);
        }

        //Closes the menu if the obstacle is turned off while it is open.
        private void OnDisable()
        {
            CloseMenu();
        }

        //Puts one item prefab inside, replacing anything already stored.
        public void SetStoredItem(Item itemPrefab)
        {
            storedItemPrefab = itemPrefab;
        }

        //Stores an item only when this obstacle is empty.
        public bool TryStoreItem(Item itemPrefab)
        {
            if (itemPrefab == null || HasItem) return false;
            storedItemPrefab = itemPrefab;
            return true;
        }

        //Guests see whether storage is empty; only the host gives out real items.
        public void ApplyNetworkContents(bool hasItem, bool containsKey = false)
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.IsClient)
            {
                networkHasItem = hasItem;
                networkContainsKey = containsKey;
            }
        }

        //Opens, takes from, or closes the menu on the Interact action.
        private void Update()
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.Active || Time.timeScale <= 0f
                || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused) return;
            if (IsMenuOpen && !CanKeepMenuOpen())
            {
                CloseMenu();
                return;
            }

            if (interactAction == null || !interactAction.WasPressedThisFrame())
            {
                return;
            }

            //Ignore a press that another obstacle already used to open or close its menu this frame.
            if (lastMenuChangeFrame == Time.frameCount)
            {
                return;
            }

            if (IsMenuOpen)
            {
                TakeItemOrClose();
                return;
            }

            if (openStorage != null || !CanInteract)
            {
                return;
            }

            PlayerCharacter player = FindAdjacentActivePlayer();
            if (player != null)
            {
                OpenMenu(player);
            }
        }

        //Finds the player whose turn it is, if they are standing right next to this obstacle and not busy.
        private PlayerCharacter FindAdjacentActivePlayer()
        {
            if (players == null)
            {
                return null;
            }

            foreach (PlayerCharacter player in players)
            {
                if (player == null || !player.isActiveAndEnabled || !player.IsCurrentTurn
                    || player.IsMoving || player.HasReachedExit || player.IsUsingItem() || player.IsInMenu())
                {
                    continue;
                }

                if (IsNextTo(player.GridPosition))
                {
                    return player;
                }
            }

            return null;
        }

        //Checks the four tiles that share a side with this obstacle.
        private bool IsNextTo(Vector2Int cell)
        {
            Vector2Int offset = cell - GridPosition;
            return Mathf.Abs(offset.x) + Mathf.Abs(offset.y) == 1;
        }

        //The host checks adjacency before giving a remote player the stored item.
        public bool TakeNetworkItem(PlayerCharacter player)
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.IsClient || player == null || !player.isActiveAndEnabled
                || !player.IsCurrentTurn || player.IsMoving || player.HasReachedExit
                || player.IsUsingItem() || player.IsInMenu() || !CanInteract
                || !IsNextTo(player.GridPosition)
                || player.Inventory == null || !HasItem
                || (storedItemPrefab is KeyItem ? player.Inventory.HasKey() : player.Inventory.IsFull)) return false;
            OpenMenu(player);
            TakeItemOrClose();
            return true;
        }

        //Keeps the menu open only while its player is still next to the obstacle on their own turn.
        private bool CanKeepMenuOpen()
        {
            return viewingPlayer != null && viewingPlayer.isActiveAndEnabled && viewingPlayer.IsCurrentTurn
                && CanInteract && IsNextTo(viewingPlayer.GridPosition);
        }

        //Shows the contents and pauses the player's other controls.
        private void OpenMenu(PlayerCharacter player)
        {
            openStorage = this;
            lastMenuChangeFrame = Time.frameCount;
            viewingPlayer = player;
            viewingPlayer.SetInMenu(true);
        }

        //Hides the menu and gives the player their controls back.
        private void CloseMenu()
        {
            if (!IsMenuOpen)
            {
                return;
            }

            if (viewingPlayer != null)
            {
                viewingPlayer.SetInMenu(false);
            }

            openStorage = null;
            lastMenuChangeFrame = Time.frameCount;
            viewingPlayer = null;
        }


        // Moves the stored item into the player's inventory when possible.
        private void TakeItemOrClose()
        {
            if (viewingPlayer == null)
            {
                CloseMenu();
                return;
            }

            PlayerInventory inventory = viewingPlayer.Inventory;
            bool isKey = storedItemPrefab is KeyItem;

            // An empty obstacle has already been searched.
            if (!HasItem)
            {
                hasBeenSearched = true;
            }
            else if (inventory != null
                && (isKey ? !inventory.HasKey() : !inventory.IsFull))
            {
                // Create a real copy of the stored item.
                Item item = Instantiate(
                    storedItemPrefab,
                    transform.position,
                    Quaternion.identity,
                    transform.parent);

                item.PickUp(inventory);

                bool wasTaken = isKey
                    ? inventory.HasKey()
                        && inventory.KeyItems[0] == item
                    : inventory.HasItem(item);

                if (wasTaken)
                {
                    Debug.Log(
                        $"[Item System] {viewingPlayer.name} took {item.ItemName} from {name}.");

                    storedItemPrefab = null;
                    hasBeenSearched = true;
                }
                else
                {
                    Destroy(item.gameObject);
                }
            }

            CloseMenu();
        }

        // Lets a bot search this object using the existing storage rules.
        public bool SearchForBot(PlayerCharacter player)
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.Active
                || player == null
                || !player.isActiveAndEnabled
                || !player.IsCurrentTurn
                || player.IsMoving
                || player.HasReachedExit
                || player.IsUsingItem()
                || player.IsInMenu()
                || !CanInteract
                || !IsNextTo(player.GridPosition)
                || player.Inventory == null)
            {
                return false;
            }

            if (HasItem)
            {
                bool isKey = StoredItemPrefab is KeyItem;

                if (isKey && player.Inventory.HasKey())
                    return false;

                if (!isKey && player.Inventory.IsFull)
                    return false;
            }

            OpenMenu(player);
            TakeItemOrClose();
            return true;
        }

        //Draws a plain placeholder menu in the middle of the screen.
        private void OnGUI()
        {
            if (Escape4Now.Multiplayer.LocalMultiplayerSession.MatchHasEnded) return;
            bool keyVisible = Escape4Now.Multiplayer.LocalMultiplayerSession.ShowKeyLocation && ContainsKey;
            bool itemVisible = Escape4Now.Multiplayer.LocalMultiplayerSession.ShowItemObstacles && HasItem;
            if (keyVisible || itemVisible)
            {
                Camera viewCamera = Camera.main;
                if (viewCamera != null)
                {
                    Vector3 screenPoint = viewCamera.WorldToScreenPoint(transform.position + locationMarkerOffset);
                    if (screenPoint.z > 0f)
                    {
                        string label = keyVisible ? "KEY LOCATION" : "ITEM";
                        Rect marker = new Rect(screenPoint.x - locationMarkerSize.x * 0.5f,
                            Screen.height - screenPoint.y - locationMarkerSize.y * 0.5f,
                            locationMarkerSize.x, locationMarkerSize.y);
                        Texture2D icon = keyVisible ? keyIndicatorIcon : itemIndicatorIcon;
                        debugMarkerStyle ??= new GUIStyle(GUI.skin.box)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = locationMarkerFontSize,
                            fontStyle = FontStyle.Bold
                        };
                        Color oldColor = GUI.color;
                        GUI.color = keyVisible ? keyIndicatorColor : itemIndicatorColor;
                        if (icon != null) GUI.DrawTexture(marker, icon, ScaleMode.ScaleToFit, true);
                        else GUI.Box(marker, label, debugMarkerStyle);
                        GUI.color = oldColor;
                    }
                }
            }

            if (!IsMenuOpen)
            {
                return;
            }

            if (menuTextStyle == null)
            {
                menuTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = menuFontSize,
                    wordWrap = true,
                    alignment = TextAnchor.UpperCenter
                };
                menuTextStyle.normal.textColor = menuTextColor;
            }

            Rect menuRect = new Rect((Screen.width - menuWidth) * 0.5f, (Screen.height - menuHeight) * 0.5f, menuWidth, menuHeight);
            GUI.Box(menuRect, name);

            string key = interactAction != null ? interactAction.GetBindingDisplayString() : "Interact";
            string body;

            if (!HasItem)
            {
                body = $"It's empty.\n\nPress {key} to close.";
            }
            else
            {
                string description = string.IsNullOrEmpty(storedItemPrefab.Description) ? "" : $"\n{storedItemPrefab.Description}";
                PlayerInventory inventory = viewingPlayer != null ? viewingPlayer.Inventory : null;
                bool isKey = storedItemPrefab is KeyItem;
                string action = inventory != null && (isKey ? !inventory.HasKey() : !inventory.IsFull)
                    ? $"Press {key} to take it."
                    : $"You can only carry one item.\nPress {key} to close.";
                body = $"Inside: {storedItemPrefab.ItemName}{description}\n\n{action}";
            }

            GUI.Label(new Rect(menuRect.x + menuContentPadding, menuRect.y + menuTitleOffset,
                menuWidth - menuContentPadding * 2f, menuHeight - menuTitleOffset - menuContentPadding), body, menuTextStyle);
        }
    }
}
