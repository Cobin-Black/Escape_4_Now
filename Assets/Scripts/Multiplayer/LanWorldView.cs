using System;
using System.Collections.Generic;
using Escape4Now.Items;
using Escape4Now.Map;
using Escape4Now.Obstacles;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using UnityEngine;

namespace Escape4Now.Multiplayer
{
    //Copies the host's board and player state using the project's existing prefabs.
    public sealed class LanWorldView
    {
        private readonly LocalMultiplayerSession session;
        private readonly IsometricMapTemplate map;
        private readonly MapEventController events;
        private readonly TurnSystemController turns;
        private readonly Dictionary<int, GameObject> copies = new Dictionary<int, GameObject>();
        private bool hasLayout;

        public LanWorldView(LocalMultiplayerSession owner)
        {
            session = owner;
            map = UnityEngine.Object.FindFirstObjectByType<IsometricMapTemplate>();
            events = map.GetComponent<MapEventController>();
            turns = UnityEngine.Object.FindFirstObjectByType<TurnSystemController>();
        }

        //Only known item types are sent over the network.
        public static int ItemType(Item item) => item is DoubleDiceItem ? 1 : item is CustomDiceItem ? 2
            : item is KeyStealerItem ? 3 : item is KeyItem ? 4 : 0;

        //The host sends results, never another player's requested dice value or position.
        public LanSnapshot Capture()
        {
            var state = new LanSnapshot { seed = map.LayoutSeed, exitX = map.ExitPosition.x,
                exitY = map.ExitPosition.y, turn = turns.CurrentPlayerIndex, round = turns.TurnNumber,
                turnOrder = turns.CopyTurnOrder(),
                blackout = events.BlackoutTurns, message = events.CurrentMessage,
                paused = LanMultiplayerSession.Instance != null && LanMultiplayerSession.Instance.MatchPaused,
                mapSettings = map.GenerationSettings,
                furnitureSettings = map.FurnitureSettings,
                players = new LanPlayerState[session.Participants.Count] };
            state.secondsRemaining = UnityEngine.Object.FindFirstObjectByType<GameTimer>()?.TimeRemaining ?? 0f;
            state.battleMessage = UnityEngine.Object.FindFirstObjectByType<Escape4Now.GameModes.Race_Gamemode>()?.NetworkMessage ?? "";
            for (int i = 0; i < state.players.Length; i++)
            {
                PlayerCharacter player = session.Participants[i];
                state.players[i] = new LanPlayerState { x = player.GridPosition.x, y = player.GridPosition.y,
                    worldX = player.transform.position.x, worldY = player.transform.position.y,
                    roll = player.LastRoll, moves = player.MovesRemaining, rolled = player.HasRolled,
                    moving = player.IsMoving, won = player.HasReachedExit,
                    protectedFromPlayers = player.EventState.IsProtectedFromPlayers,
                    hasKey = player.Inventory.HasKey(),
                    item = player.Inventory.Items.Count == 0 ? 0 : ItemType(player.Inventory.Items[0]) };
            }
            var objects = new List<LanObjectState>();
            foreach (EventTileMarker marker in UnityEngine.Object.FindObjectsByType<EventTileMarker>(FindObjectsSortMode.None))
            {
                Vector2Int cell = events.GetEventCell(marker);
                LanObjectState data = ObjectState(marker, 0, (int)marker.EventType, cell);
                objects.Add(data);
            }
            foreach (ObstacleGridControl obstacle in UnityEngine.Object.FindObjectsByType<ObstacleGridControl>(FindObjectsSortMode.None))
            {
                LanObjectState data = ObjectState(obstacle, 1, 0, obstacle.GridPosition);
                data.facing = (int)obstacle.Facing;
                ObstacleStorage storage = obstacle.GetComponent<ObstacleStorage>();
                data.interactable = obstacle.IsInteractable && storage?.HasItem == true;
                data.containsKey = storage != null && storage.ContainsKey;
                objects.Add(data);
            }
            foreach (Item item in UnityEngine.Object.FindObjectsByType<Item>(FindObjectsSortMode.None))
            {
                int type = ItemType(item);
                //Some item prefabs keep their sprite on a child object.
                Renderer renderer = item.GetComponentInChildren<Renderer>();
                if (type != 0 && renderer != null && renderer.enabled)
                    objects.Add(ObjectState(item, 2, type, map.WorldToGrid(item.transform.position)));
            }
            foreach (SchoolFurniture furniture in UnityEngine.Object.FindObjectsByType<SchoolFurniture>(FindObjectsSortMode.None))
            {
                var data = ObjectState(furniture, 3, (int)furniture.Kind, furniture.Cell);
                ObstacleStorage storage = furniture.GetComponent<ObstacleStorage>();
                data.interactable = storage?.HasItem == true;
                data.containsKey = storage != null && storage.ContainsKey;
                objects.Add(data);
            }
            state.objects = objects.ToArray();
            return state;
        }

        //Stores the stable host ID so a collected item disappears on all clients.
        private static LanObjectState ObjectState(Component source, int kind, int type, Vector2Int cell)
        {
            return new LanObjectState { id = source.GetInstanceID(), kind = kind, type = type, x = cell.x, y = cell.y };
        }

        //Rejects malformed state before creating any objects.
        public static bool IsValid(LanSnapshot state)
        {
            if (state == null || state.mapSettings == null || !state.mapSettings.IsValid
                || state.furnitureSettings == null || !state.furnitureSettings.IsValid
                || state.players == null || state.players.Length < 2 || state.players.Length > 4
                || !PlayerTurnOrder.IsValid(state.turnOrder, state.players.Length)
                || state.objects == null || state.objects.Length > 128 || state.turn < 0
                || state.turn >= state.players.Length || state.round < 1 || state.blackout < 0 || state.blackout > 4
                || state.message == null || state.message.Length > 1024 || !Cell(state.exitX, state.exitY)
                || state.battleMessage == null || state.battleMessage.Length > 1024
                || float.IsNaN(state.secondsRemaining) || float.IsInfinity(state.secondsRemaining)
                || state.secondsRemaining < 0 || state.secondsRemaining > 86400f) return false;
            bool edgeX = state.exitX == 0 || state.exitX == SchoolMapLayout.Size - 1;
            bool edgeY = state.exitY == 0 || state.exitY == SchoolMapLayout.Size - 1;
            if (edgeX == edgeY) return false;
            foreach (LanPlayerState player in state.players)
                if (player == null || !Cell(player.x, player.y) || player.roll < 0 || player.roll > 12
                    || player.moves < 0 || player.moves > SchoolMapLayout.Size * SchoolMapLayout.Size || player.item < 0 || player.item > 3
                    || float.IsNaN(player.worldX) || float.IsNaN(player.worldY)
                    || Math.Abs(player.worldX) > 1000 || Math.Abs(player.worldY) > 1000) return false;
            var ids = new HashSet<int>();
            //Each event type has one tile, and event tiles cannot overlap.
            var eventTypes = new HashSet<int>();
            var eventCells = new HashSet<Vector2Int>();
            foreach (LanObjectState item in state.objects)
                if (item == null || !ids.Add(item.id) || !Cell(item.x, item.y) || item.kind < 0 || item.kind > 3
                    || item.type < 0 || item.type > (item.kind == 3 ? (int)SchoolFurnitureKind.CleaningCabinet : (int)MapEventType.GoodLuckDay) || item.facing < 0 || item.facing > 3
                    || (item.kind == 2 && (item.type < 1 || item.type > 3))
                    || (item.kind == 0 && (!eventTypes.Add(item.type) || !eventCells.Add(new Vector2Int(item.x, item.y))))) return false;
            return true;
        }

        private static bool Cell(int x, int y) => x >= 0 && y >= 0 && x < SchoolMapLayout.Size && y < SchoolMapLayout.Size;

        //Only the first snapshot creates the map. Later snapshots update confirmed results.
        public bool Apply(LanSnapshot state)
        {
            if (!IsValid(state)) return false;
            if (!hasLayout)
            {
                map.UseNetworkLayout(state.seed, state.mapSettings, state.furnitureSettings);
                if (!session.StartNetworkMatch(state.players.Length)) return false;
                map.PlaceNetworkExit(new Vector2Int(state.exitX, state.exitY));
                hasLayout = true;
            }
            if (state.seed != map.LayoutSeed || state.players.Length != session.Participants.Count) return false;
            if (!turns.ApplyNetworkTurn(state.turn, state.round, state.turnOrder)) return false;
            var occupied = new List<Vector2Int>();
            for (int i = 0; i < state.players.Length; i++)
            {
                session.Participants[i].ApplyNetworkState(state.players[i], false);
                occupied.Add(session.Participants[i].GridPosition);
            }
            map.ReplacePlayerOccupants(occupied);
            events.ApplyNetworkDisplay(state.message, state.blackout);
            UnityEngine.Object.FindFirstObjectByType<GameTimer>()?.ApplyNetworkTime(state.secondsRemaining);
            bool matchFinished = state.secondsRemaining <= 0f;
            foreach (LanPlayerState player in state.players) matchFinished |= player.won;
            session.ApplyNetworkPause(state.paused, matchFinished);
            var present = new HashSet<int>();
            foreach (LanObjectState data in state.objects)
            {
                present.Add(data.id);
                if (!copies.ContainsKey(data.id)) copies.Add(data.id, CreateCopy(data));
                if (data.kind == 1 && copies[data.id] != null)
                    copies[data.id].GetComponent<ObstacleGridControl>().IsInteractable = data.interactable;
                if ((data.kind == 1 || data.kind == 3) && copies[data.id] != null)
                    copies[data.id].GetComponent<ObstacleStorage>()?.ApplyNetworkContents(data.interactable, data.containsKey);
            }
            var removed = new List<int>();
            foreach (var entry in copies)
                if (!present.Contains(entry.Key)) { UnityEngine.Object.Destroy(entry.Value); removed.Add(entry.Key); }
            foreach (int id in removed) copies.Remove(id);
            return true;
        }

        //The prefab list comes from this build, never a network file path or type name.
        private GameObject CreateCopy(LanObjectState data)
        {
            var cell = new Vector2Int(data.x, data.y);
            //Furniture was already built from the map seed; never create a second copy.
            if (data.kind == 3)
            {
                foreach (SchoolFurniture furniture in UnityEngine.Object.FindObjectsByType<SchoolFurniture>(FindObjectsSortMode.None))
                    if (furniture.Cell == cell && (int)furniture.Kind == data.type) return furniture.gameObject;
                throw new InvalidOperationException("Furniture does not match the map layout.");
            }
            if (data.kind == 0)
            {
                EventTileMarker prefab = events.GetNetworkPrefab(data.type);
                if (prefab == null) throw new InvalidOperationException("Event template is missing.");
                EventTileMarker copy = UnityEngine.Object.Instantiate(prefab, map.GridToWorld(cell), Quaternion.identity, map.transform);
                copy.Configure(map.TileWidth, map.TileHeight);
                events.RegisterMarker(copy, cell);
                return copy.gameObject;
            }
            if (data.kind == 1)
            {
                ObstacleGridControl prefab = map.GetComponent<ObstacleSpawner>().GetNetworkPrefab();
                if (prefab == null) throw new InvalidOperationException("Obstacle template is missing.");
                ObstacleGridControl copy = UnityEngine.Object.Instantiate(prefab, map.GridToWorld(cell), Quaternion.identity, map.transform);
                if (!copy.Place(map, cell)) throw new InvalidOperationException("Invalid obstacle position.");
                copy.SetFacing((ObstacleFacing)data.facing);
                copy.IsInteractable = data.interactable;
                if (copy.GetComponent<ObstacleStorage>() == null) copy.gameObject.AddComponent<ObstacleStorage>();
                return copy.gameObject;
            }
            Item itemPrefab = map.GetComponent<ItemSpawner>().GetNetworkPrefab(data.type);
            if (itemPrefab == null) throw new InvalidOperationException("Item template is missing.");
            Item item = UnityEngine.Object.Instantiate(itemPrefab, map.GridToWorld(cell), Quaternion.identity, map.transform);
            if (item is DoubleDiceItem doubleDice) doubleDice.SetSpawnPosition(map, cell);
            if (item is CustomDiceItem customDice) customDice.SetSpawnPosition(map, cell);
            if (item is KeyStealerItem keyStealer) keyStealer.SetSpawnPosition(map, cell);
            item.enabled = false;
            return item.gameObject;
        }
    }
}
