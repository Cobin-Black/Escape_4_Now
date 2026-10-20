using System.Collections;
using System.Collections.Generic;
using Escape4Now.GameModes;
using Escape4Now.Map;
using Escape4Now.Obstacles;
using UnityEngine;

namespace Escape4Now.Items
{
    //One item an obstacle can hold, and how often it is picked compared to the other items in the list.
    [System.Serializable]
    public sealed class ItemSpawnOption
    {
        [SerializeField] private Item prefab;
        [SerializeField, Range(0f, 100f), Tooltip("How often this item is picked when an obstacle holds an item. "
            + "Higher numbers show up more often than lower ones in the same list. 0 never spawns.")]
        private float spawnChance = 50f;

        public Item Prefab => prefab;
        public float SpawnChance => spawnChance;

        public ItemSpawnOption() { }
        public ItemSpawnOption(Item prefab, float spawnChance) { this.prefab = prefab; this.spawnChance = spawnChance; }

        public void Validate() { spawnChance = float.IsNaN(spawnChance) ? 0f : Mathf.Clamp(spawnChance, 0f, 100f); }
    }

    //Fills searchable obstacles with items. Each gamemode has its own list, so an item like the
    //Key Stealer can exist in one gamemode only. The Race key is placed by the Race_Gamemode, not this list.
    public sealed class ItemSpawner : MonoBehaviour
    {
        [Header("Map")]
        [SerializeField] private IsometricMapTemplate map;
        [Header("Race Gamemode Items")]
        [SerializeField] private ItemSpawnOption[] raceItems;
        [Header("Monster Gamemode Items")]
        [SerializeField] private ItemSpawnOption[] monsterItems;
        [Header("Free-for-All Gamemode Items")]
        [SerializeField] private ItemSpawnOption[] freeForAllItems;
        [Header("Untitled Gamemode Items")]
        [SerializeField] private ItemSpawnOption[] untitledItems;
        [Header("Ordinary Item Budget")]
        [SerializeField, Min(0), Tooltip("Maximum ordinary item-filled containers per match. The mode's guaranteed key and each special-room reward are separate. Zero disables ordinary items.")]
        private int maximumOrdinaryItems = 4;
        public int MaximumOrdinaryItems => maximumOrdinaryItems;
        [Header("Testing")]
        [SerializeField, Tooltip("Item list used when the scene has no gamemode script.")]
        private GameModeType gameModeWithoutScript = GameModeType.Race;

        //Lets other spawners wait until item locations are reserved.
        public bool HasSpawnedItems { get; private set; }
        //Obstacles can be placed after special-room item locations are chosen.
        public bool HasReservedSpecialRoomItems { get; private set; }

        //The gamemode the open scene is playing.
        public GameModeType CurrentGameMode
        {
            get
            {
                GameMode mode = GameMode.Current;
                return mode != null ? mode.Type : gameModeWithoutScript;
            }
        }

        private void OnValidate()
        {
            maximumOrdinaryItems = Mathf.Max(0, maximumOrdinaryItems);
            ValidateList(raceItems, GameModeType.Race);
            ValidateList(monsterItems, GameModeType.Monster);
            ValidateList(freeForAllItems, GameModeType.FreeForAll);
            ValidateList(untitledItems, GameModeType.Untitled);
        }

        //The key belongs to the Race_Gamemode and the Key Stealer only works in Race, so neither can be added elsewhere.
        private void ValidateList(ItemSpawnOption[] options, GameModeType mode)
        {
            if (options == null) return;
            foreach (ItemSpawnOption option in options)
            {
                if (option == null) continue;
                option.Validate();
                if (option.Prefab is KeyItem)
                {
                    Debug.LogWarning($"[Item System] The key is set on the Race_Gamemode, not in the {mode} item list. It will be ignored.", this);
                }
                else if (option.Prefab is KeyStealerItem && mode != GameModeType.Race)
                {
                    Debug.LogWarning($"[Item System] The Key Stealer only works in the Race gamemode. It will be ignored in the {mode} item list.", this);
                }
            }
        }

        public ItemSpawnOption[] GetItemOptions(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Monster: return monsterItems;
                case GameModeType.FreeForAll: return freeForAllItems;
                case GameModeType.Untitled: return untitledItems;
                default: return raceItems;
            }
        }

        //Shares the existing templates with the network view.
        public Item GetNetworkPrefab(int type)
        {
            if (type == 4) return Race_Gamemode.Current != null ? Race_Gamemode.Current.KeyPrefab : null;
            foreach (GameModeType mode in System.Enum.GetValues(typeof(GameModeType)))
                foreach (ItemSpawnOption option in GetItemOptions(mode) ?? System.Array.Empty<ItemSpawnOption>())
                {
                    Item prefab = option?.Prefab;
                    if (prefab == null) continue;
                    if ((type == 1 && prefab is DoubleDiceItem) || (type == 2 && prefab is CustomDiceItem)
                        || (type == 3 && prefab is KeyStealerItem)) return prefab;
                }
            return null;
        }

        //Each new match creates a new spawner.
        private void Start()
        {
            StartCoroutine(SpawnItems());
        }

        //Special rooms get one item each, then the gamemode places its own items, then the remaining obstacles roll for an item.
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
                    if (roomChoices.Count == 0)
                        throw new System.InvalidOperationException("A special room needs an empty obstacle for its item.");
                    if (!TryPickItem(out Item roomItem)) break;
                    ObstacleStorage roomStorage = roomChoices[Random.Range(0, roomChoices.Count)];
                    if (!roomStorage.TryStoreItem(roomItem))
                        throw new System.InvalidOperationException("A special-room item could not be stored.");
                }

                //Gamemode items, such as the Race key, go in before ordinary items are assigned.
                GameMode mode = GameMode.Current;
                if (mode != null) mode.PlaceGuaranteedItems(this);

                //Shuffle containers so items do not always go to the same obstacles.
                int ordinaryItems = 0;
                while (containers.Count > 0 && ordinaryItems < maximumOrdinaryItems)
                {
                    int index = Random.Range(0, containers.Count);
                    ObstacleStorage storage = containers[index];
                    containers.RemoveAt(index);
                    if (!IsAvailableStorage(storage, SchoolSpaceType.None)) continue;
                    obstacles.FillStorage(storage);
                    if (storage.HasItem) ordinaryItems++;
                }
            }
            HasSpawnedItems = true;
        }

        //Stores an item in a random empty obstacle outside the special rooms. Used by gamemodes for their own items.
        public bool TryStoreInRandomObstacle(Item prefab)
        {
            if (prefab == null || map == null) return false;
            var choices = new List<ObstacleStorage>(map.GetComponentsInChildren<ObstacleStorage>())
                .FindAll(storage => IsAvailableStorage(storage, SchoolSpaceType.None));
            if (choices.Count == 0) return false;
            return choices[Random.Range(0, choices.Count)].TryStoreItem(prefab);
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

        //Picks the item for one obstacle while placement is running. Finished matches never refill obstacles.
        public bool TryTakeStorageItem(out Item prefab)
        {
            prefab = null;
            if (!HasReservedSpecialRoomItems || HasSpawnedItems
                || Escape4Now.Multiplayer.LanMultiplayerSession.IsClient) return false;
            return TryPickItem(out prefab);
        }

        //Weighted pick from the current gamemode's list, using each item's spawn chance.
        private bool TryPickItem(out Item prefab)
        {
            prefab = null;
            GameModeType mode = CurrentGameMode;
            var choices = new List<ItemSpawnOption>();
            float total = 0f;
            foreach (ItemSpawnOption option in GetItemOptions(mode) ?? System.Array.Empty<ItemSpawnOption>())
            {
                if (option == null || option.Prefab == null || option.SpawnChance <= 0f || option.Prefab is KeyItem
                    || (option.Prefab is KeyStealerItem && mode != GameModeType.Race)) continue;
                choices.Add(option);
                total += option.SpawnChance;
            }
            if (choices.Count == 0) return false;
            float roll = Random.Range(0f, total);
            foreach (ItemSpawnOption option in choices)
            {
                roll -= option.SpawnChance;
                if (roll < 0f)
                {
                    prefab = option.Prefab;
                    return true;
                }
            }
            prefab = choices[choices.Count - 1].Prefab;
            return true;
        }
    }
}
