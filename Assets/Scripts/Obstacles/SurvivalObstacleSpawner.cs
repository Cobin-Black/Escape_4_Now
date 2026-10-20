using System;
using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Obstacles
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SurvivalObstacleSpawner : MonoBehaviour
    {
        [Header("Map")]
        [SerializeField] private SurvivalMap map;
        [Header("Obstacle Placement")]
        [SerializeField, Tooltip("Outdoor obstacle and decoration prefabs. Multiple prefabs of the same kind give seeded visual variation. Fence is also used for map boundaries.")]
        private SurvivalPropVisual[] obstaclePrefabs;
        [SerializeField] private SurvivalObstacleSettings placement = new SurvivalObstacleSettings();
        private GameObject spawnedRoot;
        private bool previewDirty;
        public SurvivalMap Map => map;
        public bool HasSpawnedObstacles { get; private set; }
        public int SpawnedObstacleCount { get; private set; }
        public SurvivalObstacleSettings PlacementSettings => isActiveAndEnabled ? placement
            : new SurvivalObstacleSettings { Rules = Array.Empty<OutdoorPropRule>() };

        private void OnEnable() { previewDirty = true; }
        private void OnValidate() { previewDirty = true; }
        private void Update()
        {
            if (Application.isPlaying || !previewDirty) return;
            previewDirty = false;
            if (map == null) map = GetComponent<SurvivalMap>();
            if (map != null) map.RequestPreviewRefresh();
        }

        internal void AssignMapIfMissing(SurvivalMap owner)
        {
            if (map == null) map = owner;
            if (map != owner) throw new InvalidOperationException("The Survival obstacle spawner must reference this map.");
        }

        //Move old Inspector values once; never refill or replace a teammate's catalog afterward.
        internal void MigrateFromMap(SurvivalMap owner, SurvivalPropVisual[] prefabs, SurvivalObstacleSettings settings)
        {
            AssignMapIfMissing(owner);
            obstaclePrefabs = prefabs ?? Array.Empty<SurvivalPropVisual>();
            if (settings != null) placement = settings;
        }

        internal void ValidateForLayout(SurvivalMapLayout layout)
        {
            if (obstaclePrefabs == null || !Array.Exists(obstaclePrefabs, prefab => prefab != null && prefab.Kind == OutdoorPropKind.Fence))
                throw new InvalidOperationException("Assign a Fence in Survival Obstacle Spawner > Obstacle Prefabs.");
            foreach (OutdoorPropPlacement obstacle in layout.Props)
                if (!Array.Exists(obstaclePrefabs, prefab => prefab != null && prefab.Kind == obstacle.Kind))
                    throw new InvalidOperationException("Assign " + obstacle.Kind + " in Survival Obstacle Spawner > Obstacle Prefabs.");
        }

        //The map first accepts a connected layout, then this component creates its reserved obstacles.
        internal void SpawnObstacles(SurvivalMapLayout layout, Transform parent, Vector2 tileSize, Material material)
        {
            ValidateForLayout(layout);
            ClearSpawnedObstacles();
            spawnedRoot = new GameObject("Spawned Outdoor Obstacles") { hideFlags = HideFlags.HideAndDontSave };
            spawnedRoot.transform.SetParent(parent, false);
            var random = new System.Random(layout.Seed);
            //Boundary and area fences belong to terrain, even when optional obstacle spawning is disabled.
            for (int y = 0; y < layout.Height; y++) for (int x = 0; x < layout.Width; x++)
                if (layout.GetSurface(x, y) == OutdoorSurface.Fence)
                    Spawn(OutdoorPropKind.Fence, x, y, 1, 1, random, tileSize, material);
            foreach (OutdoorPropPlacement obstacle in layout.Props)
                Spawn(obstacle.Kind, obstacle.X, obstacle.Y, obstacle.Width, obstacle.Height, random, tileSize, material, obstacle.QuarterTurns);
            SpawnedObstacleCount = layout.Props.Count;
            HasSpawnedObstacles = true;
        }

        private void Spawn(OutdoorPropKind kind, int x, int y, int width, int height,
            System.Random random, Vector2 tileSize, Material material, int quarterTurns = -1)
        {
            var choices = Array.FindAll(obstaclePrefabs, prefab => prefab != null && prefab.Kind == kind);
            SurvivalPropVisual copy = Instantiate(choices[random.Next(choices.Length)], spawnedRoot.transform);
            copy.name = kind + " " + x + "," + y;
            copy.transform.position = map.GridToWorld(new Vector2(x + (width - 1) * .5f, y + (height - 1) * .5f));
            copy.Build(tileSize, new Vector2(width, height), material, 500 - (x + y) * 10, quarterTurns: quarterTurns);
        }

        [ContextMenu("Regenerate Map and Obstacles")]
        public void Regenerate()
        {
            if (map == null) map = GetComponent<SurvivalMap>();
            if (map != null) map.Generate();
        }

        public void ClearSpawnedObstacles()
        {
            if (spawnedRoot != null)
            {
                if (Application.isPlaying) Destroy(spawnedRoot); else DestroyImmediate(spawnedRoot);
            }
            spawnedRoot = null;
            HasSpawnedObstacles = false;
            SpawnedObstacleCount = 0;
        }

        private void OnDisable()
        {
            if (map != null) map.RequestPreviewRefresh();
        }
    }
}
