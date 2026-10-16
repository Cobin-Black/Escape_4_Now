using Escape4Now.Map;
using Escape4Now.TurnSystem;
using Escape4Now.Items;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using Escape4Now.UI;

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
        [Header("Active Turn Indicator")]
        [SerializeField] private Color turnRingColor = new Color(0.22f, 0.86f, 1f, 0.9f);
        [SerializeField, Min(0.1f), Tooltip("Width and height of the isometric outline beneath the player, in world units.")]
        private Vector2 turnRingSize = new Vector2(0.86f, 0.42f);
        [SerializeField, Range(0f, 1f)] private float turnRingGlowStrength = 0.22f;
        [SerializeField, Min(0f)] private float turnRingPulseSpeed = 1.5f;
        [SerializeField, Range(0f, 0.15f)] private float turnRingPulseAmount = 0.035f;
        [SerializeField, Range(0.01f, 0.2f)] private float turnRingThickness = 0.045f;
        [SerializeField] private float turnRingGroundOffset = 0.015f;
        [Header("Current Game Mode Objective")]
        [SerializeField] private string rollPrompt = "ROLL: SPACE";
        [SerializeField] private string compactRollPrompt = "SPACE";
        [Header("Gameplay HUD")]
        [SerializeField, Tooltip("Margin from the top-left corner of the game view, in HUD units.")]
        private Vector2 hudTopLeftMargin = new Vector2(12f, 8f);
        [SerializeField, Tooltip("Width and height of the gameplay HUD, in HUD units.")]
        private Vector2 hudPanelSize = new Vector2(260f, 210f);
        [SerializeField, Tooltip("Width and height of the tutorial HUD, in HUD units.")]
        private Vector2 tutorialHudSize = new Vector2(360f, 118f);
        [SerializeField, Min(4f)] private float hudPadding = 14f;
        [SerializeField, Min(0f), Tooltip("Keeps the HUD clear of the Pause button on the right.")]
        private float hudTopRightClearance = 150f;
        [SerializeField, Range(12, 28)] private int hudTitleFontSize = 18;
        [SerializeField, Range(10, 20)] private int hudInfoFontSize = 13;
        [SerializeField, Range(10, 24)] private int hudTimerFontSize = 18;
        [SerializeField, Range(10, 24)] private int hudObjectiveFontSize = 15;
        [SerializeField, Range(10, 24)] private int hudObjectiveBodyFontSize = 15;
        [SerializeField] private Color hudBackgroundColor = new Color(0.02f, 0.04f, 0.08f, 0.9f);
        [SerializeField] private Color hudTextColor = Color.white;
        [SerializeField] private Color hudMutedTextColor = new Color(0.72f, 0.79f, 0.86f, 1f);
        [SerializeField] private Color hudDividerColor = new Color(0.72f, 0.79f, 0.86f, 0.22f);
        [SerializeField] private Color hudOutlineColor = new Color(0f, 0.78f, 0.95f, 1f);
        [SerializeField] private Color hudObjectiveColor = new Color(1f, 0.76f, 0.16f, 1f);
        [SerializeField, Min(0f)] private float hudBorderThickness = 1.5f;
        [SerializeField, Range(0f, 8f)] private float hudCornerRadius = 3f;
        [Header("Gameplay HUD Layout")]
        [SerializeField, Min(60f)] private float hudTimerWidth = 82f;
        [SerializeField, Min(16f)] private float hudRollIconSize = 44f;
        [SerializeField, Min(24f)] private float hudRollTextOffset = 50f;
        [SerializeField, Min(24f)] private float hudDetailsRowHeight = 44f;
        [SerializeField, Range(0.1f, 0.6f)] private float hudDetailsRowPosition = 0.30f;
        [SerializeField, Range(0.3f, 0.85f)] private float hudObjectiveRowPosition = 0.60f;
        [SerializeField, Range(0.5f, 1f), Tooltip("Width of the objective copy relative to the content area; narrower values encourage short, readable lines.")]
        private float hudObjectiveTextWidthRatio = 0.82f;
        [SerializeField, Min(0f)] private float hudColumnGap = 8f;
        [SerializeField, Min(0f)] private float hudSectionGap = 8f;
        [SerializeField, Min(0f)] private float hudDividerThickness = 1f;
        [Header("HUD Icons")]
        [SerializeField, Tooltip("Optional timer image. Leave empty to use the built-in stopwatch outline.")]
        private Texture2D hudTimerIcon;
        [SerializeField, Min(8f)] private float hudTimerIconSize = 25f;
        [SerializeField, Range(0.01f, 0.1f), Tooltip("Stroke width of the built-in stopwatch, relative to its image size.")]
        private float hudTimerIconStroke = 0.045f;
        [SerializeField, Min(0f)] private float hudDieBorderThickness = 3f;
        [SerializeField, Range(0f, 12f)] private float hudDieCornerRadius = 6f;
        [SerializeField] private Color hudDieBorderColor = new Color(0f, 0.78f, 0.95f, 1f);
        [SerializeField] private Color hudDieFaceColor = new Color(0.97f, 0.98f, 1f, 1f);
        [SerializeField] private Color hudDiePipColor = new Color(0.02f, 0.04f, 0.08f, 1f);
        [Header("Key Badge")]
        [SerializeField, Min(0f)] private float keyBadgeHeightAbovePlayer = 1.05f;
        [SerializeField, Min(8f)] private float keyBadgeIconSize = 28f;
        [SerializeField, Min(0f)] private float keyBadgePadding = 6f;
        [SerializeField, Min(0f)] private float keyBadgeBorder = 2f;
        [SerializeField] private Color keyBadgeBorderColor = new Color(0.98f, 0.78f, 0.2f, 1f);
        [SerializeField] private Color keyBadgeBackgroundColor = new Color(0.02f, 0.04f, 0.08f, 0.9f);
        [Header("Movement Display")]
        [SerializeField, Min(0.1f)] private float diceRollAnimationDuration = 0.9f;
        [SerializeField, Min(0f)] private float sharedTileHorizontalOffset = 0.14f;
        [SerializeField, Min(0f)] private float sharedTileVerticalOffset = 0.08f;

        private SpriteRenderer spriteRenderer;
        private Coroutine moveRoutine;
        private Sprite markerSprite;
        private Texture2D markerTexture;
        private Sprite turnRingSprite;
        private Texture2D turnRingTexture;
        private SpriteRenderer turnRingRenderer;
        private Transform turnRingTransform;
        private float builtTurnRingGlow = -1f;
        private float builtTurnRingThickness = -1f;
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
        public bool CanReadTurnInput => Time.timeScale > 0f
            && !Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused
            && (turnSystem == null || turnSystem.CanReadInput(this));
        private Coroutine sharedTilePositionRoutine;

        //Read-only movement state for the turn and map scripts.
        public Vector2Int GridPosition => gridPosition;
        public bool IsMoving => moveRoutine != null;

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
            isTutorial = FindFirstObjectByType<Escape4Now.GameModes.Tutorial_Functionality>() != null;
            ValidateSettings();
            SetupInputActions();
            SetupMarker();
            SetupTurnRing();
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
            if (Time.timeScale <= 0f || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused) return;
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

        private void LateUpdate()
        {
            UpdateTurnRing();
        }


           private Escape4Now.TurnSystem.GameTimer gameTimer;
        private GUIStyle hudTitleStyle;
        private GUIStyle hudInfoStyle;
        private GUIStyle hudPlayerStyle;
        private GUIStyle hudObjectiveStyle;
        private GUIStyle hudObjectiveBodyStyle;
        private GUIStyle hudActionStyle;
        private GUIStyle hudPromptPrefixStyle;
        private GUIStyle hudTimerStyle;
        private Texture2D stopwatchTexture;
        private float builtStopwatchStroke = -1f;

private float rollAnimStart = -10f;
private Texture2D circleTexture;
        private bool IsRollAnimating => rollAnimStart >= 0f
            && Time.realtimeSinceStartup - rollAnimStart < diceRollAnimationDuration;
// Which of the 3x3 cells hold a dot for faces 1-6 (cell = row * 3 + column)
private static readonly int[] DiceFaceMasks = { 0, 16, 257, 273, 325, 341, 365 };
        //The turn panel's size before shrinking, and how much it shrinks to fit a small window.
        private Vector2 GetHudPanelSize(out float fitScale)
        {
            Vector2 requestedSize = isTutorial ? tutorialHudSize : hudPanelSize;
            float availableWidth = HudScale.Width - hudTopLeftMargin.x * 2f - hudTopRightClearance;
            fitScale = Mathf.Clamp(Mathf.Min(availableWidth / requestedSize.x,
                (HudScale.Height - hudTopLeftMargin.y * 2f) / requestedSize.y), 0.01f, 1f);
            return requestedSize;
        }

        //Where the turn panel is drawn, in HudScale units, so other UI (like the tutorial) can point at it.
        public Rect HudPanelRect
        {
            get
            {
                Vector2 size = GetHudPanelSize(out float fitScale);
                return new Rect(hudTopLeftMargin.x, hudTopLeftMargin.y, size.x * fitScale, size.y * fitScale);
            }
        }

        //Draws the roll prompt or the current move budget.
        private void OnGUI()
        {
            if (Escape4Now.Multiplayer.LocalMultiplayerSession.MatchHasEnded) return;
            DrawKeyMarker();
            if (turnSystem != null && !turnSystem.IsPlayersTurn(this)) return;
            if (gameTimer == null) gameTimer = FindFirstObjectByType<Escape4Now.TurnSystem.GameTimer>();
            PrepareHudStyles();

            Matrix4x4 previousMatrix = HudScale.Apply();
            Vector2 requestedSize = GetHudPanelSize(out float fitScale);
            GUI.matrix *= Matrix4x4.Scale(new Vector3(fitScale, fitScale, 1f));
            Rect panel = new Rect(hudTopLeftMargin.x / fitScale, hudTopLeftMargin.y / fitScale,
                requestedSize.x, requestedSize.y);
            DrawHudColor(panel, hudBackgroundColor, hudCornerRadius);
            DrawHudOutline(panel, hudOutlineColor, hudBorderThickness, hudCornerRadius);

            float contentX = panel.x + hudPadding;
            float contentWidth = panel.width - hudPadding * 2f;
            float titleHeight = hudTitleStyle.lineHeight;
            float titleY = panel.y + hudPadding;
            float detailsY = panel.y + Mathf.Max(hudPadding + titleHeight + hudSectionGap * 2f,
                panel.height * hudDetailsRowPosition);
            float detailsHeight = hudDetailsRowHeight;
            string turnName = turnSystem != null ? turnSystem.CurrentTurnName : gameObject.name;
            int turn = turnSystem != null ? turnSystem.TurnNumber : 1;
            float playerNameWidth = contentWidth * 0.5f;
            float playerColumnX = contentX + contentWidth - playerNameWidth;
            GUI.Label(new Rect(contentX, titleY, contentWidth - playerNameWidth - hudColumnGap, titleHeight), $"TURN  {turn}", hudTitleStyle);
            DrawHudColor(new Rect(playerColumnX, titleY, hudDividerThickness, titleHeight), hudDividerColor);
            GUI.Label(new Rect(playerColumnX + hudColumnGap, titleY, playerNameWidth - hudColumnGap, titleHeight),
                turnName.ToUpperInvariant(), hudPlayerStyle);

            DrawHudColor(new Rect(contentX, detailsY - hudSectionGap, contentWidth, hudDividerThickness), hudDividerColor);
            float timerWidth = Mathf.Min(hudTimerWidth, contentWidth * 0.42f);
            float minimumActionWidth = hudRollTextOffset + hudActionStyle.CalcSize(new GUIContent(compactRollPrompt)).x;
            bool stackHudDetails = contentWidth - timerWidth - hudColumnGap < minimumActionWidth;
            float timerX = stackHudDetails ? contentX : panel.xMax - hudPadding - timerWidth;
            float actionWidth = Mathf.Max(0f, timerX - contentX - hudColumnGap);
            if (stackHudDetails) actionWidth = contentWidth;
            else DrawHudColor(new Rect(timerX - hudColumnGap * 0.5f, detailsY,
                hudDividerThickness, detailsHeight), hudDividerColor);
            if (hasReachedExit)
            {
                GUI.Label(new Rect(contentX, detailsY, actionWidth, detailsHeight), "You escaped! You win!", hudInfoStyle);
            }
            else if (hasRolled)
            {
                DrawRollDisplay(new Rect(contentX, detailsY + (detailsHeight - hudRollIconSize) * 0.5f,
                    hudRollIconSize, hudRollIconSize), hudTitleStyle);
                string movesText = IsRollAnimating ? "MOVING" : "MOVES " + movesRemaining;
                GUI.Label(new Rect(contentX + hudRollTextOffset, detailsY,
                    actionWidth - hudRollTextOffset, detailsHeight), movesText, hudInfoStyle);
            }
            else
            {
                DrawDie(new Rect(contentX, detailsY + (detailsHeight - hudRollIconSize) * 0.5f,
                    hudRollIconSize, hudRollIconSize), 1);
                Rect promptRect = new Rect(contentX + hudRollTextOffset, detailsY,
                    Mathf.Max(0f, actionWidth - hudRollTextOffset), detailsHeight);
                string prompt = hudActionStyle.CalcSize(new GUIContent(rollPrompt)).x <= promptRect.width
                    ? rollPrompt : compactRollPrompt;
                DrawHudRollPrompt(promptRect, prompt);
            }

            if (gameTimer != null)
            {
                int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(gameTimer.TimeRemaining));
                string time = (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
                float timerY = stackHudDetails ? detailsY + detailsHeight : detailsY;
                DrawHudTimerIcon(new Rect(timerX, timerY + (detailsHeight - hudTimerIconSize) * 0.5f,
                    hudTimerIconSize, hudTimerIconSize));
                float timerTextOffset = hudTimerIconSize + hudColumnGap * 0.5f;
                GUI.Label(new Rect(timerX + timerTextOffset, timerY,
                    (stackHudDetails ? contentWidth : timerWidth) - timerTextOffset, detailsHeight), time, hudTimerStyle);
            }

            var gameMode = isTutorial ? null : Escape4Now.GameModes.GameMode.Current;
            string objective = gameMode != null ? gameMode.GetObjective(this) : "";
            if (!hasReachedExit && !string.IsNullOrEmpty(objective))
            {
                float minimumObjectiveY = detailsY + detailsHeight * (stackHudDetails ? 2f : 1f) + hudSectionGap * 2f;
                float objectiveY = Mathf.Max(minimumObjectiveY, panel.y + panel.height * hudObjectiveRowPosition);
                DrawHudColor(new Rect(contentX, objectiveY - hudSectionGap, contentWidth, hudDividerThickness), hudDividerColor);
                float objectiveTextY = objectiveY + hudObjectiveStyle.lineHeight + hudSectionGap * 0.5f;
                GUI.Label(new Rect(contentX, objectiveY, contentWidth, hudObjectiveStyle.lineHeight), "OBJECTIVE", hudObjectiveStyle);
                GUI.Label(new Rect(contentX, objectiveTextY, contentWidth * hudObjectiveTextWidthRatio,
                    Mathf.Max(0f, panel.yMax - hudPadding - objectiveTextY)), objective, hudObjectiveBodyStyle);
            }

            GUI.matrix = previousMatrix;
        }

        private void PrepareHudStyles()
        {
            if (hudTitleStyle == null)
            {
                hudTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(0, 0, 0, 0),
                    wordWrap = false
                };
                hudPlayerStyle = new GUIStyle(hudTitleStyle) { alignment = TextAnchor.MiddleRight };
                hudInfoStyle = new GUIStyle(hudTitleStyle);
                hudObjectiveStyle = new GUIStyle(hudTitleStyle);
                hudObjectiveBodyStyle = new GUIStyle(hudTitleStyle)
                {
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true
                };
                hudActionStyle = new GUIStyle(hudTitleStyle);
                hudPromptPrefixStyle = new GUIStyle(hudTitleStyle);
                hudTimerStyle = new GUIStyle(hudTitleStyle);
            }
            hudTitleStyle.fontSize = hudTitleFontSize;
            hudTitleStyle.normal.textColor = hudTextColor;
            hudPlayerStyle.fontSize = hudTitleFontSize;
            hudPlayerStyle.normal.textColor = hudTextColor;
            hudInfoStyle.fontSize = hudInfoFontSize;
            hudInfoStyle.normal.textColor = hudMutedTextColor;
            hudObjectiveStyle.fontSize = hudObjectiveFontSize;
            hudObjectiveStyle.normal.textColor = hudObjectiveColor;
            hudObjectiveBodyStyle.fontSize = hudObjectiveBodyFontSize;
            hudObjectiveBodyStyle.normal.textColor = hudTextColor;
            hudActionStyle.fontSize = hudInfoFontSize;
            hudActionStyle.normal.textColor = hudTextColor;
            hudPromptPrefixStyle.fontSize = hudInfoFontSize;
            hudPromptPrefixStyle.normal.textColor = hudMutedTextColor;
            hudTimerStyle.fontSize = hudTimerFontSize;
            hudTimerStyle.normal.textColor = hudTextColor;
        }

        private void DrawHudRollPrompt(Rect rect, string prompt)
        {
            int separator = prompt.IndexOf(':');
            if (separator < 0)
            {
                GUI.Label(rect, prompt, hudActionStyle);
                return;
            }
            string prefix = prompt.Substring(0, separator + 1) + " ";
            float prefixWidth = hudPromptPrefixStyle.CalcSize(new GUIContent(prefix)).x;
            GUI.Label(new Rect(rect.x, rect.y, prefixWidth, rect.height), prefix, hudPromptPrefixStyle);
            GUI.Label(new Rect(rect.x + prefixWidth, rect.y, Mathf.Max(0f, rect.width - prefixWidth), rect.height),
                prompt.Substring(separator + 1).TrimStart(), hudActionStyle);
        }

        private void DrawHudTimerIcon(Rect rect)
        {
            if (hudTimerIcon == null && (stopwatchTexture == null || builtStopwatchStroke != hudTimerIconStroke))
            {
                if (stopwatchTexture != null) Destroy(stopwatchTexture);
                stopwatchTexture = MakeStopwatchTexture(hudTimerIconStroke);
                builtStopwatchStroke = hudTimerIconStroke;
            }
            Color previous = GUI.color;
            GUI.color = hudTextColor;
            GUI.DrawTexture(rect, hudTimerIcon != null ? hudTimerIcon : stopwatchTexture, ScaleMode.ScaleToFit);
            GUI.color = previous;
        }

        private static Texture2D MakeStopwatchTexture(float stroke)
        {
            const int resolution = 64;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var center = new Vector2(0f, -0.08f);
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                Vector2 point = new Vector2((x + 0.5f) / resolution * 2f - 1f, (y + 0.5f) / resolution * 2f - 1f);
                float distance = Mathf.Abs((point - center).magnitude - 0.66f);
                distance = Mathf.Min(distance, DistanceToSegment(point, new Vector2(0f, 0.58f), new Vector2(0f, 0.8f)));
                distance = Mathf.Min(distance, DistanceToSegment(point, new Vector2(-0.14f, 0.8f), new Vector2(0.14f, 0.8f)));
                distance = Mathf.Min(distance, DistanceToSegment(point, new Vector2(0.49f, 0.46f), new Vector2(0.61f, 0.6f)));
                distance = Mathf.Min(distance, DistanceToSegment(point, center, center + new Vector2(0.28f, 0.3f)));
                float alpha = Mathf.Clamp01((stroke - distance) * resolution * 0.5f + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply(false, true);
            return texture;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float along = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
            return Vector2.Distance(point, start + segment * along);
        }

        private static void DrawHudOutline(Rect rect, Color color, float thickness, float cornerRadius)
        {
            if (thickness <= 0f) return;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, thickness, cornerRadius);
        }

        private static void DrawHudColor(Rect rect, Color color, float cornerRadius = 0f)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, cornerRadius);
        }
    //Shows the key's own image in a gold-bordered badge above a player who carries it.
private void DrawKeyMarker()
{
    bool hasKey = networkHasKey || (inventory != null && inventory.HasKey());
    Camera viewCamera = Camera.main;
    if (!hasKey || !isActiveAndEnabled || viewCamera == null || mapTemplate == null) return;
    var race = Escape4Now.GameModes.Race_Gamemode.Current;
    SpriteRenderer keyVisual = race != null && race.KeyPrefab != null
        ? race.KeyPrefab.GetComponentInChildren<SpriteRenderer>(true) : null;
    Sprite sprite = keyVisual != null ? keyVisual.sprite : null;
    if (sprite == null || sprite.texture == null) return;

    Vector3 screenPosition = viewCamera.WorldToScreenPoint(transform.position + Vector3.up * keyBadgeHeightAbovePlayer);
    if (screenPosition.z <= 0f) return;

    float badgeSize = keyBadgeIconSize + keyBadgePadding * 2f;

    float bx = Mathf.Clamp(screenPosition.x - badgeSize * 0.5f, 0f, Mathf.Max(0f, Screen.width - badgeSize));
    float by = Mathf.Clamp(Screen.height - screenPosition.y - badgeSize, 0f, Mathf.Max(0f, Screen.height - badgeSize));
    Rect badge = new Rect(bx, by, badgeSize, badgeSize);

    Color oldColor = GUI.color;

    // Gold square first, then a dark square on top, so the gold shows as a border
    GUI.color = keyBadgeBorderColor;
    GUI.DrawTexture(badge, Texture2D.whiteTexture);
    GUI.color = keyBadgeBackgroundColor;
    GUI.DrawTexture(new Rect(badge.x + keyBadgeBorder, badge.y + keyBadgeBorder,
        badge.width - keyBadgeBorder * 2f, badge.height - keyBadgeBorder * 2f), Texture2D.whiteTexture);

    // The key image, centered inside the badge
    Rect spriteRect = sprite.textureRect;
    Rect uv = new Rect(spriteRect.x / sprite.texture.width, spriteRect.y / sprite.texture.height,
        spriteRect.width / sprite.texture.width, spriteRect.height / sprite.texture.height);
    GUI.color = keyVisual.color;
    GUI.DrawTextureWithTexCoords(new Rect(badge.x + keyBadgePadding, badge.y + keyBadgePadding, keyBadgeIconSize, keyBadgeIconSize),
        sprite.texture, uv, true);

    GUI.color = oldColor;
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
    float size = Mathf.Min(area.width, area.height);
    Rect die = new Rect(area.center.x - size * 0.5f, area.center.y - size * 0.5f, size, size);

    if (t < diceRollAnimationDuration)
    {
        face = (int)(Time.realtimeSinceStartup * 18f) % 6 + 1;              // flicker, no Random
        die.x += Mathf.Sin(t * 60f) * size * 0.08f * (1f - t / diceRollAnimationDuration); // shake that fades out
    }

    DrawDie(die, face);
}

// Draws the configured die face; pip positions encode the six standard faces.
private void DrawDie(Rect rect, int face)
{
    if (circleTexture == null) circleTexture = MakeCircleTexture(32);
    Color oldColor = GUI.color;

    float border = Mathf.Min(hudDieBorderThickness, rect.width * 0.25f);
    DrawHudColor(rect, hudDieBorderColor, hudDieCornerRadius);
    DrawHudColor(new Rect(rect.x + border, rect.y + border, rect.width - border * 2f, rect.height - border * 2f),
        hudDieFaceColor, Mathf.Max(0f, hudDieCornerRadius - border));

    GUI.color = hudDiePipColor;
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
            if (!CanReadTurnInput || hasRolled || IsMoving || hasReachedExit || isUsingItem || isInMenu || !IsCurrentTurn) return false;
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
            turnRingSize.x = float.IsNaN(turnRingSize.x) ? 0.86f : Mathf.Clamp(turnRingSize.x, 0.1f, 5f);
            turnRingSize.y = float.IsNaN(turnRingSize.y) ? 0.42f : Mathf.Clamp(turnRingSize.y, 0.1f, 5f);
            turnRingGlowStrength = float.IsNaN(turnRingGlowStrength) ? 0.22f : Mathf.Clamp01(turnRingGlowStrength);
            turnRingPulseSpeed = float.IsNaN(turnRingPulseSpeed) ? 1.5f : Mathf.Clamp(turnRingPulseSpeed, 0f, 20f);
            turnRingPulseAmount = float.IsNaN(turnRingPulseAmount) ? 0.035f : Mathf.Clamp(turnRingPulseAmount, 0f, 0.15f);
            turnRingThickness = float.IsNaN(turnRingThickness) ? 0.045f : Mathf.Clamp(turnRingThickness, 0.01f, 0.2f);
            turnRingGroundOffset = float.IsNaN(turnRingGroundOffset) ? 0.015f : Mathf.Clamp(turnRingGroundOffset, -0.5f, 0.5f);
            hudTopLeftMargin.x = Mathf.Max(0f, hudTopLeftMargin.x);
            hudTopLeftMargin.y = Mathf.Max(0f, hudTopLeftMargin.y);
            hudPanelSize.x = float.IsNaN(hudPanelSize.x) ? 260f : Mathf.Clamp(hudPanelSize.x, 200f, 1200f);
            hudPanelSize.y = float.IsNaN(hudPanelSize.y) ? 210f : Mathf.Clamp(hudPanelSize.y, 48f, 300f);
            tutorialHudSize.x = float.IsNaN(tutorialHudSize.x) ? 360f : Mathf.Clamp(tutorialHudSize.x, 200f, 1200f);
            tutorialHudSize.y = float.IsNaN(tutorialHudSize.y) ? 118f : Mathf.Clamp(tutorialHudSize.y, 48f, 300f);
            hudPadding = float.IsNaN(hudPadding) ? 12f : Mathf.Clamp(hudPadding, 4f, 32f);
            hudTopRightClearance = float.IsNaN(hudTopRightClearance) ? 150f : Mathf.Clamp(hudTopRightClearance, 0f, 500f);
            hudTitleFontSize = Mathf.Clamp(hudTitleFontSize, 12, 28);
            hudInfoFontSize = Mathf.Clamp(hudInfoFontSize, 10, 20);
            hudTimerFontSize = Mathf.Clamp(hudTimerFontSize, 10, 24);
            hudObjectiveFontSize = Mathf.Clamp(hudObjectiveFontSize, 10, 24);
            hudObjectiveBodyFontSize = Mathf.Clamp(hudObjectiveBodyFontSize, 10, 24);
            hudBorderThickness = Mathf.Clamp(hudBorderThickness, 0f, hudPadding);
            hudCornerRadius = Mathf.Clamp(hudCornerRadius, 0f, 8f);
            hudTimerWidth = Mathf.Clamp(hudTimerWidth, 60f, 300f);
            hudRollIconSize = Mathf.Clamp(hudRollIconSize, 16f, 80f);
            hudColumnGap = Mathf.Clamp(hudColumnGap, 0f, 24f);
            hudRollTextOffset = Mathf.Clamp(hudRollTextOffset, 24f, 160f);
            hudRollTextOffset = Mathf.Max(hudRollTextOffset, hudRollIconSize + hudColumnGap * 0.5f);
            hudDetailsRowHeight = Mathf.Clamp(hudDetailsRowHeight, hudRollIconSize, 100f);
            hudSectionGap = Mathf.Clamp(hudSectionGap, 0f, 24f);
            hudDividerThickness = Mathf.Clamp(hudDividerThickness, 0f, 4f);
            hudTimerIconSize = Mathf.Clamp(hudTimerIconSize, 8f, 48f);
            hudTimerIconStroke = Mathf.Clamp(hudTimerIconStroke, 0.01f, 0.1f);
            hudDieBorderThickness = Mathf.Clamp(hudDieBorderThickness, 0f, hudRollIconSize * 0.25f);
            hudDieCornerRadius = Mathf.Clamp(hudDieCornerRadius, 0f, 12f);
            if (string.IsNullOrWhiteSpace(rollPrompt)) rollPrompt = "ROLL: SPACE";
            if (string.IsNullOrWhiteSpace(compactRollPrompt)) compactRollPrompt = "SPACE";
            hudDetailsRowPosition = Mathf.Clamp(hudDetailsRowPosition, 0.1f, 0.6f);
            hudObjectiveRowPosition = Mathf.Clamp(hudObjectiveRowPosition, 0.3f, 0.85f);
            hudObjectiveTextWidthRatio = Mathf.Clamp(hudObjectiveTextWidthRatio, 0.5f, 1f);
            keyBadgeHeightAbovePlayer = float.IsNaN(keyBadgeHeightAbovePlayer) ? 1.05f : Mathf.Clamp(keyBadgeHeightAbovePlayer, 0f, 5f);
            keyBadgeIconSize = float.IsNaN(keyBadgeIconSize) ? 28f : Mathf.Clamp(keyBadgeIconSize, 8f, 128f);
            keyBadgePadding = float.IsNaN(keyBadgePadding) ? 6f : Mathf.Clamp(keyBadgePadding, 0f, 32f);
            keyBadgeBorder = float.IsNaN(keyBadgeBorder) ? 2f : Mathf.Clamp(keyBadgeBorder, 0f, 12f);
            diceRollAnimationDuration = float.IsNaN(diceRollAnimationDuration) ? 0.9f : Mathf.Clamp(diceRollAnimationDuration, 0.1f, 5f);
            sharedTileHorizontalOffset = float.IsNaN(sharedTileHorizontalOffset) ? 0.14f : Mathf.Clamp(sharedTileHorizontalOffset, 0f, 1f);
            sharedTileVerticalOffset = float.IsNaN(sharedTileVerticalOffset) ? 0.08f : Mathf.Clamp(sharedTileVerticalOffset, 0f, 1f);
        }

        //Places the player on an open tile without playing a movement animation.
        public bool SetGridPosition(Vector2Int newGridPosition)
        {
            if (Time.timeScale <= 0f || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused
                || (IsMoving && !resolvingStep) || hasReachedExit || mapTemplate == null || !mapTemplate.IsWalkable(newGridPosition))
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
            if (!CanReadTurnInput || !isActiveAndEnabled || IsMoving || hasReachedExit || mapTemplate == null
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
            if (turnRingTransform != null) UpdateTurnRingLayout();
        }

        private void SetupTurnRing()
        {
            turnRingTransform = transform.Find("Active Turn Ring");
            if (turnRingTransform == null)
            {
                GameObject ringObject = new GameObject("Active Turn Ring");
                turnRingTransform = ringObject.transform;
                turnRingTransform.SetParent(transform, false);
            }

            turnRingRenderer = turnRingTransform.GetComponent<SpriteRenderer>();
            if (turnRingRenderer == null) turnRingRenderer = turnRingTransform.gameObject.AddComponent<SpriteRenderer>();
            RebuildTurnRingSprite();
            UpdateTurnRingLayout();
            UpdateTurnRing();
        }

        private void UpdateTurnRingLayout()
        {
            if (turnRingTransform == null) return;
            turnRingTransform.localPosition = new Vector3(0f, turnRingGroundOffset, 0.01f);
            turnRingTransform.localScale = new Vector3(
                turnRingSize.x / Mathf.Max(0.01f, markerWidth),
                turnRingSize.y / Mathf.Max(0.01f, markerHeight), 1f);
            if (spriteRenderer != null && turnRingRenderer != null)
            {
                turnRingRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                turnRingRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
            }
        }

        private void UpdateTurnRing()
        {
            if (turnRingRenderer == null || turnRingTransform == null) return;
            if (!Mathf.Approximately(turnRingGlowStrength, builtTurnRingGlow)
                || !Mathf.Approximately(turnRingThickness, builtTurnRingThickness))
                RebuildTurnRingSprite();

            bool isActivePlayer = isActiveAndEnabled
                && (turnSystem != null ? turnSystem.IsPlayersTurn(this) : isTutorial);
            if (turnRingRenderer.enabled != isActivePlayer) turnRingRenderer.enabled = isActivePlayer;
            turnRingTransform.localPosition = new Vector3(0f, turnRingGroundOffset, 0.01f);
            if (!isActivePlayer) return;

            float pulse = Mathf.Sin(Time.unscaledTime * turnRingPulseSpeed * Mathf.PI * 2f);
            float scale = 1f + pulse * turnRingPulseAmount;
            turnRingTransform.localScale = new Vector3(
                turnRingSize.x * scale / Mathf.Max(0.01f, markerWidth),
                turnRingSize.y * scale / Mathf.Max(0.01f, markerHeight), 1f);
            turnRingRenderer.color = turnRingColor;
            if (spriteRenderer != null) turnRingRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        }

        private void RebuildTurnRingSprite()
        {
            if (turnRingRenderer == null) return;
            ReleaseTurnRingSprite();

            const int textureSize = 128;
            turnRingTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            float center = (textureSize - 1) * 0.5f;
            const float ringRadius = 0.78f;
            float antiAlias = 2f / textureSize;
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    // Distance to an isometric diamond edge; normalize before smoothing its alpha.
                    float distance = Mathf.Abs(Mathf.Abs(dx) + Mathf.Abs(dy) - ringRadius) / Mathf.Sqrt(2f);
                    float edge = Mathf.InverseLerp(turnRingThickness, turnRingThickness + antiAlias, distance);
                    float line = 1f - Mathf.SmoothStep(0f, 1f, edge);
                    float glow = turnRingGlowStrength * Mathf.Exp(-(distance * distance) / 0.018f);
                    turnRingTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(line, glow)));
                }
            }
            turnRingTexture.Apply();
            builtTurnRingGlow = turnRingGlowStrength;
            builtTurnRingThickness = turnRingThickness;
            turnRingSprite = Sprite.Create(turnRingTexture,
                new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
            turnRingRenderer.sprite = turnRingSprite;
            turnRingRenderer.color = turnRingColor;
        }

        private void ReleaseTurnRingSprite()
        {
            if (turnRingSprite != null)
            {
                if (Application.isPlaying) Destroy(turnRingSprite);
                else DestroyImmediate(turnRingSprite);
                turnRingSprite = null;
            }
            if (turnRingTexture != null)
            {
                if (Application.isPlaying) Destroy(turnRingTexture);
                else DestroyImmediate(turnRingTexture);
                turnRingTexture = null;
            }
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

        //Releases the generated image and stops tracking this player on the map.
        private void OnDestroy()
        {
            if (mapTemplate != null)
            {
                mapTemplate.RemoveOccupant(registeredPosition);
            }

            ReleaseMarker();
            ReleaseTurnRingSprite();
            if (circleTexture != null) Destroy(circleTexture);
            if (stopwatchTexture != null) Destroy(stopwatchTexture);
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
