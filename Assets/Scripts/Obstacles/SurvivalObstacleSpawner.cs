// Uses the area's assigned prefab lists to draw the outdoor props already chosen by the map generator.

using System;
using System.Collections.Generic;
using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Obstacles
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SurvivalObstacleSpawner : MonoBehaviour
    {
        [Header("Map")]
        [SerializeField] private SurvivalMap map;
        [Header("Area Obstacle Prefabs")]
        [SerializeField, Tooltip("Bleachers and sports equipment used by Track / Field placement rules. Same-kind variants are chosen using the match seed.")]
        private SurvivalPropVisual[] trackFieldPrefabs;
        [SerializeField] private SurvivalPropVisual[] poolPrefabs;
        [SerializeField] private SurvivalPropVisual[] courtsPrefabs;
        [SerializeField] private SurvivalPropVisual[] courtyardPrefabs;
        [SerializeField] private SurvivalPropVisual[] gardenPrefabs;
        [SerializeField] private SurvivalPropVisual[] maintenancePrefabs;
        [SerializeField] private SurvivalPropVisual[] parkingPrefabs;
        [Header("Area Fence Styles")]
        [SerializeField] private SurvivalPropVisual[] sportsFencePrefabs;
        [SerializeField] private SurvivalPropVisual[] serviceFencePrefabs;
        [Header("Paths and Boundaries")]
        [SerializeField, Tooltip("Trees, bushes, flowers, and planters outside named areas.")]
        private SurvivalPropVisual[] pathPrefabs;
        [SerializeField, Tooltip("Fence variants used for the campus perimeter and fenced areas.")]
        private SurvivalPropVisual[] boundaryPrefabs;
        [SerializeField, HideInInspector]
        private SurvivalPropVisual[] obstaclePrefabs;
        [SerializeField, HideInInspector] private bool areaSectionsInitialized;
        [Header("Obstacle Placement Rules")]
        [Tooltip("Counts, chances, footprints, spacing, and allowed areas. Every requested kind must also have a prefab in that area's section above.")]
        [SerializeField] private SurvivalObstacleSettings placement = new SurvivalObstacleSettings();
        private GameObject spawnedRoot;
        private readonly Dictionary<Vector2Int, SurvivalPropVisual> fences = new Dictionary<Vector2Int, SurvivalPropVisual>();
        private readonly Dictionary<Vector2Int, SurvivalPropVisual> spawnedProps = new Dictionary<Vector2Int, SurvivalPropVisual>();
        public bool TryGetSpawnedProp(Vector2Int cell, out SurvivalPropVisual prop) => spawnedProps.TryGetValue(cell, out prop);
        private bool previewDirty;
        public SurvivalMap Map => map;
        public bool HasSpawnedObstacles { get; private set; }
        public int SpawnedObstacleCount { get; private set; }
        public SurvivalObstacleSettings PlacementSettings => isActiveAndEnabled ? placement
            : new SurvivalObstacleSettings { Rules = Array.Empty<OutdoorPropRule>() };

        private void OnEnable() { InitializeAreaSections(); previewDirty = true; }
        private void OnValidate() { previewDirty = true; }
        private void Update()
        {
            if (Application.isPlaying || !previewDirty) return;
            previewDirty = false;
            InitializeAreaSections();
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
            areaSectionsInitialized = false;
            InitializeAreaSections();
        }

        //Old scenes migrate once; an intentionally cleared area list stays empty afterward.
        private void InitializeAreaSections()
        {
            if (areaSectionsInitialized || obstaclePrefabs == null || obstaclePrefabs.Length == 0) return;
            foreach (OutdoorAreaKind area in Enum.GetValues(typeof(OutdoorAreaKind)))
            {
                var section = new List<SurvivalPropVisual>(GetSection(area) ?? Array.Empty<SurvivalPropVisual>());
                foreach (OutdoorPropRule rule in placement.Rules)
                {
                    if (Array.IndexOf(rule.AllowedAreas, area) < 0) continue;
                    foreach (SurvivalPropVisual prefab in obstaclePrefabs)
                        if (prefab != null && prefab.Kind == rule.Prop && !section.Contains(prefab)) section.Add(prefab);
                }
                SetSection(area, section.ToArray());
            }
            var boundary = new List<SurvivalPropVisual>(boundaryPrefabs ?? Array.Empty<SurvivalPropVisual>());
            foreach (SurvivalPropVisual prefab in obstaclePrefabs)
                if (prefab != null && prefab.Kind == OutdoorPropKind.Fence && !boundary.Contains(prefab)) boundary.Add(prefab);
            boundaryPrefabs = boundary.ToArray();
            obstaclePrefabs = null;
            areaSectionsInitialized = true;
        }

        private SurvivalPropVisual[] GetSection(OutdoorAreaKind area)
        {
            switch (area)
            {
                case OutdoorAreaKind.TrackField: return trackFieldPrefabs;
                case OutdoorAreaKind.Pool: return poolPrefabs;
                case OutdoorAreaKind.Courts: return courtsPrefabs;
                case OutdoorAreaKind.Courtyard: return courtyardPrefabs;
                case OutdoorAreaKind.Garden: return gardenPrefabs;
                case OutdoorAreaKind.Maintenance: return maintenancePrefabs;
                case OutdoorAreaKind.Parking: return parkingPrefabs;
                default: return pathPrefabs;
            }
        }

        private void SetSection(OutdoorAreaKind area, SurvivalPropVisual[] prefabs)
        {
            switch (area)
            {
                case OutdoorAreaKind.TrackField: trackFieldPrefabs = prefabs; break;
                case OutdoorAreaKind.Pool: poolPrefabs = prefabs; break;
                case OutdoorAreaKind.Courts: courtsPrefabs = prefabs; break;
                case OutdoorAreaKind.Courtyard: courtyardPrefabs = prefabs; break;
                case OutdoorAreaKind.Garden: gardenPrefabs = prefabs; break;
                case OutdoorAreaKind.Maintenance: maintenancePrefabs = prefabs; break;
                case OutdoorAreaKind.Parking: parkingPrefabs = prefabs; break;
                default: pathPrefabs = prefabs; break;
            }
        }

        public IReadOnlyList<SurvivalPropVisual> GetAreaPrefabs(OutdoorAreaKind area)
        {
            InitializeAreaSections();
            return GetSection(area) ?? Array.Empty<SurvivalPropVisual>();
        }

        public IEnumerable<SurvivalPropVisual> GetAllPrefabs()
        {
            InitializeAreaSections();
            var seen = new HashSet<SurvivalPropVisual>();
            foreach (OutdoorAreaKind area in Enum.GetValues(typeof(OutdoorAreaKind)))
                foreach (SurvivalPropVisual prefab in GetAreaPrefabs(area))
                    if (prefab != null && seen.Add(prefab)) yield return prefab;
            foreach (var catalog in new[] { boundaryPrefabs, sportsFencePrefabs, serviceFencePrefabs })
                foreach (SurvivalPropVisual prefab in catalog ?? Array.Empty<SurvivalPropVisual>())
                    if (prefab != null && seen.Add(prefab)) yield return prefab;
        }

        private SurvivalPropVisual[] Choices(OutdoorPropKind kind, OutdoorAreaKind area)
        {
            InitializeAreaSections();
            var fenceChoices = area == OutdoorAreaKind.Courts ? sportsFencePrefabs : area == OutdoorAreaKind.Maintenance ? serviceFencePrefabs : null;
            if (fenceChoices == null || fenceChoices.Length == 0) fenceChoices = boundaryPrefabs;
            return Array.FindAll((kind == OutdoorPropKind.Fence ? fenceChoices : GetSection(area))
                ?? Array.Empty<SurvivalPropVisual>(), prefab => prefab != null && prefab.Kind == kind);
        }

        internal void ValidateForLayout(SurvivalMapLayout layout)
        {
            if (Choices(OutdoorPropKind.Fence, OutdoorAreaKind.None).Length == 0)
                throw new InvalidOperationException("Assign a Fence in Survival Obstacle Spawner > Boundary Prefabs.");
            foreach (OutdoorPropPlacement obstacle in layout.Props)
            {
                OutdoorAreaKind area = layout.GetArea(obstacle.X, obstacle.Y);
                if (Choices(obstacle.Kind, area).Length == 0)
                    throw new InvalidOperationException($"Assign {obstacle.Kind} in Survival Obstacle Spawner > {(area == OutdoorAreaKind.None ? "Path" : area.ToString())} Prefabs.");
            }
        }

        //The map first accepts a connected layout, then this component creates its reserved obstacles.
        //Draw the prop placements accepted by the generator instead of scattering a second set of obstacles.
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
                    Spawn(OutdoorPropKind.Fence, layout.GetArea(x, y), x, y, 1, 1, random, tileSize, material);
            foreach (OutdoorPropPlacement obstacle in layout.Props)
                Spawn(obstacle.Kind, layout.GetArea(obstacle.X, obstacle.Y), obstacle.X, obstacle.Y, obstacle.Width, obstacle.Height,
                    random, tileSize, material, obstacle.QuarterTurns);
            SpawnedObstacleCount = layout.Props.Count;
            HasSpawnedObstacles = true;
        }

        private void Spawn(OutdoorPropKind kind, OutdoorAreaKind area, int x, int y, int width, int height,
            System.Random random, Vector2 tileSize, Material material, int quarterTurns = -1)
        {
            var choices = Choices(kind, area);
            SurvivalPropVisual copy = Instantiate(choices[random.Next(choices.Length)], spawnedRoot.transform);
            copy.name = kind + " " + x + "," + y;
            copy.transform.position = map.GridToWorld(new Vector2(x + (width - 1) * .5f, y + (height - 1) * .5f));
            copy.Build(tileSize, new Vector2(width, height), material, 500 - (x + y) * 10, quarterTurns: quarterTurns);
            if (kind == OutdoorPropKind.Fence) fences[new Vector2Int(x, y)] = copy;
            else spawnedProps[new Vector2Int(x, y)] = copy;
        }

        // Remove the fence piece at the generated gate so its visible opening agrees with the escape location.
        // Remove the fence piece at the generated gate so its visible opening agrees with the escape location.
        public void ReplaceFenceWithExit(Vector2Int boundary)
        {
            if (fences.TryGetValue(boundary, out var fence) && fence != null)
                fence.gameObject.SetActive(false);
        }

        [ContextMenu("Regenerate Map and Obstacles")]
        public void Regenerate()
        {
            if (map == null) map = GetComponent<SurvivalMap>();
            if (map != null) map.Generate();
        }

        public void ClearSpawnedObstacles()
        {
            fences.Clear();
            spawnedProps.Clear();
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
