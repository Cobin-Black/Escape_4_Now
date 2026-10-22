using Escape4Now.Items;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.Obstacles
{
    //Stores one item and owns the adjacent player's interaction state; StorageMenuUI presents it.
    public sealed class ObstacleStorage : MonoBehaviour
    {
        //Item prefab stored inside. Empty means nothing is left in the obstacle.
        [SerializeField] private Item storedItemPrefab;
        //Only one obstacle menu can be open at a time, even if a player stands next to several.
        private static ObstacleStorage openStorage;
        private static int lastMenuChangeFrame = -1;

        private ObstacleGridControl gridControl;
        private Escape4Now.Map.SchoolFurniture furniture;
        private PlayerCharacter[] players;
        private PlayerCharacter viewingPlayer;
        private InputAction interactAction;
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
            var bindings = Escape4Now.Input.GameplayInputContext.Current;
            interactAction = bindings != null ? bindings.Interact
                : null;

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
                if (player == null || player.IsBotControlled || !player.isActiveAndEnabled || !player.IsCurrentTurn
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

        public static ObstacleStorage OpenStorage => openStorage;
        public PlayerCharacter ViewingPlayer => viewingPlayer;

        public void ConfirmMenu()
        {
            if (!IsMenuOpen || !CanKeepMenuOpen() || Time.timeScale <= 0f
                || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused) return;
            TakeItemOrClose();
        }

        public void DismissMenu()
        {
            if (Time.timeScale > 0f && !Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused)
                CloseMenu();
        }
    }
}
