using System.Collections;
using System.Collections.Generic;
using Escape4Now.Items;
using Escape4Now.Map;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Escape4Now.Obstacles
{
    //Places obstacles on random empty tiles once the events and items have their spots, and fills interactable ones with an item.
    public sealed class ObstacleSpawner : MonoBehaviour
    {
        [Header("Map")]
        [SerializeField] private IsometricMapTemplate map;
        [Header("Room Obstacle Prefabs")]
        [SerializeField, Tooltip("Obstacles that only spawn in the Computer Lab. Add more SchoolFurniture prefabs here to give the room variety.")]
        private Component[] computerLabPrefabs;
        [SerializeField, Tooltip("Obstacles that only spawn in the Classroom.")]
        private Component[] classroomPrefabs;
        [SerializeField, Tooltip("Obstacles that only spawn in hallways, including the searchable hallway locker (ObstacleGridControl). Special rooms off the hallway check this list first.")]
        private Component[] hallwayPrefabs;
        [SerializeField, Tooltip("Obstacles that only spawn in the Library.")]
        private Component[] libraryPrefabs;
        [SerializeField, Tooltip("Obstacles that only spawn in the Science Lab.")]
        private Component[] scienceLabPrefabs;
        //Older scenes kept every obstacle in one list. They are sorted into rooms once.
        [SerializeField, HideInInspector] private Component[] obstaclePrefabs;
        [SerializeField, HideInInspector, FormerlySerializedAs("schoolObstaclePrefabs")]
        private SchoolFurniture[] previousSchoolPrefabs;
        [SerializeField, HideInInspector] private bool obstacleCatalogInitialized;
        [SerializeField, HideInInspector] private bool roomSectionsInitialized;
        [Header("Additional Hallway Obstacles")]
        [SerializeField, Range(0, 10), InspectorName("Minimum Hallway Obstacles")] private int minimumObstacleCount = 2;
        [SerializeField, Range(0, 10), InspectorName("Maximum Hallway Obstacles")] private int maximumObstacleCount = 3;
        [Header("Stored Items")]
        [SerializeField, Range(0f, 1f), Tooltip("Chance that each obstacle holds an item. Which item is set per gamemode on the Item Spawner.")]
        private float interactableChance = 0.5f;
        [Header("Spawner References")]
        [SerializeField] private MapEventController mapEvents;
        [SerializeField] private ItemSpawner itemSpawner;
        private int? forcedObstacleCount;
        private float? forcedInteractableChance;
        private Vector2Int? forcedObstacleCell;
        private SchoolFurnitureSettings placementSettings;
        //Item placement waits until all searchable obstacles have been created.
        public bool HasSpawnedObstacles { get; private set; }

        public void SetObstacleCount(int count) { forcedObstacleCount = Mathf.Max(0, count); }
        public void SetInteractableChance(float chance) { forcedInteractableChance = Mathf.Clamp01(chance); }
        public void ForceObstacleTile(Vector2Int cell) { forcedObstacleCell = cell; }

        private void Awake()
        {
            if (map == null) map = FindFirstObjectByType<IsometricMapTemplate>();
            InitializeObstacleCatalog();
            if (map != null && map.GetComponent<ObstacleSpawner>() != this) map.SetObstacleSpawner(this);
        }

        private void OnValidate()
        {
            minimumObstacleCount = Mathf.Clamp(minimumObstacleCount, 0, 10);
            maximumObstacleCount = Mathf.Clamp(maximumObstacleCount, minimumObstacleCount, 10);
            interactableChance = float.IsNaN(interactableChance) ? 0.5f : Mathf.Clamp01(interactableChance);
#if UNITY_EDITOR
            if (!roomSectionsInitialized && !catalogInitializationQueued)
            {
                catalogInitializationQueued = true;
                UnityEditor.EditorApplication.delayCall += InitializeCatalogInEditor;
            }
#endif
        }

        //Migrate existing scenes once, then preserve the team's Inspector edits.
        private void InitializeObstacleCatalog()
        {
            if (roomSectionsInitialized) return;
            var catalog = new List<Component>();
            var furnitureKinds = new HashSet<SchoolFurnitureKind>();
            void Add(Component prefab)
            {
                if (prefab == null || catalog.Exists(entry => entry.gameObject == prefab.gameObject)) return;
                catalog.Add(prefab);
                SchoolFurniture furniture = prefab.GetComponent<SchoolFurniture>();
                if (furniture != null) furnitureKinds.Add(furniture.Kind);
            }
            foreach (Component prefab in obstaclePrefabs ?? System.Array.Empty<Component>()) Add(prefab);
            foreach (SchoolFurniture prefab in previousSchoolPrefabs ?? System.Array.Empty<SchoolFurniture>()) Add(prefab);
            if (!obstacleCatalogInitialized)
                foreach (SchoolFurniture prefab in Resources.LoadAll<SchoolFurniture>("SchoolObstacles"))
                    if (prefab != null && !furnitureKinds.Contains(prefab.Kind)) Add(prefab);
            foreach (Component prefab in catalog) AddToDefaultSections(prefab);
            obstaclePrefabs = null;
            previousSchoolPrefabs = null;
            obstacleCatalogInitialized = true;
            roomSectionsInitialized = true;
        }

        //A furniture kind goes in every room whose layout uses it. Anything else belongs to the hallway.
        private void AddToDefaultSections(Component prefab)
        {
            SchoolFurniture furniture = prefab.GetComponent<SchoolFurniture>();
            bool added = false;
            if (furniture != null)
                foreach (SchoolRoomType room in RoomSections)
                    if (room != SchoolRoomType.None && System.Array.IndexOf(SchoolFurniturePlan.Pool(room), furniture.Kind) >= 0)
                    {
                        AddToSection(room, prefab);
                        added = true;
                    }
            if (!added) AddToSection(SchoolRoomType.None, prefab);
        }

        private void AddToSection(SchoolRoomType room, Component prefab)
        {
            Component[] section = GetSection(room) ?? System.Array.Empty<Component>();
            foreach (Component entry in section)
                if (entry != null && entry.gameObject == prefab.gameObject) return;
            var list = new List<Component>(section) { prefab };
            SetSection(room, list.ToArray());
        }

        private static readonly SchoolRoomType[] RoomSections =
        {
            SchoolRoomType.ComputerLab, SchoolRoomType.Classroom, SchoolRoomType.None,
            SchoolRoomType.Library, SchoolRoomType.ScienceLab
        };

        //SchoolRoomType.None is the hallway section.
        private Component[] GetSection(SchoolRoomType room)
        {
            switch (room)
            {
                case SchoolRoomType.ComputerLab: return computerLabPrefabs;
                case SchoolRoomType.Classroom: return classroomPrefabs;
                case SchoolRoomType.Library: return libraryPrefabs;
                case SchoolRoomType.ScienceLab: return scienceLabPrefabs;
                default: return hallwayPrefabs;
            }
        }

        private void SetSection(SchoolRoomType room, Component[] prefabs)
        {
            switch (room)
            {
                case SchoolRoomType.ComputerLab: computerLabPrefabs = prefabs; break;
                case SchoolRoomType.Classroom: classroomPrefabs = prefabs; break;
                case SchoolRoomType.Library: libraryPrefabs = prefabs; break;
                case SchoolRoomType.ScienceLab: scienceLabPrefabs = prefabs; break;
                default: hallwayPrefabs = prefabs; break;
            }
        }

        //Every furniture prefab from all five sections.
        public IEnumerable<SchoolFurniture> GetSchoolPrefabs()
        {
            var seen = new HashSet<SchoolFurniture>();
            foreach (SchoolRoomType room in RoomSections)
                foreach (SchoolFurniture prefab in GetRoomPrefabs(room))
                    if (seen.Add(prefab)) yield return prefab;
        }

        //Furniture prefabs that may only spawn in one room. SchoolRoomType.None is the hallway.
        public IEnumerable<SchoolFurniture> GetRoomPrefabs(SchoolRoomType room) => GetObstaclePrefabs<SchoolFurniture>(room);

        private IEnumerable<T> GetObstaclePrefabs<T>(SchoolRoomType room) where T : Component
        {
            InitializeObstacleCatalog();
            foreach (Component entry in GetSection(room) ?? System.Array.Empty<Component>())
            {
                if (entry == null) continue;
                T prefab = entry.GetComponent<T>();
                if (prefab != null) yield return prefab;
            }
        }

#if UNITY_EDITOR
        private bool catalogInitializationQueued;

        private void InitializeCatalogInEditor()
        {
            catalogInitializationQueued = false;
            if (this == null || roomSectionsInitialized) return;
            bool persistChanges = !Application.IsPlaying(gameObject);
            if (persistChanges) UnityEditor.Undo.RecordObject(this, "Initialize obstacle prefabs");
            InitializeObstacleCatalog();
            if (!persistChanges) return;
            UnityEditor.EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        [ContextMenu("Populate Missing Obstacle Prefabs")]
        private void PopulateMissingObstaclePrefabs()
        {
            UnityEditor.Undo.RecordObject(this, "Populate obstacle prefabs");
            obstacleCatalogInitialized = false;
            roomSectionsInitialized = false;
            InitializeCatalogInEditor();
        }
#endif

        private void Start()
        {
            StartCoroutine(SpawnObstacles());
        }

        //The current scene uses one obstacle template on every computer.
        public ObstacleGridControl GetNetworkPrefab()
        {
            foreach (ObstacleGridControl prefab in GetObstaclePrefabs<ObstacleGridControl>(SchoolRoomType.None)) return prefab;
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
            placementSettings = map.FurnitureSettings;

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
                ? forcedObstacleCount.Value : Random.Range(minimumObstacleCount, maximumObstacleCount + 1);
            while (placed < target && emptyTiles.Count > 0)
            {
                int index = Random.Range(0, emptyTiles.Count);
                Vector2Int cell = emptyTiles[index];
                emptyTiles.RemoveAt(index);

                //Leave at least two tile steps between hallway objects to avoid clusters.
                if (decorations.Exists(other => Mathf.Abs(other.x - cell.x) + Mathf.Abs(other.y - cell.y) <= placementSettings.HallwaySpacing)) continue;
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
            placementSettings ??= map.FurnitureSettings;
            int clearance = placementSettings.DoorClearance;
            for (int y = cell.y - clearance; y <= cell.y + clearance; y++)
                for (int x = cell.x - clearance; x <= cell.x + clearance; x++)
                    if (Mathf.Abs(x - cell.x) + Mathf.Abs(y - cell.y) <= clearance && map.IsDoorway(new Vector2Int(x, y))) return true;
            return false;
        }

        //Keeps obstacles on open floor and off players, the exit, events, and items.
        private bool IsValidObstacleTile(Vector2Int cell)
        {
            placementSettings ??= map.FurnitureSettings;
            for (int y = cell.y - placementSettings.SpawnClearance; y <= cell.y + placementSettings.SpawnClearance; y++)
                for (int x = cell.x - placementSettings.SpawnClearance; x <= cell.x + placementSettings.SpawnClearance; x++)
                    if (Mathf.Abs(x - cell.x) + Mathf.Abs(y - cell.y) <= placementSettings.SpawnClearance
                        && map.IsOccupied(new Vector2Int(x, y))) return false;
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
            if (connected && placementSettings != null && placementSettings.PreventNewDeadEnds)
            {
                Vector2Int[] steps = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
                foreach (Vector2Int step in steps)
                {
                    Vector2Int neighbour = cell + step;
                    if (!map.IsWalkable(neighbour) || map.GetSpaceType(neighbour) != SchoolSpaceType.None) continue;
                    int exits = 0;
                    foreach (Vector2Int direction in steps) if (map.IsWalkable(neighbour + direction)) exits++;
                    if (exits == 1) { connected = false; break; }
                }
            }
            if (connected)
                foreach (ObstacleStorage storage in map.GetComponentsInChildren<ObstacleStorage>())
                {
                    bool accessible = false;
                    foreach (Vector2Int step in new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down })
                        accessible |= map.IsWalkable(storage.GridPosition + step);
                    if (!accessible) { connected = false; break; }
                }
            map.UnregisterBlockedPosition(cell);
            return connected;
        }

        //Every open tile, including the exit approach and every room, must be reachable without crossing an event that moves the player.
        private bool AllWalkableTilesReachable()
        {
            PlayerCharacter player = FindFirstObjectByType<PlayerCharacter>();
            return map.AllWalkableTilesConnected(cell => mapEvents != null && mapEvents.ProtectsPaths && mapEvents.HasMovingEvent(cell),
                player != null ? player.GridPosition : (Vector2Int?)null);
        }

        //Creates an empty obstacle. The shared item pool fills containers afterward.
        private bool SpawnObstacle(Vector2Int cell)
        {
            ObstacleGridControl prefab = PickRandom(new List<ObstacleGridControl>(GetObstaclePrefabs<ObstacleGridControl>(SchoolRoomType.None)).ToArray());
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

        //Rolls for an item from the current gamemode's list in regular rooms and hallways.
        public void FillStorage(ObstacleStorage storage)
        {
            if (map == null || storage == null || storage.HasItem || !storage.CanInteract
                || !storage.transform.IsChildOf(map.transform)) return;
            if (itemSpawner == null) itemSpawner = map.GetComponent<ItemSpawner>();
            if (itemSpawner != null && Random.value < (forcedInteractableChance ?? interactableChance)
                && itemSpawner.TryTakeStorageItem(out Item item)) storage.TryStoreItem(item);
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
