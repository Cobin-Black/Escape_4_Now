using Escape4Now.Map;
using Escape4Now.TurnSystem;
using Escape4Now.Items;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Escape4Now.GameModes;
using Escape4Now.Input;

namespace Escape4Now.Player
{
    //Owns player movement, turn actions, and per-player event state.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerCharacter : MonoBehaviour
    {
        //Starting tile, appearance, and movement speed.
        [Header("Map and Turn References")]
        [SerializeField] private IsometricMapTemplate mapTemplate;
        [SerializeField] private GameModeController gameMode;
        private IPlayerGridMap configuredMap;
        private IPlayerGridMap MovementMap => configuredMap ?? gameMode?.GridMap ?? mapTemplate;
        public GameModeController Mode => gameMode;
        public GameplayInputBindings InputBindings => gameMode != null ? gameMode.InputBindings : GameplayInputContext.Current;
        [SerializeField] private TurnSystemController turnSystem;
        [Header("Player Setup")]
        [SerializeField] private Vector2Int gridPosition = new Vector2Int(2, 2);
        [SerializeField] private Color playerColor = Color.black;
        [SerializeField] private PlayerAppearance appearance;
        [SerializeField, Min(0.1f)] private float moveSpeed = 4f;
        [SerializeField] private PlayerInventory inventory;
        [Header("Health")]
        [SerializeField, Min(0)] private int health = 5;
        [Header("Movement Display")]
        [SerializeField, Min(0.1f)] private float diceRollAnimationDuration = 0.9f;
        [SerializeField, Min(0f)] private float sharedTileHorizontalOffset = 0.14f;
        [SerializeField, Min(0f)] private float sharedTileVerticalOffset = 0.08f;

        private SpriteRenderer spriteRenderer;
        private Coroutine moveRoutine;
        private Vector2Int registeredPosition;
        private bool hasReachedExit;
        private bool hasRolled;
        private int lastRoll;
        private int movesRemaining;
        private MapEventController mapEvents;
        private Vector2Int lastDirection = Vector2Int.up;
        private bool resolvingStep;
        private bool networkHasKey;
        private readonly PlayerEventState eventState = new PlayerEventState();
        private InputAction moveAction;
        private InputAction rollDiceAction;

        //Keeps event effects separate for each player.
        public PlayerEventState EventState => eventState;
        private bool isUsingItem;
        private bool isInMenu;
        private bool isBotControlled;
        private bool roarSlowActive;
        public PlayerInventory Inventory => inventory;
        public bool HasReachedExit => hasReachedExit;
        public bool HasRolled => hasRolled;
        public int LastRoll => lastRoll;
        public int MovesRemaining => movesRemaining;
        internal bool IsResolvingEventStep => resolvingStep;
        public bool IsCurrentTurn => turnSystem == null || turnSystem.IsPlayersTurn(this);
        //Blocks the previous player's last key press from reaching this player.
        public bool CanReadTurnInput => Time.timeScale > 0f
            && !Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused
            && (turnSystem == null || turnSystem.CanReadInput(this));
        private Coroutine sharedTilePositionRoutine;

        //Read-only movement state for the turn and map scripts.
        public Vector2Int GridPosition => gridPosition;
        public bool IsMoving => moveRoutine != null;
        public bool IsBotControlled => isBotControlled;


        //The scene's gamemode decides who may leave. With no gamemode (such as the tutorial) the exit is open.
        private bool CanUseExit
        {
            get
            {
                var mode = Escape4Now.GameModes.GameMode.Current;
                return mode == null || mode.CanUseExit(this);
            }
        }

        //Checks settings and places the player on its starting tile.
        private void Awake()
        {
            ValidateSettings();
            SetupInputActions();
            SetupMarker();
            SnapToGridPosition();
            registeredPosition = gridPosition;
            if (MovementMap != null)
            {
                mapEvents = mapTemplate != null ? mapTemplate.GetComponent<MapEventController>() : null;
                MovementMap.SetOccupantPosition(gridPosition, gridPosition);
            }
        }

        //Reads dice roll and movement key input each frame.
        private void Update()
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.Active) return;
            if (Time.timeScale <= 0f || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused) return;
            if (isBotControlled) return;
            if (!CanReadTurnInput) return;
            if (hasReachedExit)
            {
                return;
            }
            if (isUsingItem || isInMenu)
            {
                return;
            }

            HandleDiceRollInput();
            if (IsRollAnimating) return;

            Vector2Int direction = ReadDirectionPressedThisFrame();
            if (direction != Vector2Int.zero)
            {
                TryMoveInDirection(direction);
            }
        }

        private GameTimer gameTimer;
        private float rollAnimStart = -10f;
        public bool IsRollAnimating => rollAnimStart >= 0f
            && Time.realtimeSinceStartup - rollAnimStart < diceRollAnimationDuration;
        public bool HasKey => networkHasKey || (inventory != null && inventory.HasKey());
        public Color IdentityColor => playerColor;
        public IPlayerGridMap GridMap => MovementMap;

        //Uses the scene's Inspector-assigned movement and roll actions.
        private void SetupInputActions()
        {
            if (InputBindings != null)
            {
                moveAction = InputBindings.Move;
                rollDiceAction = InputBindings.RollDice;
                return;
            }
            Debug.LogError("Assign GameplayInputBindings through the scene's GameplayInputContext.", this);
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
                RollDice();
            }
        }

        //The host uses the same dice rules for local and remote players.
        public bool RollDice()
        {
            if (!CanReadTurnInput || hasRolled || IsMoving || hasReachedExit || isUsingItem || isInMenu || !IsCurrentTurn) return false;
            int first = Random.Range(1, 7);
            bool lucky = eventState.HasLuckyRoll;
            bool bad = eventState.HasBadRoll;
            bool bonus = eventState.HasRollBonus;
            int second = lucky || bad ? Random.Range(1, 7) : first;
            lastRoll = lucky ? Mathf.Max(first, second) : bad ? Mathf.Min(first, second) : first;
            movesRemaining = eventState.UseRoll(first, second);
            if (roarSlowActive)
            {
                movesRemaining = Mathf.Max(1, movesRemaining / 2);
                roarSlowActive = false;
            }
            rollAnimStart = Time.realtimeSinceStartup;
            hasRolled = true;
            if (mapEvents != null && (lucky || bad || bonus))
            {
                string result = lucky || bad ? (lucky ? "Lucky Roll: " : "Bad Roll: ") + first + " and " + second + ". Kept " + lastRoll + ". " : "Good Luck Day: +1. ";
                mapEvents.ShowMessage(this, result + "Movement: " + movesRemaining + ".");
            }
            return true;
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

        //Sets a local player's starting space and identifying color before the match.
        public void ConfigureParticipant(IPlayerGridMap map, TurnSystemController turns,
            Vector2Int cell, int number, Color color, string displayName = null, GameModeController mode = null)
        {
            if (spriteRenderer != null && MovementMap != null)
                MovementMap.RemoveOccupant(registeredPosition);
            mapTemplate = map as IsometricMapTemplate;
            configuredMap = map;
            if (mode != null) gameMode = mode;
            SetupInputActions();
            turnSystem = turns;
            gridPosition = cell;
            string safeName = string.IsNullOrWhiteSpace(displayName) ? "Player " + number : displayName.Trim();
            name = safeName.Length > 16 ? safeName.Substring(0, 16) : safeName;
            playerColor = color;
            if (spriteRenderer != null)
            {
                SetupMarker();
                SnapToGridPosition();
                registeredPosition = cell;
                MovementMap.SetOccupantPosition(cell, cell);
            }
        }

        //Clients show confirmed state without running dice, pickups, or events again.
        public void ApplyNetworkState(Escape4Now.Multiplayer.LanPlayerState state, bool updateOccupancy = true)
        {
            gridPosition = new Vector2Int(state.x, state.y);
            lastRoll = state.roll;
            movesRemaining = state.moves;
            hasRolled = state.rolled;
            hasReachedExit = state.won;
            networkHasKey = state.hasKey;
            eventState.SetPlayerProtection(state.protectedFromPlayers);
            if (updateOccupancy) UpdateMapOccupancy(gridPosition);
            else registeredPosition = gridPosition;
            transform.position = new Vector3(state.worldX, state.worldY, -1f);
            float row = (state.worldY - MovementMap.transform.position.y) / (MovementMap.TileHeight * 0.5f);
            spriteRenderer.sortingOrder = 505 - Mathf.RoundToInt(row * 10f);
        }

        //Checks settings after changes in the Inspector.
        private void OnValidate()
        {
            ValidateSettings();
        }

        //Keeps sizes and speed positive and within useful limits.
        private void ValidateSettings()
        {
            moveSpeed = float.IsNaN(moveSpeed) ? 4f : Mathf.Clamp(moveSpeed, 0.1f, 100f);
            diceRollAnimationDuration = float.IsNaN(diceRollAnimationDuration) ? 0.9f : Mathf.Clamp(diceRollAnimationDuration, 0.1f, 5f);
            sharedTileHorizontalOffset = Mathf.Clamp(sharedTileHorizontalOffset, 0f, 1f);
            sharedTileVerticalOffset = Mathf.Clamp(sharedTileVerticalOffset, 0f, 1f);
        }

        //Places the player on an open tile without playing a movement animation.
        public bool SetGridPosition(Vector2Int newGridPosition)
        {
            if (Time.timeScale <= 0f || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused
                || (IsMoving && !resolvingStep) || hasReachedExit || MovementMap == null || !MovementMap.IsWalkable(newGridPosition))
            {
                return false;
            }

            gridPosition = newGridPosition;
            SnapToGridPosition();
            UpdateMapOccupancy(newGridPosition);
            if (MovementMap.IsExit(newGridPosition) && CanUseExit) ReachExit();
            return true;
        }

        //Checks the whole swap before moving anyone. Items, turns, and movement stay with their owners.
        internal static bool SwitchLocations(IReadOnlyList<PlayerCharacter> participants, int[] destinations)
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.IsClient || participants == null
                || participants.Count < 2 || participants.Count > 4 || destinations == null
                || destinations.Length != participants.Count) return false;
            var cells = new Vector2Int[participants.Count];
            var people = new HashSet<PlayerCharacter>();
            var indices = new HashSet<int>();
            IPlayerGridMap map = participants[0] != null ? participants[0].MovementMap : null;
            if (map == null) return false;
            for (int i = 0; i < participants.Count; i++)
            {
                PlayerCharacter player = participants[i];
                int target = destinations[i];
                if (player == null || !player.isActiveAndEnabled || !people.Add(player) || player.MovementMap != map
                    || player.hasReachedExit || (player.IsMoving && !player.resolvingStep)
                    || !map.IsWalkable(player.gridPosition) || map.IsExit(player.gridPosition)
                    || target < 0 || target >= participants.Count
                    || target == i || !indices.Add(target)) return false;
                cells[i] = player.gridPosition;
            }
            for (int i = 0; i < participants.Count; i++)
                if (cells[i] == cells[destinations[i]]) return false;
            for (int i = 0; i < participants.Count; i++)
            {
                PlayerCharacter player = participants[i];
                player.gridPosition = cells[destinations[i]];
                player.registeredPosition = player.gridPosition;
                player.SnapToGridPosition();
            }
            //Shared destinations keep their player offsets after everyone has moved.
            map.ReplacePlayerOccupants(cells);
            foreach (Vector2Int cell in cells) participants[0].RefreshSharedTilePositions(cell);
            return true;
        }

        //Event movement cannot go below zero or grow beyond the board's tile count.
        internal void ChangeEventMovement(int amount)
        {
            if (!resolvingStep || MovementMap == null) return;
            movesRemaining = Mathf.Clamp(movesRemaining + Mathf.Clamp(amount, -2, 2), 0, MovementMap.Width * MovementMap.Height);
        }

        //Checks all destinations before moving the group, without changing inventory or moves.
        internal static bool RelocatePlayers(IReadOnlyList<PlayerCharacter> participants, Vector2Int[] targets)
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.IsClient || participants == null || targets == null
                || participants.Count < 2 || participants.Count > 4 || targets.Length != participants.Count) return false;
            IPlayerGridMap map = participants[0] != null ? participants[0].MovementMap : null;
            if (map == null) return false;
            var cells = new HashSet<Vector2Int>();
            var people = new HashSet<PlayerCharacter>();
            for (int i = 0; i < participants.Count; i++)
            {
                PlayerCharacter player = participants[i];
                if (player == null || !player.isActiveAndEnabled || player.MovementMap != map || !people.Add(player)
                    || player.hasReachedExit || (player.IsMoving && !player.resolvingStep)
                    || !map.IsWalkable(targets[i]) || map.IsOccupied(targets[i]) || map.IsExit(targets[i])
                    || !cells.Add(targets[i])) return false;
            }
            for (int i = 0; i < participants.Count; i++)
            {
                participants[i].gridPosition = targets[i];
                participants[i].registeredPosition = targets[i];
                participants[i].SnapToGridPosition();
            }
            map.ReplacePlayerOccupants(targets);
            return true;
        }

        //Starts a move only when the player is ready and a valid route exists.
        public bool MoveToGridPosition(Vector2Int newGridPosition)
        {
            if (!CanReadTurnInput || !isActiveAndEnabled || IsMoving || hasReachedExit || MovementMap == null
                || !hasRolled || IsRollAnimating || movesRemaining <= 0
                || (turnSystem != null && !turnSystem.IsPlayersTurn(this)))
            {
                return false;
            }

            if (!MovementMap.TryFindPath(gridPosition, newGridPosition, out List<Vector2Int> path))
            {
                return false;
            }

            if (path.Count > movesRemaining) return false;
            //Players may share a tile; walls and the movement budget still limit the route.
            if (path.Count == 0 || (MovementMap.IsExit(path[0]) && !CanUseExit)) return false;
            moveRoutine = StartCoroutine(MoveAlongPath(path));
            return true;
        }

        private void SetupMarker()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (appearance != null) appearance.ApplyIdentity(playerColor);
            if (spriteRenderer != null) spriteRenderer.sortingOrder = 505 - (gridPosition.x + gridPosition.y) * 10;
        }

        //Lines the player's feet up with the center of its current tile.
        private void SnapToGridPosition()
        {
            if (MovementMap == null || spriteRenderer == null)
            {
                return;
            }

            Vector3 worldPosition = MovementMap.GridToWorld(gridPosition);
            transform.position = new Vector3(worldPosition.x, worldPosition.y, -1f);
            spriteRenderer.sortingOrder = 505 - (gridPosition.x + gridPosition.y) * 10;
        }

        // Adjusts player positions when multiple players share the same tile.
        private void RefreshSharedTilePositions(Vector2Int tile)
        {
            if (MovementMap == null)
            {
                return;
            }

            PlayerCharacter[] allPlayers =
                FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);

            List<PlayerCharacter> playersOnTile = new List<PlayerCharacter>();

            foreach (PlayerCharacter player in allPlayers)
            {
                if (player != null && player.isActiveAndEnabled
                    && player.MovementMap == MovementMap
                    && player.gridPosition == tile)
                {
                    playersOnTile.Add(player);
                }
            }

            Vector3 tileCenter = MovementMap.GridToWorld(tile);

            // Keep one player centered.
            if (playersOnTile.Count == 1)
            {
                PlayerCharacter player = playersOnTile[0];
                player.transform.position = new Vector3(
                    tileCenter.x, tileCenter.y, -1f);
            }
            else
            {
                // Arrange multiple players in a compact diamond.
                float offsetX = sharedTileHorizontalOffset;
                float offsetY = sharedTileVerticalOffset;

                Vector2[] offsets =
                {
            new Vector2(-offsetX, 0f),
            new Vector2(offsetX, 0f),
            new Vector2(0f, offsetY),
            new Vector2(0f, -offsetY)
        };

                for (int i = 0; i < playersOnTile.Count; i++)
                {
                    PlayerCharacter player = playersOnTile[i];
                    Vector2 offset = offsets[i % offsets.Length];

                    player.transform.position = new Vector3(
                        tileCenter.x + offset.x,
                        tileCenter.y + offset.y,
                        -1f);
                }
            }

            foreach (PlayerCharacter player in playersOnTile)
            {
                float row = (player.transform.position.y - MovementMap.transform.position.y)
                    / (MovementMap.TileHeight * 0.5f);

                player.spriteRenderer.sortingOrder =
                    505 - Mathf.RoundToInt(row * 10f);
            }
        }

        //Slides through each tile in the route and records completed steps.
        private IEnumerator MoveAlongPath(List<Vector2Int> path)
        {
            // Move through the grid one tile at a time.
            foreach (Vector2Int next in path)
            {
                if (!MovementMap.IsWalkable(next) || movesRemaining <= 0) break;
                if (MovementMap.IsExit(next) && !CanUseExit)
                {
                    Debug.Log("The exit is locked. Go find a key.");
                    break;
                }
                Vector3 targetPosition = MovementMap.GridToWorld(next);
                targetPosition.z = -1f;
                while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
                    float row = (transform.position.y - MovementMap.transform.position.y) / (MovementMap.TileHeight * 0.5f);
                    spriteRenderer.sortingOrder = 505 - Mathf.RoundToInt(row * 10f);
                    yield return null;
                }
                transform.position = targetPosition;
                lastDirection = next - gridPosition;
                gridPosition = next;
                movesRemaining--;
                UpdateMapOccupancy(next);
                CheckForItem(next);

                if (MovementMap.IsExit(next))
                {
                    if (CanUseExit)
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
            SnapToGridPosition();
            moveRoutine = null;
            RefreshSharedTilePositions(gridPosition);
            if (movesRemaining == 0)
            {
                if (turnSystem != null) turnSystem.AdvanceTurn();
                else
                {
                    BeginEventTurn();
                }
            }
        }

        //Future direct actions must check this before changing a victim's items or state.
        public bool CanBeTargetedBy(PlayerCharacter attacker)
        {
            return !Escape4Now.Multiplayer.LanMultiplayerSession.IsClient && attacker != null && attacker != this
                && isActiveAndEnabled && attacker.isActiveAndEnabled && MovementMap != null && attacker.MovementMap == MovementMap
                && attacker.turnSystem == turnSystem && attacker.IsCurrentTurn && !attacker.IsMoving
                && !attacker.HasReachedExit && !attacker.IsInMenu() && !attacker.IsUsingItem()
                && !IsMoving && !hasReachedExit && !eventState.IsProtectedFromPlayers;
        }

        //Call only after a direct negative action succeeds, never for environmental events.
        public bool RecordSuccessfulTargetedAction(PlayerCharacter attacker)
        {
            if (!CanBeTargetedBy(attacker)) return false;
            eventState.SetPlayerProtection(true);
            return true;
        }

        //Updates which tile the map thinks this player occupies, for wall fading.
        private void UpdateMapOccupancy(Vector2Int newPosition)
        {
            if (MovementMap == null)
            {
                return;
            }

            Vector2Int oldPosition = registeredPosition;

            registeredPosition = newPosition;
            //Rebuild the occupied set so leaving a shared tile does not mark it empty.
            var occupied = new List<Vector2Int>();

            // Only living players count as occupying a tile.
            foreach (PlayerCharacter player in FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None))
            {
                if (player.isActiveAndEnabled && player.Health > 0
                    && player.MovementMap == MovementMap)
                {
                    occupied.Add(player.gridPosition);
                }
            }

            MovementMap.ReplacePlayerOccupants(occupied);

            RefreshSharedTilePositions(oldPosition);
            RefreshSharedTilePositions(newPosition);
        }

        //Stops the player at the exit and pauses the game to show the win.
        private void ReachExit()
        {
            if (hasReachedExit || Escape4Now.Multiplayer.LocalMultiplayerSession.MatchHasEnded) return;
            if (gameTimer == null) gameTimer = FindFirstObjectByType<GameTimer>();
            if (gameTimer != null && gameTimer.HasExpired) return;
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

        // Sets whether the player is currently using an item.
        public void SetUsingItem(bool usingItem)
        {
            isUsingItem = usingItem;
        }

        // Checks whether the player is using an item.
        public bool IsUsingItem()
        {
            return isUsingItem;
        }

        //Pauses movement, dice rolls, and item use while a menu such as an obstacle's contents is open.
        public void SetInMenu(bool inMenu)
        {
            isInMenu = inMenu;
        }

        // Checks whether a menu is currently open for the player.
        public bool IsInMenu()
        {
            return isInMenu;
        }

        // Checks whether the player is allowed to use an item.
        public bool CanUseItem()
        {
            return CanReadTurnInput && !hasRolled && !IsMoving && !hasReachedExit && !isInMenu;
        }

        // Gives the player a movement allowance from an item.
        public void SetMovesFromItem(int moveAmount)
        {
            if (!CanReadTurnInput || hasReachedExit || IsMoving || moveAmount <= 0)
            {
                return;
            }

            lastRoll = moveAmount;
            movesRemaining = moveAmount;
            hasRolled = true;

            Debug.Log($"Item gave the player {moveAmount} moves.");
        }

        // Checks whether the player has stepped onto an item and collects it.
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

                Debug.Log($"[Key Debug] Player: {name}, " +
                    $"Inventory: {inventory.name}, Has Key: {inventory.HasKey()}");
                return;
            }
        }

        // Sets whether this player is controlled by a bot.
        public void SetBotControlled(bool value)
        {
            isBotControlled = value;
        }

        // Gets the player's current health.
        public int Health => health;

        // Removes one heart and eliminates the player when health reaches zero.
        public void TakeDamage()
        {
            if (health <= 0)
                return;

            health--;
            Debug.Log(name + " has " + health + " hearts remaining.");

            if (health == 0)
            {
                Debug.Log(name + " has been eliminated!");

                UpdateMapOccupancy(gridPosition);

                // Deactivate the character while retaining its health for match results.
                gameObject.SetActive(false);
            }
        }

        // Applies the Monster's Roar effect to the player's next dice roll.
        public void ApplyMonsterSlow()
        {
            roarSlowActive = true;
        }

        //Stops tracking this player when the scene or participant is removed.
        private void OnDestroy()
        {
            if (MovementMap != null)
            {
                MovementMap.RemoveOccupant(registeredPosition);
            }
        }
    }
}
