// Connects the outdoor layout to its drawing, movement checks, spawn locations and temporary blocks.

using System;
using System.Collections.Generic;
using Escape4Now.Obstacles;
using UnityEngine;
using UnityEngine.Serialization;

namespace Escape4Now.Map
{
    [ExecuteAlways, RequireComponent(typeof(SurvivalObstacleSpawner))]
    public sealed class SurvivalMap : MonoBehaviour, IPlayerGridMap, IWeightedPlayerGridMap
    {
        [Serializable]
        private struct SurfaceStyle { public OutdoorSurface surface; public Color color; }
        [Header("Generation Rules")]
        [SerializeField] private SurvivalGenerationSettings generation = new SurvivalGenerationSettings();
        [Header("Seeds and Preview")]
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int fixedSeed = 42;
        [SerializeField] private bool showEditorPreview = true;
        [SerializeField] private int editorPreviewSeed = 42;
        [Header("Tile Rendering")]
        [SerializeField] private Material mapMaterial;
        [SerializeField] private Vector2 tileSize = new Vector2(1.2f, .6f);
        [SerializeField, Min(.01f)] private float tileDepth = .12f;
        [SerializeField, Range(.5f, 1f)] private float alternatingTileBrightness = .94f;
        [Header("Campus Presentation")]
        [SerializeField] private SurvivalCampusVisuals campusVisuals;
        [SerializeField] private SurfaceStyle[] surfaces;
        [Header("System References")]
        [SerializeField] private SurvivalObstacleSpawner obstacleSpawner;
        [SerializeField] private SurvivalEventController eventController;
        public int TraversalCost(Vector2Int cell) => eventController != null ? eventController.MovementCost(cell) : 1;
        [SerializeField, HideInInspector, FormerlySerializedAs("obstaclePrefabs")]
        private SurvivalPropVisual[] previousObstaclePrefabs;
        [SerializeField, Range(0, 31)] private int blockedTileLayer;
        [Header("Sports Markings")]
        [SerializeField] private Color markingColor = new Color(.93f, .96f, .9f, 1f);
        [SerializeField, Min(.01f)] private float markingWidth = .035f;
        [SerializeField, Min(0f)] private float sportsMarkingInset = .5f;
        [SerializeField, Range(8, 128)] private int curveSegments = 48;
        [SerializeField, Range(1, 5)] private int trackLanes = 2;
        [Header("Field and Court Markings")]
        [SerializeField, Range(.3f, .7f), Tooltip("Scale of the field rectangle within the inner track ellipse.")]
        private float fieldMarkingScale = .68f;
        [SerializeField, Range(.2f, 1.5f)] private float courtMarkingInset = .65f;
        [SerializeField, Range(.05f, .3f)] private float centerCircleRadiusRatio = .14f;
        [SerializeField, Range(.1f, .4f)] private float basketballKeyDepthRatio = .22f;
        [SerializeField, Range(.2f, .6f)] private float basketballKeyWidthRatio = .45f;
        [SerializeField, Range(.1f, .4f)] private float tennisServiceDepthRatio = .3f;
        [SerializeField, Range(.2f, 1f)] private float tennisSinglesInset = .5f;
        [Header("Pool and Parking Markings")]
        [SerializeField, Range(1, 6)] private int poolLaneCount = 3;
        [SerializeField] private Color poolLaneColor = new Color(.9f, .96f, 1f, .9f);
        [SerializeField, Range(.01f, .3f)] private float parkingMarkingInset = .08f;
        [Header("Spawn Preview")]
        [SerializeField, Tooltip("Small reservations only; these are not player characters or a monster system.")]
        private bool showSpawnMarkers = true;
        [SerializeField, Range(.1f, .8f)] private float spawnMarkerSize = .35f;
        [SerializeField, Min(.005f)] private float spawnMarkerHeight = .035f;
        [SerializeField] private Color[] spawnColors;
        [Header("Camera Framing")]
        [SerializeField] private Camera mapCamera;
        [SerializeField] private bool fitCamera = true;
        [SerializeField, Min(0f)] private float cameraPadding = 1.2f;
        [SerializeField] private Vector2 cameraOffset = new Vector2(0f, .5f);
        [SerializeField] private Color backgroundColor = new Color(.055f, .11f, .14f, 1f);
        private GameObject generatedRoot;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private bool previewDirty;
        private int screenWidth, screenHeight;
        private readonly HashSet<Vector2Int> occupants = new HashSet<Vector2Int>();
        public SurvivalMapLayout Layout { get; private set; }
        public int CurrentSeed => Layout?.Seed ?? 0;
        public int LayoutSeed => CurrentSeed;
        public int Width => Layout?.Width ?? generation.Width;
        public int Height => Layout?.Height ?? generation.Height;
        public float TileHeight => tileSize.y;
        public Vector2 TileSize => tileSize;
        public float TrackWidth => generation.TrackWidth;
        public Material MapMaterial => mapMaterial;
        public SurvivalObjectiveLayout Objectives { get; private set; }
        private readonly HashSet<Vector2Int> temporaryBlocks = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> forcedMovementHazards = new HashSet<Vector2Int>();
        public void SetForcedMovementHazards(IEnumerable<Vector2Int> cells)
        { forcedMovementHazards.Clear(); foreach(var cell in cells) forcedMovementHazards.Add(cell); }
        public void SetObjectives(SurvivalObjectiveLayout layout) { Objectives = layout; temporaryBlocks.Clear(); forcedMovementHazards.Clear(); }
        // Test the block against current objectives and hazards before keeping it; reject it if any required route is lost.
        // Test the block against current objectives and hazards before keeping it; reject it if any required route is lost.
        public bool TryBlockTemporarily(Vector2Int cell)
        {
            if (Objectives == null || Objectives.IsReserved(cell) || IsOccupied(cell) || !IsWalkable(cell)) return false;
            temporaryBlocks.Add(cell);
            var unsafeCells = new HashSet<Vector2Int>(temporaryBlocks); unsafeCells.UnionWith(forcedMovementHazards);
            if (Objectives.ValidateConnectivity(unsafeCells)) return true;
            temporaryBlocks.Remove(cell); return false;
        }
        public void RemoveTemporaryBlock(Vector2Int cell) { temporaryBlocks.Remove(cell); }
        public string LastGenerationError { get; private set; }
        public void RequestPreviewRefresh() { previewDirty = true; }

        private void OnEnable() { previewDirty = true; }
        private void Start() { if (Application.isPlaying && generateOnStart && Layout == null) Generate(); }
        private void OnValidate()
        {
            tileSize.x = Mathf.Max(.1f, tileSize.x); tileSize.y = Mathf.Max(.1f, tileSize.y);
            curveSegments = Mathf.Clamp(curveSegments, 8, 128);
            trackLanes = Mathf.Clamp(trackLanes, 1, 5);
            fieldMarkingScale = Mathf.Clamp(fieldMarkingScale, .3f, .7f);
            poolLaneCount = Mathf.Clamp(poolLaneCount, 1, 6);
            previewDirty = true;
        }
        private void Update()
        {
            if (!Application.isPlaying && previewDirty)
            {
                previewDirty = false;
                if (showEditorPreview) GenerateWithSeed(useFixedSeed ? fixedSeed : editorPreviewSeed);
                else ClearPreview();
            }
            if (Layout != null && fitCamera && (screenWidth != Screen.width || screenHeight != Screen.height)) FitCamera();
        }

        [ContextMenu("Generate Map")]
        public void Generate() => GenerateWithSeed(useFixedSeed ? fixedSeed : Guid.NewGuid().GetHashCode());

        // Choose the match layout separately from the edit-mode preview so a preview does not consume the match's seed choice.
        // Choose the match layout separately from the edit-mode preview so a preview does not consume the match's seed choice.
        public void PrepareLayout()
        {
            //An editor preview must not choose the seed for a new match.
            if (!GenerateWithSeed(useFixedSeed ? fixedSeed : Guid.NewGuid().GetHashCode()))
                throw new InvalidOperationException(LastGenerationError);
        }

        // Generate and validate the data before drawing it; do not rebuild an active match underneath its players.
        // Generate and validate the data before drawing it; do not rebuild an active match underneath its players.
        public bool GenerateWithSeed(int seed)
        {
            if (Application.isPlaying && Escape4Now.Multiplayer.LocalMultiplayerSession.MatchIsRunning)
            {
                LastGenerationError = "Leave the match before regenerating its map.";
                return false;
            }
            try
            {
                if (mapMaterial == null) throw new InvalidOperationException("Assign the Survival map material.");
                PrepareObstacleSpawner();
                var next = new SurvivalMapLayout(seed, generation, obstacleSpawner.PlacementSettings);
                ValidateVisuals(next);
                ClearPreview(); occupants.Clear(); Layout = next; Objectives = null; temporaryBlocks.Clear();
                generatedRoot = new GameObject("Generated Survival Map") { hideFlags = HideFlags.HideAndDontSave };
                generatedRoot.transform.SetParent(transform, false);
                DrawMap(); FitCamera(); LastGenerationError = null;
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            {
                LastGenerationError = error.Message;
                Debug.LogError("Survival map: " + error.Message, this);
                return false;
            }
        }

        private void ValidateVisuals(SurvivalMapLayout layout)
        {
            foreach (OutdoorSurface type in Enum.GetValues(typeof(OutdoorSurface)))
                if (surfaces == null || !Array.Exists(surfaces, entry => entry.surface == type))
                    throw new InvalidOperationException("Assign a surface color for " + type + ".");
            obstacleSpawner.ValidateForLayout(layout);
        }

        private void PrepareObstacleSpawner()
        {
            if (obstacleSpawner == null) obstacleSpawner = GetComponent<SurvivalObstacleSpawner>();
            //Existing open scenes may still contain the old map-owned catalog after a script reload.
            if (obstacleSpawner == null && previousObstaclePrefabs != null && previousObstaclePrefabs.Length > 0)
            {
#if UNITY_EDITOR
                if (!Application.IsPlaying(gameObject)) obstacleSpawner = UnityEditor.Undo.AddComponent<SurvivalObstacleSpawner>(gameObject);
                else
#endif
                    obstacleSpawner = gameObject.AddComponent<SurvivalObstacleSpawner>();
            }
            if (obstacleSpawner == null) throw new InvalidOperationException("Assign a Survival Obstacle Spawner.");
            obstacleSpawner.AssignMapIfMissing(this);
            //Unity serializes missing array fields as empty after a domain reload.
            if (previousObstaclePrefabs == null || previousObstaclePrefabs.Length == 0) return;
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject)) UnityEditor.Undo.RecordObjects(new UnityEngine.Object[] { this, obstacleSpawner }, "Migrate Survival obstacle settings");
#endif
            generation.TryTakePreviousObstacles(out SurvivalObstacleSettings settings);
            obstacleSpawner.MigrateFromMap(this, previousObstaclePrefabs, settings);
            previousObstaclePrefabs = null;
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.EditorUtility.SetDirty(obstacleSpawner);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        // Tasks and temporary blocks can make a tile unavailable even when its underlying ground is walkable.
        // Tasks and temporary blocks can make a tile unavailable even when its underlying ground is walkable.
        public bool IsWalkable(Vector2Int cell) => Layout != null && Layout.IsWalkable(cell.x, cell.y)
            && (Objectives == null || !Objectives.IsTaskObject(cell)) && !temporaryBlocks.Contains(cell);
        public bool IsExit(Vector2Int cell)
        { if (Objectives != null) foreach (var exit in Objectives.Exits) if (exit == cell) return true; return false; }
        private System.Func<Vector2Int, bool> monsterOccupancy;
        public void SetMonsterOccupancy(System.Func<Vector2Int, bool> occupancy) { monsterOccupancy = occupancy; }
        public bool IsOccupied(Vector2Int cell) => occupants.Contains(cell) || monsterOccupancy != null && monsterOccupancy(cell);
        public void SetOccupantPosition(Vector2Int previous, Vector2Int next)
        { occupants.Remove(previous); occupants.Add(next); }
        public void RemoveOccupant(Vector2Int cell) => occupants.Remove(cell);
        public void ReplacePlayerOccupants(IReadOnlyList<Vector2Int> cells)
        { occupants.Clear(); foreach (Vector2Int cell in cells) occupants.Add(cell); }
        public bool TryFindPath(Vector2Int start, Vector2Int end, out List<Vector2Int> path)
            => GridPathfinding.TryFindPath(this, start, end, out path);
        public Vector3 GridToWorld(Vector2Int cell) => GridToWorld((Vector2)cell);
        public Vector3 GridToWorld(Vector2 cell) => transform.TransformPoint(new Vector3((cell.x - cell.y) * tileSize.x * .5f,
            (cell.x + cell.y) * tileSize.y * .5f, 0f));
        public Vector2Int WorldToGrid(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            return new Vector2Int(Mathf.RoundToInt(local.y / tileSize.y + local.x / tileSize.x),
                Mathf.RoundToInt(local.y / tileSize.y - local.x / tileSize.x));
        }
        public bool TryGetPlayerSpawn(int playerIndex, out Vector2Int cell)
        {
            cell = default;
            if (Layout == null || playerIndex < 0 || playerIndex >= Layout.PlayerSpawns.Count) return false;
            int index = Layout.PlayerSpawns[playerIndex]; cell = new Vector2Int(index % Layout.Width, index / Layout.Width);
            return true;
        }
        public IEnumerable<Vector2Int> GetSpawnCandidates(int minimumDistanceFromPlayers = 0)
        {
            if (Layout == null) yield break;
            foreach (int index in Layout.SpawnCandidates)
            {
                var cell = new Vector2Int(index % Layout.Width, index / Layout.Width);
                bool valid = !occupants.Contains(cell);
                foreach (int spawn in Layout.PlayerSpawns)
                    if (index == spawn || Mathf.Abs(cell.x - spawn % Layout.Width) + Mathf.Abs(cell.y - spawn / Layout.Width) < minimumDistanceFromPlayers) { valid = false; break; }
                foreach (Vector2Int player in occupants)
                    if (Mathf.Abs(cell.x - player.x) + Mathf.Abs(cell.y - player.y) < minimumDistanceFromPlayers) { valid = false; break; }
                if (valid) yield return cell;
            }
        }

        private void DrawMap()
        {
            for (int y = 0; y < Layout.Height; y++) for (int x = 0; x < Layout.Width; x++)
            {
                OutdoorSurface surface = Layout.GetSurface(x, y);
                Color color = Array.Find(surfaces, style => style.surface == surface).color;
                if (campusVisuals != null && campusVisuals.enabled && campusVisuals.Settings != null)
                    color = campusVisuals.GroundColor(Layout, x, y, color);
                else if ((x + y) % 2 == 0) { color *= alternatingTileBrightness; color.a = 1f; }
                Renderer floor = AddBlock(generatedRoot.transform, "Tile " + x + "," + y, Vector2.zero, 1f, 1f,
                    -tileDepth, tileDepth, color, -10000 - (x + y));
                floor.transform.position = GridToWorld(new Vector2(x, y));
                var surfaceMaterial = campusVisuals != null && campusVisuals.enabled ? campusVisuals.GroundMaterial(surface) : null;
                if (surfaceMaterial != null) floor.sharedMaterial = surfaceMaterial;
                floor.gameObject.AddComponent<IsometricMapTile>().SetGridPosition(new Vector2Int(x, y));
                if (!Layout.IsWalkable(x, y))
                {
                    floor.gameObject.layer = blockedTileLayer;
                    floor.gameObject.AddComponent<PolygonCollider2D>().points = new[] { new Vector2(0f, tileSize.y * .5f),
                        new Vector2(tileSize.x * .5f, 0f), new Vector2(0f, -tileSize.y * .5f), new Vector2(-tileSize.x * .5f, 0f) };
                }
            }
            obstacleSpawner.SpawnObstacles(Layout, generatedRoot.transform, tileSize, mapMaterial);
            foreach (OutdoorArea area in Layout.Areas) DrawMarkings(area);
            if (campusVisuals != null) campusVisuals.Build(this, generatedRoot.transform);
            foreach (OutdoorParkingBay bay in Layout.ParkingBays)
                DrawRectangle(bay.X - .5f + parkingMarkingInset, bay.Y - .5f + parkingMarkingInset,
                    bay.X + bay.Width - .5f - parkingMarkingInset, bay.Y + bay.Height - .5f - parkingMarkingInset);
            if (!Application.isPlaying && showSpawnMarkers && spawnColors != null && spawnColors.Length > 0)
                for (int i = 0; i < Layout.PlayerSpawns.Count; i++)
                {
                    int cell = Layout.PlayerSpawns[i];
                    Renderer marker = AddBlock(generatedRoot.transform, "Player spawn " + (i + 1), Vector2.zero,
                        spawnMarkerSize, spawnMarkerSize, 0f, spawnMarkerHeight, spawnColors[i % spawnColors.Length], 500 - (cell % Layout.Width + cell / Layout.Width) * 10);
                    marker.transform.position = GridToWorld(new Vector2(cell % Layout.Width, cell / Layout.Width));
                }
        }

        private Renderer AddBlock(Transform parent, string label, Vector2 offset, float width, float depth,
            float bottom, float height, Color color, int order)
        {
            Renderer result = IsometricMapTemplate.CreateBlock(parent, label, offset, width, depth, bottom, height,
                color, order, tileSize.x, tileSize.y, mapMaterial, HideFlags.HideAndDontSave);
            meshes.Add(result.GetComponent<MeshFilter>().sharedMesh);
            return result;
        }

        private void DrawMarkings(OutdoorArea area)
        {
            if (area.Kind != OutdoorAreaKind.TrackField && area.Kind != OutdoorAreaKind.Courts && area.Kind != OutdoorAreaKind.Pool) return;
            float cx = area.X + (area.Width - 1) * .5f, cy = area.Y + (area.Height - 1) * .5f;
            if (area.Kind == OutdoorAreaKind.TrackField)
            {
                for (int lane = 0; lane <= trackLanes; lane++)
                {
                    float inset = generation.TrackWidth * lane / (float)trackLanes;
                    DrawEllipse(new Vector2(cx, cy), new Vector2(area.Width * .5f - inset, area.Height * .5f - inset));
                }
                float rx = (area.Width * .5f - generation.TrackWidth) * fieldMarkingScale;
                float ry = (area.Height * .5f - generation.TrackWidth) * fieldMarkingScale;
                DrawRectangle(cx - rx, cy - ry, cx + rx, cy + ry);
                bool longX = area.Width >= area.Height;
                AddLine(longX ? new[] { new Vector2(cx, cy - ry), new Vector2(cx, cy + ry) }
                    : new[] { new Vector2(cx - rx, cy), new Vector2(cx + rx, cy) }, false);
                float radius = Mathf.Min(rx, ry) * centerCircleRadiusRatio * 2f;
                DrawEllipse(new Vector2(cx, cy), Vector2.one * radius);
                return;
            }
            if (area.Kind == OutdoorAreaKind.Pool)
            {
                float left = area.WaterX - .5f, bottom = area.WaterY - .5f;
                DrawRectangle(left, bottom, left + area.WaterWidth, bottom + area.WaterHeight);
                bool longX = area.WaterWidth >= area.WaterHeight;
                for (int lane = 1; lane < poolLaneCount; lane++)
                {
                    float t = lane / (float)poolLaneCount;
                    AddLine(longX ? new[] { new Vector2(left, bottom + area.WaterHeight * t), new Vector2(left + area.WaterWidth, bottom + area.WaterHeight * t) }
                        : new[] { new Vector2(left + area.WaterWidth * t, bottom), new Vector2(left + area.WaterWidth * t, bottom + area.WaterHeight) }, false, poolLaneColor);
                }
                return;
            }
            DrawCourtMarkings(area);
        }

        private void DrawCourtMarkings(OutdoorArea area)
        {
            float inset = Mathf.Max(sportsMarkingInset, courtMarkingInset);
            float left = area.X - .5f + inset, bottom = area.Y - .5f + inset;
            float width = area.Width - inset * 2f, height = area.Height - inset * 2f;
            if (width <= 0f || height <= 0f) return;
            bool longX = width >= height;
            float length = longX ? width : height, shortSide = longX ? height : width;
            Vector2 Point(float u, float v) => new Vector2(left + (longX ? u : v) * width, bottom + (longX ? v : u) * height);
            void Segment(float u1, float v1, float u2, float v2) => AddLine(new[] { Point(u1, v1), Point(u2, v2) }, false);
            DrawRectangle(left, bottom, left + width, bottom + height);
            Segment(.5f, 0f, .5f, 1f);
            if (area.CourtSport == OutdoorCourtSport.Tennis)
            {
                float singles = Mathf.Min(.3f, tennisSinglesInset / shortSide);
                Segment(0f, singles, 1f, singles); Segment(0f, 1f - singles, 1f, 1f - singles);
                Segment(tennisServiceDepthRatio, singles, tennisServiceDepthRatio, 1f - singles);
                Segment(1f - tennisServiceDepthRatio, singles, 1f - tennisServiceDepthRatio, 1f - singles);
                Segment(tennisServiceDepthRatio, .5f, 1f - tennisServiceDepthRatio, .5f);
                return;
            }
            float radius = shortSide * centerCircleRadiusRatio;
            DrawEllipse(Point(.5f, .5f), Vector2.one * radius);
            float low = .5f - basketballKeyWidthRatio * .5f, high = 1f - low;
            foreach (bool far in new[] { false, true })
            {
                float baseline = far ? 1f : 0f, key = far ? 1f - basketballKeyDepthRatio : basketballKeyDepthRatio;
                AddLine(new[] { Point(baseline, low), Point(key, low), Point(key, high), Point(baseline, high) }, false);
                var arc = new List<Vector2>();
                for (int i = 0; i <= curveSegments / 2; i++)
                {
                    float angle = -Mathf.PI * .5f + i * Mathf.PI / (curveSegments / 2);
                    arc.Add(Point(key + Mathf.Cos(angle) * radius / length * (far ? -1f : 1f), .5f + Mathf.Sin(angle) * radius / shortSide));
                }
                AddLine(arc, false);
            }
        }

        private void DrawRectangle(float left, float bottom, float right, float top)
            => AddLine(new[] { new Vector2(left, bottom), new Vector2(right, bottom), new Vector2(right, top), new Vector2(left, top) }, true);

        private void DrawEllipse(Vector2 center, Vector2 radius)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < curveSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / curveSegments;
                points.Add(center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y));
            }
            AddLine(points, true);
        }

        private void AddLine(IReadOnlyList<Vector2> points, bool loop, Color? color = null)
        {
            var lineObject = new GameObject("Sports marking") { hideFlags = HideFlags.HideAndDontSave };
            lineObject.transform.SetParent(generatedRoot.transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = mapMaterial; line.startColor = line.endColor = color ?? markingColor;
            line.startWidth = line.endWidth = markingWidth; line.loop = loop;
            line.sortingOrder = -9000; line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) line.SetPosition(i, GridToWorld(points[i]));
        }

        private void FitCamera()
        {
            if (!fitCamera || Layout == null || mapCamera == null || !mapCamera.orthographic) return;
            Vector3 center = GridToWorld(new Vector2((Layout.Width - 1) * .5f, (Layout.Height - 1) * .5f));
            mapCamera.transform.position = new Vector3(center.x + cameraOffset.x, center.y + cameraOffset.y, mapCamera.transform.position.z);
            mapCamera.orthographicSize = Mathf.Max((Layout.Width + Layout.Height) * tileSize.y * .25f,
                (Layout.Width + Layout.Height) * tileSize.x * .25f / Mathf.Max(.1f, mapCamera.aspect)) + cameraPadding;
            mapCamera.backgroundColor = backgroundColor;
            screenWidth = Screen.width; screenHeight = Screen.height;
        }

        [ContextMenu("Clear Generated Preview")]
        public void ClearPreview()
        {
            if (campusVisuals != null) campusVisuals.Clear();
            if (obstacleSpawner != null) obstacleSpawner.ClearSpawnedObstacles();
            if (generatedRoot != null) Release(generatedRoot);
            foreach (Mesh mesh in meshes) if (mesh != null) Release(mesh);
            meshes.Clear(); generatedRoot = null; Layout = null;
        }
        private void OnDisable() { ClearPreview(); }
        private static void Release(UnityEngine.Object target)
        { if (Application.isPlaying) Destroy(target); else DestroyImmediate(target); }
    }
}
