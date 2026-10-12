using System.Collections;
using System.Collections.Generic;
using Escape4Now.Map;
using Escape4Now.Obstacles;
using UnityEngine;

namespace Escape4Now.Items
{
    public sealed class ItemSpawner : MonoBehaviour
    {
        [SerializeField] private IsometricMapTemplate map;
        [SerializeField] private DoubleDiceItem doubleDicePrefab;
        [SerializeField] private CustomDiceItem customDicePrefab;
        [SerializeField] private KeyStealerItem keyStealerPrefab;
        private readonly List<Item> remainingItems = new List<Item>();

        //Lets other spawners wait until every item has its tile.
        public bool HasSpawnedItems { get; private set; }
        //Obstacles can be placed once the guaranteed floor rewards are protected.
        public bool HasPlacedFloorRewards { get; private set; }

        //Shares the existing templates with the network view.
        public Item GetNetworkPrefab(int type) => type == 1 ? doubleDicePrefab : type == 2 ? customDicePrefab
            : type == 3 ? keyStealerPrefab : type == 4 ? map.GetComponent<ObstacleSpawner>()?.KeyPrefab : null;

        //Each new match creates a new spawner and a fresh item pool.
        private void Start()
        {
            StartCoroutine(SpawnItems());
        }

        //Special rooms choose first, then containers, then any remaining floor items.
        private IEnumerator SpawnItems()
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.IsClient) yield break;
            // Wait for the runtime map to finish generating.
            yield return null;

            if (map == null)
            {
                Debug.LogWarning("[Item System] ItemSpawner could not find the map.");
                HasPlacedFloorRewards = true;
                HasSpawnedItems = true;
                yield break;
            }

            MapEventController events = map.GetComponent<MapEventController>();
            yield return new WaitUntil(() => events == null || !events.isActiveAndEnabled || events.EventsPlaced);
            //One entry per type covers floor pickups and every searchable obstacle.
            AddItemType(doubleDicePrefab);
            AddItemType(customDicePrefab);
            AddItemType(keyStealerPrefab);
            if (remainingItems.Count < map.SpecialItemCells.Count)
                throw new System.InvalidOperationException("Each special room needs a different item type. Assign more item prefabs.");
            foreach (int address in map.SpecialItemCells)
            {
                var cell = new Vector2Int(address % map.Width, address / map.Width);
                if (!IsValidItemTile(cell)) throw new System.InvalidOperationException("A special room reward tile is blocked.");
                int index = Random.Range(0, remainingItems.Count);
                SpawnItem(remainingItems[index], new List<Vector2Int> { cell });
                remainingItems.RemoveAt(index);
            }
            HasPlacedFloorRewards = true;
            ObstacleSpawner obstacles = map.GetComponent<ObstacleSpawner>();
            yield return new WaitUntil(() => obstacles == null || !obstacles.isActiveAndEnabled || obstacles.HasSpawnedObstacles);
            if (obstacles != null && obstacles.isActiveAndEnabled)
            {
                //Shuffle containers so the first room built does not always receive the items.
                var containers = new List<ObstacleStorage>(map.GetComponentsInChildren<ObstacleStorage>());
                while (containers.Count > 0)
                {
                    int index = Random.Range(0, containers.Count);
                    obstacles.FillStorage(containers[index]);
                    containers.RemoveAt(index);
                }
            }
            List<Vector2Int> emptyTiles = new List<Vector2Int>();

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    SchoolSpaceType space = map.GetSpaceType(cell);
                    if (IsValidItemTile(cell) && space == SchoolSpaceType.None
                        && (events == null || !events.HasEvent(cell)))
                    {
                        emptyTiles.Add(cell);
                    }
                }
            }

            foreach (Item prefab in remainingItems) SpawnItem(prefab, emptyTiles);
            remainingItems.Clear();
            HasSpawnedItems = true;
        }

        //Duplicate references or variants of the same item type use just one slot.
        private void AddItemType(Item prefab)
        {
            if (prefab == null || remainingItems.Exists(item => item.GetType() == prefab.GetType())) return;
            remainingItems.Add(prefab);
        }

        //Removing the chosen type prevents another container or floor tile from receiving it.
        public bool TryTakeStorageItem(Item[] allowedItems, out Item prefab)
        {
            prefab = null;
            if (!HasPlacedFloorRewards || HasSpawnedItems || allowedItems == null
                || Escape4Now.Multiplayer.LanMultiplayerSession.IsClient) return false;
            var choices = new List<int>();
            for (int i = 0; i < remainingItems.Count; i++)
                foreach (Item allowed in allowedItems)
                    if (allowed != null && allowed.GetType() == remainingItems[i].GetType())
                    { choices.Add(i); break; }
            if (choices.Count == 0) return false;
            int index = choices[Random.Range(0, choices.Count)];
            prefab = remainingItems[index];
            remainingItems.RemoveAt(index);
            return true;
        }

        //Rewards must stay on clear floor, away from players and the exit.
        private bool IsValidItemTile(Vector2Int cell)
        {
            return map.IsWalkable(cell)
                && !map.IsExit(cell)
                && !map.IsDoorway(cell)
                && !map.IsOccupied(cell);
        }

        //Places one pickup and removes its tile from this batch's available positions.
        private void SpawnItem(Item prefab, List<Vector2Int> emptyTiles)
        {
            if (prefab == null || emptyTiles.Count == 0)
            {
                return;
            }

            int index = Random.Range(0, emptyTiles.Count);
            Vector2Int cell = emptyTiles[index];

            Item spawnedItem = Instantiate(
                prefab,
                map.GridToWorld(cell),
                Quaternion.identity,
                transform
            );

            if (spawnedItem is DoubleDiceItem doubleDice)
            {
                doubleDice.SetSpawnPosition(map, cell);
            }
            else if (spawnedItem is CustomDiceItem customDice)
            {
                customDice.SetSpawnPosition(map, cell);
            }
            else if (spawnedItem is KeyStealerItem keyStealer)
            {
                keyStealer.SetSpawnPosition(map, cell);
            }

            emptyTiles.RemoveAt(index);

            Debug.Log(
                $"[Item System] Spawned {spawnedItem.ItemName} at {cell}."
            );
        }
    }
}
