using System.Collections.Generic;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using UnityEngine;

namespace Escape4Now.Map
{
    //Lists the five kinds of event tiles.
    public enum MapEventType { Warp, RandomEvent, Freeze, Blackout, LuckyRoll }

    //Places special tiles around the map and applies their effects when crossed.
    [RequireComponent(typeof(IsometricMapTemplate))]
    public sealed class MapEventController : MonoBehaviour
    {
        private IsometricMapTemplate map;
        private readonly Dictionary<Vector2Int, MapEventType> events = new Dictionary<Vector2Int, MapEventType>();
        //Reusable templates for the five colored event tiles.
        [SerializeField] private EventTileMarker[] eventPrefabs;
        private string message = "";
        private int visibilityTurnsRemaining;
        private bool blackoutStartedThisTurn;
        private TurnSystemController turnSystem;
        private Texture2D visibilityMask;
        private PlayerCharacter[] players;

        //Optional limits set before the events are placed, such as by the tutorial. Null places every event type.
        private HashSet<MapEventType> allowedTypes;
        private readonly Dictionary<MapEventType, Vector2Int> forcedTiles = new Dictionary<MapEventType, Vector2Int>();

        //Lets other spawners wait until every event has its tile.
        public bool EventsPlaced { get; private set; }

        //Tells listeners, such as the tutorial, when a player sets off an event tile.
        public event System.Action<PlayerCharacter, MapEventType> EventTriggered;

        //Places only the listed event types. Call before the events are placed.
        public void OnlyPlaceEvents(params MapEventType[] types)
        {
            allowedTypes = new HashSet<MapEventType>(types);
        }

        //Puts an event type on a set tile instead of a random one, if that tile is free. Call before the events are placed.
        public void ForceEventTile(MapEventType type, Vector2Int cell)
        {
            forcedTiles[type] = cell;
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
        }

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

        //Blackout lasts four player turns after the turn that triggered it.
        private void CountBlackoutTurn()
        {
            if (visibilityTurnsRemaining <= 0) return;
            if (blackoutStartedThisTurn)
            {
                blackoutStartedThisTurn = false;
                return;
            }
            visibilityTurnsRemaining--;
            if (visibilityTurnsRemaining == 0) message = "Blackout ended. Visibility restored.";
        }

        //Waits one frame for setup, then randomly places one of each event where space allows.
        private System.Collections.IEnumerator Start()
        {
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
            if (eventPrefabs == null)
            {
                EventsPlaced = true;
                yield break;
            }
            HashSet<MapEventType> placedTypes = new HashSet<MapEventType>();
            foreach (EventTileMarker prefab in eventPrefabs)
            {
                if (emptyTiles.Count == 0) break;
                //Skip templates that are missing, turned off, invalid, or already used.
                if (prefab == null || !prefab.gameObject.activeSelf || !prefab.enabled
                    || !System.Enum.IsDefined(typeof(MapEventType), prefab.EventType)
                    || (allowedTypes != null && !allowedTypes.Contains(prefab.EventType))
                    || !placedTypes.Add(prefab.EventType)) continue;
                int index = PickEventTileIndex(prefab.EventType, emptyTiles);
                Vector2Int cell = emptyTiles[index];
                EventTileMarker marker = Instantiate(prefab, map.GridToWorld(cell), Quaternion.identity, transform);
                marker.Configure(map.TileWidth, map.TileHeight);
                events.Add(cell, prefab.EventType);
                emptyTiles.RemoveAt(index);
            }
            EventsPlaced = true;
        }

        //Uses the forced tile for this event type when it is free, otherwise a random empty tile.
        private int PickEventTileIndex(MapEventType type, List<Vector2Int> emptyTiles)
        {
            if (forcedTiles.TryGetValue(type, out Vector2Int cell))
            {
                int forced = emptyTiles.IndexOf(cell);
                if (forced >= 0) return forced;
                Debug.LogWarning($"{type} could not use tile {cell}, so it was placed randomly.");
            }
            return Random.Range(0, emptyTiles.Count);
        }

        //Keeps events off players, obstacles, exits, and other events.
        private bool IsEmptyEventTile(Vector2Int cell)
        {
            return map.IsWalkable(cell) && !map.IsExit(cell) && !map.IsOccupied(cell)
                && !events.ContainsKey(cell);
        }

        //Shows the last event message and the number of Blackout turns left.
        private void OnGUI()
        {
            if (visibilityTurnsRemaining > 0)
            {
                DrawReducedVisibility();
                GUI.Label(new Rect(16f, 210f, 350f, 30f), "Blackout: " + visibilityTurnsRemaining + " player turns remaining");
            }
            GUI.Label(new Rect(16f, 140f, Mathf.Max(0f, Screen.width - 32f), 65f), message);
        }

        //Adds the player's name to the message explaining what happened.
        public void ShowMessage(PlayerCharacter player, string result)
        {
            if (player != null) message = player.name + ": " + result;
        }

        //Checks for an event after each completed step, not just at the end of a turn.
        public bool ResolveStep(PlayerCharacter player, Vector2Int direction)
        {
            if (player == null || !player.IsResolvingEventStep) return false;
            return ResolveEvent(player, direction, new HashSet<Vector2Int>());
        }

        //Applies the effect. Each event tile can activate only once during this step's effects.
        private bool ResolveEvent(PlayerCharacter player, Vector2Int direction, HashSet<Vector2Int> visited)
        {
            if (player == null || player.HasReachedExit || map == null
                || !map.IsWalkable(player.GridPosition) || map.IsExit(player.GridPosition)
                || !events.TryGetValue(player.GridPosition, out MapEventType type)
                || !visited.Add(player.GridPosition)) return false;

            EventTriggered?.Invoke(player, type);
            switch (type)
            {
                case MapEventType.Warp:
                    Warp(player);
                    //Keep unused moves after teleporting.
                    return false;
                case MapEventType.RandomEvent:
                    int result = Random.Range(0, 4);
                    if (result == 0 || result == 1)
                    {
                        return MoveExtra(player, result == 0 ? direction : -direction, result == 0, visited);
                    }
                    else if (result == 2)
                    {
                        player.EventState.GiveLuckyRoll();
                        ShowMessage(player, "Random Event: Lucky Roll next turn.");
                    }
                    else
                    {
                        player.EventState.GiveFreeze();
                        ShowMessage(player, "Random Event: Freeze. The next turn will be skipped.");
                    }
                    break;
                case MapEventType.Freeze:
                    player.EventState.GiveFreeze();
                    ShowMessage(player, "Freeze. The next turn will be skipped.");
                    break;
                case MapEventType.Blackout:
                    visibilityTurnsRemaining = 4;
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

        //Moves up to two extra spaces, checking each tile and any event along the way.
        private bool MoveExtra(PlayerCharacter player, Vector2Int direction, bool forward, HashSet<Vector2Int> visited)
        {
            if (direction != Vector2Int.up && direction != Vector2Int.down
                && direction != Vector2Int.left && direction != Vector2Int.right) return false;

            string effect = forward ? "Move Forward" : "Move Backward";
            ShowMessage(player, "Random Event: " + effect + " 2 spaces.");
            int previousEvents = visited.Count;
            int moved = 0;
            for (int i = 0; i < 2; i++)
            {
                Vector2Int next = player.GridPosition + direction;
                if (!map.IsWalkable(next) || player.IsOccupiedByOtherPlayer(next)
                    || !player.SetGridPosition(next)) break;
                moved++;
                if (map.IsExit(next)) break;
                if (ResolveEvent(player, direction, visited)) return true;
                if (player.HasReachedExit || player.GridPosition != next) break;
            }
            if (visited.Count == previousEvents)
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
