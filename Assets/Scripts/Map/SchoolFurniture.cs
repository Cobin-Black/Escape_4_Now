using System;
using UnityEngine;

namespace Escape4Now.Map
{
    //Draws room obstacles from small prefab parts. Storage works just like a locker.
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
        private IsometricMapTemplate owner;
        private MeshRenderer[] renderers;
        private MaterialPropertyBlock fade;

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
        public void Build(IsometricMapTemplate map, Vector2Int cell)
        {
            Cell = cell;
            if (owner != null) owner.OccupantsChanged -= RefreshFade;
            owner = map;
            //Furniture uses the same one-item storage as the lockers.
            var storage = GetComponent<Escape4Now.Obstacles.ObstacleStorage>();
            if (storage == null) storage = gameObject.AddComponent<Escape4Now.Obstacles.ObstacleStorage>();
            if (!Escape4Now.Multiplayer.LanMultiplayerSession.IsClient)
                map.GetComponent<Escape4Now.Obstacles.ObstacleSpawner>()?.FillStorage(storage);
            int order = 500 - (cell.x + cell.y) * 10;
            for (int i = 0; i < parts.Length; i++)
            {
                Part part = parts[i];
                map.CreateBlock(transform, "Furniture part", part.offset, part.width, part.depth,
                    part.bottom, part.height, part.color, order + i);
            }
            //The collider uses the tile footprint, not the tall drawing above it.
            var collider = GetComponent<PolygonCollider2D>();
            if (collider == null) collider = gameObject.AddComponent<PolygonCollider2D>();
            collider.points = new[] { new Vector2(0, map.TileHeight * 0.45f), new Vector2(map.TileWidth * 0.45f, 0),
                new Vector2(0, -map.TileHeight * 0.45f), new Vector2(-map.TileWidth * 0.45f, 0) };
            renderers = GetComponentsInChildren<MeshRenderer>();
            owner.OccupantsChanged += RefreshFade;
            RefreshFade();
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
            if (owner != null) owner.OccupantsChanged -= RefreshFade;
        }
    }
}
