using System.Collections;
using System.Collections.Generic;
using Escape4Now.Map;
using Escape4Now.Obstacles;
using UnityEngine;

namespace Escape4Now.Items
{
    public sealed class ItemSpawner : MonoBehaviour
    {
        [Header("Map")]
        [SerializeField] private IsometricMapTemplate map;
        [Header("Item Prefabs")]
        [SerializeField] private DoubleDiceItem doubleDicePrefab;
        [SerializeField] private CustomDiceItem customDicePrefab;
        [SerializeField] private KeyStealerItem keyStealerPrefab;
        [SerializeField] private KeyItem keyItemPrefab;
        private readonly List<Item> remainingItems = new List<Item>();

        //Lets other spawners wait until item locations are reserved.
        public bool HasSpawnedItems { get; private set; }
        //Obstacles can be placed after special-room item locations are chosen.
        public bool HasReservedSpecialRoomItems { get; private set; }
        public KeyItem KeyPrefab => keyItemPrefab;

        //Shares the existing templates with the network view.
        public Item GetNetworkPrefab(int type) => type == 1 ? doubleDicePrefab : type == 2 ? customDicePrefab
            : type == 3 ? keyStealerPrefab : type == 4 ? keyItemPrefab : null;

        //Each new match creates a new spawner and a fresh item pool.
        private void Start()
        {
            StartCoroutine(SpawnItems());
        }

        //Special-room items go inside furniture, then other items go in obstacles or on the floor.
        private IEnumerator SpawnItems()
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.IsClient) yield break;
            // Wait for the runtime map to finish generating.
            yield return null;

            if (map == null)
            {
                Debug.LogWarning("[Item System] ItemSpawner could not find the map.");
                HasReservedSpecialRoomItems = true;
                HasSpawnedItems = true;
                yield break;
            }

            MapEventController events = map.GetComponent<MapEventController>();
            yield return new WaitUntil(() => events == null || !events.isActiveAndEnabled || events.EventsPlaced);
            //One entry per type covers floor pickups and every searchable obstacle.
            AddItemType(doubleDicePrefab);
            AddItemType(customDicePrefab);
            AddItemType(keyStealerPrefab);
            if (remainingItems.Count < map.SecondarySpaces.Count)
                throw new System.InvalidOperationException("Each special room needs a different item type. Assign more item prefabs.");
            //Tell the obstacle spawner the special rooms are ready for item placement.
            HasReservedSpecialRoomItems = true;
            ObstacleSpawner obstacles = map.GetComponent<ObstacleSpawner>();
            yield return new WaitUntil(() => obstacles == null || !obstacles.isActiveAndEnabled || obstacles.HasSpawnedObstacles);
            if (map.SecondarySpaces.Count > 0 && (obstacles == null || !obstacles.isActiveAndEnabled))
                throw new System.InvalidOperationException("Special rooms need the obstacle spawner to place their items.");
            if (obstacles != null && obstacles.isActiveAndEnabled)
            {
                var containers = new List<ObstacleStorage>(map.GetComponentsInChildren<ObstacleStorage>());
                foreach (SchoolSecondarySpace room in map.SecondarySpaces)
                {
                    var roomChoices = containers.FindAll(storage => IsAvailableStorage(storage, room.Type));
                    if (roomChoices.Count == 0 || remainingItems.Count == 0)
                        throw new System.InvalidOperationException("A special room needs an empty obstacle for its item.");
                    ObstacleStorage roomStorage = roomChoices[Random.Range(0, roomChoices.Count)];
                    int itemIndex = Random.Range(0, remainingItems.Count);
                    if (!roomStorage.TryStoreItem(remainingItems[itemIndex]))
                        throw new System.InvalidOperationException("A special-room item could not be stored.");
                    remainingItems.RemoveAt(itemIndex);
                }

                //Choose one empty obstacle for the key before ordinary items are assigned.
                var keyChoices = containers.FindAll(storage => IsAvailableStorage(storage, SchoolSpaceType.None));
                if (keyItemPrefab != null && keyChoices.Count > 0)
                {
                    ObstacleStorage keyStorage = keyChoices[Random.Range(0, keyChoices.Count)];
                    if (!keyStorage.TryStoreItem(keyItemPrefab))
                        Debug.LogError("[Item System] The key could not be stored.");
                }
                else
                {
                    Debug.LogError("[Item System] No empty obstacle was available for the key.");
                }

                //Shuffle containers so items do not always go to the same obstacles.
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

        private bool IsAvailableStorage(ObstacleStorage storage, SchoolSpaceType roomType)
        {
            return storage != null && storage.CanInteract && !storage.HasItem
                && storage.transform.IsChildOf(map.transform)
                && map.GetSpaceType(storage.GridPosition) == roomType
                && (map.IsWalkable(storage.GridPosition + Vector2Int.up)
                    || map.IsWalkable(storage.GridPosition + Vector2Int.down)
                    || map.IsWalkable(storage.GridPosition + Vector2Int.left)
                    || map.IsWalkable(storage.GridPosition + Vector2Int.right));
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
            if (!HasReservedSpecialRoomItems || HasSpawnedItems || allowedItems == null
                || Escape4Now.Multiplayer.LanMultiplayerSession.IsClient) return false;
            var choices = new List<int>();
            for (int i = 0; i < remainingItems.Count; i++)
            {
                //The Key Stealer can use any searchable obstacle, even if it is not in the Inspector list.
                if (remainingItems[i] is KeyStealerItem)
                {
                    choices.Add(i);
                    continue;
                }

                foreach (Item allowed in allowedItems)
                    if (allowed != null && allowed.GetType() == remainingItems[i].GetType())
                    { choices.Add(i); break; }
            }
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
