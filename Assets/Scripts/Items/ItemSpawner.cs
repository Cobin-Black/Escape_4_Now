using System.Collections;
using System.Collections.Generic;
using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Items
{
    public sealed class ItemSpawner : MonoBehaviour
    {
        [SerializeField] private IsometricMapTemplate map;
        [SerializeField] private DoubleDiceItem doubleDicePrefab;
        [SerializeField] private CustomDiceItem customDicePrefab;

        //Lets other spawners wait until every item has its tile.
        public bool HasSpawnedItems { get; private set; }

        //Shares the existing templates with the network view.
        public Item GetNetworkPrefab(int type) => type == 1 ? doubleDicePrefab : type == 2 ? customDicePrefab : null;

        private void Start()
        {
            StartCoroutine(SpawnItems());
        }

        private IEnumerator SpawnItems()
        {
            // Wait for the runtime map to finish generating.
            yield return null;

            if (map == null)
            {
                Debug.LogWarning("[Item System] ItemSpawner could not find the map.");
                HasSpawnedItems = true;
                yield break;
            }

            MapEventController events = map.GetComponent<MapEventController>();
            yield return new WaitUntil(() => events == null || !events.isActiveAndEnabled || events.EventsPlaced);
            //Each generated Storage room or Janitor Closet has one reserved, reachable reward.
            var rewards = new List<Item>();
            if (doubleDicePrefab != null) rewards.Add(doubleDicePrefab);
            if (customDicePrefab != null) rewards.Add(customDicePrefab);
            foreach (int address in map.SpecialItemCells)
            {
                if (rewards.Count == 0) throw new System.InvalidOperationException("Special rooms need at least one item prefab.");
                var cell = new Vector2Int(address % map.Width, address / map.Width);
                if (!IsValidItemTile(cell)) throw new System.InvalidOperationException("A special room reward tile is blocked.");
                SpawnItem(rewards[Random.Range(0, rewards.Count)], new List<Vector2Int> { cell });
            }
            List<Vector2Int> emptyTiles = new List<Vector2Int>();

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    SchoolSpaceType space = map.GetSpaceType(cell);
                    if (IsValidItemTile(cell) && space != SchoolSpaceType.Storage && space != SchoolSpaceType.JanitorCloset
                        && (events == null || !events.HasEvent(cell)))
                    {
                        emptyTiles.Add(cell);
                    }
                }
            }

            SpawnItem(doubleDicePrefab, emptyTiles);
            SpawnItem(customDicePrefab, emptyTiles);
            HasSpawnedItems = true;
        }

        private bool IsValidItemTile(Vector2Int cell)
        {
            return map.IsWalkable(cell)
                && !map.IsExit(cell)
                && !map.IsOccupied(cell);
        }

        private void SpawnItem<T>(T prefab, List<Vector2Int> emptyTiles)
            where T : Item
        {
            if (prefab == null || emptyTiles.Count == 0)
            {
                return;
            }

            int index = Random.Range(0, emptyTiles.Count);
            Vector2Int cell = emptyTiles[index];

            T spawnedItem = Instantiate(
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

            emptyTiles.RemoveAt(index);

            Debug.Log(
                $"[Item System] Spawned {spawnedItem.ItemName} at {cell}."
            );
        }
    }
}
