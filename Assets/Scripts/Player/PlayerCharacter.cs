using Escape4Now.Map;
using Escape4Now.TurnSystem;
using Escape4Now.Items;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;

namespace Escape4Now.Player
{
    //Draws the player, rolls its move budget, and moves it between floor tiles using the Input System's Player actions.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerCharacter : MonoBehaviour
    {
        //Starting tile, appearance, and movement speed.
        [SerializeField] private IsometricMapTemplate mapTemplate;
        [SerializeField] private TurnSystemController turnSystem;
        [SerializeField] private Vector2Int gridPosition = new Vector2Int(2, 2);
        [SerializeField] private Color playerColor = Color.black;
        [SerializeField] private Color outlineColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        [SerializeField, Min(0.1f)] private float markerWidth = 0.38f;
        [SerializeField, Min(0.1f)] private float markerHeight = 0.72f;
        [SerializeField, Min(0.1f)] private float moveSpeed = 4f;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private int health = 5;

        private SpriteRenderer spriteRenderer;
        private Coroutine moveRoutine;
        private Sprite markerSprite;
        private Texture2D markerTexture;
        private Vector2Int registeredPosition;
        private bool hasReachedExit;
        private bool hasRolled;
        private int lastRoll;
        private int movesRemaining;
        private GUIStyle rollDisplayStyle;
        private MapEventController mapEvents;
        private Vector2Int lastDirection = Vector2Int.up;
        private bool resolvingStep;
        private readonly PlayerEventState eventState = new PlayerEventState();
        private InputAction moveAction;
        private InputAction rollDiceAction;

        //Keeps event effects separate for each player.
        public PlayerEventState EventState => eventState;
        private bool isUsingItem;
        private bool isInMenu;
        public PlayerInventory Inventory => inventory;
        public bool HasReachedExit => hasReachedExit;
        public bool HasRolled => hasRolled;
        //new thing 
        public int LastRoll => lastRoll;
        public int MovesRemaining => movesRemaining;
        internal bool IsResolvingEventStep => resolvingStep;
        public bool IsCurrentTurn => turnSystem == null || turnSystem.IsPlayersTurn(this);

        //Read-only movement state for the turn and map scripts.
        public Vector2Int GridPosition => gridPosition;
        public bool IsMoving => moveRoutine != null;

        //Checks settings and places the player on its starting tile.
        private void Awake()
        {
            ValidateSettings();
            SetupInputActions();
            SetupMarker();
            SnapToGridPosition();
            registeredPosition = gridPosition;
            if (mapTemplate != null)
            {
                mapEvents = mapTemplate.GetComponent<MapEventController>();
                mapTemplate.SetOccupantPosition(gridPosition, gridPosition);
            }
        }

        //Reads dice roll and movement key input each frame.
        private void Update()
        {
            if (hasReachedExit)
            {
                return;
            }
            if (isUsingItem || isInMenu)
            {
                return;
            }

            HandleDiceRollInput();

            Vector2Int direction = ReadDirectionPressedThisFrame();
            if (direction != Vector2Int.zero)
            {
                TryMoveInDirection(direction);
            }
        }

        //Draws the roll prompt or the current move budget.
        private void OnGUI()
        {
            if (turnSystem != null && !turnSystem.IsPlayersTurn(this)) return;

            // Main panel
            GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = MakeTransparentTexture(new Color(0.02f, 0.04f, 0.08f, 0.88f));

            //header style
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 13;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            headerStyle.normal.textColor = new Color(0.65f, 0.85f, 1f);

            // Player name style 
            GUIStyle playerStyle = new GUIStyle(GUI.skin.label);
            playerStyle.fontSize = 22;
            playerStyle.fontStyle = FontStyle.Bold;
            playerStyle.alignment = TextAnchor.MiddleCenter;
            playerStyle.normal.textColor = Color.white;

            // Large roll number 
            GUIStyle rollStyle = new GUIStyle(GUI.skin.label);
            rollStyle.fontSize = 42;
            rollStyle.fontStyle = FontStyle.Bold;
            rollStyle.alignment = TextAnchor.MiddleCenter;
            rollStyle.normal.textColor = Color.white;

            //Small label style
            GUIStyle smallStyle = new GUIStyle(GUI.skin.label);
            smallStyle.fontSize = 12;
            smallStyle.fontStyle = FontStyle.Bold;
            smallStyle.alignment = TextAnchor.MiddleCenter;
            smallStyle.normal.textColor = new Color(0.7f, 0.75f, 0.8f);

            GUIStyle promptStyle = new GUIStyle(smallStyle);
            promptStyle.fontSize = 16;
            promptStyle.normal.textColor = Color.white;

            string turnName = turnSystem != null ? turnSystem.CurrentTurnName : gameObject.name;

            float panelX = 16f, panelY = 85f, panelW = 360f, panelH = 235f;
            float padding = 16f;
            float x = panelX + padding;
            float contentW = panelW - padding * 2f;
            float colW = contentW / 2f;

            float moveOffset = -6f;

            GUI.Box(new Rect(panelX, panelY, panelW, panelH), "", panelStyle);



            //header
            GUI.Label(new Rect(x, 95f, contentW, 22f), "CURRENT TURN", headerStyle);

            // Player name
            GUI.Label(new Rect(x, 114f, contentW, 34f), turnName.ToUpper(), playerStyle);

            //Divder
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(new Rect(x + 15f, 164f, contentW - 30f, 2f), Texture2D.whiteTexture);
            GUI.color = oldColor;


            if (hasReachedExit)
            {
                GUI.Label(new Rect(x, 180f, contentW, 50f), "EXIT REACHED", playerStyle);
            }
            else if (hasRolled)
            {
                GUI.Label(new Rect(x, 172f, colW, 20f), "ROLL", smallStyle);

                //Moves Label
                GUI.Label(new Rect(x + colW, 172f, colW, 20f), "MOVES LEFT", smallStyle);

                //Roll Number
                GUI.Label(new Rect(x, 192f, colW, 55f), lastRoll.ToString(), rollStyle);

                //Moves remaining
                GUI.Label(new Rect(x + colW, 192f, colW, 55f), movesRemaining.ToString(), rollStyle);
                //Bottom instruction
                GUI.Label(new Rect(x, 262f, contentW, 24f), "Press Arrow Keys or WASD to Move", smallStyle);
            }
            else
            {
                GUI.Label(new Rect(x, 176f, contentW, 30f), "YOUR TURN", playerStyle);
                GUI.Label(new Rect(x, 222f, contentW, 24f), "Press Spacebar to Roll Dice", playerStyle);
            }
        }
        //
        private Texture2D MakeTransparentTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }


        //Finds the player's actions in the project-wide Input System asset (Assets/Settings/InputSystem_Actions).
        private void SetupInputActions()
        {
            InputActionAsset actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogWarning($"{name}: No project-wide Input Actions asset is set, so the player cannot be controlled.");
                return;
            }

            moveAction = actions.FindAction("Player/Move");
            rollDiceAction = actions.FindAction("Player/Roll Dice");
            if (moveAction == null) Debug.LogWarning($"{name}: Input action 'Player/Move' was not found.");
            if (rollDiceAction == null) Debug.LogWarning($"{name}: Input action 'Player/Roll Dice' was not found.");
        }

        //Rolls a six-sided die on the Roll Dice action, giving this turn's move budget.
        private void HandleDiceRollInput()
        {
            if (rollDiceAction == null || hasRolled || IsMoving || isUsingItem || (turnSystem != null && !turnSystem.IsPlayersTurn(this)))
            {
                return;
            }

            if (rollDiceAction.WasPressedThisFrame())
            {
                int first = Random.Range(1, 7);
                bool lucky = eventState.HasLuckyRoll;
                int second = lucky ? Random.Range(1, 7) : first;
                lastRoll = lucky ? Mathf.Max(first, second) : first;
                movesRemaining = eventState.UseRoll(first, second);
                hasRolled = true;
                if (mapEvents != null && lucky)
                {
                    string result = lucky ? "Lucky Roll: " + first + " and " + second + ". Kept " + lastRoll + ". " : "";
                    mapEvents.ShowMessage(this, result + "Movement: " + movesRemaining + ".");
                }
            }
        }

        //Reads the Move action once when it is first pressed, snapped to one grid direction, or zero if it wasn't.
        private Vector2Int ReadDirectionPressedThisFrame()
        {
            if (moveAction == null || !moveAction.WasPressedThisFrame())
            {
                return Vector2Int.zero;
            }

            Vector2 input = moveAction.ReadValue<Vector2>();
            if (input == Vector2.zero)
            {
                return Vector2Int.zero;
            }

            //Pick the stronger axis so diagonals and analog sticks still move one tile; ties favor up/down.
            if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
            {
                return input.y > 0f ? Vector2Int.up : Vector2Int.down;
            }

            return input.x > 0f ? Vector2Int.right : Vector2Int.left;
        }

        //Rejects moves before a roll, outside the player's turn, or past the roll's move budget.
        private bool TryMoveInDirection(Vector2Int direction)
        {
            if (!hasRolled || movesRemaining <= 0)
            {
                return false;
            }

            if (turnSystem != null && !turnSystem.IsPlayersTurn(this))
            {
                return false;
            }

            return MoveToGridPosition(gridPosition + direction);
        }

        //Starts a fresh movement budget, or consumes one frozen turn.
        public bool BeginEventTurn()
        {
            Debug.Log($"[Turn Debug] BeginEventTurn called for {name}. Resetting hasRolled.");
            hasRolled = false;
            movesRemaining = 0;
            bool canPlay = eventState.BeginTurn();
            if (!canPlay && mapEvents != null) mapEvents.ShowMessage(this, "Frozen: this turn was skipped.");
            return canPlay;
        }

        //Checks the turn's player list before an event moves onto another player.
        public bool IsOccupiedByOtherPlayer(Vector2Int cell)
        {
            return turnSystem != null && turnSystem.IsOccupiedByOtherPlayer(this, cell);
        }

        //Checks settings after changes in the Inspector.
        private void OnValidate()
        {
            ValidateSettings();
        }

        //Keeps sizes and speed positive and within useful limits.
        private void ValidateSettings()
        {
            markerWidth = float.IsNaN(markerWidth) ? 0.38f : Mathf.Clamp(markerWidth, 0.1f, 10f);
            markerHeight = float.IsNaN(markerHeight) ? 0.72f : Mathf.Clamp(markerHeight, 0.1f, 10f);
            moveSpeed = float.IsNaN(moveSpeed) ? 4f : Mathf.Clamp(moveSpeed, 0.1f, 100f);
        }

        //Places the player on an open tile without playing a movement animation.
        public bool SetGridPosition(Vector2Int newGridPosition)
        {
            if ((IsMoving && !resolvingStep) || hasReachedExit || mapTemplate == null || !mapTemplate.IsWalkable(newGridPosition))
            {
                return false;
            }

            gridPosition = newGridPosition;
            SnapToGridPosition();
            UpdateMapOccupancy(newGridPosition);
            if (mapTemplate.IsExit(newGridPosition)) ReachExit();
            return true;
        }

        //Starts a move only when the player is ready and a valid route exists.
        public bool MoveToGridPosition(Vector2Int newGridPosition)
        {
            if (!isActiveAndEnabled || IsMoving || hasReachedExit || mapTemplate == null
                || !hasRolled || movesRemaining <= 0
                || (turnSystem != null && !turnSystem.IsPlayersTurn(this)))
            {
                return false;
            }

            if (!mapTemplate.TryFindPath(gridPosition, newGridPosition, out List<Vector2Int> path))
            {
                return false;
            }

            if (path.Count > movesRemaining) return false;
            moveRoutine = StartCoroutine(MoveAlongPath(path));
            return true;
        }

        //Creates the player's rectangle and sets its size and drawing order.
        private void SetupMarker()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                return;
            }

            ReleaseMarker();
            markerSprite = CreateMarkerSprite();
            spriteRenderer.sprite = markerSprite;
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 505 - (gridPosition.x + gridPosition.y) * 10;
            transform.localScale = new Vector3(markerWidth, markerHeight, 1f);
        }

        //Draws a black rectangle with a visible border.
        private Sprite CreateMarkerSprite()
        {
            Texture2D texture = new Texture2D(12, 24)
            {
                filterMode = FilterMode.Point
            };
            markerTexture = texture;

            //Draw a simple standing rectangle with an outline.
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    bool isOutline = x == 0 || y == 0 || x == texture.width - 1 || y == texture.height - 1;
                    texture.SetPixel(x, y, isOutline ? outlineColor : playerColor);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0f), 24f);
        }

        //Lines the player's feet up with the center of its current tile.
        private void SnapToGridPosition()
        {
            if (mapTemplate == null || spriteRenderer == null)
            {
                return;
            }

            Vector3 worldPosition = mapTemplate.GridToWorld(gridPosition);
            transform.position = new Vector3(worldPosition.x, worldPosition.y, -1f);
            spriteRenderer.sortingOrder = 505 - (gridPosition.x + gridPosition.y) * 10;
        }

        //Slides through each tile in the route and records completed steps.
        private IEnumerator MoveAlongPath(List<Vector2Int> path)
        {
            // Move through the grid one tile at a time.
            foreach (Vector2Int next in path)
            {
                if (!mapTemplate.IsWalkable(next) || IsOccupiedByOtherPlayer(next)) break;
                if (mapTemplate.IsExit(next) && (inventory == null || !inventory.HasKey()))
                {
                    Debug.Log("The exit is locked. Go find a key.");
                    break;
                }
                Vector3 targetPosition = mapTemplate.GridToWorld(next);
                targetPosition.z = -1f;
                while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
                    float row = (transform.position.y - mapTemplate.transform.position.y) / (mapTemplate.TileHeight * 0.5f);
                    spriteRenderer.sortingOrder = 505 - Mathf.RoundToInt(row * 10f);
                    yield return null;
                }
                transform.position = targetPosition;
                lastDirection = next - gridPosition;
                gridPosition = next;
                movesRemaining--;
                UpdateMapOccupancy(next);
                CheckForItem(next);

                if (mapTemplate.IsExit(next))
                {
                    if (inventory != null && inventory.HasKey())
                    {
                        ReachExit();
                        yield break;
                    }

                    Debug.Log("The exit is locked. Go find a key.");
                }

                //Resolve each crossed tile before starting the next step.
                bool endTurn = false;
                resolvingStep = true;
                try
                {
                    if (mapEvents != null) endTurn = mapEvents.ResolveStep(this, lastDirection);
                }
                finally
                {
                    resolvingStep = false;
                }
                if (hasReachedExit) yield break;
                if (endTurn) movesRemaining = 0;
                //A teleport or extra move cancels the old route from this point.
                if (endTurn || gridPosition != next) break;
            }
            moveRoutine = null;
            if (movesRemaining == 0)
            {
                if (turnSystem != null) turnSystem.AdvanceTurn();
                else
                {
                    BeginEventTurn();
                }
            }
        }

        //Updates which tile the map thinks this player occupies, for wall fading.
        private void UpdateMapOccupancy(Vector2Int newPosition)
        {
            if (mapTemplate == null)
            {
                return;
            }

            mapTemplate.SetOccupantPosition(registeredPosition, newPosition);
            registeredPosition = newPosition;
        }

        //Stops the player at the exit and pauses the game to show the win.
        private void ReachExit()
        {
            hasReachedExit = true;
            moveRoutine = null;
            Debug.Log($"{name} reached the exit. You win!");
            Time.timeScale = 0f;
        }

        //Cancels an interrupted move and returns to the last completed tile.
        private void OnDisable()
        {
            if (moveRoutine == null)
            {
                return;
            }

            StopCoroutine(moveRoutine);
            moveRoutine = null;
            SnapToGridPosition();
        }

        public void SetUsingItem(bool usingItem)
        {
            isUsingItem = usingItem;
        }

        public bool IsUsingItem()
        {
            return isUsingItem;
        }

        //Pauses movement, dice rolls, and item use while a menu such as an obstacle's contents is open.
        public void SetInMenu(bool inMenu)
        {
            isInMenu = inMenu;
        }

        public bool IsInMenu()
        {
            return isInMenu;
        }

        public bool CanUseItem()
        {
            return !hasRolled && !IsMoving && !hasReachedExit && !isInMenu;
        }

        public void SetMovesFromItem(int moveAmount)
        {
            if (hasReachedExit || IsMoving || moveAmount <= 0)
            {
                return;
            }

            lastRoll = moveAmount;
            movesRemaining = moveAmount;
            hasRolled = true;

            Debug.Log($"Item gave the player {moveAmount} moves.");
        }

        private void CheckForItem(Vector2Int position)
        {
            if (inventory == null)
                return;

            Item[] items = FindObjectsByType<Item>(FindObjectsSortMode.None);

            foreach (Item item in items)
            {
                if (item == null)
                    continue;

                if (!item.IsAtPosition(position))
                    continue;

                Debug.Log($"[Item System] Found {item.ItemName} at grid position {position}.");
                item.PickUp(inventory);
                return;
            }
        }

        //Releases the generated image and stops tracking this player on the map.
        private void OnDestroy()
        {
            if (mapTemplate != null)
            {
                mapTemplate.RemoveOccupant(registeredPosition);
            }

            ReleaseMarker();
        }

        //Removes only the sprite and texture created by this script.
        private void ReleaseMarker()
        {
            if (Application.isPlaying)
            {
                if (markerSprite != null) Destroy(markerSprite);
                if (markerTexture != null) Destroy(markerTexture);
            }
            else
            {
                if (markerSprite != null) DestroyImmediate(markerSprite);
                if (markerTexture != null) DestroyImmediate(markerTexture);
            }
            markerSprite = null;
            markerTexture = null;
        }
    }
}

