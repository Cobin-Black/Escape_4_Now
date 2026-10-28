// Draws an outdoor prop from its editable parts and lines, using the map's size and drawing order.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Escape4Now.Map
{
    [ExecuteAlways]
    public sealed class SurvivalPropVisual : MonoBehaviour
    {
        [Serializable]
        private struct Part
        {
            public Vector2 offset;
            [Min(.01f)] public float width, depth, height;
            [Min(0f)] public float bottom;
            public Color color;
        }
        [Serializable]
        private struct Stroke
        {
            public Vector2[] points;
            [Min(0f)] public float height;
            [Min(.001f)] public float width;
            public Color color;
            public bool loop;
        }
        [Header("Prefab Identity and Preview")]
        [SerializeField] private OutdoorPropKind kind;
        [SerializeField, Tooltip("Material for standalone prefab previews. Spawned props inherit the map material unless Material Override is assigned.")]
        private Material material;
        [SerializeField] private Vector2 previewTileSize = new Vector2(1.2f, .6f);
        [SerializeField] private Vector2 previewFootprint = Vector2.one;
        [Header("Runtime Rendering")]
        [SerializeField, Tooltip("Optional material used for this prop's geometry and detail lines, in the map and in previews. Leave empty to inherit the map material.")]
        private Material materialOverride;
        [Header("Editable Geometry")]
        [SerializeField] private Part[] parts;
        [SerializeField] private Stroke[] strokes;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private GameObject drawing;
        public OutdoorPropKind Kind => kind;

#if UNITY_EDITOR
        private bool needsPreview, ownedByMap;
        private void OnEnable() { needsPreview = true; }
        private void OnValidate() { needsPreview = true; }
        private void Update()
        {
            if (Application.IsPlaying(gameObject) || ownedByMap || !needsPreview || UnityEditor.EditorUtility.IsPersistent(this)) return;
            needsPreview = false;
            Build(previewTileSize, previewFootprint, material, 0, false);
        }
#endif

        //Turn the prefab's saved parts and lines into a drawing at the current map's tile size.
        public void Build(Vector2 tileSize, Vector2 footprint, Material sharedMaterial, int order, bool generated = true, int quarterTurns = -1)
        {
            Clear();
#if UNITY_EDITOR
            ownedByMap = generated;
#endif
            Material renderMaterial = materialOverride != null ? materialOverride : sharedMaterial;
            if (parts == null || renderMaterial == null) return;
            drawing = new GameObject("Prop geometry") { hideFlags = HideFlags.HideAndDontSave };
            drawing.transform.SetParent(transform, false);
            drawing.AddComponent<SortingGroup>().sortingOrder = order;
            bool rotate = (footprint.x > footprint.y) != (previewFootprint.x > previewFootprint.y) && footprint.x != footprint.y;
            int turns = quarterTurns < 0 ? (rotate ? 1 : 0) : quarterTurns % 4;
            rotate = turns % 2 != 0;
            for (int i = 0; i < parts.Length; i++)
            {
                Part part = parts[i];
                Vector2 offset = Rotate(part.offset, turns);
                Renderer piece = IsometricMapTemplate.CreateBlock(drawing.transform, "Part", new Vector2(offset.x * footprint.x, offset.y * footprint.y),
                    (rotate ? part.depth : part.width) * footprint.x, (rotate ? part.width : part.depth) * footprint.y,
                    part.bottom, part.height, part.color, i, tileSize.x, tileSize.y, renderMaterial, HideFlags.HideAndDontSave);
                meshes.Add(piece.GetComponent<MeshFilter>().sharedMesh);
            }
            if (strokes == null) return;
            foreach (Stroke stroke in strokes)
            {
                if (stroke.points == null || stroke.points.Length < 2) continue;
                var lineObject = new GameObject("Detail line");
                lineObject.transform.SetParent(drawing.transform, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false; line.sharedMaterial = renderMaterial;
                line.startColor = line.endColor = stroke.color;
                line.startWidth = line.endWidth = stroke.width;
                line.loop = stroke.loop; line.positionCount = stroke.points.Length;
                line.sortingOrder = parts.Length;
                for (int i = 0; i < stroke.points.Length; i++)
                {
                    Vector2 p = Rotate(stroke.points[i], turns) * footprint;
                    line.SetPosition(i, new Vector3((p.x - p.y) * tileSize.x * .5f,
                        (p.x + p.y) * tileSize.y * .5f + stroke.height, -.001f));
                }
            }
        }

        private static Vector2 Rotate(Vector2 point, int turns) => turns == 1 ? new Vector2(-point.y, point.x)
            : turns == 2 ? -point : turns == 3 ? new Vector2(point.y, -point.x) : point;

        private void OnDisable() { Clear(); }
        private void Clear()
        {
            if (drawing != null) Release(drawing);
            foreach (Mesh mesh in meshes) if (mesh != null) Release(mesh);
            meshes.Clear(); drawing = null;
        }
        private static void Release(UnityEngine.Object target)
        { if (Application.isPlaying) Destroy(target); else DestroyImmediate(target); }
    }
}
