
using System.Collections.Generic;
using System.Linq;
using Escape4Now.Arena;
using UnityEngine;

namespace Escape4Now.Items
{
    // Spawns and replaces items during FreeForAll matches.
    public sealed class FreeForAllItemSpawner : MonoBehaviour
    {
        [SerializeField] private FreeForAllItem[] itemPrefabs;
        [SerializeField, Range(1, 10)] private int maxFloorItems = 10;
        [SerializeField, Min(1f)] private float spawnDelay = 10f;
        [SerializeField, Min(0.1f)] private float itemSpacing = 1f;

        private readonly List<FreeForAllItem> floorItems = new();
        private readonly Dictionary<FreeForAllItem, Vector2> itemPositions = new();
        // Allow bots to check available item locations.
        public IReadOnlyDictionary<FreeForAllItem, Vector2> AvailableItems => itemPositions;
        private readonly List<float> spawnTimers = new();
        private KingOfTheHillController match;
        private float replacementTimer;
        private readonly Dictionary<int, FreeForAllItem> replicas = new();
        public T FindPrefab<T>() where T : FreeForAllItem => itemPrefabs?.OfType<T>().FirstOrDefault();

        [System.Serializable]
        public sealed class OnlinePickup
        {
            public int id;
            public FreeForAllItemType type;
            public Vector3 position;
        }

        public OnlinePickup[] CaptureOnlinePickups() => floorItems.Where(item =>
            item != null && item.GetComponent<FreeForAllItemPickup>()?.Collected != true)
            .Select(item => new OnlinePickup { id = item.GetInstanceID(), type = item.ItemType, position = item.transform.position }).ToArray();

        public void ApplyOnlinePickups(OnlinePickup[] pickups)
        {
            if (pickups == null || itemPrefabs == null) return;
            var visible = new HashSet<int>(pickups.Select(pickup => pickup.id));
            foreach (int id in replicas.Keys.ToArray())
            {
                if (visible.Contains(id)) continue;
                if (replicas[id] != null) Destroy(replicas[id].gameObject);
                replicas.Remove(id);
            }
            foreach (var pickup in pickups)
            {
                if (replicas.ContainsKey(pickup.id)) continue;
                var prefab = System.Array.Find(itemPrefabs, item => item != null && item.ItemType == pickup.type);
                if (prefab == null) continue;
                var replica = Instantiate(prefab, pickup.position, Quaternion.identity, transform);
                var input = replica.GetComponent<FreeForAllItemPickup>();
                if (input != null) input.enabled = false;
                var collider = replica.GetComponent<Collider2D>();
                if (collider != null) collider.enabled = false;
                replicas.Add(pickup.id, replica);
            }
        }

        // Start with randomly placed items.
        public void SpawnItems(KingOfTheHillController controller)
        {
            ClearItems();

            match = controller;

            for (int i = 0; i < maxFloorItems; i++)
                SpawnRandomItem();

            replacementTimer = spawnDelay;
        }

        private void Update()
        {
            if (match == null || !match.Playing || match.Paused)
                return;

            // Remove items that players have collected.
            for (int i = floorItems.Count - 1; i >= 0; i--)
            {
                FreeForAllItem item = floorItems[i];

                if (item == null)
                {
                    floorItems.RemoveAt(i);
                    continue;
                }

                FreeForAllItemPickup pickup = item.GetComponent<FreeForAllItemPickup>();

                if (pickup != null && pickup.Collected)
                {
                    itemPositions.Remove(item);
                    floorItems.RemoveAt(i);
                    spawnTimers.Add(spawnDelay);
                }
            }

            // Count down each collected item's replacement.
            for (int i = spawnTimers.Count - 1; i >= 0; i--)
            {
                spawnTimers[i] -= Time.deltaTime;

                if (spawnTimers[i] <= 0f)
                {
                    if (floorItems.Count < maxFloorItems)
                        SpawnRandomItem();

                    spawnTimers.RemoveAt(i);
                }
            }

            // Replace the oldest item after the floor stays full for 10 seconds.
            if (floorItems.Count == maxFloorItems)
            {
                replacementTimer -= Time.deltaTime;

                if (replacementTimer <= 0f)
                {
                    FreeForAllItem oldest = floorItems[0];
                    floorItems.RemoveAt(0);
                    itemPositions.Remove(oldest);

                    if (oldest != null)
                        Destroy(oldest.gameObject);

                    SpawnRandomItem();
                    replacementTimer = spawnDelay;
                }
            }
            else
            {
                replacementTimer = spawnDelay;
            }
        }

        // Place a random item on a valid floor tile.
        private void SpawnRandomItem()
        {
            if (itemPrefabs == null || itemPrefabs.Length == 0)
                return;

            ArenaMap map = match.Map;
            List<Vector2Int> cells = map.Layout.Cells();
            ArenaLayout.Shuffle(cells, map.Random);

            foreach (Vector2Int cell in cells)
            {
                if (!map.Layout.Fits(cell, map.Settings.playerRadius))
                    continue;

                bool occupied = false;

                foreach (ArenaParticipant player in match.Players)
                {
                    if (player.Alive &&
                        Vector2.Distance(cell, player.Position) < itemSpacing)
                    {
                        occupied = true;
                        break;
                    }
                }

                foreach (FreeForAllItem item in floorItems)
                {
                    if (item != null &&
                        itemPositions.TryGetValue(item, out Vector2 position) &&
                        Vector2.Distance(cell, position) < itemSpacing)
                    {
                        occupied = true;
                        break;
                    }
                }

                if (occupied)
                    continue;

                FreeForAllItem prefab =
                    itemPrefabs[Random.Range(0, itemPrefabs.Length)];

                FreeForAllItem spawned = Instantiate(
                    prefab,
                    map.World(cell) + Vector3.back,
                    Quaternion.identity,
                    transform
                );

                floorItems.Add(spawned);
                itemPositions[spawned] = cell;
                return;
            }
        }

        // Remove items left from a previous match.
        private void ClearItems()
        {
            foreach (FreeForAllItem item in floorItems)
                if (item != null)
                    Destroy(item.gameObject);

            floorItems.Clear();
            spawnTimers.Clear();
            itemPositions.Clear();
        }
    }
}
