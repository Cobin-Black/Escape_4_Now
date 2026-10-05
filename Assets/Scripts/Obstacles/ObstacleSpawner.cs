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
        [SerializeField] private ObstacleGridControl lockerPrefab;
        [SerializeField] private KeyItem keyItemPrefab;
        [SerializeField, Min(1)] private int obstacleCount = 6;

        //Chance that each placed obstacle can be opened, and the item prefabs one of them may hold.
        [SerializeField, Range(0f, 1f)] private float interactableChance = 0.5f;
        [SerializeField] private Item[] possibleItems;

        //Spawners that must finish first so obstacles never cover an event or item. Found on the map when left empty.
        [SerializeField] private MapEventController mapEvents;
        [SerializeField] private ItemSpawner itemSpawner;

        private void Start()
        {
            StartCoroutine(SpawnObstacles());
        }

        private bool SpawnLocker(Vector2Int cell)
        {
            ObstacleGridControl locker = Instantiate(
                lockerPrefab,
                map.GridToWorld(cell),
                Quaternion.identity,
                transform
            );

            locker.name = lockerPrefab.name;

            if (!locker.Place(map, cell))
            {
                Destroy(locker.gameObject);
                return false;
            }

            locker.IsInteractable = true;

            ObstacleStorage storage = locker.GetComponent<ObstacleStorage>();

            if (storage == null)
            {
                storage = locker.gameObject.AddComponent<ObstacleStorage>();
            }

            storage.SetStoredItem(keyItemPrefab);

            Debug.Log(
                $"[Obstacle System] Spawned guaranteed {locker.name} with key at {cell}."
            );

            return true;
        }

        private IEnumerator SpawnObstacles()
        {
            // Wait for the runtime map to finish generating.
            yield return null;

            if (map == null)
            {
                Debug.LogWarning("[Obstacle System] ObstacleSpawner could not find the map.");
                yield break;
            }

            if (mapEvents == null) mapEvents = map.GetComponent<MapEventController>();
            if (itemSpawner == null) itemSpawner = map.GetComponent<ItemSpawner>();

            //Events and items pick their tiles on the same frame as this, so wait until both are done.
            yield return new WaitUntil(() =>
                (mapEvents == null || !mapEvents.isActiveAndEnabled || mapEvents.EventsPlaced)
                && (itemSpawner == null || !itemSpawner.isActiveAndEnabled || itemSpawner.HasSpawnedItems));

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

            if (lockerPrefab != null && keyItemPrefab != null && emptyTiles.Count > 0)
            {
                int index = Random.Range(0, emptyTiles.Count);
                Vector2Int lockerCell = emptyTiles[index];
                emptyTiles.RemoveAt(index);

                if (KeepsMapConnected(lockerCell) && SpawnLocker(lockerCell))
                {
                    placed++;
                }
            }

            while (placed < obstacleCount && emptyTiles.Count > 0)
            {
                int index = Random.Range(0, emptyTiles.Count);
                Vector2Int cell = emptyTiles[index];
                emptyTiles.RemoveAt(index);

                if (KeepsMapConnected(cell) && SpawnObstacle(cell))
                {
                    placed++;
                }
            }

            if (placed < obstacleCount)
            {
                Debug.LogWarning($"[Obstacle System] Only found room for {placed} of {obstacleCount} obstacles.");
            }
        }

        //Keeps obstacles on open floor and off players, the exit, events, and items.
        private bool IsValidObstacleTile(Vector2Int cell)
        {
            return map.IsInteriorFloor(cell)
                && map.IsWalkable(cell)
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

        //Creates one obstacle on the tile and decides whether it holds an item.
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

            Item contents = PickRandom(possibleItems);
            obstacle.IsInteractable = contents != null && Random.value < interactableChance;

            if (obstacle.IsInteractable)
            {
                ObstacleStorage storage = obstacle.GetComponent<ObstacleStorage>();
                if (storage == null) storage = obstacle.gameObject.AddComponent<ObstacleStorage>();
                storage.SetStoredItem(contents);
            }

            string detail = obstacle.IsInteractable ? $" holding {contents.ItemName}" : "";
            Debug.Log($"[Obstacle System] Spawned {obstacle.name} at {cell}{detail}.");
            return true;
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
