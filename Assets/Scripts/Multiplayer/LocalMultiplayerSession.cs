using System.Collections.Generic;
using Escape4Now.Map;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using Escape4Now.UI;
using Escape4Now.Bots;
using Escape4Now.GameModes;
using Escape4Now.Input;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Escape4Now.Multiplayer
{
    //Sets up two to four people sharing one computer and keyboard.
    [DefaultExecutionOrder(-10000)]
    public sealed class LocalMultiplayerSession : MonoBehaviour
    {
        [SerializeField] private bool showTemporaryUI = true;
        [Header("Game Mode")]
        [SerializeField] private string setupTitle = "Escape 4 Now";
        [SerializeField] private GameModeController gameMode;
        [SerializeField] private bool showDisplayOptions = true;
        [Header("Pause Menu Layout")]
        [SerializeField, Tooltip("Pause button width and height in screen pixels.")]
        private Vector2 pauseButtonSize = new Vector2(126f, 36f);
        [SerializeField, Tooltip("Pause button distance from the top and right edges.")]
        private Vector2 pauseButtonMargin = new Vector2(16f, 16f);
        [SerializeField, Tooltip("Pause panel width and height in screen pixels.")]
        private Vector2 pauseMenuSize = new Vector2(380f, 360f);
        [SerializeField] private string displayOptionsTitle = "DISPLAY OPTIONS";
        [Header("Player Colors")]
        [SerializeField] private Color[] playerColors =
        {
            new Color(0.15f, 0.7f, 1f), new Color(1f, 0.35f, 0.3f),
            new Color(0.3f, 0.85f, 0.4f), new Color(1f, 0.8f, 0.2f)
        };
        [Header("Pause Menu Colors")]
        [SerializeField] private Color pauseBackdropTint = new Color(0f, 0f, 0f, 0.52f);
        [SerializeField] private Color pausePanelColor = new Color(0.035f, 0.055f, 0.085f, 0.98f);
        [SerializeField] private Color pauseTitleBarColor = new Color(0.08f, 0.13f, 0.19f, 1f);
        [SerializeField] private Color pauseTitleColor = Color.white;
        [SerializeField, Range(18, 40)] private int pauseTitleFontSize = 28;
        [Header("Result Screen Text")]
        [SerializeField] private string resultTimeLabel = "TIME LEFT";
        [SerializeField] private string resultButtonLabel = "Back to Lobby";
        [Header("Result Screen Layout")]
        [SerializeField, Min(180f)] private float resultContentWidth = 420f;
        [SerializeField, Min(0f)] private float resultScreenMargin = 24f;
        [SerializeField, Min(0f)] private float resultSpacing = 18f;
        [SerializeField, Min(28f)] private float resultButtonHeight = 44f;
        [SerializeField, Min(1f)] private float resultAccentThickness = 2f;
        [SerializeField, Range(24, 72)] private int resultTitleFontSize = 48;
        [SerializeField, Range(12, 28)] private int resultMessageFontSize = 20;
        [SerializeField, Range(10, 24)] private int resultDetailFontSize = 16;
        [Header("Result Screen Colors")]
        [SerializeField] private Color resultBackdropColor = new Color(0.02f, 0.035f, 0.055f, 0.94f);
        [SerializeField] private Color resultWinColor = new Color(0.3f, 0.9f, 0.73f);
        [SerializeField] private Color resultLoseColor = new Color(1f, 0.43f, 0.4f);
        [SerializeField] private Color resultTextColor = Color.white;
        [SerializeField] private Color resultDetailColor = new Color(0.7f, 0.77f, 0.82f);
        private static LocalMultiplayerSession instance;
        private readonly List<MonoBehaviour> heldScripts = new List<MonoBehaviour>();
        private IsometricMapTemplate map;
        private IPlayerGridMap ActiveMap => gameMode != null ? gameMode.GridMap : null;
        public GameModeController Mode => gameMode;
        public static GameplayInputBindings InputBindings => instance != null && instance.gameMode != null
            ? instance.gameMode.InputBindings : null;
        private TurnSystemController turns;
        private GameTimer matchTimer;
        private PlayerCharacter sourcePlayer;
        private PlayerCharacter[] participants;
        private int playerCount = 2;
        private readonly string[] playerNames = { "Player 1", "Player 2", "Player 3", "Player 4" };
        [Header("Local Player Controllers")]
        [SerializeField, Tooltip("Default Human/Bot choices for the four lobby slots. Bots are supported in Race Mode only.")]
        private bool[] playerIsBot = new bool[4];
        private LanMultiplayerSession lan;
        public IReadOnlyList<PlayerCharacter> Participants => participants;
        private string status = "";
        private Vector2 setupScroll;
        private GUIStyle titleStyle, labelStyle, smallStyle, buttonStyle, selectedStyle, errorStyle;
        private GUIStyle pauseTitleStyle;
        private GUIStyle resultTitleStyle, resultMessageStyle, resultDetailStyle;
        private Texture2D buttonTexture, hoverTexture, selectedTexture;
        private bool pauseMenuOpen;
        private InputAction pauseAction;
        private bool showEventTiles;
        private bool showItemObstacles;
        private bool showKeyLocation;
        public bool MatchStarted { get; private set; }
        public string Status => status;
        public bool ShowTemporaryUI => showTemporaryUI;
        public static bool MatchIsRunning => instance != null && instance.MatchStarted;
        public static bool MatchHasEnded => instance != null && instance.MatchStarted && instance.HasMatchFinished();
        public static bool GameplayPaused => instance != null && instance.pauseMenuOpen;
        public static bool ShowItemObstacles => instance != null && instance.showItemObstacles;
        public static bool ShowKeyLocation => instance != null && instance.showKeyLocation;

        private void OnValidate()
        {
            if (playerIsBot == null || playerIsBot.Length != 4) System.Array.Resize(ref playerIsBot, 4);
            if (playerColors == null || playerColors.Length != 4)
                System.Array.Resize(ref playerColors, 4);
            pauseButtonSize = new Vector2(Mathf.Clamp(pauseButtonSize.x, 80f, 240f), Mathf.Clamp(pauseButtonSize.y, 28f, 80f));
            pauseButtonMargin = new Vector2(Mathf.Clamp(pauseButtonMargin.x, 0f, 100f), Mathf.Clamp(pauseButtonMargin.y, 0f, 100f));
            pauseMenuSize = new Vector2(Mathf.Clamp(pauseMenuSize.x, 320f, 700f), Mathf.Clamp(pauseMenuSize.y, showDisplayOptions ? 320f : 180f, 700f));
            if (string.IsNullOrWhiteSpace(displayOptionsTitle)) displayOptionsTitle = "DISPLAY OPTIONS";
            pauseTitleFontSize = Mathf.Clamp(pauseTitleFontSize, 18, 40);
            resultContentWidth = Mathf.Max(180f, resultContentWidth);
            resultScreenMargin = Mathf.Max(0f, resultScreenMargin);
            resultSpacing = Mathf.Max(0f, resultSpacing);
            resultButtonHeight = Mathf.Max(28f, resultButtonHeight);
            resultAccentThickness = Mathf.Max(1f, resultAccentThickness);
            resultTitleFontSize = Mathf.Clamp(resultTitleFontSize, 24, 72);
            resultMessageFontSize = Mathf.Clamp(resultMessageFontSize, 12, 28);
            resultDetailFontSize = Mathf.Clamp(resultDetailFontSize, 10, 24);
            pauseTitleStyle = null;
            resultTitleStyle = null;
        }

        private Color GetPlayerColor(int index)
        {
            return playerColors != null && index >= 0 && index < playerColors.Length
                ? playerColors[index] : Color.white;
        }

        //Turn controls are available once the shared-device match has started.
        public static bool AllowTurnButtons => (instance == null || instance.MatchStarted)
            && (!LanMultiplayerSession.Active || LanMultiplayerSession.Instance.IsMyTurn);

        //Clears the scene reference when another Play Mode run begins.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { instance = null; }

        //Applies the host's pause state to every local copy of a network match.
        internal void ApplyNetworkPause(bool paused, bool matchFinished)
        {
            pauseMenuOpen = paused;
            Time.timeScale = paused || matchFinished ? 0f : 1f;
        }

        //Waits for a player count without disabling a teammate's menu canvas.
        private void Awake()
        {
            instance = this;
            string configurationError = "";
            if (gameMode == null || !gameMode.ValidateConfiguration(out configurationError))
            {
                status = gameMode == null ? "Assign a game mode component in the Inspector." : configurationError;
                Debug.LogError(status, this);
                enabled = false;
                return;
            }
            map = ActiveMap as IsometricMapTemplate;
            turns = gameMode.Turns;
            matchTimer = gameMode.Timer;
            sourcePlayer = gameMode.PlayerTemplate;
            foreach (MonoBehaviour script in gameMode.GameplayComponents)
            {
                if (script == null || !script.enabled || heldScripts.Contains(script)) continue;
                heldScripts.Add(script);
                script.enabled = false;
            }
            if (sourcePlayer != null)
                foreach (Renderer renderer in sourcePlayer.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            if (gameMode.SupportsLan && map != null)
            {
                lan = gameObject.AddComponent<LanMultiplayerSession>();
                lan.Initialize(this);
            }
            pauseAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Pause") : null;
        }

        //The shared Pause action is supplied by the project's Input Actions asset.
        private void Update()
        {
            if (!MatchStarted || pauseAction == null || !pauseAction.WasPressedThisFrame() || HasMatchFinished()) return;
            if (pauseMenuOpen) ResumeMatch();
            else OpenPauseMenu();
        }

        private void OpenPauseMenu() { PauseGame(); }
        private void ResumeMatch() { ResumeGame(); }

        //A replacement UI can connect its player-count selector to this method.
        public void SetPlayerCount(int count) { playerCount = Mathf.Clamp(count, 2, 4); }

        //A replacement UI can turn off the placeholder panel without changing gameplay.
        public void SetTemporaryUIVisible(bool visible) { showTemporaryUI = visible; }

        //A replacement UI can connect its Start button to this method.
        public void StartLocalMatch() { TryStartLocalMatch(playerCount); }

        //Places separate players on valid room floors before any other spawners start.
        public bool TryStartLocalMatch(int count)
        {
            if (LanMultiplayerSession.Active) return false;
            return CreateParticipants(count);
        }

        //The network session calls this after the lobby is ready.
        internal bool StartNetworkMatch(int count) => gameMode != null && gameMode.SupportsLan && CreateParticipants(count);

        //Both play options share the same player setup.
        private bool CreateParticipants(int count)
        {
            if (MatchStarted) return false;
            if (count < 2 || count > 4)
            { status = "Choose two to four players."; return false; }
            var usedNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < count; i++)
                if (!usedNames.Add(GetPlayerName(i)))
                { status = "Each player needs a different name."; return false; }
            if (gameMode == null || !gameMode.ValidateConfiguration(out _) || ActiveMap == null || turns == null || sourcePlayer == null)
            { status = "The scene needs a map, player, turn system, and input actions."; return false; }
            try { ActiveMap.PrepareLayout(); }
            catch (System.InvalidOperationException error) { status = error.Message; return false; }
            //The hidden scene template has not joined yet, so its old tile is not reserved.
            ActiveMap.RemoveOccupant(sourcePlayer.GridPosition);
            var cells = new List<Vector2Int>();
            for (int i = 0; i < count; i++)
            {
                if (ActiveMap.TryGetPlayerSpawn(i, out Vector2Int cell)) cells.Add(cell);
            }
            if (cells.Count < count)
            { status = "There are not enough open spaces for the players."; return false; }

            participants = new PlayerCharacter[count];
            participants[0] = sourcePlayer;
            sourcePlayer.gameObject.SetActive(false);
            for (int i = 1; i < count; i++) participants[i] = Instantiate(sourcePlayer);

            for (int i = 0; i < count; i++)
            {
                PlayerCharacter player = participants[i];
                player.ConfigureParticipant(ActiveMap, turns, cells[i], i + 1, GetPlayerColor(i), GetPlayerName(i), gameMode);

                bool isBot = gameMode.SupportsBots && !LanMultiplayerSession.Active && IsBotPlayer(i);
                player.SetBotControlled(isBot);
                BotController bot = player.GetComponent<BotController>();
                if (isBot)
                {
                    if (bot == null)
                        bot = player.gameObject.AddComponent<BotController>();

                    bot.enabled = true;
                    bot.ResetSearchMemory();
                }
                else if (bot != null)
                {
                    bot.enabled = false;
                }
                player.enabled = true;

                if (player.Inventory != null)
                    player.Inventory.enabled = true;

                foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = true;
            }
            turns.SetPlayers(participants, ActiveMap.LayoutSeed);
            gameMode.EnableMatchInputs();
            foreach (PlayerCharacter player in participants) player.gameObject.SetActive(true);
            foreach (MonoBehaviour script in heldScripts)
            {
                if (script == null) continue;
                if (LanMultiplayerSession.IsClient && (script is Escape4Now.Items.ItemSpawner
                    || script is Escape4Now.Obstacles.ObstacleSpawner)) continue;
                script.enabled = true;
            }
            if (LanMultiplayerSession.IsClient && map != null) map.GenerateBlankMap();
            MatchStarted = true;
            status = "";
            Time.timeScale = 1f;
            return true;
        }

        //Returns whether the selected player slot is a bot.
        public bool IsBotPlayer(int index)
        {
            return gameMode != null && gameMode.SupportsBots && index >= 0
                && playerIsBot != null && index < playerIsBot.Length && playerIsBot[index];
        }

        public void SetBotPlayer(int index, bool value)
        {
            if (MatchStarted || index < 0 || index >= playerIsBot.Length) return;
            playerIsBot[index] = gameMode != null && gameMode.SupportsBots && value;
        }

        //Temporary controls can be replaced by the team's main menu later.
        private void OnGUI()
        {
            PrepareSetupStyles();
            if (MatchStarted)
            {
                if (gameMode.TryGetResult(participants, out MatchResult result)) DrawMatchResult(result);
                else DrawMatchPauseMenu();
                return;
            }
            if (!showTemporaryUI) return;
            DrawSetupColor(new Rect(0, 0, Screen.width, Screen.height), new Color(0.075f, 0.085f, 0.08f));
            float width = Mathf.Max(180, Mathf.Min(480, Screen.width - 32));
            float height = Mathf.Max(120, Mathf.Min(530, Screen.height - 32));
            Rect area = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
            setupScroll = GUI.BeginScrollView(area, setupScroll, new Rect(0, 0, width - 20, 520));
            float contentWidth = width - 24;
            GUI.Label(new Rect(0, 0, contentWidth, 44), setupTitle, titleStyle);
            GUI.enabled = !LanMultiplayerSession.Active && (lan == null || !lan.Connecting);
            GUI.Toolbar(new Rect(0, 50, contentWidth, 32), 0, new[] { "Same Device" });
            GUI.enabled = true;
            DrawSetupColor(new Rect(0, 90, contentWidth, 1), new Color(0.23f, 0.27f, 0.24f));
            GUI.Label(new Rect(0, 108, contentWidth, 24), "Players", labelStyle);
            float segmentWidth = (contentWidth - 16) / 3;
            for (int count = 2; count <= 4; count++)
            {
                Rect choice = new Rect((count - 2) * (segmentWidth + 8), 142, segmentWidth, 44);
                if (GUI.Button(choice, count.ToString(), playerCount == count ? selectedStyle : buttonStyle))
                    SetPlayerCount(count);
            }
            for (int i = 0; i < 4; i++)
            {
                float y = 212 + i * 42;
                bool playing = i < playerCount;
                Color color = playing ? GetPlayerColor(i) : new Color(0.24f, 0.27f, 0.25f);
                DrawSetupColor(new Rect(8, y + 4, 14, 26), color);

                if (playing)
                {
                    GUI.Label(new Rect(40, y, 48, 32), "Name", smallStyle);

                    float buttonWidth = gameMode != null && gameMode.SupportsBots ? 76 : 0;
                    float buttonX = contentWidth - buttonWidth;
                    float nameWidth = Mathf.Max(30, buttonX - 100);

                    if (IsBotPlayer(i))
                    {
                        GUI.Label(new Rect(92, y + 2, nameWidth, 28), "Bot " + GetBotNumber(i), buttonStyle);
                    }
                    else
                    {
                        playerNames[i] = GUI.TextField(
                            new Rect(92, y + 2, nameWidth, 28),
                            playerNames[i], 16, buttonStyle);
                    }

                    if (buttonWidth > 0 && GUI.Button(
                        new Rect(buttonX, y + 2, buttonWidth, 28),
                        playerIsBot[i] ? "Bot" : "Human",
                        playerIsBot[i] ? selectedStyle : buttonStyle))
                    {
                        SetBotPlayer(i, !IsBotPlayer(i));
                    }
                }
                else
                {
                    GUI.Label(new Rect(40, y + 4, 110, 26), "Not playing", smallStyle);
                }

                DrawSetupColor(
                    new Rect(0, y + 38, contentWidth, 1),
                    new Color(0.16f, 0.19f, 0.17f));
            }
            if (GUI.Button(new Rect(0, 402, contentWidth, 48), "Start Match", selectedStyle)) StartLocalMatch();
            GUI.Label(new Rect(0, 464, contentWidth, 56), status, errorStyle);
            GUI.EndScrollView();
        }

        //Keeps match controls in one menu instead of covering the map.
        public void PauseGame()
        {
            if (!MatchStarted || HasMatchFinished()) return;
            pauseMenuOpen = true;
            Time.timeScale = 0f;
            if (LanMultiplayerSession.Active) lan.Submit("pause");
        }

        public void ResumeGame()
        {
            if (LanMultiplayerSession.Active) lan.Submit("resume");
            else ApplyNetworkPause(false, HasMatchFinished());
        }

        private void DrawMatchPauseMenu()
        {
            Rect pauseButton = new Rect(Screen.width - pauseButtonMargin.x - pauseButtonSize.x,
                pauseButtonMargin.y, pauseButtonSize.x, pauseButtonSize.y);
            if (!pauseMenuOpen && GUI.Button(pauseButton, "Pause", buttonStyle)) OpenPauseMenu();

            if (!pauseMenuOpen) return;

            float panelWidth = Mathf.Min(pauseMenuSize.x, Screen.width - pauseButtonMargin.x * 2f);
            float panelHeight = Mathf.Min(pauseMenuSize.y, Screen.height - pauseButtonMargin.y * 2f);
            Rect panel = new Rect((Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);
            DrawSetupColor(new Rect(0f, 0f, Screen.width, Screen.height), pauseBackdropTint);
            DrawSetupColor(panel, pausePanelColor);
            DrawSetupColor(new Rect(panel.x, panel.y, panel.width, 46f), pauseTitleBarColor);
            if (pauseTitleStyle == null)
            {
                pauseTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = pauseTitleFontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                pauseTitleStyle.normal.textColor = pauseTitleColor;
            }
            GUI.Label(new Rect(panel.x + 12f, panel.y + 4f, panel.width - 24f, 38f), "PAUSED", pauseTitleStyle);
            float left = panel.x + 20f;
            float rowWidth = panel.width - 40f;
            if (GUI.Button(new Rect(left, panel.y + 52f, rowWidth, 38f), "Resume Game", selectedStyle))
                ResumeMatch();

            if (showDisplayOptions)
            {
                GUI.Label(new Rect(left, panel.y + 100f, rowWidth, 24f), displayOptionsTitle, labelStyle);
                DrawPauseToggle(new Rect(left, panel.y + 132f, rowWidth, 30f), "Show Event Tiles", ref showEventTiles, true);
                DrawPauseToggle(new Rect(left, panel.y + 170f, rowWidth, 30f), "Show Item Obstacles", ref showItemObstacles);
                DrawPauseToggle(new Rect(left, panel.y + 208f, rowWidth, 30f), "Show Key Location", ref showKeyLocation);
            }

            float leaveY = showDisplayOptions ? panel.y + 278f : panel.yMax - 58f;
            DrawSetupColor(new Rect(left, leaveY - 20f, rowWidth, 1f), new Color(1f, 1f, 1f, 0.2f));
            if (GUI.Button(new Rect(left, leaveY, rowWidth, 38f), "Leave Match", buttonStyle))
                ReturnToSetup();
        }

        //Sends event visibility changes to the controller that already owns the event markers.
        private void DrawPauseToggle(Rect rect, string label, ref bool value, bool controlsEventVisibility = false)
        {
            bool nextValue = GUI.Toggle(rect, value, label);
            if (nextValue == value) return;
            value = nextValue;
            if (controlsEventVisibility)
                map?.GetComponent<MapEventController>()?.SetEventLocationsVisible(value);
        }

        //Keeps a finished match stopped if the pause menu is opened after the result.
        private bool HasMatchFinished()
        {
            return gameMode != null && gameMode.TryGetResult(participants, out _);
        }

        private void DrawMatchResult(MatchResult result)
        {
            Color accent = result.Won ? resultWinColor : resultLoseColor;
            if (resultTitleStyle == null)
            {
                resultTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = resultTitleFontSize, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter, wordWrap = true
                };
                resultMessageStyle = new GUIStyle(resultTitleStyle) { fontSize = resultMessageFontSize, fontStyle = FontStyle.Normal };
                resultDetailStyle = new GUIStyle(resultMessageStyle) { fontSize = resultDetailFontSize };
            }
            resultTitleStyle.normal.textColor = accent;
            resultMessageStyle.normal.textColor = resultTextColor;
            resultDetailStyle.normal.textColor = resultDetailColor;
            string title = result.Title;
            string message = result.Message;
            int seconds = matchTimer != null ? Mathf.Max(0, Mathf.CeilToInt(matchTimer.TimeRemaining)) : 0;
            string detail = resultTimeLabel + " " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");

            Matrix4x4 previousMatrix = HudScale.Apply();
            int previousDepth = GUI.depth;
            bool previousEnabled = GUI.enabled;
            GUI.depth = -1000;
            GUI.enabled = true;
            DrawSetupColor(new Rect(0f, 0f, HudScale.Width, HudScale.Height), resultBackdropColor);
            float width = Mathf.Max(1f, Mathf.Min(resultContentWidth, HudScale.Width - resultScreenMargin * 2f));
            float titleHeight = resultTitleStyle.CalcHeight(new GUIContent(title), width);
            float messageHeight = resultMessageStyle.CalcHeight(new GUIContent(message), width);
            float detailHeight = resultDetailStyle.CalcHeight(new GUIContent(detail), width);
            float height = titleHeight + resultAccentThickness + messageHeight + detailHeight + resultButtonHeight + resultSpacing * 5f;
            float left = (HudScale.Width - width) * 0.5f;
            float top = Mathf.Max(resultScreenMargin, (HudScale.Height - height) * 0.5f);
            GUI.Label(new Rect(left, top, width, titleHeight), title, resultTitleStyle);
            top += titleHeight + resultSpacing;
            DrawSetupColor(new Rect(left + width * 0.4f, top, width * 0.2f, resultAccentThickness), accent);
            top += resultAccentThickness + resultSpacing;
            GUI.Label(new Rect(left, top, width, messageHeight), message, resultMessageStyle);
            top += messageHeight + resultSpacing;
            GUI.Label(new Rect(left, top, width, detailHeight), detail, resultDetailStyle);
            top += detailHeight + resultSpacing * 2f;
            if (GUI.Button(new Rect(left, top, width, resultButtonHeight), resultButtonLabel, selectedStyle)) ReturnToSetup();
            GUI.enabled = previousEnabled;
            GUI.depth = previousDepth;
            GUI.matrix = previousMatrix;
        }

        private string GetPlayerName(int index)
        {
            string value = index >= 0 && index < playerNames.Length ? playerNames[index] : null;
            value = string.IsNullOrWhiteSpace(value) ? "Player " + (index + 1) : value.Trim();
            value = value.Replace("<", "").Replace(">", "").Trim();
            if (value.Length == 0) value = "Player " + (index + 1);
            return value.Length > 16 ? value.Substring(0, 16) : value;
        }

        //Returns the bot's display number.
        private int GetBotNumber(int index)
        {
            return index + 1;
        }

        //Creates the temporary menu styles once, instead of allocating them every frame.
        private void PrepareSetupStyles()
        {
            if (titleStyle != null) return;
            buttonTexture = MakeSetupTexture(new Color(0.15f, 0.18f, 0.16f));
            hoverTexture = MakeSetupTexture(new Color(0.27f, 0.36f, 0.3f));
            selectedTexture = MakeSetupTexture(new Color(0.36f, 0.7f, 0.48f));
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = new Color(0.95f, 0.97f, 0.95f);
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            labelStyle.normal.textColor = new Color(0.9f, 0.93f, 0.9f);
            smallStyle = new GUIStyle(labelStyle) { fontSize = 14 };
            smallStyle.normal.textColor = new Color(0.6f, 0.67f, 0.62f);
            errorStyle = new GUIStyle(smallStyle) { wordWrap = true };
            errorStyle.normal.textColor = new Color(1f, 0.62f, 0.53f);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 17, alignment = TextAnchor.MiddleCenter, border = new RectOffset() };
            buttonStyle.normal.background = buttonTexture;
            buttonStyle.hover.background = hoverTexture;
            buttonStyle.active.background = selectedTexture;
            buttonStyle.focused.background = hoverTexture;
            buttonStyle.normal.textColor = buttonStyle.hover.textColor = buttonStyle.focused.textColor = Color.white;
            buttonStyle.active.textColor = new Color(0.04f, 0.1f, 0.06f);
            selectedStyle = new GUIStyle(buttonStyle) { fontStyle = FontStyle.Bold };
            selectedStyle.normal.background = selectedTexture;
            selectedStyle.normal.textColor = new Color(0.04f, 0.1f, 0.06f);
            selectedStyle.hover.background = selectedTexture;
            selectedStyle.hover.textColor = selectedStyle.normal.textColor;
            pauseTitleStyle = null;
        }

        //Makes a small reusable button background.
        private Texture2D MakeSetupTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        //Draws a flat color while keeping other GUI colors unchanged.
        private void DrawSetupColor(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        //A new match reloads the scene and gets a new school floor plan.
        public void ReturnToSetup()
        {
            if (lan != null) lan.Disconnect();
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameObject.scene.name);
        }

        //Clears only this session's static reference when the scene closes.
        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (buttonTexture != null) Destroy(buttonTexture);
            if (hoverTexture != null) Destroy(hoverTexture);
            if (selectedTexture != null) Destroy(selectedTexture);
        }
    }
}
