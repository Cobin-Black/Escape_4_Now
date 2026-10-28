// Manages shared-device player setup, starts the chosen mode and coordinates Pause and match results.

using System.Collections.Generic;
using Escape4Now.Map;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using Escape4Now.Bots;
using Escape4Now.GameModes;
using Escape4Now.Input;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Escape4Now.Multiplayer
{
    //Owns participant setup, match lifecycle, and the shared pause state.
    [DefaultExecutionOrder(-10000)]
    public sealed class LocalMultiplayerSession : MonoBehaviour
    {
        [SerializeField] private bool showTemporaryUI = true;
        [Header("Game Mode")]
        [SerializeField] private string setupTitle = "Escape 4 Now";
        [SerializeField] private GameModeController gameMode;
        [SerializeField] private bool showDisplayOptions = true;
        [Header("Player Colors")]
        [SerializeField]
        private Color[] playerColors =
        {
            new Color(0.15f, 0.7f, 1f), new Color(1f, 0.35f, 0.3f),
            new Color(0.3f, 0.85f, 0.4f), new Color(1f, 0.8f, 0.2f)
        };
        [Header("Player Skins")]
        [SerializeField, Tooltip("Gives each player a different skin when the match starts.")]
        private Skin_Handler skinHandler;
        private static LocalMultiplayerSession instance;
        private readonly List<MonoBehaviour> heldScripts = new List<MonoBehaviour>();
        private IsometricMapTemplate map;
        private IPlayerGridMap ActiveMap => gameMode != null ? gameMode.GridMap : null;
        public GameModeController Mode => gameMode;
        public Skin_Handler Skins => skinHandler;
        public static GameplayInputBindings InputBindings => instance != null && instance.gameMode != null
            ? instance.gameMode.InputBindings : null;
        private TurnSystemController turns;
        private GameTimer matchTimer;
        private PlayerCharacter sourcePlayer;
        private PlayerCharacter[] participants;
        [Header("Lobby Defaults")]
        [SerializeField, Range(1, 4)] private int playerCount = 2;
        [SerializeField] private string[] playerNames = { "Player 1", "Player 2", "Player 3", "Player 4" };
        [Header("Local Player Controllers")]
        [SerializeField, Tooltip("Default Human/Bot choices for the four lobby slots. Bots are supported in Race Mode only.")]
        private bool[] playerIsBot = new bool[4];
        [Header("Optional Network Component")]
        [SerializeField] private LanMultiplayerSession lan;

        [Header("Monster Mode")]
        [SerializeField] private MonsterCharacter monster;
        [SerializeField, Tooltip("Use the existing AI monster when no lobby slot selects Monster. Turn off for survivor-only map and puzzle tests.")]
        private bool spawnAIMonsterWhenUnassigned = true;
        public IReadOnlyList<PlayerCharacter> Participants => participants;
        private string status = "";
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
        private int monsterPlayerIndex = -1;

        private void OnValidate()
        {
            if (playerIsBot == null || playerIsBot.Length != 4) System.Array.Resize(ref playerIsBot, 4);
            if (playerColors == null || playerColors.Length != 4)
                System.Array.Resize(ref playerColors, 4);
            if (playerNames == null || playerNames.Length != 4)
                System.Array.Resize(ref playerNames, 4);
            playerCount = Mathf.Clamp(playerCount, gameMode != null ? gameMode.MinimumPlayers : 2, 4);
        }

        public Color GetPlayerColor(int index)
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
            if (gameMode.SupportsLan && map != null && lan != null)
            {
                lan.Initialize(this);
            }
            pauseAction = gameMode.InputBindings.Pause;
        }

        //The shared Pause action is supplied by the project's Input Actions asset.
        private void Update()
        {
            if (!MatchStarted)
                return;

            if (HasMatchFinished())
            {
                Time.timeScale = 0f;
                return;
            }

            if (pauseAction == null || !pauseAction.WasPressedThisFrame())
                return;

            if (pauseMenuOpen)
                ResumeMatch();
            else
                OpenPauseMenu();
        }

        private void OpenPauseMenu() { PauseGame(); }
        private void ResumeMatch() { ResumeGame(); }

        //A replacement UI can connect its player-count selector to this method.
        //Updates the lobby player count and clears invalid Monster selections.
        public void SetPlayerCount(int count)
        {
            playerCount = Mathf.Clamp(count, gameMode != null ? gameMode.MinimumPlayers : 2, gameMode != null ? gameMode.MaximumPlayers : 4);

            if (monsterPlayerIndex >= playerCount)
            {
                if (gameMode is SurvivalModeController survival) survival.SetSetupRole(monsterPlayerIndex, SurvivalRole.Survivor);
                monsterPlayerIndex = -1;
            }
        }

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


        // Creates the players and Monster for the selected game mode.
        // Check setup and prepare the chosen mode before cloning players or starting the timer.
        private bool CreateParticipants(int count)
        {
            if (MatchStarted) return false;
            if (gameMode == null || count < gameMode.MinimumPlayers || count > gameMode.MaximumPlayers)
            {
                status = "Choose a supported player count for this mode.";
                return false;
            }

            var usedNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (skinHandler != null && !skinHandler.AllAssigned(count))
            { status = "Each player needs to pick a skin."; return false; }
            for (int i = 0; i < count; i++)
            {
                if (!usedNames.Add(GetPlayerName(i)))
                {
                    status = "Each player needs a different name.";
                    return false;
                }
            }

            if (gameMode == null)
            {
                status = "Assign a game mode component in the Inspector.";
                return false;
            }
            if (!gameMode.ValidateConfiguration(out string configurationError))
            {
                status = configurationError;
                return false;
            }
            if (ActiveMap == null || turns == null || sourcePlayer == null)
            {
                status = "The scene needs a map, player, turn system, and input actions.";
                return false;
            }

            if (gameMode.SupportsBots && sourcePlayer.GetComponent<BotController>() == null)
            {
                status = "Assign a Bot Controller on the Race player template.";
                return false;
            }

            var survival = gameMode as SurvivalModeController;
            int monsterSlot = survival != null ? monsterPlayerIndex : -1;
            bool monsterIsPlayer = monsterSlot >= 0;
            bool monsterMode = monsterIsPlayer || survival != null && spawnAIMonsterWhenUnassigned && monster != null && !survival.HasExternalMonsterConfiguration;
            if (monsterIsPlayer && (monsterSlot >= count || count < 2 || monster == null))
            {
                status = "Assign a Monster and at least one survivor for the selected role.";
                return false;
            }

            try
            {
                ActiveMap.PrepareLayout();
            }
            catch (System.InvalidOperationException error)
            {
                status = error.Message;
                return false;
            }

            if (survival != null) survival.ConfigureMonster(monsterMode ? monster : null);
            if (!gameMode.PrepareMatch(count, out status)) return false;

            // Removes the hidden template from its previous tile.
            ActiveMap.RemoveOccupant(sourcePlayer.GridPosition);

            var cells = new List<Vector2Int>();

            for (int i = 0; i < count; i++)
            {
                if (ActiveMap.TryGetPlayerSpawn(i, out Vector2Int cell))
                    cells.Add(cell);
            }

            if (cells.Count < count)
            {
                status = "There are not enough open spaces for the players.";
                return false;
            }

            MonsterCharacter matchMonster = null;
            SurvivalMap survivalMap = null;
            Vector2Int monsterSpawn = default;

            if (monsterMode)
            {
                matchMonster = monster;
                survivalMap = ActiveMap as SurvivalMap;

                if (matchMonster == null || survivalMap == null)
                {
                    status = "Assign a Monster and Survival Map.";
                    return false;
                }


                monsterSpawn = survival.Objectives.MonsterSpawn;
            }

            int survivorCount = monsterIsPlayer ? count - 1 : count;
            participants = new PlayerCharacter[survivorCount];

            sourcePlayer.gameObject.SetActive(false);

            for (int i = 0; i < survivorCount; i++)
            {
                int lobbyIndex = i;

                // Skips the lobby slot selected to become the Monster.
                if (monsterIsPlayer && lobbyIndex >= monsterSlot)
                    lobbyIndex++;

                PlayerCharacter player = lobbyIndex == 0
                    ? sourcePlayer
                    : Instantiate(sourcePlayer);

                participants[i] = player;

                player.ConfigureParticipant(
                    ActiveMap,
                    turns,
                    cells[i],
                    lobbyIndex + 1,
                    GetPlayerColor(lobbyIndex),
                    GetPlayerName(lobbyIndex),
                    gameMode);

                //Uses the lobby slot so skins stay correct when a slot is skipped for the Monster.
                if (skinHandler != null)
                    skinHandler.ApplyTo(player, lobbyIndex);

                bool isBot = gameMode.SupportsBots
                    && !LanMultiplayerSession.Active
                    && IsBotPlayer(lobbyIndex);

                player.SetBotControlled(isBot);

                BotController bot = player.GetComponent<BotController>();

                if (isBot)
                {
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

            gameMode.BeginMatch(participants);
            if (survival != null) showEventTiles = survival.Events.EventLocationsVisible;
            turns.ConfigureEnemyTurn(matchMonster != null, matchMonster != null ? GetPlayerName(monsterSlot) : "", ActiveMap.LayoutSeed);
            turns.SetPlayers(gameMode.BuildTurnParticipants(participants), ActiveMap.LayoutSeed);

            // Initializes the Monster with the surviving players.
            if (matchMonster != null)
            {
                matchMonster.Initialize(
                    survivalMap,
                    turns,
                    participants,
                    monsterSpawn);
                matchMonster.SetAIControlled(!monsterIsPlayer);

                matchMonster.gameObject.SetActive(true);
                matchMonster.enabled = true;
            }

            gameMode.EnableMatchInputs();

            foreach (PlayerCharacter player in participants)
                player.gameObject.SetActive(true);

            foreach (MonoBehaviour script in heldScripts)
            {
                if (script == null) continue;

                if (LanMultiplayerSession.IsClient
                    && (script is Escape4Now.Items.ItemSpawner
                    || script is Escape4Now.Obstacles.ObstacleSpawner))
                    continue;

                script.enabled = true;
            }

            if (LanMultiplayerSession.IsClient && map != null)
                map.GenerateBlankMap();

            MatchStarted = true;
            status = "";
            Time.timeScale = 1f;

            return true;
        }

        // Selects or deselects a player as the Monster in Survival Mode.
        public void SetMonsterPlayer(int index)
        {
            if (MatchStarted || !(gameMode is SurvivalModeController)
                || index < 0 || index >= playerCount)
                return;

            monsterPlayerIndex = monsterPlayerIndex == index ? -1 : index;
            var survival = (SurvivalModeController)gameMode;
            for (int slot = 0; slot < gameMode.MaximumPlayers; slot++)
                survival.SetSetupRole(slot, slot == monsterPlayerIndex ? SurvivalRole.Monster : SurvivalRole.Survivor);
        }

        // Returns whether the specified player is selected as the Monster.
        public bool IsMonsterPlayer(int index)
        {
            return monsterPlayerIndex == index;
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

        public int PlayerCount => playerCount;
        public string SetupTitle => setupTitle;
        public bool ShowDisplayOptions => showDisplayOptions;
        public GameTimer Timer => matchTimer;
        public bool ShowEventTiles => showEventTiles;
        public bool TryGetResult(out MatchResult result)
        {
            result = default;
            return MatchStarted && gameMode != null && gameMode.TryGetResult(participants, out result);
        }

        public void SetPlayerName(int index, string value)
        {
            if (!MatchStarted && index >= 0 && index < playerNames.Length) playerNames[index] = value;
        }

        // Forward the reveal choice to the current mode's event source without changing event placement.
        public void SetEventTilesVisible(bool value)
        {
            showEventTiles = value;
            map?.GetComponent<MapEventController>()?.SetEventLocationsVisible(value);
            if (gameMode is SurvivalModeController survival) survival.Events.SetEventLocationsVisible(value);
        }

        public void SetItemObstaclesVisible(bool value) { showItemObstacles = value; }
        public void SetKeyLocationVisible(bool value) { showKeyLocation = value; }

        //Keeps a finished match stopped if the pause menu is opened after the result.
        private bool HasMatchFinished()
        {
            return gameMode != null && gameMode.TryGetResult(participants, out _);
        }

        public string GetPlayerName(int index)
        {
            string value = index >= 0 && index < playerNames.Length ? playerNames[index] : null;
            value = string.IsNullOrWhiteSpace(value) ? "Player " + (index + 1) : value.Trim();
            value = value.Replace("<", "").Replace(">", "").Trim();
            if (value.Length == 0) value = "Player " + (index + 1);
            return value.Length > 16 ? value.Substring(0, 16) : value;
        }

        //A new match reloads the scene and gets a new school floor plan.
        // Reload setup instead of carrying a finished match's objects and progress into the next match.
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
        }
    }
}
