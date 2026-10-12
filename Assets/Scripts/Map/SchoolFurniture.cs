using System;
using System.Collections.Generic;
using Escape4Now.Obstacles;
using UnityEngine;
using UnityEngine.Rendering;

namespace Escape4Now.Map
{
    //Draws room obstacles from small prefab parts. Storage works just like a locker.
    [ExecuteAlways]
    [RequireComponent(typeof(Escape4Now.Obstacles.ObstacleStorage))]
    public sealed class SchoolFurniture : MonoBehaviour
    {
        [Serializable]
        private struct Part
        {
            public Vector2 offset;
            public float width, depth, bottom, height;
            public Color color;
        }
        [SerializeField] private SchoolFurnitureKind kind;
        [SerializeField] private Part[] parts;
        public SchoolFurnitureKind Kind => kind;
        public Vector2Int Cell { get; private set; }
        public ObstacleFacing Facing { get; private set; }
        private IsometricMapTemplate owner;
        private MeshRenderer[] renderers;
        private MaterialPropertyBlock fade;

#if UNITY_EDITOR
        private GameObject preview;
        private Material previewMaterial;
        private bool previewNeedsUpdate;

        //Wait for an editor update before creating temporary drawing objects.
        private void OnEnable() { previewNeedsUpdate = true; }

        //Changing a shape or color in the Inspector refreshes its preview.
        private void OnValidate() { previewNeedsUpdate = true; }

        //Prefab Mode gets a preview even when the main game is running.
        private void Update()
        {
            if (Application.IsPlaying(gameObject)) { ClearPreview(); return; }
            if (!previewNeedsUpdate || UnityEditor.EditorUtility.IsPersistent(this)) return;
            previewNeedsUpdate = false;
            ClearPreview();
            if (!IsValidTemplate()) return;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            previewMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            preview = new GameObject("Obstacle preview") { hideFlags = HideFlags.HideAndDontSave };
            preview.transform.SetParent(transform, false);
            preview.AddComponent<SortingGroup>();
            DrawParts(ObstacleFacing.South, (part, order) => IsometricMapTemplate.CreateBlock(
                preview.transform, "Preview part", part.offset, part.width, part.depth,
                part.bottom, part.height, part.color, order, 1.2f, 0.6f,
                previewMaterial, HideFlags.HideAndDontSave));
        }

        //Closing a prefab or disabling it releases only this object's preview.
        private void OnDisable() { ClearPreview(); }

        //Temporary geometry must not become part of a saved prefab, scene, or match.
        private void ClearPreview()
        {
            if (preview != null)
            {
                foreach (MeshFilter filter in preview.GetComponentsInChildren<MeshFilter>())
                    if (filter.sharedMesh != null) DestroyImmediate(filter.sharedMesh);
                DestroyImmediate(preview);
            }
            if (previewMaterial != null) DestroyImmediate(previewMaterial);
            preview = null;
            previewMaterial = null;
        }
#endif

        //Checks the full footprint before the prefab is allowed to reserve a tile.
        public bool IsValidTemplate()
        {
            if (parts == null || parts.Length == 0 || parts.Length > 32) return false;
            foreach (Part part in parts)
                if (!(part.width > 0 && part.depth > 0 && part.height > 0 && part.bottom >= 0
                    && part.height + part.bottom <= 0.75f
                    && Mathf.Abs(part.offset.x) + part.width / 2 <= 0.45f
                    && Mathf.Abs(part.offset.y) + part.depth / 2 <= 0.45f)) return false;
            return true;
        }

        //The plan already reserved this tile; this method only draws the selected prefab.
        public void Build(IsometricMapTemplate map, Vector2Int cell, ObstacleFacing facing = ObstacleFacing.South)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (!IsValidTemplate() || (int)facing < 0 || (int)facing > 3)
                throw new ArgumentException("Invalid obstacle template or direction.");
#if UNITY_EDITOR
            ClearPreview();
#endif
            Cell = cell;
            Facing = facing;
            if (owner != null) owner.OccupantsChanged -= RefreshFade;
            owner = map;
            //Furniture uses the same one-item storage as the lockers.
            var storage = GetComponent<Escape4Now.Obstacles.ObstacleStorage>();
            if (storage == null) storage = gameObject.AddComponent<Escape4Now.Obstacles.ObstacleStorage>();
            //Items are assigned after all obstacles exist, with special-room rewards first.
            storage.SetStoredItem(null);
            int order = 500 - (cell.x + cell.y) * 10;
            //Keep all parts on this tile's drawing layer, even when a prefab has many parts.
            var group = GetComponent<SortingGroup>();
            if (group == null) group = gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = order;
            DrawParts(facing, (part, index) => map.CreateBlock(transform, "Furniture part",
                part.offset, part.width, part.depth, part.bottom, part.height, part.color, index));
            //The collider uses the tile footprint, not the tall drawing above it.
            var collider = GetComponent<PolygonCollider2D>();
            if (collider == null) collider = gameObject.AddComponent<PolygonCollider2D>();
            collider.points = new[] { new Vector2(0, map.TileHeight * 0.45f), new Vector2(map.TileWidth * 0.45f, 0),
                new Vector2(0, -map.TileHeight * 0.45f), new Vector2(-map.TileWidth * 0.45f, 0) };
            renderers = GetComponentsInChildren<MeshRenderer>();
            owner.OccupantsChanged += RefreshFade;
            RefreshFade();
        }

        //The editor preview and the game use the same parts and drawing order.
        private void DrawParts(ObstacleFacing facing, Action<Part, int> draw)
        {
            var remaining = new List<Part>();
            foreach (Part part in parts) remaining.Add(TurnPart(part, facing));
            for (int i = 0; remaining.Count > 0; i++)
            {
                int next = NextPart(remaining);
                Part part = remaining[next];
                remaining.RemoveAt(next);
                draw(part, i);
            }
        }

        //Turn the footprint on the grid, leaving the object's height upright.
        private static Part TurnPart(Part part, ObstacleFacing facing)
        {
            for (int i = 0; i < (int)facing; i++)
                part.offset = new Vector2(part.offset.y, -part.offset.x);
            if (facing == ObstacleFacing.West || facing == ObstacleFacing.East)
            {
                float width = part.width;
                part.width = part.depth;
                part.depth = width;
            }
            return part;
        }

        //Draw lower and farther parts first so backs hide details facing away from the camera.
        private static int NextPart(List<Part> remaining)
        {
            for (int i = 0; i < remaining.Count; i++)
            {
                bool ready = true;
                for (int j = 0; j < remaining.Count; j++)
                    if (i != j && DrawBefore(remaining[j], remaining[i])) { ready = false; break; }
                if (ready) return i;
            }
            //Overlapping prefab pieces can disagree; keep a stable order instead of looping.
            return 0;
        }

        //Pieces above another piece draw later; pieces at the same height use grid depth.
        private static bool DrawBefore(Part first, Part second)
        {
            if (first.bottom + first.height <= second.bottom + 0.0001f) return true;
            if (second.bottom + second.height <= first.bottom + 0.0001f) return false;
            return first.offset.x + first.offset.y > second.offset.x + second.offset.y + 0.0001f;
        }

        //Fade room obstacles when they hide a player, like the hallway lockers.
        private void RefreshFade()
        {
            if (owner == null || renderers == null) return;
            float top = 0f;
            foreach (Part part in parts) top = Mathf.Max(top, part.bottom + part.height);
            float rowHeight = owner.TileHeight * 0.5f;
            int rows = Mathf.CeilToInt((top + rowHeight) / rowHeight);
            bool behind = false;
            for (int x = 0; x <= rows; x++)
                for (int y = 0; y <= rows; y++)
                    if (x + y > 0 && x + y <= rows && Mathf.Abs(x - y) <= 1)
                        behind |= owner.IsOccupied(Cell + new Vector2Int(x, y));
            fade ??= new MaterialPropertyBlock();
            fade.SetColor("_Color", new Color(1, 1, 1, behind ? 0.3f : 1f));
            foreach (MeshRenderer renderer in renderers) if (renderer != null) renderer.SetPropertyBlock(fade);
        }

        //Stop listening when the map removes this obstacle.
        private void OnDestroy()
        {
#if UNITY_EDITOR
            ClearPreview();
#endif
            if (owner != null) owner.OccupantsChanged -= RefreshFade;
        }
    }
}
