
using System.Collections.Generic;
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
        private readonly List<float> spawnTimers = new();
        private FreeForAllController match;
        private float replacementTimer;

        // Start with randomly placed items.
        public void SpawnItems(FreeForAllController controller)
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
