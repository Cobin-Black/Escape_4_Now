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

            List<Vector2Int> emptyTiles = new List<Vector2Int>();

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    if (IsValidItemTile(cell))
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