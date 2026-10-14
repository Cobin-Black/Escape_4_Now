using System.Collections;
using System.Collections.Generic;
using Escape4Now.Items;
using Escape4Now.Map;
using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.Obstacles
{
    //Places obstacles on random empty tiles once the events and items have their spots, and fills interactable ones with an item.
    public sealed class ObstacleSpawner : MonoBehaviour
    {
        [SerializeField] private IsometricMapTemplate map;

        //Obstacle prefabs to pick from, and how many to place.
        [SerializeField] private ObstacleGridControl[] obstaclePrefabs;
        [SerializeField, Min(1)] private int obstacleCount = 6;

        //Chance that a searchable object holds an item, and the allowed item prefabs.
        [SerializeField, Range(0f, 1f)] private float interactableChance = 0.5f;
        [SerializeField] private Item[] possibleItems;

        //Spawners that must finish first so obstacles never cover an event or item. Found on the map when left empty.
        [SerializeField] private MapEventController mapEvents;
        [SerializeField] private ItemSpawner itemSpawner;
        private int? forcedObstacleCount;
        private float? forcedInteractableChance;
        private Vector2Int? forcedObstacleCell;
        //Item placement waits until all searchable obstacles have been created.
        public bool HasSpawnedObstacles { get; private set; }

        public void SetObstacleCount(int count) { forcedObstacleCount = Mathf.Max(0, count); }
        public void SetInteractableChance(float chance) { forcedInteractableChance = Mathf.Clamp01(chance); }
        public void ForceObstacleTile(Vector2Int cell) { forcedObstacleCell = cell; }

        private void Start()
        {
            StartCoroutine(SpawnObstacles());
        }

        //The current scene uses one obstacle template on every computer.
        public ObstacleGridControl GetNetworkPrefab()
        {
            if (obstaclePrefabs == null) return null;
            foreach (ObstacleGridControl prefab in obstaclePrefabs) if (prefab != null) return prefab;
            return null;
        }

        private IEnumerator SpawnObstacles()
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.IsClient) yield break;
            // Wait for the runtime map to finish generating.
            yield return null;

            if (map == null)
            {
                Debug.LogWarning("[Obstacle System] ObstacleSpawner could not find the map.");
                HasSpawnedObstacles = true;
                yield break;
            }

            if (mapEvents == null) mapEvents = map.GetComponent<MapEventController>();
            if (itemSpawner == null) itemSpawner = map.GetComponent<ItemSpawner>();

            //Wait until events and special-room item spots are ready before placing obstacles.
            yield return new WaitUntil(() =>
                (mapEvents == null || !mapEvents.isActiveAndEnabled || mapEvents.EventsPlaced)
                && (itemSpawner == null || !itemSpawner.isActiveAndEnabled || itemSpawner.HasReservedSpecialRoomItems));

            List<Vector2Int> emptyTiles = new List<Vector2Int>();
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    if (IsValidObstacleTile(cell))
                    {
                        emptyTiles.Add(cell);
                    }
                }
            }

            int placed = 0;
            var decorations = new List<Vector2Int>();
            foreach (SchoolFurniture furniture in FindObjectsByType<SchoolFurniture>(FindObjectsSortMode.None))
                if (map.IsHallway(furniture.Cell)) decorations.Add(furniture.Cell);
            if (forcedObstacleCell.HasValue)
            {
                Vector2Int cell = forcedObstacleCell.Value;
                bool canForce = emptyTiles.Remove(cell) || (map.IsWalkable(cell) && !map.IsOccupied(cell)
                    && !map.IsExit(cell) && (mapEvents == null || !mapEvents.HasEvent(cell)) && !HasItemAt(cell));
                if (canForce && SpawnObstacle(cell))
                {
                    placed++;
                    decorations.Add(cell);
                }
            }

            int target = forcedObstacleCount.HasValue
                ? forcedObstacleCount.Value : Mathf.Min(obstacleCount, Random.Range(2, 4));
            while (placed < target && emptyTiles.Count > 0)
            {
                int index = Random.Range(0, emptyTiles.Count);
                Vector2Int cell = emptyTiles[index];
                emptyTiles.RemoveAt(index);

                //Leave at least two tile steps between hallway objects to avoid clusters.
                if (decorations.Exists(other => Mathf.Abs(other.x - cell.x) + Mathf.Abs(other.y - cell.y) <= 2)) continue;
                if (KeepsMapConnected(cell) && SpawnObstacle(cell))
                {
                    placed++;
                    decorations.Add(cell);
                }
            }
            HasSpawnedObstacles = true;
        }

        private bool IsNearDoorway(Vector2Int cell)
        {
            Vector2Int[] directions =
            {
        Vector2Int.right,
        Vector2Int.up,
        Vector2Int.left,
        Vector2Int.down
    };

            foreach (Vector2Int direction in directions)
            {
                if (map.IsDoorway(cell + direction))
                {
                    return true;
                }
            }

            return false;
        }

        //Keeps obstacles on open floor and off players, the exit, events, and items.
        private bool IsValidObstacleTile(Vector2Int cell)
        {
            return map.IsInteriorFloor(cell)
                && map.IsLockerPosition(cell)
                && map.IsWalkable(cell)
                && !IsNearDoorway(cell)
                && !map.IsDoorway(cell)
                && !map.IsOccupied(cell)
                && (mapEvents == null || !mapEvents.HasEvent(cell))
                && !HasItemAt(cell);
        }

        //Checks every item lying on the map.
        private static bool HasItemAt(Vector2Int cell)
        {
            foreach (Item item in FindObjectsByType<Item>(FindObjectsSortMode.None))
            {
                if (item != null && item.IsAtPosition(cell))
                {
                    return true;
                }
            }

            return false;
        }

        //Blocks the tile for a moment and makes sure every open tile, including the exit, can still be reached.
        private bool KeepsMapConnected(Vector2Int cell)
        {
            if (!map.RegisterBlockedPosition(cell))
            {
                return false;
            }

            bool connected = AllWalkableTilesReachable();
            map.UnregisterBlockedPosition(cell);
            return connected;
        }

        //Floods out from a player's tile, or any open tile, and compares the count against all open tiles.
        private bool AllWalkableTilesReachable()
        {
            int walkableCount = 0;
            Vector2Int? start = null;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!map.IsWalkable(cell)) continue;
                    walkableCount++;
                    start ??= cell;
                }
            }

            PlayerCharacter player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null && map.IsWalkable(player.GridPosition))
            {
                start = player.GridPosition;
            }

            if (start == null)
            {
                return false;
            }

            HashSet<Vector2Int> reached = new HashSet<Vector2Int> { start.Value };
            Queue<Vector2Int> pending = new Queue<Vector2Int>();
            pending.Enqueue(start.Value);
            Vector2Int[] steps = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

            while (pending.Count > 0)
            {
                Vector2Int cell = pending.Dequeue();
                foreach (Vector2Int step in steps)
                {
                    Vector2Int next = cell + step;
                    if (map.IsWalkable(next) && reached.Add(next))
                    {
                        pending.Enqueue(next);
                    }
                }
            }

            return reached.Count == walkableCount;
        }

        //Creates an empty obstacle. The shared item pool fills containers afterward.
        private bool SpawnObstacle(Vector2Int cell)
        {
            ObstacleGridControl prefab = PickRandom(obstaclePrefabs);
            if (prefab == null)
            {
                return false;
            }

            ObstacleGridControl obstacle = Instantiate(prefab, map.GridToWorld(cell), Quaternion.identity, transform);
            obstacle.name = prefab.name;

            if (!obstacle.Place(map, cell))
            {
                Destroy(obstacle.gameObject);
                return false;
            }

            //Put the back toward a wall and leave the door facing open hallway floor.
            obstacle.SetFacing(map.GetObstacleFacing(cell));
            obstacle.IsInteractable = true;
            ObstacleStorage storage = obstacle.GetComponent<ObstacleStorage>();
            if (storage == null) storage = obstacle.gameObject.AddComponent<ObstacleStorage>();
            storage.SetStoredItem(null);
            return true;
        }

        //Use the shared item pool in regular rooms and hallways.
        public void FillStorage(ObstacleStorage storage)
        {
            if (map == null || storage == null || storage.HasItem || !storage.CanInteract
                || !storage.transform.IsChildOf(map.transform)) return;
            if (itemSpawner == null) itemSpawner = map.GetComponent<ItemSpawner>();
            if (itemSpawner != null && Random.value < (forcedInteractableChance ?? interactableChance)
                && itemSpawner.TryTakeStorageItem(possibleItems, out Item item)) storage.TryStoreItem(item);
        }

        //Picks one entry from a list, skipping empty slots.
        private static T PickRandom<T>(T[] options) where T : Object
        {
            if (options == null)
            {
                return null;
            }

            List<T> valid = new List<T>();
            foreach (T option in options)
            {
                if (option != null) valid.Add(option);
            }

            return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
        }
    }
}
