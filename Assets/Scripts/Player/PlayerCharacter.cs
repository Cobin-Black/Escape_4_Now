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
        private bool exitNeedsKey = true;

        private SpriteRenderer spriteRenderer;
        private Coroutine moveRoutine;
        private Sprite markerSprite;
        private Texture2D markerTexture;
        private Vector2Int registeredPosition;
        private bool hasReachedExit;
        private bool hasRolled;
        private bool isTutorial;
        private int lastRoll;
        private int movesRemaining;
        private GUIStyle rollDisplayStyle;
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
        public PlayerInventory Inventory => inventory;
        public bool HasReachedExit => hasReachedExit;
        public bool HasRolled => hasRolled;
        //new thing 
        public int LastRoll => lastRoll;
        public int MovesRemaining => movesRemaining;
        internal bool IsResolvingEventStep => resolvingStep;
        public bool IsCurrentTurn => turnSystem == null || turnSystem.IsPlayersTurn(this);
        //Blocks the previous player's last key press from reaching this player.
        public bool CanReadTurnInput => turnSystem == null || turnSystem.CanReadInput(this);
        private Coroutine sharedTilePositionRoutine;

        //Read-only movement state for the turn and map scripts.
        public Vector2Int GridPosition => gridPosition;
        public bool IsMoving => moveRoutine != null;

        public void SetExitNeedsKey(bool value) { exitNeedsKey = value; }
        private bool CanUseExit => !exitNeedsKey || (inventory != null && inventory.HasKey());

        //Checks settings and places the player on its starting tile.
        private void Awake()
        {
            isTutorial = FindFirstObjectByType<Escape4Now.GameModes.Tutorial_Functionality>() != null;
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
            if (Escape4Now.Multiplayer.LanMultiplayerSession.Active) return;
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


           private Escape4Now.TurnSystem.GameTimer gameTimer;

private float rollAnimStart = -10f;
private Texture2D circleTexture;
private const float RollAnimDuration = 0.9f;
        private bool IsRollAnimating => rollAnimStart >= 0f
            && Time.realtimeSinceStartup - rollAnimStart < RollAnimDuration;
// Which of the 3x3 cells hold a dot for faces 1-6 (cell = row * 3 + column)
private static readonly int[] DiceFaceMasks = { 0, 16, 257, 273, 325, 341, 365 };
        //Draws the roll prompt or the current move budget.
        private void OnGUI()
        {
            DrawKeyMarker();
            if (turnSystem != null && !turnSystem.IsPlayersTurn(this)) return;
            
            //find the timer once and remember it 
             if (gameTimer == null)gameTimer = FindObjectOfType<Escape4Now.TurnSystem.GameTimer>();
            // Main panel
            GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = MakeTransparentTexture(new Color(0.02f, 0.04f, 0.08f, 0.88f));

            GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 13;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            headerStyle.normal.textColor = new Color(0.65f, 0.85f, 1f);

            //Player status and prompt style.
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

            float panelX = 16f, panelY = 85f, panelW = 360f, panelH = 270f;
            float padding = 16f;
            float x = panelX + padding;
            float contentW = panelW - padding * 2f;
            float colW = contentW / 2f;

            string turnName = turnSystem != null ? turnSystem.CurrentTurnName : gameObject.name;

            GUI.Box(new Rect(panelX, panelY, panelW, panelH), "", panelStyle);

            GUI.Label(new Rect(x, 95f, contentW, 22f), "CURRENT TURN", headerStyle);
            GUI.Label(new Rect(x, 114f, contentW, 34f), turnName.ToUpper(), playerStyle);
            Color dividerColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(new Rect(x + 15f, 164f, contentW - 30f, 2f), Texture2D.whiteTexture);
            GUI.color = dividerColor;



            if (hasReachedExit)
            {
                //Reuse the player panel to show who escaped.
                GUI.Label(new Rect(x, 180f, contentW, 80f), name + " escaped!\nYou win!", playerStyle);
            }
            else if (hasRolled)
            {
                GUI.Label(new Rect(x, 175f, colW, 20f), "DIE RESULT", smallStyle);

                //Moves Label
                GUI.Label(new Rect(x + colW, 175f, colW, 20f), "MOVES LEFT", smallStyle);

                //Roll Number
                //GUI.Label(new Rect(x, 192f, colW, 55f), lastRoll.ToString(), rollStyle);
                 
                 DrawRollDisplay(new Rect(x, 195f, colW, 55f),  rollStyle);

                //Moves remaining
                GUI.Label(new Rect(x + colW, 195f, colW, 55f), IsRollAnimating ? "..." : movesRemaining.ToString(), rollStyle);
                //Bottom instruction
                GUI.Label(new Rect(x, 260f, contentW, 24f), "Arrows/WASD move; bonuses and events can change moves.", smallStyle);
            }
            else
            {
                GUI.Label(new Rect(x, 180f, contentW, 30f), "YOUR TURN", playerStyle);
                GUI.Label(new Rect(x, 222f, contentW, 24f), "Press Spacebar to Roll Dice", promptStyle);
            }

            //timer row.
            if(gameTimer != null)
            {
                int totalSeconds = Mathf.CeilToInt(gameTimer.TimeRemaining);
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                
                bool lowTime = totalSeconds <= 30;
                Color borderColor = lowTime ? new Color(1f, 0.45f, 0.45f) : new Color(0.65f, 0.85f, 1f);
                
                GUIStyle timerStyle = new GUIStyle(promptStyle);
                timerStyle.fontSize = 18;
                timerStyle.normal.textColor = totalSeconds <= 30 ? new Color(1f, 0.45f,0.45f) : Color.white;
            
            //Timer box CENTERED in the panel
                float boxW = 160f, boxH = 36f, border = 2f;
                Rect timerRect = new Rect(panelX +(panelW - boxW) / 2f, 296f, boxW, boxH);
                Color oldColor = GUI.color;
              
              //Border the four thin lines 
               GUI.color = borderColor;
               GUI.DrawTexture(new Rect(timerRect.x ,timerRect.y , timerRect.width ,border), Texture2D.whiteTexture);
               GUI.DrawTexture(new Rect(timerRect.x,  timerRect.yMax - border,  timerRect.width, border), Texture2D.whiteTexture);
               GUI.DrawTexture(new Rect(timerRect.x, timerRect.y, border, timerRect.height), Texture2D.whiteTexture);
               GUI.DrawTexture(new Rect(timerRect.xMax - border, timerRect.y, border, timerRect.height), Texture2D.whiteTexture);

                GUI.color = oldColor;

                //Timer text, centered inside the box.
                GUI.Label(timerRect, "TIME " + minutes.ToString("00") + ":" + seconds.ToString("00"), timerStyle);
            }
        }
    //Shows the key's own image in a gold-bordered badge above a player who carries it.
private void DrawKeyMarker()
{
    bool hasKey = networkHasKey || (inventory != null && inventory.HasKey());
    Camera viewCamera = Camera.main;
    if (!hasKey || !isActiveAndEnabled || viewCamera == null || mapTemplate == null) return;
    ItemSpawner spawner = mapTemplate.GetComponent<ItemSpawner>();
    SpriteRenderer keyVisual = spawner != null && spawner.KeyPrefab != null
        ? spawner.KeyPrefab.GetComponentInChildren<SpriteRenderer>(true) : null;
    Sprite sprite = keyVisual != null ? keyVisual.sprite : null;
    if (sprite == null || sprite.texture == null) return;

    Vector3 screenPosition = viewCamera.WorldToScreenPoint(transform.position + Vector3.up * 1.05f);
    if (screenPosition.z <= 0f) return;

    const float iconSize = 28f;                    // the key image
    const float pad = 6f;                          // space between image and badge edge
    const float border = 2f;                       // gold outline thickness
    const float badgeSize = iconSize + pad * 2f;

    float bx = Mathf.Clamp(screenPosition.x - badgeSize * 0.5f, 0f, Mathf.Max(0f, Screen.width - badgeSize));
    float by = Mathf.Clamp(Screen.height - screenPosition.y - badgeSize, 0f, Mathf.Max(0f, Screen.height - badgeSize));
    Rect badge = new Rect(bx, by, badgeSize, badgeSize);

    Color oldColor = GUI.color;

    // Gold square first, then a dark square on top, so the gold shows as a border
    GUI.color = new Color(0.98f, 0.78f, 0.2f, 1f);
    GUI.DrawTexture(badge, Texture2D.whiteTexture);
    GUI.color = new Color(0.02f, 0.04f, 0.08f, 0.9f);
    GUI.DrawTexture(new Rect(badge.x + border, badge.y + border,
        badge.width - border * 2f, badge.height - border * 2f), Texture2D.whiteTexture);

    // The key image, centered inside the badge
    Rect spriteRect = sprite.textureRect;
    Rect uv = new Rect(spriteRect.x / sprite.texture.width, spriteRect.y / sprite.texture.height,
        spriteRect.width / sprite.texture.width, spriteRect.height / sprite.texture.height);
    GUI.color = keyVisual.color;
    GUI.DrawTextureWithTexCoords(new Rect(badge.x + pad, badge.y + pad, iconSize, iconSize),
        sprite.texture, uv, true);

    GUI.color = oldColor;
}
                private Texture2D MakeTransparentTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        // Shows a tumbling die for a moment, then the real rolled face
private void DrawRollDisplay(Rect area, GUIStyle fallbackStyle)
{
    // Item rewards can be bigger than 6, so fall back to a plain number
    if (lastRoll < 1 || lastRoll > 6)
    {
        GUI.Label(area, lastRoll.ToString(), fallbackStyle);
        return;
    }

    float t = Time.realtimeSinceStartup - rollAnimStart;
    int face = lastRoll;
    Rect die = new Rect(area.center.x - 23f, area.y + 4f, 46f, 46f);

    if (t < RollAnimDuration)
    {
        face = (int)(Time.realtimeSinceStartup * 18f) % 6 + 1;              // flicker, no Random
        die.x += Mathf.Sin(t * 60f) * 4f * (1f - t / RollAnimDuration);     // shake that fades out
    }

    DrawDie(die, face);
}

// Draws one die face: light-blue border, white body, dark dots
private void DrawDie(Rect rect, int face)
{
    if (circleTexture == null) circleTexture = MakeCircleTexture(32);
    Color oldColor = GUI.color;

    GUI.color = new Color(0.65f, 0.85f, 1f);
    GUI.DrawTexture(rect, Texture2D.whiteTexture);
    GUI.color = new Color(0.97f, 0.97f, 0.97f);
    GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), Texture2D.whiteTexture);

    GUI.color = new Color(0.05f, 0.08f, 0.14f);
    float pip = rect.width * 0.18f;
    float margin = rect.width * 0.2f;
    float step = (rect.width - margin * 2f - pip) / 2f;
    for (int cell = 0; cell < 9; cell++)
    {
        if ((DiceFaceMasks[face] & (1 << cell)) == 0) continue;
        float px = rect.x + margin + (cell % 3) * step;
        float py = rect.y + margin + (cell / 3) * step;
        GUI.DrawTexture(new Rect(px, py, pip, pip), circleTexture);
    }

    GUI.color = oldColor;
}

// A white circle with soft edges, used for the dots
private Texture2D MakeCircleTexture(int size)
{
    Texture2D tex = new Texture2D(size, size);
    float r = size / 2f;
    for (int y = 0; y < size; y++)
    {
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - r;
            float dy = y + 0.5f - r;
            float alpha = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
    }
    tex.Apply();
    return tex;
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
                RollDice();
            }
        }

        //The host uses the same dice rules for local and remote players.
        public bool RollDice()
        {
            if (hasRolled || IsMoving || hasReachedExit || isUsingItem || isInMenu || !IsCurrentTurn) return false;
            int first = Random.Range(1, 7);
            bool lucky = eventState.HasLuckyRoll;
            bool bad = eventState.HasBadRoll;
            bool bonus = eventState.HasRollBonus;
            int second = lucky || bad ? Random.Range(1, 7) : first;
            lastRoll = lucky ? Mathf.Max(first, second) : bad ? Mathf.Min(first, second) : first;
            movesRemaining = eventState.UseRoll(first, second);
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
        public void ConfigureParticipant(IsometricMapTemplate map, TurnSystemController turns,
            Vector2Int cell, int number, Color color, string displayName = null)
        {
            if (spriteRenderer != null && mapTemplate != null)
                mapTemplate.RemoveOccupant(registeredPosition);
            mapTemplate = map;
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
                mapTemplate.SetOccupantPosition(cell, cell);
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
            float row = (state.worldY - mapTemplate.transform.position.y) / (mapTemplate.TileHeight * 0.5f);
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
            if (mapTemplate.IsExit(newGridPosition) && CanUseExit) ReachExit();
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
            IsometricMapTemplate map = participants[0] != null ? participants[0].mapTemplate : null;
            if (map == null) return false;
            for (int i = 0; i < participants.Count; i++)
            {
                PlayerCharacter player = participants[i];
                int target = destinations[i];
                if (player == null || !player.isActiveAndEnabled || !people.Add(player) || player.mapTemplate != map
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
            if (!resolvingStep || mapTemplate == null) return;
            movesRemaining = Mathf.Clamp(movesRemaining + Mathf.Clamp(amount, -2, 2), 0, mapTemplate.Width * mapTemplate.Height);
        }

        //Checks all destinations before moving the group, without changing inventory or moves.
        internal static bool RelocatePlayers(IReadOnlyList<PlayerCharacter> participants, Vector2Int[] targets)
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.IsClient || participants == null || targets == null
                || participants.Count < 2 || participants.Count > 4 || targets.Length != participants.Count) return false;
            IsometricMapTemplate map = participants[0] != null ? participants[0].mapTemplate : null;
            if (map == null) return false;
            var cells = new HashSet<Vector2Int>();
            var people = new HashSet<PlayerCharacter>();
            for (int i = 0; i < participants.Count; i++)
            {
                PlayerCharacter player = participants[i];
                if (player == null || !player.isActiveAndEnabled || player.mapTemplate != map || !people.Add(player)
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
            if (!isActiveAndEnabled || IsMoving || hasReachedExit || mapTemplate == null
                || !hasRolled || IsRollAnimating || movesRemaining <= 0
                || (turnSystem != null && !turnSystem.IsPlayersTurn(this)))
            {
                return false;
            }

            if (!mapTemplate.TryFindPath(gridPosition, newGridPosition, out List<Vector2Int> path))
            {
                return false;
            }

            if (path.Count > movesRemaining) return false;
            //Players may share a tile; walls and the movement budget still limit the route.
            if (path.Count == 0 || (mapTemplate.IsExit(path[0]) && !CanUseExit)) return false;
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

        // Adjusts player positions when multiple players share the same tile.
        private void RefreshSharedTilePositions(Vector2Int tile)
        {
            if (mapTemplate == null)
            {
                return;
            }

            PlayerCharacter[] allPlayers =
                FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);

            List<PlayerCharacter> playersOnTile = new List<PlayerCharacter>();

            foreach (PlayerCharacter player in allPlayers)
            {
                if (player != null && player.isActiveAndEnabled
                    && player.mapTemplate == mapTemplate
                    && player.gridPosition == tile)
                {
                    playersOnTile.Add(player);
                }
            }

            Vector3 tileCenter = mapTemplate.GridToWorld(tile);

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
                float offsetX = 0.14f;
                float offsetY = 0.08f;

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
                float row = (player.transform.position.y - mapTemplate.transform.position.y)
                    / (mapTemplate.TileHeight * 0.5f);

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
                if (!mapTemplate.IsWalkable(next) || movesRemaining <= 0) break;
                if (mapTemplate.IsExit(next) && !CanUseExit)
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
                && isActiveAndEnabled && attacker.isActiveAndEnabled && mapTemplate != null && attacker.mapTemplate == mapTemplate
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
            if (mapTemplate == null)
            {
                return;
            }

            Vector2Int oldPosition = registeredPosition;

            registeredPosition = newPosition;
            //Rebuild the occupied set so leaving a shared tile does not mark it empty.
            var occupied = new List<Vector2Int>();
            foreach (PlayerCharacter player in FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None))
                if (player.isActiveAndEnabled && player.mapTemplate == mapTemplate) occupied.Add(player.gridPosition);
            mapTemplate.ReplacePlayerOccupants(occupied);

            RefreshSharedTilePositions(oldPosition);
            RefreshSharedTilePositions(newPosition);
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
            if (hasReachedExit || IsMoving || moveAmount <= 0)
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
