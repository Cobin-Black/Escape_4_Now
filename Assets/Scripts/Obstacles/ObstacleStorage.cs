using Escape4Now.Items;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.Obstacles
{
    //Holds one item inside an interactable obstacle and shows a simple menu when a player next to it presses Interact.
    [RequireComponent(typeof(ObstacleGridControl))]
    public sealed class ObstacleStorage : MonoBehaviour
    {
        //Item prefab stored inside. Empty means nothing is left in the obstacle.
        [SerializeField] private Item storedItemPrefab;

        //Only one obstacle menu can be open at a time, even if a player stands next to several.
        private static ObstacleStorage openStorage;
        private static int lastMenuChangeFrame = -1;

        private ObstacleGridControl gridControl;
        private PlayerCharacter[] players;
        private PlayerCharacter viewingPlayer;
        private InputAction interactAction;
        private GUIStyle menuTextStyle;
        private GUIStyle menuTitleStyle;

        //Read-only contents for other scripts.
        public Item StoredItemPrefab => storedItemPrefab;
        public bool HasItem => storedItemPrefab != null;
        public bool IsMenuOpen => openStorage == this;

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

        //Opens, takes from, or closes the menu on the Interact action.
        private void Update()
        {
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

            if (openStorage != null || gridControl == null || !gridControl.IsInteractable)
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
            Vector2Int offset = cell - gridControl.GridPosition;
            return Mathf.Abs(offset.x) + Mathf.Abs(offset.y) == 1;
        }

        //Keeps the menu open only while its player is still next to the obstacle on their own turn.
        private bool CanKeepMenuOpen()
        {
            return viewingPlayer != null && viewingPlayer.isActiveAndEnabled && viewingPlayer.IsCurrentTurn
                && gridControl != null && gridControl.IsInteractable && IsNextTo(viewingPlayer.GridPosition);
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

        //Moves the stored item into the player's inventory when there is room, then closes the menu.
        private void TakeItemOrClose()
        {
            PlayerInventory inventory = viewingPlayer.Inventory;

            if (HasItem && inventory != null && !inventory.IsFull)
            {
                //The obstacle only stores the prefab, so a real copy is made at the moment it is taken.
                Item item = Instantiate(storedItemPrefab, transform.position, Quaternion.identity, transform.parent);
                item.PickUp(inventory);

                if (inventory.HasItem(item))
                {
                    Debug.Log($"[Item System] {viewingPlayer.name} took {item.ItemName} from {name}.");
                    storedItemPrefab = null;
                }
                else
                {
                    Destroy(item.gameObject);
                }
            }

            CloseMenu();
        }

        //Draws a plain placeholder menu in the middle of the screen.
        private void OnGUI()
        {
            if (!IsMenuOpen)
            {
                return;
            }

            if (menuTextStyle == null)
            {
                menuTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    wordWrap = true,
                    alignment = TextAnchor.UpperCenter
                };
                menuTextStyle.normal.textColor = Color.white;
            }

            //Scales the menu with the window size, like the rest of the HUD.
            Matrix4x4 oldMatrix = Escape4Now.UI.HudScale.Apply();
            const float menuWidth = 360f;
            const float menuHeight = 180f;
            Rect menuRect = new Rect((Escape4Now.UI.HudScale.Width - menuWidth) * 0.5f, (Escape4Now.UI.HudScale.Height - menuHeight) * 0.5f, menuWidth, menuHeight);

            //Dark panel with a light border and the obstacle's name as a header.
            Color oldColor = GUI.color;
            GUI.color = new Color(0.5f, 0.77f, 1f, 1f);
            GUI.DrawTexture(new Rect(menuRect.x - 2f, menuRect.y - 2f, menuRect.width + 4f, menuRect.height + 4f), Texture2D.whiteTexture);
            GUI.color = new Color(0.02f, 0.04f, 0.08f, 0.96f);
            GUI.DrawTexture(menuRect, Texture2D.whiteTexture);
            GUI.color = oldColor;
            if (menuTitleStyle == null)
            {
                menuTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                menuTitleStyle.normal.textColor = new Color(0.65f, 0.85f, 1f);
            }
            GUI.Label(new Rect(menuRect.x, menuRect.y + 6f, menuRect.width, 22f), name.ToUpper(), menuTitleStyle);

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
                string action = inventory != null && !inventory.IsFull
                    ? $"Press {key} to take it."
                    : $"You can only carry one item.\nPress {key} to close.";
                body = $"Inside: {storedItemPrefab.ItemName}{description}\n\n{action}";
            }

            GUI.Label(new Rect(menuRect.x + 16f, menuRect.y + 36f, menuWidth - 32f, menuHeight - 44f), body, menuTextStyle);
            GUI.matrix = oldMatrix;
        }
    }
}
