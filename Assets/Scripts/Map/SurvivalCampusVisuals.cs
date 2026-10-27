// Adds campus decoration over the existing map data without changing movement or objective placement.

using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Map
{
    //Presentation only. This layer never places a collider or changes the authoritative grid.
    [DisallowMultipleComponent]
    public sealed class SurvivalCampusVisuals : MonoBehaviour
    {
        [Header("Designer Settings")]
        [SerializeField] private SurvivalCampusVisualSettings settings;
        private SurvivalMap map;
        private GameObject root, wetRoot;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private bool wet;
        public SurvivalCampusVisualSettings Settings => settings;
        //Choose the area's palette and subtle variation without changing the cell's gameplay surface.
        public Color GroundColor(SurvivalMapLayout layout, int x, int y, Color fallback)
        {
            if (!enabled || settings == null) return fallback;
            var surface = layout.GetSurface(x, y);
            //The continuous track mesh supplies the terracotta surface; its underlying square cells stay grass.
            if (surface == OutdoorSurface.Track && settings.smoothTrackSurface) surface = OutdoorSurface.Grass;
            var style = System.Array.Find(settings.surfaces, item => item.surface == surface);
            if (style == null) return fallback;
            float seed = (uint)layout.Seed % 10000;
            float noise = Mathf.PerlinNoise((x + seed) * settings.noiseScale, (y + seed) * settings.noiseScale);
            float blend = style.pattern == CampusSurfacePattern.Stripes ? (x / Mathf.Max(1, settings.mowingStripeWidth)) % 2
                : style.pattern == CampusSurfacePattern.Stone ? ((x / 2 + y / 2) % 3) * .25f
                : style.pattern == CampusSurfacePattern.Solid ? 0 : noise;
            Color color = Color.Lerp(style.primary, style.secondary, blend);
            if (layout.GetSurface(x, y) == OutdoorSurface.Grass && layout.GetArea(x, y) == OutdoorAreaKind.Garden)
                color = Color.Lerp(color, settings.gardenGrass, .65f);
            if (layout.IsMainPath(x, y)) color = settings.mainPath;
            color *= 1 + (noise - .5f) * settings.naturalVariation * 2;
            color.a = 1;
            return color;
        }
        public Material GroundMaterial(OutdoorSurface surface)
            => settings == null ? null : System.Array.Find(settings.surfaces, style => style.surface == surface)?.material;

        //Add drawings over the accepted map data; decoration must not create a second layout.
        public void Build(SurvivalMap source, Transform parent)
        {
            map = source;
            if (!enabled || settings == null) return;
            Clear();
            root = new GameObject("Campus surface details") { hideFlags = HideFlags.HideAndDontSave };
            root.transform.SetParent(parent, false);
            wetRoot = new GameObject("Rain on pavement") { hideFlags = HideFlags.HideAndDontSave };
            wetRoot.transform.SetParent(root.transform, false);
            var random = new System.Random(unchecked(map.LayoutSeed ^ 0x341D));
            for (int y = 1; y < map.Height - 1; y++) for (int x = 1; x < map.Width - 1; x++)
            {
                var surface = map.Layout.GetSurface(x, y);
                bool pavement = surface == OutdoorSurface.Path || surface == OutdoorSurface.Paving || surface == OutdoorSurface.Deck || surface == OutdoorSurface.Asphalt || surface == OutdoorSurface.Service;
                if (pavement)
                {
                    Color rain = settings.wetPavementColor; rain.a = settings.wetPavementOpacity;
                    Quad(wetRoot.transform, "Wet pavement", x - .5f, y - .5f, 1, 1, rain);
                    if (random.NextDouble() < settings.puddleChance)
                        Disc(wetRoot.transform, new Vector2(x, y), settings.puddleRadius, settings.puddleGlint);
                }
                if (!settings.showGroundDetails) continue;
                if (map.Layout.IsMainPath(x, y))
                {
                    foreach (var step in SurvivalObjectiveLayout.Steps)
                        if (!map.Layout.IsMainPath(x + step.x, y + step.y))
                        {
                            float width = step.x == 0 ? 1 : settings.pathBorderWidth;
                            float depth = step.y == 0 ? 1 : settings.pathBorderWidth;
                            Quad(root.transform, "Path edging", x - .5f + (step.x > 0 ? 1 - width : 0), y - .5f + (step.y > 0 ? 1 - depth : 0), width, depth, settings.pathBorder);
                        }
                }
                if (pavement && surface != OutdoorSurface.Asphalt && random.NextDouble() < settings.detailDensity)
                {
                    Quad(root.transform, "Paving joint", x - .5f, y - .5f, 1, settings.pavingJointWidth, settings.pavingJoint);
                    if (x % 2 == 0) Quad(root.transform, "Paving joint", x - .5f, y - .5f, settings.pavingJointWidth, 1, settings.pavingJoint);
                }
            }
            if (settings.showGroundDetails)
            {
                foreach (var area in map.Layout.Areas) AreaDetails(area);
                foreach (var prop in map.Layout.Props)
                {
                    var center = new Vector2(prop.X + (prop.Width - 1) * .5f, prop.Y + (prop.Height - 1) * .5f);
                    Disc(root.transform, center, settings.propShadowRadius, settings.propShadow);
                    if (prop.Kind == OutdoorPropKind.Tree || prop.Kind == OutdoorPropKind.Bush)
                    {
                        Disc(root.transform, center, settings.plantingBedRadius, settings.gardenSoil);
                        if (settings.flowerDetailPrefab != null && prop.BlocksMovement)
                        {
                            int count = random.Next(Mathf.Max(0, settings.flowersPerPlantingBed.x), Mathf.Max(settings.flowersPerPlantingBed.x, settings.flowersPerPlantingBed.y) + 1);
                            for (int i = 0; i < count; i++)
                            {
                                var flower = Instantiate(settings.flowerDetailPrefab, root.transform);
                                flower.name = "Planting bed flowers";
                                var offset = new Vector2((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f) * .55f;
                                flower.transform.position = map.GridToWorld(center + offset);
                                flower.Build(map.TileSize, Vector2.one * settings.flowerDetailScale, map.MapMaterial, 502 - (prop.X + prop.Y) * 10);
                            }
                        }
                    }
                    if (prop.Kind == OutdoorPropKind.Bench && map.Layout.GetArea(prop.X, prop.Y) == OutdoorAreaKind.Courtyard)
                        Quad(root.transform, "Seating pad", prop.X - .4f, prop.Y - .4f, prop.Width - .2f, prop.Height - .2f, settings.courtyardAccent);
                }
                foreach (var bay in map.Layout.ParkingBays)
                    Quad(root.transform, "Parking wheel stop", bay.X - .45f, bay.Y - .45f, bay.Width - .1f, settings.pathBorderWidth, settings.parkingCurbs);
            }
            wetRoot.SetActive(false);
        }
        private void AreaDetails(OutdoorArea area)
        {
            float cx = area.X + (area.Width - 1) * .5f, cy = area.Y + (area.Height - 1) * .5f;
            if (area.Kind == OutdoorAreaKind.Garden && settings.showGardenTrails)
                foreach (int entrance in area.Entrances)
                {
                    var start = new Vector2Int(entrance % map.Width, entrance / map.Width);
                    var end = new Vector2Int(area.AnchorCell % map.Width, area.AnchorCell / map.Width);
                    if (!map.TryFindPath(start, end, out var trail) || trail.Count < 2) continue;
                    if (trail.Exists(cell => !area.Contains(cell.x, cell.y))) continue;
                    var item = new GameObject("Garden walking trail") { hideFlags = HideFlags.HideAndDontSave };
                    item.transform.SetParent(root.transform, false);
                    var line = item.AddComponent<LineRenderer>(); line.sharedMaterial = map.MapMaterial;
                    line.startColor = line.endColor = settings.gardenTrail;
                    line.startWidth = line.endWidth = settings.gardenTrailWidth;
                    line.numCornerVertices = line.numCapVertices = 4;
                    line.sortingOrder = -9500; line.positionCount = trail.Count;
                    for (int i = 0; i < trail.Count; i++) line.SetPosition(i, map.GridToWorld(trail[i]));
                }
            if (area.Kind == OutdoorAreaKind.Pool)
            {
                float w = settings.poolCopingWidth;
                float x = area.WaterX - .5f, y = area.WaterY - .5f;
                Quad(root.transform, "Pool coping", x - w, y - w, area.WaterWidth + w * 2, w, settings.poolCoping);
                Quad(root.transform, "Pool coping", x - w, y + area.WaterHeight, area.WaterWidth + w * 2, w, settings.poolCoping);
                Quad(root.transform, "Pool coping", x - w, y, w, area.WaterHeight, settings.poolCoping);
                Quad(root.transform, "Pool coping", x + area.WaterWidth, y, w, area.WaterHeight, settings.poolCoping);
                for (float i = 0; i < area.WaterWidth; i += Mathf.Max(.05f, settings.drainSpacing))
                    Quad(root.transform, "Pool drain", x + i, y - w * 2, settings.drainSize.x, settings.drainSize.y, settings.drainage);
            }
            if (area.Kind == OutdoorAreaKind.Courtyard)
            {
                float radius = Mathf.Min(area.Width, area.Height) * settings.courtyardPatternRadius;
                Disc(root.transform, new Vector2(cx, cy), radius, settings.courtyardAccent);
                Disc(root.transform, new Vector2(cx, cy), radius - settings.pathBorderWidth, settings.mainPath);
            }
            if (area.Kind == OutdoorAreaKind.TrackField)
            {
                if (settings.smoothTrackSurface)
                {
                    var style = System.Array.Find(settings.surfaces, item => item.surface == OutdoorSurface.Track);
                    if (style != null) TrackRing(new Vector2(cx, cy), new Vector2(area.Width, area.Height) * .5f, style.primary);
                }
                //Paint follows the existing ellipse; no track terrain is replaced.
                float x = cx, y = area.Y - .25f;
                for (int row = 0; row < 2; row++) for (int column = 0; column < settings.finishMarkColumns; column++)
                    if ((row + column) % 2 == 0) Quad(root.transform, "Track finish marking", x + column * settings.finishMarkSize, y + row * settings.finishMarkSize, settings.finishMarkSize, settings.finishMarkSize, settings.trackStart);
            }
            if (area.Kind == OutdoorAreaKind.Maintenance)
                for (int i = 0; i < area.Width - 2; i++)
                    Quad(root.transform, "Service safety stripe", area.X + .5f + i, area.Y + .45f, settings.serviceStripeSize.x, settings.serviceStripeSize.y, settings.serviceWarning);
        }
        private void Disc(Transform parent, Vector2 center, float radius, Color color)
        {
            var points = new Vector2[settings.curveSegments + 1]; points[0] = center;
            for (int i = 0; i < settings.curveSegments; i++)
                points[i + 1] = center + new Vector2(Mathf.Cos(i * Mathf.PI * 2 / settings.curveSegments), Mathf.Sin(i * Mathf.PI * 2 / settings.curveSegments)) * radius;
            var triangles = new int[settings.curveSegments * 3];
            for (int i = 0; i < settings.curveSegments; i++) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % settings.curveSegments + 1; }
            Shape(parent, "Ground detail", points, triangles, color);
        }
        //Draw one smooth oval over the track cells so their square outlines do not define the track's shape.
        private void TrackRing(Vector2 center, Vector2 outer, Color color)
        {
            int count = Mathf.Max(settings.curveSegments, 16) * 2;
            var points = new Vector2[count * 2]; var triangles = new int[count * 6];
            var inner = outer - Vector2.one * map.TrackWidth;
            for (int i = 0; i < count; i++)
            {
                var direction = new Vector2(Mathf.Cos(i * Mathf.PI * 2 / count), Mathf.Sin(i * Mathf.PI * 2 / count));
                points[i * 2] = center + direction * outer; points[i * 2 + 1] = center + direction * inner;
                int next = (i + 1) % count;
                int k = i * 6;
                triangles[k] = i * 2; triangles[k + 1] = next * 2; triangles[k + 2] = i * 2 + 1;
                triangles[k + 3] = next * 2; triangles[k + 4] = next * 2 + 1; triangles[k + 5] = i * 2 + 1;
            }
            Shape(root.transform, "Smooth running track", points, triangles, color, -9600);
        }
        private void Quad(Transform parent, string label, float x, float y, float width, float depth, Color color)
            => Shape(parent, label, new[] { new Vector2(x, y), new Vector2(x + width, y), new Vector2(x + width, y + depth), new Vector2(x, y + depth) }, new[] { 0, 1, 2, 0, 2, 3 }, color);
        private void Shape(Transform parent, string label, Vector2[] points, int[] triangles, Color color, int order = -9500)
        {
            var item = new GameObject(label) { hideFlags = HideFlags.HideAndDontSave }; item.transform.SetParent(parent, false);
            var mesh = new Mesh { name = label, hideFlags = HideFlags.HideAndDontSave };
            var vertices = new Vector3[points.Length]; var colors = new Color[points.Length];
            for (int i = 0; i < points.Length; i++) { vertices[i] = parent.InverseTransformPoint(map.GridToWorld(points[i])); colors[i] = color; }
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.colors = colors; mesh.RecalculateBounds(); meshes.Add(mesh);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>(); renderer.sharedMaterial = map.MapMaterial; renderer.sortingOrder = order;
        }
        private void Update()
        {
            if (wetRoot == null) return;
            bool raining = false;
            foreach (var effect in map.GetComponentsInChildren<SurvivalAmbientEffect>())
                if (effect.IsCampusWeather) { raining = true; break; }
            if (raining != wet) { wet = raining; wetRoot.SetActive(wet); }
        }
        public void Clear()
        {
            if (root != null) Release(root);
            foreach (var mesh in meshes) if (mesh != null) Release(mesh);
            meshes.Clear(); root = wetRoot = null; wet = false;
        }
        private void OnDisable() { Clear(); }
        private static void Release(Object item) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
    }
}
