
using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Arena
{
    public sealed class KingOfTheHillZone : MonoBehaviour
    {
        [Header("Hill Settings")]
        [SerializeField] private float radius = 3f;
        [SerializeField] private int circleSegments = 64;
        [SerializeField] private float lineWidth = 0.06f;
        [SerializeField] private Color hillColor = Color.yellow;

        private ArenaMap map;
        private LineRenderer circle;
        private Vector2 center;
        private MeshFilter fillFilter;
        private MeshRenderer fillRenderer;
        private Mesh fillMesh;

        [SerializeField, Range(0f, 1f)]
        private float fillOpacity = 0.15f;

        public Vector2 Center => center;
        public float Radius => radius;

        // Create the visible hill boundary.
        public void Initialize(ArenaMap arenaMap)
        {
            map = arenaMap;

            circle = GetComponent<LineRenderer>();
            if (circle == null)
                circle = gameObject.AddComponent<LineRenderer>();

            circle.useWorldSpace = true;
            circle.loop = true;
            circle.positionCount = circleSegments;
            circle.startWidth = lineWidth;
            circle.endWidth = lineWidth;
            circle.startColor = hillColor;
            circle.endColor = hillColor;
            circle.sharedMaterial = map.Settings.material;
            circle.sortingOrder = -850;

            // Create the transparent hill fill.
            GameObject fillObject = new GameObject("Hill Fill");
            fillObject.transform.SetParent(transform, false);

            fillFilter = fillObject.AddComponent<MeshFilter>();
            fillRenderer = fillObject.AddComponent<MeshRenderer>();

            fillMesh = new Mesh();
            fillFilter.mesh = fillMesh;

            Material fillMaterial = new Material(
                Shader.Find("Sprites/Default")
            );

            Color fillColor = hillColor;
            fillColor.a = fillOpacity;

            fillMaterial.color = fillColor;
            fillRenderer.material = fillMaterial;
            fillRenderer.sortingOrder = -851;

            Relocate();
        }

        // Find a random clear location on the basketball court.
        public void Relocate()
        {
            if (map == null || map.Layout == null)
                return;

            List<Vector2Int> candidates = new List<Vector2Int>();

            for (int x = 0; x < map.Layout.Width; x++)
            {
                for (int y = 0; y < map.Layout.Height; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    if (map.Layout.surfaces[x, y] != ArenaSurface.Court)
                        continue;

                    // Only the center must be walkable.
                    // The hill can overlap surrounding walls and obstacles.
                    if (!map.Layout.Walkable(cell))
                        continue;

                    if (Vector2.Distance(cell, center) < radius * 2f)
                        continue;

                    candidates.Add(cell);
                }
            }

            if (candidates.Count == 0)
            {
                Debug.LogWarning("King of the Hill: No valid hill locations found.");
                return;
            }

            center = candidates[map.Random.Next(candidates.Count)];
            DrawCircle();
        }

        // Check whether a player is standing inside the hill.
        public bool Contains(ArenaParticipant player)
        {
            return player != null &&
                   player.Alive &&
                   Vector2.Distance(player.Position, center) <= radius;
        }

        // Draw the circular zone using the map's isometric coordinates.
        private void DrawCircle()
        {
            Vector3[] vertices = new Vector3[circleSegments + 1];
            int[] triangles = new int[circleSegments * 3];

            vertices[0] = map.World(center) + Vector3.back * 0.025f;

            for (int i = 0; i < circleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / circleSegments;

                Vector2 point = center + new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                ) * radius;

                Vector3 worldPoint = map.World(point);

                circle.SetPosition(i, worldPoint + Vector3.back * 0.03f);

                vertices[i + 1] = worldPoint + Vector3.back * 0.025f;

                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % circleSegments + 1;
            }

            fillMesh.Clear();
            fillMesh.vertices = vertices;
            fillMesh.triangles = triangles;
            fillMesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (fillMesh != null)
                Destroy(fillMesh);

            if (fillRenderer != null && fillRenderer.material != null)
                Destroy(fillRenderer.material);
        }
    }
}
