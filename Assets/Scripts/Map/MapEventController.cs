using System.Collections.Generic;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using UnityEngine;

namespace Escape4Now.Map
{
    //Append new events so existing prefab numbers keep their meaning.
    public enum MapEventType { Warp, RandomEvent, Freeze, Blackout, LuckyRoll, LocationSwitch,
        ExtraStep, SecondChance, BadRoll, Stumble, WrongRoom, FireDrill, GoodLuckDay }
    public enum MapEventCategory { Positive, Negative, Neutral, Global }

    [System.Serializable]
    public sealed class MapEventSpawnChance
    {
        [SerializeField] private MapEventType eventType;
        [SerializeField, Range(0f, 1f)] private float chance = 1f;

        public MapEventType EventType { get => eventType; set => eventType = value; }
        public float Chance { get => chance; set => chance = value; }
    }

    //Places special tiles around the map and applies their effects when crossed.
    [RequireComponent(typeof(IsometricMapTemplate))]
    public sealed class MapEventController : MonoBehaviour
    {
        private IsometricMapTemplate map;
        private readonly Dictionary<Vector2Int, MapEventType> events = new Dictionary<Vector2Int, MapEventType>();
        private bool checkMovingEventPaths = true;
        [Header("Event Placement")]
        [SerializeField, Tooltip("Event prefabs available for spawning. Removing a prefab excludes that event tile; its spawn chance controls how often an included event appears.")]
        private EventTileMarker[] eventPrefabs;
        [SerializeField, HideInInspector] private bool eventCatalogInitialized;
        [Header("Event Spawn Chances")]
        [SerializeField] private MapEventSpawnChance[] spawnChances =
        {
            new MapEventSpawnChance { EventType = MapEventType.Warp, Chance = 0.6f },
            new MapEventSpawnChance { EventType = MapEventType.RandomEvent, Chance = 1f },
            new MapEventSpawnChance { EventType = MapEventType.Freeze, Chance = 0.35f },
            new MapEventSpawnChance { EventType = MapEventType.Blackout, Chance = 0.35f },
            new MapEventSpawnChance { EventType = MapEventType.LuckyRoll, Chance = 1f },
            new MapEventSpawnChance { EventType = MapEventType.LocationSwitch, Chance = 0.35f },
            new MapEventSpawnChance { EventType = MapEventType.ExtraStep, Chance = 1f },
            new MapEventSpawnChance { EventType = MapEventType.SecondChance, Chance = 1f },
            new MapEventSpawnChance { EventType = MapEventType.BadRoll, Chance = 1f },
            new MapEventSpawnChance { EventType = MapEventType.Stumble, Chance = 1f },
            new MapEventSpawnChance { EventType = MapEventType.WrongRoom, Chance = 0.6f },
            new MapEventSpawnChance { EventType = MapEventType.FireDrill, Chance = 0.35f },
            new MapEventSpawnChance { EventType = MapEventType.GoodLuckDay, Chance = 1f }
        };
        private EventTileMarker[] availablePrefabs;
        private bool eventPrefabsDirty = true;
        private string message = "";
        private int visibilityTurnsRemaining;
        private bool blackoutStartedThisTurn;
        private TurnSystemController turnSystem;
        private Texture2D visibilityMask;
        private PlayerCharacter[] players;
        private GUIStyle noticeText;
        private GUIStyle noticeTitle;
        private HashSet<MapEventType> allowedTypes;
        private readonly Dictionary<MapEventType, Vector2Int> forcedTiles = new Dictionary<MapEventType, Vector2Int>();
        private readonly List<EventTileMarker> placedMarkers = new List<EventTileMarker>();
        private readonly Dictionary<EventTileMarker, Vector2Int> cellsByMarker = new Dictionary<EventTileMarker, Vector2Int>();
        private bool showEventLocations;
        private bool isTutorial;
        private GUIStyle eventToggleStyle;

        public event System.Action<PlayerCharacter, MapEventType> EventTriggered;

        //Lets other spawners wait until every event has its tile.
        public bool EventsPlaced { get; private set; }
        public string CurrentMessage => message;
        public int BlackoutTurns => visibilityTurnsRemaining;
        public bool EventLocationsVisible => showEventLocations;
        public Vector2Int GetEventCell(EventTileMarker marker)
        {
            return marker != null && cellsByMarker.TryGetValue(marker, out Vector2Int cell)
                ? cell : map.WorldToGrid(marker.transform.position);
        }

        public void RegisterMarker(EventTileMarker marker)
        {
            if (marker == null || map == null) return;
            RegisterMarker(marker, map.WorldToGrid(marker.transform.position));
        }

        public void RegisterMarker(EventTileMarker marker, Vector2Int cell)
        {
            if (marker == null || map == null) return;
            if (!placedMarkers.Contains(marker)) placedMarkers.Add(marker);
            cellsByMarker[marker] = cell;
            marker.SetVisible(showEventLocations);
        }

        public void SetEventLocationsVisible(bool visible)
        {
            showEventLocations = visible;
            placedMarkers.RemoveAll(marker => marker == null);
            foreach (EventTileMarker marker in placedMarkers)
            {
                marker.SetVisible(visible);
            }
        }

        public void OnlyPlaceEvents(params MapEventType[] types)
        {
            allowedTypes = new HashSet<MapEventType>(types ?? System.Array.Empty<MapEventType>());
        }

        public void ForceEventTile(MapEventType type, Vector2Int cell)
        {
            if (System.Enum.IsDefined(typeof(MapEventType), type)) forcedTiles[type] = cell;
        }

        //Finds a known event template without loading anything supplied over the network.
        public EventTileMarker GetNetworkPrefab(int type)
        {
            RefreshEventPrefabs();
            foreach (EventTileMarker prefab in availablePrefabs)
                if (prefab != null && (int)prefab.EventType == type) return prefab;
            return null;
        }

        //Copies the host's event message and visibility timer for display only.
        public void ApplyNetworkDisplay(string text, int blackout)
        {
            message = text;
            visibilityTurnsRemaining = blackout;
            if (players == null) players = FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);
        }

        //Checks whether an event marker sits on a tile.
        public bool HasEvent(Vector2Int cell)
        {
            return events.ContainsKey(cell);
        }

        //Finds the map used by the event tiles.
        private void Awake()
        {
            map = GetComponent<IsometricMapTemplate>();
            isTutorial = FindFirstObjectByType<Escape4Now.GameModes.Tutorial_Functionality>() != null;
            showEventLocations = isTutorial;
            RefreshEventPrefabs();
        }

        //Existing partial lists receive their missing defaults once; later edits stay authoritative.
        private void InitializeEventCatalog()
        {
            if (eventCatalogInitialized) return;
            var templates = new Dictionary<MapEventType, EventTileMarker>();
            foreach (EventTileMarker prefab in eventPrefabs ?? System.Array.Empty<EventTileMarker>())
                if (prefab != null && System.Enum.IsDefined(typeof(MapEventType), prefab.EventType)) templates[prefab.EventType] = prefab;
            foreach (EventTileMarker prefab in Resources.LoadAll<EventTileMarker>("EventTiles"))
                if (prefab != null && System.Enum.IsDefined(typeof(MapEventType), prefab.EventType)
                    && !templates.ContainsKey(prefab.EventType)) templates.Add(prefab.EventType, prefab);
            eventPrefabs = new List<EventTileMarker>(templates.Values).ToArray();
            System.Array.Sort(eventPrefabs, (a, b) => a.EventType.CompareTo(b.EventType));
            eventCatalogInitialized = true;
            eventPrefabsDirty = true;
        }

        private void RefreshEventPrefabs()
        {
            InitializeEventCatalog();
            if (availablePrefabs != null && !eventPrefabsDirty) return;
            var templates = new Dictionary<MapEventType, EventTileMarker>();
            foreach (EventTileMarker prefab in eventPrefabs ?? System.Array.Empty<EventTileMarker>())
                if (prefab != null && System.Enum.IsDefined(typeof(MapEventType), prefab.EventType)) templates[prefab.EventType] = prefab;
            availablePrefabs = new List<EventTileMarker>(templates.Values).ToArray();
            System.Array.Sort(availablePrefabs, (a, b) => a.EventType.CompareTo(b.EventType));
            eventPrefabsDirty = false;
        }

        private void OnValidate()
        {
            eventPrefabsDirty = true;
            foreach (MapEventSpawnChance setting in spawnChances ?? System.Array.Empty<MapEventSpawnChance>())
                if (setting != null)
                    setting.Chance = float.IsNaN(setting.Chance) ? 1f : Mathf.Clamp01(setting.Chance);
#if UNITY_EDITOR
            if (!eventCatalogInitialized && !catalogInitializationQueued)
            {
                catalogInitializationQueued = true;
                UnityEditor.EditorApplication.delayCall += InitializeEventCatalogInEditor;
            }
#endif
        }

#if UNITY_EDITOR
        private bool catalogInitializationQueued;

        private void InitializeEventCatalogInEditor()
        {
            catalogInitializationQueued = false;
            if (this == null || eventCatalogInitialized) return;
            bool persistChanges = !Application.IsPlaying(gameObject);
            if (persistChanges) UnityEditor.Undo.RecordObject(this, "Initialize event prefabs");
            InitializeEventCatalog();
            if (!persistChanges) return;
            UnityEditor.EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        [ContextMenu("Populate Missing Event Prefabs")]
        private void PopulateMissingEventPrefabs()
        {
            UnityEditor.Undo.RecordObject(this, "Populate event prefabs");
            eventCatalogInitialized = false;
            InitializeEventCatalogInEditor();
        }
#endif

        //Starts receiving turn updates so Blackout can count finished player turns.
        private void OnEnable()
        {
            turnSystem = FindFirstObjectByType<TurnSystemController>();
            if (turnSystem != null) turnSystem.PlayerTurnEnded += CountBlackoutTurn;
        }

        //Stops receiving turn updates when this script is turned off.
        private void OnDisable()
        {
            if (turnSystem != null) turnSystem.PlayerTurnEnded -= CountBlackoutTurn;
        }

        //Counts full player turns after the turn that started Blackout.
        private void CountBlackoutTurn()
        {
            if (visibilityTurnsRemaining <= 0 || Time.timeScale <= 0f) return;
            if (blackoutStartedThisTurn)
            {
                blackoutStartedThisTurn = false;
                return;
            }
            visibilityTurnsRemaining--;
            if (visibilityTurnsRemaining == 0) message = "Blackout ended. Visibility restored.";
        }

        //Small events appear regularly. Stronger events may be absent from a match.
        private System.Collections.IEnumerator Start()
        {
            RefreshEventPrefabs();
            if (Escape4Now.Multiplayer.LanMultiplayerSession.IsClient) yield break;
            yield return null;
            players = FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);
            List<Vector2Int> emptyTiles = new List<Vector2Int>();
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (IsEmptyEventTile(cell)) emptyTiles.Add(cell);
                }
            }
            //Only use the reachability rule when the map starts out fully connected, otherwise no tile could pass it.
            checkMovingEventPaths = map.AllWalkableTilesConnected(HasMovingEvent);
            if (!checkMovingEventPaths) Debug.LogWarning("[Events] Some open tiles are already unreachable; moving events only avoid doorways and the exit.");
            var templates = new List<EventTileMarker>(availablePrefabs);
            HashSet<MapEventType> placedTypes = new HashSet<MapEventType>();
            foreach (EventTileMarker prefab in templates)
            {
                if (emptyTiles.Count == 0) break;
                //Skip templates that are missing, turned off, invalid, or already used.
                if (prefab == null || !prefab.gameObject.activeSelf || !prefab.enabled
                    || !System.Enum.IsDefined(typeof(MapEventType), prefab.EventType)
                    || (allowedTypes != null && !allowedTypes.Contains(prefab.EventType))
                    || !placedTypes.Add(prefab.EventType)
                    || (!forcedTiles.ContainsKey(prefab.EventType) && Random.value >= GetSpawnChance(prefab.EventType))) continue;
                int index = forcedTiles.TryGetValue(prefab.EventType, out Vector2Int forcedCell)
                    ? emptyTiles.IndexOf(forcedCell) : -1;
                if (index < 0) index = PickEventTile(emptyTiles, prefab.EventType);
                if (index < 0)
                {
                    Debug.Log($"[Events] No safe tile for {prefab.EventType}; it is skipped this match.");
                    continue;
                }
                Vector2Int cell = emptyTiles[index];
                EventTileMarker marker = Instantiate(prefab, map.GridToWorld(cell), Quaternion.identity, transform);
                marker.Configure(map.TileWidth, map.TileHeight);
                RegisterMarker(marker, cell);
                events.Add(cell, prefab.EventType);
                emptyTiles.RemoveAt(index);
            }
            EventsPlaced = true;
        }

        //Controls how often each event type appears in a match.
        public static float SpawnChance(MapEventType type)
        {
            if (type == MapEventType.Freeze || type == MapEventType.Blackout
                || type == MapEventType.LocationSwitch || type == MapEventType.FireDrill) return 0.35f;
            if (type == MapEventType.Warp || type == MapEventType.WrongRoom) return 0.6f;
            return 1f;
        }

        //Uses a designer-selected spawn chance and keeps the old defaults for unlisted types.
        private float GetSpawnChance(MapEventType type)
        {
            foreach (MapEventSpawnChance setting in spawnChances ?? System.Array.Empty<MapEventSpawnChance>())
                if (setting != null && setting.EventType == type) return Mathf.Clamp01(setting.Chance);
            return SpawnChance(type);
        }

        //Categories describe who the effect helps or affects.
        public static MapEventCategory Category(MapEventType type)
        {
            if (type == MapEventType.LuckyRoll || type == MapEventType.ExtraStep || type == MapEventType.SecondChance) return MapEventCategory.Positive;
            if (type == MapEventType.Freeze || type == MapEventType.BadRoll || type == MapEventType.Stumble) return MapEventCategory.Negative;
            if (type == MapEventType.Blackout || type == MapEventType.LocationSwitch || type == MapEventType.FireDrill
                || type == MapEventType.GoodLuckDay) return MapEventCategory.Global;
            return MapEventCategory.Neutral;
        }

        //Events that move players somewhere else. Crossing one always sends the player away,
        //so they must never sit where a player has to pass to reach a room or the exit.
        public static bool MovesPlayers(MapEventType type)
        {
            return type == MapEventType.Warp || type == MapEventType.RandomEvent || type == MapEventType.LocationSwitch
                || type == MapEventType.WrongRoom || type == MapEventType.FireDrill;
        }

        //False when the map was already split before events were placed, so path checks are skipped.
        public bool ProtectsPaths => checkMovingEventPaths;

        public bool HasMovingEvent(Vector2Int cell)
        {
            return events.TryGetValue(cell, out MapEventType type) && MovesPlayers(type);
        }

        //True when this tile or one beside it holds an event that moves players. Used to keep the exit's approach clear.
        public bool HasMovingEventNear(Vector2Int cell)
        {
            if (HasMovingEvent(cell)) return true;
            foreach (Vector2Int step in new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down })
                if (HasMovingEvent(cell + step)) return true;
            return false;
        }

        //Ordinary events can go on any empty tile. Moving events try tiles in random order until one is safe.
        private int PickEventTile(List<Vector2Int> emptyTiles, MapEventType type)
        {
            if (!MovesPlayers(type)) return Random.Range(0, emptyTiles.Count);
            var order = new List<int>(emptyTiles.Count);
            for (int i = 0; i < emptyTiles.Count; i++) order.Add(i);
            while (order.Count > 0)
            {
                int pick = Random.Range(0, order.Count);
                int index = order[pick];
                order.RemoveAt(pick);
                if (CanHoldMovingEvent(emptyTiles[index], type)) return index;
            }
            return -1;
        }

        //Keeps moving events off doorways, the tiles in front of them, and next to the exit,
        //and off any tile every route would have to cross.
        private bool CanHoldMovingEvent(Vector2Int cell, MapEventType type)
        {
            if (map.IsDoorway(cell) || map.IsExit(cell)) return false;
            foreach (Vector2Int step in new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down })
                if (map.IsDoorway(cell + step) || map.IsExit(cell + step)) return false;
            if (!checkMovingEventPaths) return true;
            events.Add(cell, type);
            bool connected = map.AllWalkableTilesConnected(HasMovingEvent);
            events.Remove(cell);
            return connected;
        }

        //Keeps events off players, obstacles, exits, and other events.
        private bool IsEmptyEventTile(Vector2Int cell)
        {
            return map.IsWalkable(cell) && !map.IsExit(cell) && !map.IsOccupied(cell)
                && !map.IsSpecialItemTile(cell)
                && !events.ContainsKey(cell);
        }

        //Keeps the latest result readable beside the turn panel, including during Blackout.
        private void OnGUI()
        {
            if (Escape4Now.Multiplayer.LocalMultiplayerSession.MatchHasEnded) return;
            if (visibilityTurnsRemaining > 0) DrawReducedVisibility();
            if (!Escape4Now.Multiplayer.LocalMultiplayerSession.MatchIsRunning && isTutorial)
            {
                eventToggleStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
                bool visible = GUI.Toggle(new Rect(Screen.width - 174f, 62f, 162f, 32f),
                    showEventLocations, showEventLocations ? "Hide Event Tiles" : "Show Event Tiles", eventToggleStyle);
                if (visible != showEventLocations) SetEventLocationsVisible(visible);
            }
            if (string.IsNullOrEmpty(message)) return;
            noticeText ??= new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, richText = false };
            noticeTitle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, richText = false };
            noticeText.normal.textColor = Color.white;
            noticeTitle.normal.textColor = new Color(1f, 0.87f, 0.45f);
            float width = Mathf.Min(360f, Mathf.Max(100f, Screen.width - 32f));
            string text = message;
            if (visibilityTurnsRemaining > 0) text += "\nLights return in " + visibilityTurnsRemaining + " player turns.";
            float height = noticeText.CalcHeight(new GUIContent(text), width - 24f) + 48f;
            float top = Screen.width >= 800 ? 110f : 330f;
            Rect panel = new Rect(Screen.width - width - 16f, top, width, height);
            int oldDepth = GUI.depth;
            Color oldColor = GUI.color;
            GUI.depth = -100;
            GUI.color = new Color(0.04f, 0.04f, 0.04f, 0.95f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, width - 24f, 22f), "LATEST RESULT", noticeTitle);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 34f, width - 24f, height - 40f), text, noticeText);
            GUI.color = oldColor;
            GUI.depth = oldDepth;
        }

        //Adds the player's name to the message explaining what happened.
        public void ShowMessage(PlayerCharacter player, string result)
        {
            if (player != null) message = player.name + ": " + result;
        }

        //Checks for an event after each completed step, not just at the end of a turn.
        public bool ResolveStep(PlayerCharacter player, Vector2Int direction)
        {
            if (Time.timeScale <= 0f || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused
                || player == null || !player.IsResolvingEventStep) return false;
            return ResolveEvent(player, direction, player.GridPosition);
        }

        //Only the tile crossed by normal movement activates an event.
        private bool ResolveEvent(PlayerCharacter player, Vector2Int direction, Vector2Int eventCell)
        {
            if (player == null || player.HasReachedExit || map == null
                || !map.IsWalkable(eventCell) || map.IsExit(eventCell)
                || !events.TryGetValue(eventCell, out MapEventType type)) return false;

            if (Category(type) == MapEventCategory.Negative && BlockNegative(player)) return false;

            EventTriggered?.Invoke(player, type);

            switch (type)
            {
                case MapEventType.LocationSwitch:
                    var session = FindFirstObjectByType<Escape4Now.Multiplayer.LocalMultiplayerSession>();
                    bool switched = session != null && SwitchLocations(session.Participants);
                    ShowMessage(player, switched ? "Location Switch: everyone swapped places. Unused moves are kept."
                        : "Location Switch: players cannot switch right now.");
                    return false;
                case MapEventType.Warp:
                    Warp(player);
                    //Keep unused moves after teleporting.
                    return false;
                case MapEventType.RandomEvent:
                    //Four small effects have 20% each. Freeze and Warp have 10% each.
                    int result = Random.Range(0, 10);
                    if (result < 4)
                    {
                        bool forward = result < 2;
                        if (!forward && BlockNegative(player)) return false;
                        return MoveExtra(player, forward ? direction : -direction, forward, eventCell);
                    }
                    else if (result < 6)
                    {
                        player.EventState.GiveLuckyRoll();
                        ShowMessage(player, "Random Event: next turn, roll twice and keep the higher number.");
                    }
                    else if (result < 9)
                    {
                        if (BlockNegative(player)) return false;
                        if (result < 8) player.EventState.GiveBadRoll();
                        else player.EventState.GiveFreeze();
                        ShowMessage(player, result < 8 ? "Random Event: next turn, roll twice and keep the lower number." : "Random Event: Freeze. You will miss your next turn.");
                    }
                    else Warp(player);
                    break;
                case MapEventType.ExtraStep:
                    int beforeExtra = player.MovesRemaining;
                    player.ChangeEventMovement(2);
                    ShowMessage(player, "Extra Step: you gained " + (player.MovesRemaining - beforeExtra) + " moves. You have " + player.MovesRemaining + " left.");
                    break;
                case MapEventType.Stumble:
                    int beforeStumble = player.MovesRemaining;
                    player.ChangeEventMovement(-2);
                    ShowMessage(player, "Stumble: you lost " + (beforeStumble - player.MovesRemaining) + " moves. You have " + player.MovesRemaining + " left.");
                    break;
                case MapEventType.SecondChance:
                    player.EventState.GiveSecondChance();
                    ShowMessage(player, "Second Chance: your next harmful event will be blocked. Events that affect everyone still work.");
                    break;
                case MapEventType.BadRoll:
                    player.EventState.GiveBadRoll();
                    ShowMessage(player, "Bad Roll: use the lower of two dice next turn.");
                    break;
                case MapEventType.WrongRoom:
                    WrongRoom(player, eventCell);
                    break;
                case MapEventType.FireDrill:
                    FireDrill(player);
                    break;
                case MapEventType.GoodLuckDay:
                    foreach (PlayerCharacter participant in FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None))
                        participant.EventState.GiveRollBonus();
                    ShowMessage(player, "Good Luck Day: everyone gets +1 on their next normal roll.");
                    break;
                case MapEventType.Freeze:
                    player.EventState.GiveFreeze();
                    ShowMessage(player, "Freeze: you will miss your next turn, then play normally.");
                    break;
                case MapEventType.Blackout:
                    if (visibilityTurnsRemaining > 0)
                    {
                        ShowMessage(player, "Blackout is already active; its duration is unchanged.");
                        break;
                    }
                    visibilityTurnsRemaining = Mathf.Clamp(6 - FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None).Length, 2, 4);
                    blackoutStartedThisTurn = true;
                    ShowMessage(player, "Blackout: everyone's visibility is reduced for the next " + visibilityTurnsRemaining + " player turns.");
                    break;
                case MapEventType.LuckyRoll:
                    player.EventState.GiveLuckyRoll();
                    ShowMessage(player, "Lucky Roll: roll twice next turn and keep the higher roll.");
                    break;
            }
            return false;
        }

        //Second Chance is separate from protection against other players.
        private bool BlockNegative(PlayerCharacter player)
        {
            if (!player.EventState.BlockNegativeEvent()) return false;
            ShowMessage(player, "Second Chance saved you from a harmful event. That protection is now used up.");
            return true;
        }

        //Chooses another room first, then a safe tile within that room.
        private void WrongRoom(PlayerCharacter player, Vector2Int from)
        {
            var rooms = new List<List<Vector2Int>>();
            foreach (SchoolRoomType room in System.Enum.GetValues(typeof(SchoolRoomType)))
            {
                if (room == SchoolRoomType.None || room == map.GetRoomType(from)) continue;
                var cells = new List<Vector2Int>();
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (map.GetRoomType(cell) == room && IsWarpDestination(cell)) cells.Add(cell);
                    }
                if (cells.Count > 0) rooms.Add(cells);
            }
            if (rooms.Count == 0) { ShowMessage(player, "Wrong Room: no safe destination."); return; }
            List<Vector2Int> chosen = rooms[Random.Range(0, rooms.Count)];
            player.SetGridPosition(chosen[Random.Range(0, chosen.Count)]);
            ShowMessage(player, "Wrong Room: moved to another room. Unused moves are kept.");
        }

        //Reserves different hallway tiles before moving anyone. No events or items are activated.
        private void FireDrill(PlayerCharacter player)
        {
            var session = FindFirstObjectByType<Escape4Now.Multiplayer.LocalMultiplayerSession>();
            if (session == null) return;
            var cells = new List<Vector2Int>();
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (map.IsHallway(cell) && IsWarpDestination(cell)) cells.Add(cell);
                }
            if (cells.Count < session.Participants.Count)
            { ShowMessage(player, "Fire Drill: not enough safe hallway tiles."); return; }
            var targets = new Vector2Int[session.Participants.Count];
            for (int i = 0; i < targets.Length; i++)
            {
                int index = Random.Range(0, cells.Count);
                targets[i] = cells[index];
                cells.RemoveAt(index);
            }
            bool moved = PlayerCharacter.RelocatePlayers(session.Participants, targets);
            ShowMessage(player, moved ? "Fire Drill: everyone moved to the hallway. Unused moves are kept." : "Fire Drill: players cannot move right now.");
        }

        //These are all possible swaps where nobody stays put, including two separate pairs.
        private static bool SwitchLocations(IReadOnlyList<PlayerCharacter> participants)
        {
            if (participants == null || participants.Count < 2 || participants.Count > 4) return false;
            int[][] choices = participants.Count == 2 ? new[] { new[] { 1, 0 } }
                : participants.Count == 3 ? new[] { new[] { 1, 2, 0 }, new[] { 2, 0, 1 } }
                : new[] { new[] { 1, 0, 3, 2 }, new[] { 1, 2, 3, 0 }, new[] { 1, 3, 0, 2 },
                    new[] { 2, 0, 3, 1 }, new[] { 2, 3, 0, 1 }, new[] { 2, 3, 1, 0 },
                    new[] { 3, 0, 1, 2 }, new[] { 3, 2, 0, 1 }, new[] { 3, 2, 1, 0 } };
            //Sharing a tile is allowed, but a switch must still move every player somewhere else.
            var valid = new List<int[]>();
            foreach (int[] choice in choices)
            {
                bool movesEveryone = true;
                for (int i = 0; i < participants.Count; i++)
                    if (participants[i] == null || participants[choice[i]] == null
                        || participants[i].GridPosition == participants[choice[i]].GridPosition) movesEveryone = false;
                if (movesEveryone) valid.Add(choice);
            }
            return valid.Count > 0 && PlayerCharacter.SwitchLocations(participants, valid[Random.Range(0, valid.Count)]);
        }

        //Lists safe destinations first, then picks one randomly for Warp.
        private void Warp(PlayerCharacter player)
        {
            List<Vector2Int> choices = new List<Vector2Int>();
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (IsWarpDestination(cell)
                        && !player.IsOccupiedByOtherPlayer(cell)) choices.Add(cell);
                }
            }

            if (choices.Count == 0)
            {
                ShowMessage(player, "Warp: no safe destination.");
                return;
            }

            bool moved = player.SetGridPosition(choices[Random.Range(0, choices.Count)]);
            ShowMessage(player, moved ? "Warp: teleported. Unused moves are kept." : "Warp: destination unavailable.");
        }

        //Keeps Warp away from blocked tiles, players, the exit, and other events.
        private bool IsWarpDestination(Vector2Int cell)
        {
            return IsEmptyEventTile(cell);
        }

        //Moves up to two extra spaces without activating any crossed or destination events.
        private bool MoveExtra(PlayerCharacter player, Vector2Int direction, bool forward, Vector2Int cursor)
        {
            if (direction != Vector2Int.up && direction != Vector2Int.down
                && direction != Vector2Int.left && direction != Vector2Int.right) return false;

            string effect = forward ? "Move Forward" : "Move Backward";
            ShowMessage(player, "Random Event: " + effect + " 2 spaces.");
            int moved = 0;
            for (int i = 0; i < 2; i++)
            {
                Vector2Int next = cursor + direction;
                if (!map.IsWalkable(next) || player.IsOccupiedByOtherPlayer(next)) break;
                cursor = next;
                if (!player.SetGridPosition(next)) break;
                moved = i + 1;
                if (map.IsExit(next)) break;
            }
            ShowMessage(player, "Random Event: " + effect + ". Moved " + moved + " spaces.");
            return false;
        }


        //Darkens the shared screen except for a small area around the current player.
        private void DrawReducedVisibility()
        {
            Camera camera = Camera.main;
            if (camera == null || players == null) return;
            PlayerCharacter active = null;
            foreach (PlayerCharacter player in players)
            {
                if (player != null && player.isActiveAndEnabled && player.IsCurrentTurn)
                {
                    active = player;
                    break;
                }
            }

            int oldDepth = GUI.depth;
            Color oldColor = GUI.color;
            GUI.depth = 100;
            GUI.color = Color.black;
            if (active == null)
            {
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            else
            {
                if (visibilityMask == null) CreateVisibilityMask();
                Vector3 center = camera.WorldToScreenPoint(active.transform.position);
                Vector3 edge = camera.WorldToScreenPoint(active.transform.position + Vector3.right * map.TileWidth * 2f);
                float radius = Mathf.Max(1f, Mathf.Abs(edge.x - center.x));
                float left = center.x - radius;
                float top = Screen.height - center.y - radius;
                float size = radius * 2f;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Mathf.Max(0, top)), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, top + size, Screen.width, Mathf.Max(0, Screen.height - top - size)), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, top, Mathf.Max(0, left), size), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(left + size, top, Mathf.Max(0, Screen.width - left - size), size), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(left, top, size, size), visibilityMask);
            }
            GUI.color = oldColor;
            GUI.depth = oldDepth;
        }

        //Makes an image with a clear center and dark edges for the Blackout effect.
        private void CreateVisibilityMask()
        {
            const int size = 128;
            visibilityMask = new Texture2D(size, size, TextureFormat.RGBA32, false);
            visibilityMask.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f).magnitude;
                    float alpha = Mathf.InverseLerp(0.7f, 1f, distance);
                    visibilityMask.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }
            visibilityMask.Apply();
        }

        //Releases the screen mask created by this component.
        private void OnDestroy()
        {
            if (visibilityMask != null) Destroy(visibilityMask);
        }
    }
}
