using System.Collections.Generic;
using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Arena
{
    // Presentation reads event state; it never applies damage, chooses players or changes walkability.
    public sealed class ArenaEventView : MonoBehaviour
    {
        private ArenaEventDirector owner;
        private ArenaEventState state;
        private ArenaEventVisualSettings settings;
        private LineRenderer ring, path, beam, assistance;
        private Mesh lightMesh;
        private MeshRenderer lightRenderer;
        private readonly List<LineRenderer> outlines = new List<LineRenderer>();
        public static ArenaEventView Create(ArenaEventDirector owner, ArenaEventState state)
        {
            var view = new GameObject(state.Rule.label + " event visuals").AddComponent<ArenaEventView>();
            view.transform.SetParent(owner.transform, false);
            view.owner = owner;
            view.state = state;
            view.settings = owner.Pool.visuals;
            view.assistance = view.Line("Show Events indicator");
            view.ring = view.Line("Warning / effect radius");
            view.path = view.Line("Incoming direction");
            view.beam = view.Line("Target / spotlight");
            var light = new GameObject("Soft light beam and pool", typeof(MeshFilter), typeof(MeshRenderer));
            light.transform.SetParent(view.transform, false);
            view.lightMesh = new Mesh
            {
                name = "Generated arena spotlight"
            };
            light.GetComponent<MeshFilter>().sharedMesh = view.lightMesh;
            view.lightRenderer = light.GetComponent<MeshRenderer>();
            view.lightRenderer.sharedMaterial = view.settings.lineMaterial;
            view.lightRenderer.sortingOrder = view.settings.floorSortingOrder + 1;
            foreach (var cell in state.Cells)
            {
                var outline = view.Line("Affected ground");
                outline.loop = true;
                outline.positionCount = 4;
                var corners = new[]
                {
                    new Vector2(-.48f, -.48f),
                    new Vector2(.48f, -.48f),
                    new Vector2(.48f, .48f),
                    new Vector2(-.48f, .48f)
                };
                for (int i = 0; i < 4; i++)
                    outline.SetPosition(i, owner.Match.Map.World((Vector2)cell + corners[i]));
                view.outlines.Add(outline);
            }

            return view;
        }

        private LineRenderer Line(string label)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.useWorldSpace = true;
            line.sharedMaterial = settings.lineMaterial;
            line.startWidth = line.endWidth = settings.lineWidth;
            line.sortingOrder = settings.floorSortingOrder;
            return line;
        }

        private void Circle(LineRenderer line, Vector2 center, float radius)
        {
            line.loop = true;
            line.positionCount = settings.circleSegments;
            for (int i = 0; i < line.positionCount; i++)
            {
                float a = i * Mathf.PI * 2 / line.positionCount;
                line.SetPosition(i, owner.Match.Map.World(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius));
            }
        }

        // Draw warnings and active feedback from the event state, without applying gameplay effects.
        private void LateUpdate()
        {
            if (owner == null || state == null || state.Ended)
                return;
            // This uses the already registered event position; hiding it never touches the effect.
            assistance.enabled = owner.EventLocationsVisible;
            if (assistance.enabled)
                Circle(assistance, state.Center, settings.assistanceRadius);
            var rule = state.Rule;
            Color color = state.Warning ? settings.warningColor : rule.color;
            float clock = state.Warning ? -state.WarningRemaining : state.Elapsed;
            color.a *= .7f + .3f * Mathf.Sin(clock * settings.warningPulseSpeed * Mathf.PI * 2);
            foreach (var line in GetComponentsInChildren<LineRenderer>())
                line.startColor = line.endColor = color;
            bool local = state.Cells.Count > 0;
            foreach (var line in outlines)
                line.enabled = state.Warning || rule.IsBlock || rule.kind == ArenaEventKind.HazardZone;
            ring.enabled = rule.IsWave || rule.kind == ArenaEventKind.Spotlight || rule.kind == ArenaEventKind.MovingStageLights || rule.IsInstant;
            Vector2 center = state.Center;
            if ((rule.kind == ArenaEventKind.Spotlight || rule.kind == ArenaEventKind.PotatoShuffle || rule.kind == ArenaEventKind.ExtraTime || rule.kind == ArenaEventKind.TimeCut) && owner.Match is HotPotatoController hot && hot.Holder != null)
                center = hot.Holder.Position;
            if (rule.kind == ArenaEventKind.MovingStageLights && state.Cells.Count > 1)
                center = Vector2.Lerp(state.Cells[0], state.Cells[state.Cells.Count - 1], Mathf.PingPong(state.Elapsed / settings.movingLightSeconds, 1));
            float radius = rule.IsWave && !state.Warning ? rule.radius * Mathf.Clamp01(state.Elapsed / rule.waveSeconds) : rule.kind == ArenaEventKind.Spotlight ? settings.spotlightRadius : rule.radius;
            if (ring.enabled)
                Circle(ring, center, radius);
            path.enabled = local && (rule.kind == ArenaEventKind.BallBarrage || rule.kind == ArenaEventKind.CourtShuffle || rule.kind == ArenaEventKind.MovingStageLights);
            if (path.enabled)
            {
                Vector2 start = rule.kind == ArenaEventKind.CourtShuffle ? state.sourceCell : state.Cells[0];
                Vector2 end = state.Cells[state.Cells.Count - 1];
                Vector2 direction = (end - start).normalized, side = new Vector2(-direction.y, direction.x);
                path.positionCount = 5;
                path.SetPosition(0, owner.Match.Map.World(start));
                path.SetPosition(1, owner.Match.Map.World(end));
                path.SetPosition(2, owner.Match.Map.World(end - direction * .6f + side * .3f));
                path.SetPosition(3, owner.Match.Map.World(end));
                path.SetPosition(4, owner.Match.Map.World(end - direction * .6f - side * .3f));
            }

            beam.enabled = rule.kind == ArenaEventKind.PlayerSwap || rule.kind == ArenaEventKind.PotatoShuffle && state.first != null || rule.kind == ArenaEventKind.Spotlight || rule.kind == ArenaEventKind.MovingStageLights;
            if (beam.enabled)
            {
                beam.positionCount = 3;
                if ((rule.kind == ArenaEventKind.PlayerSwap || rule.kind == ArenaEventKind.PotatoShuffle) && state.first != null && state.second != null)
                {
                    beam.SetPosition(0, state.first.transform.position);
                    beam.SetPosition(1, (state.first.transform.position + state.second.transform.position) * .5f + Vector3.up * .6f);
                    beam.SetPosition(2, state.second.transform.position);
                }
                else
                {
                    var point = owner.Match.Map.World(center);
                    beam.SetPosition(0, point + Vector3.left * .5f);
                    beam.SetPosition(1, point + Vector3.up * settings.coneHeight);
                    beam.SetPosition(2, point + Vector3.right * .5f);
                }
            }

            lightRenderer.enabled = rule.kind == ArenaEventKind.Spotlight || rule.kind == ArenaEventKind.MovingStageLights;
            if (lightRenderer.enabled)
                DrawLight(center, radius, color);
        }

        private void DrawLight(Vector2 center, float radius, Color color)
        {
            int count = settings.circleSegments;
            var vertices = new Vector3[count + 4];
            var colors = new Color[count + 4];
            var triangles = new int[3 + count * 3];
            Vector3 point = owner.Match.Map.World(center);
            vertices[0] = transform.InverseTransformPoint(point + Vector3.up * settings.coneHeight);
            vertices[1] = transform.InverseTransformPoint(point + Vector3.left * radius * .6f);
            vertices[2] = transform.InverseTransformPoint(point + Vector3.right * radius * .6f);
            colors[0] = new Color(color.r, color.g, color.b, settings.beamOpacity);
            colors[1] = colors[2] = new Color(color.r, color.g, color.b, settings.beamOpacity * .2f);
            triangles[0] = 0;
            triangles[1] = 1;
            triangles[2] = 2;
            vertices[3] = transform.InverseTransformPoint(point);
            colors[3] = new Color(color.r, color.g, color.b, settings.lightPoolOpacity);
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count;
                vertices[4 + i] = transform.InverseTransformPoint(owner.Match.Map.World(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius));
                colors[4 + i] = new Color(color.r, color.g, color.b, 0);
                int t = 3 + i * 3;
                triangles[t] = 3;
                triangles[t + 1] = 4 + i;
                triangles[t + 2] = 4 + (i + 1) % count;
            }

            lightMesh.Clear();
            lightMesh.vertices = vertices;
            lightMesh.colors = colors;
            lightMesh.triangles = triangles;
            lightMesh.RecalculateBounds();
        }

        internal void ActivateGround()
        {
            GameObject prefab = state.Rule.kind == ArenaEventKind.SlipperyFloor ? settings.wetFloorPrefab : state.Rule.kind == ArenaEventKind.HazardZone ? settings.hazardFloorPrefab : null;
            if (prefab != null)
            {
                var floor = Instantiate(prefab, transform);
                var surface = floor.GetComponent<SurvivalEnvironmentalSurface>();
                surface?.Configure(state.Cells, p => owner.Match.Map.World(p), owner.Match.Map.Seed, owner.Match.Map.Settings.material);
            }

            if (state.Rule.kind == ArenaEventKind.SoundBlast && settings.speakerPrefab != null)
                SpawnObstacle(settings.speakerPrefab, state.sourceCell);
        }

        internal GameObject SpawnObstacle(GameObject prefab, Vector2 center)
        {
            var item = Instantiate(prefab, transform);
            item.transform.position = owner.Match.Map.World(center);
            foreach (var prop in item.GetComponentsInChildren<SurvivalPropVisual>())
                prop.Build(owner.Match.Map.Settings.tileSize, Vector2.one * .88f, owner.Match.Map.Settings.material, owner.Match.Map.Order(center), true, 0);
            return item;
        }

        internal GameObject SpawnBall()
        {
            var item = Instantiate(settings.basketballPrefab, transform);
            foreach (var renderer in item.GetComponentsInChildren<Renderer>())
                renderer.sortingOrder = 900;
            return item;
        }

        private void OnDestroy()
        {
            if (lightMesh != null)
                Destroy(lightMesh);
        }
    }
}
