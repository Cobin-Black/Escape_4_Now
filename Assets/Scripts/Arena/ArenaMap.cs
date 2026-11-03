using System;
using System.Collections.Generic;
using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Arena
{
    // Rendering uses the same isometric blocks and existing prop prefabs as the outdoor map.
    public sealed class ArenaMap : MonoBehaviour
    {
        [Header("Mode Map Settings")]
        [SerializeField]
        private ArenaMapSettings settings;
        [Header("System References")]
        [SerializeField]
        private ArenaEventDirector eventController;
        public ArenaEventDirector EventController => eventController;
        public ArenaMapSettings Settings => settings;
        public ArenaLayout Layout { get; private set; }
        public int Seed { get; private set; }
        public System.Random Random { get; private set; }

        private readonly List<Mesh> meshes = new List<Mesh>();
        private Transform drawing;
        // Event hooks only: register existing equipment without changing how the room is generated.
        public sealed class EquipmentView
        {
            public Vector2Int Cell;
            public Transform Visual;
            public bool IsBleachers;
            public int QuarterTurns;
        }

        public readonly List<EquipmentView> EquipmentViews = new List<EquipmentView>();
        // Build a seeded room, then accept it only when routes and player spawns are valid.
        public bool Generate(int players, out List<Vector2Int> spawns)
        {
            Clear();
            Seed = settings.fixedSeed ? settings.seed : unchecked(Environment.TickCount ^ Guid.NewGuid().GetHashCode());
            Random = new System.Random(Seed);
            spawns = null;
            for (int i = 0; i < Mathf.Max(1, settings.generationAttempts); i++)
            {
                Layout = new ArenaLayout(settings, Random);
                if (Layout.Connected() && Layout.TrySpawns(players, settings, Random, out spawns))
                {
                    Draw();
                    return true;
                }
            }

            Layout = null;
            return false;
        }

        // Convert logical floor coordinates into the shared isometric view.
        public Vector3 World(Vector2 p) => transform.TransformPoint(new Vector3((p.x - p.y) * settings.tileSize.x * .5f, (p.x + p.y) * settings.tileSize.y * .5f, 0));
        public Vector2 GridDirection(Vector2 screen) => new Vector2(screen.x / settings.tileSize.x + screen.y / settings.tileSize.y, -screen.x / settings.tileSize.x + screen.y / settings.tileSize.y);
        public Vector2 ScreenDirection(Vector2 grid) => new Vector2((grid.x - grid.y) * settings.tileSize.x * .5f, (grid.x + grid.y) * settings.tileSize.y * .5f);
        public int Order(Vector2 p) => 505 - Mathf.RoundToInt((p.x + p.y) * 10);
        // Render the same blocked cells that movement uses, so visible props match collision.
        private void Draw()
        {
            drawing = new GameObject("Generated room").transform;
            drawing.SetParent(transform, false);
            var floorParts = new List<CombineInstance>();
            for (int x = 0; x < Layout.Width; x++)
                for (int y = 0; y < Layout.Height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    var anchor = new GameObject("Tile " + cell).transform;
                    anchor.SetParent(drawing, false);
                    anchor.position = World(cell);
                    Color color = settings.SurfaceColor(Layout.surfaces[x, y]);
                    float variation = (((x * 17 + y * 7) % 7) / 6f - .5f) * settings.floorVariation;
                    color += new Color(variation, variation, variation, 0);
                    var floor = Block(anchor, "Floor", Vector2.zero, 1, 1, -settings.floorDepth, settings.floorDepth, color, -1000);
                    // Hidden tile side faces otherwise draw a distracting square grid across the floor.
                    floor.GetComponent<MeshFilter>().sharedMesh.triangles = new[]
                    {
                        0,
                        1,
                        2,
                        0,
                        2,
                        3
                    };
                    floorParts.Add(new CombineInstance { mesh = floor.GetComponent<MeshFilter>().sharedMesh, transform = floor.transform.localToWorldMatrix });
                    floor.gameObject.SetActive(false);
                    if (Layout.blocked[x, y] && !Layout.seats.Contains(cell) && !Layout.props.Contains(cell) && !Layout.fixtures.ContainsKey(cell) && !Layout.groupedProps.Exists(p => p.cell == cell))
                        Block(anchor, "Wall", Vector2.zero, .98f, .98f, 0, x == 0 || y == 0 ? settings.frontWallHeight : settings.room == ArenaRoom.Gymnasium && settings.roomVisuals != null && (x == Layout.Width - 1 || y == Layout.Height - 1) ? settings.roomVisuals.gymBackWallHeight : settings.wallHeight, settings.wall, Order(cell));
                    if (Layout.seats.Contains(cell) && settings.roomVisuals != null && settings.roomVisuals.auditoriumSeat != null)
                    {
                        var seat = Instantiate(settings.roomVisuals.auditoriumSeat, anchor);
                        seat.Build(settings.tileSize, Vector2.one * .88f, settings.material, Order(cell), true, settings.seatQuarterTurns);
                    }
                    else if (Layout.seats.Contains(cell))
                    {
                        var s = settings.seatSize;
                        Block(anchor, "Seat", Vector2.zero, s.x, s.y, .08f, s.z, settings.seatColor, Order(cell));
                        Block(anchor, "Seat back", new Vector2(0, .22f), s.x, .10f, .16f, s.z + .12f, settings.seatColor, Order(cell) + 1);
                    }

                    if (settings.room == ArenaRoom.Auditorium && y == Layout.Height - Mathf.Clamp(settings.sideRoomDepth, 3, Layout.Height / 4) - 1 && x > 2 && x < Layout.Width - 3 && Layout.blocked[x, y])
                        Block(anchor, "Stage curtain", Vector2.zero, .98f, .18f, settings.wallHeight, settings.curtainHeight, settings.curtainColor, Order(cell) + 2);
                }

            // The floor is one draw call instead of hundreds of separate renderers.
            var floorObject = new GameObject("Combined room floor", typeof(MeshFilter), typeof(MeshRenderer));
            floorObject.transform.SetParent(drawing, false);
            var combined = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            combined.CombineMeshes(floorParts.ToArray(), true, true);
            meshes.Add(combined);
            floorObject.GetComponent<MeshFilter>().sharedMesh = combined;
            floorObject.GetComponent<MeshRenderer>().sharedMaterial = settings.material;
            floorObject.GetComponent<MeshRenderer>().sortingOrder = -1000;
            foreach (var item in floorParts)
            {
                meshes.Remove(item.mesh);
                Destroy(item.mesh);
            }

            foreach (var cell in Layout.props)
            {
                var prefab = settings.equipment.Length > 0 ? settings.equipment[Random.Next(settings.equipment.Length)] : settings.crates;
                if (prefab == null)
                    continue;
                var prop = Instantiate(prefab, drawing);
                prop.transform.position = World(cell);
                int turns = ArenaPropPlacement.Facing(cell, Layout, ArenaPropFacing.TowardRoom);
                prop.Build(settings.tileSize, new Vector2(.88f, .88f), settings.material, Order(cell), true, turns);
                EquipmentViews.Add(new EquipmentView { Cell = cell, Visual = prop.transform, QuarterTurns = turns });
            }

            foreach (var fixture in Layout.fixtures)
            {
                var prefab = fixture.Value == 0 ? settings.hoop : fixture.Value == 1 ? settings.bleachers : settings.bench;
                if (prefab == null)
                    continue;
                var prop = Instantiate(prefab, drawing);
                prop.transform.position = World(fixture.Key);
                prop.Build(settings.tileSize, new Vector2(.88f, .88f), settings.material, Order(fixture.Key), true, ArenaPropPlacement.Facing(fixture.Key, Layout, ArenaPropFacing.TowardRoom));
                if (fixture.Value != 0)
                    EquipmentViews.Add(new EquipmentView { Cell = fixture.Key, Visual = prop.transform, IsBleachers = fixture.Value == 1, QuarterTurns = ArenaPropPlacement.Facing(fixture.Key, Layout, ArenaPropFacing.TowardRoom) });
            }

            foreach (var placed in Layout.groupedProps)
            {
                var prop = Instantiate(placed.prefab, drawing);
                prop.transform.position = World(placed.cell);
                prop.Build(settings.tileSize, Vector2.one * .88f, settings.material, Order(placed.cell), true, placed.quarterTurns);
                EquipmentViews.Add(new EquipmentView { Cell = placed.cell, Visual = prop.transform, QuarterTurns = placed.quarterTurns });
            }

            if (settings.room == ArenaRoom.Gymnasium)
                Court();
            RoomNames();
            if (settings.roomVisuals != null)
                ArenaRoomDecor.Build(this, drawing, settings.roomVisuals);
        }

        private void Court()
        {
            float left = 4, right = Layout.Width - 5, bottom = settings.sideRoomDepth + 2, top = Layout.Height - settings.sideRoomDepth - 3;
            Line("Court boundary", new[] { new Vector2(left - .5f, bottom - .5f), new Vector2(right + .5f, bottom - .5f), new Vector2(right + .5f, top + .5f), new Vector2(left - .5f, top + .5f) }, true);
            float middle = (left + right) / 2, cy = (bottom + top) / 2;
            Line("Half court", new[] { new Vector2(middle, bottom - .5f), new Vector2(middle, top + .5f) }, false);
            var circle = new Vector2[49];
            for (int i = 0; i < circle.Length; i++)
            {
                float a = i * Mathf.PI * 2 / (circle.Length - 1);
                circle[i] = new Vector2(middle + Mathf.Cos(a) * 2, cy + Mathf.Sin(a) * 2);
            }

            Line("Center circle", circle, false);
            foreach (float end in new[]
            {
                left,
                right
            }

            )
            {
                float inside = end == left ? end + 3 : end - 3;
                Line("Basket key", new[] { new Vector2(end, cy - 2), new Vector2(inside, cy - 2), new Vector2(inside, cy + 2), new Vector2(end, cy + 2) }, false);
            }
        }

        private Renderer Block(Transform parent, string label, Vector2 offset, float w, float d, float bottom, float height, Color color, int order)
        {
            var renderer = IsometricMapTemplate.CreateBlock(parent, label, offset, w, d, bottom, height, color, order, settings.tileSize.x, settings.tileSize.y, settings.material);
            meshes.Add(renderer.GetComponent<MeshFilter>().sharedMesh);
            return renderer;
        }

        private void Line(string label, Vector2[] points, bool loop)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(drawing, false);
            line.useWorldSpace = true;
            line.loop = loop;
            line.sharedMaterial = settings.material;
            line.startColor = line.endColor = settings.markings;
            line.startWidth = line.endWidth = settings.lineWidth;
            line.sortingOrder = -900;
            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++)
                line.SetPosition(i, World(points[i]) + Vector3.back * .02f);
        }

        private void RoomNames()
        {
            if (!settings.showRoomNames || settings.labelFont == null)
                return;
            string[] names = settings.room == ArenaRoom.Gymnasium ? new[]
            {
                "LOCKER ROOM",
                "EQUIPMENT",
                "STORAGE",
                "SIDE ENTRANCE"
            }

            : new[]
            {
                "CONTROL BOOTH",
                "SIDE AISLE",
                "BACKSTAGE",
                "DRESSING ROOM"
            };
            Vector2[] centers =
            {
                new Vector2(Layout.Width * .25f, 2),
                new Vector2(Layout.Width * .75f, 2),
                new Vector2(Layout.Width * .25f, Layout.Height - 3),
                new Vector2(Layout.Width * .75f, Layout.Height - 3)
            };
            for (int i = 0; i < names.Length; i++)
            {
                var text = new GameObject(names[i]).AddComponent<TextMesh>();
                text.transform.SetParent(drawing, false);
                text.transform.position = World(centers[i]) + Vector3.up * .15f;
                text.font = settings.labelFont;
                text.fontSize = 40;
                text.characterSize = settings.labelSize;
                text.text = names[i];
                text.color = settings.labelColor;
                text.anchor = TextAnchor.MiddleCenter;
                text.GetComponent<Renderer>().sharedMaterial = settings.labelFont.material;
                text.GetComponent<Renderer>().sortingOrder = 1000;
            }
        }

        private void Clear()
        {
            EquipmentViews.Clear();
            if (drawing != null)
                Destroy(drawing.gameObject);
            foreach (var mesh in meshes)
                if (mesh != null)
                    Destroy(mesh);
            meshes.Clear();
        }

        private void OnDestroy() => Clear();
    }
}
