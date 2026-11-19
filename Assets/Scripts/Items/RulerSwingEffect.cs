using UnityEngine;

namespace Escape4Now.Items
{
    // Draws a temporary filled wedge covering the area the Ruler swing hits, with a brighter edge along the arc.
    public sealed class RulerSwingEffect : MonoBehaviour
    {
        [SerializeField] private float duration = 0.2f;
        [SerializeField] private int segments = 16;
        [SerializeField, Tooltip("Color of the arc line at the edge of the swing.")]
        private Color swingColor = new Color(1f, 0.85f, 0.3f, 0.9f);
        [SerializeField, Tooltip("Color filling the whole swing area. Alpha controls how see-through it is.")]
        private Color fillColor = new Color(1f, 0.85f, 0.3f, 0.45f);
        [SerializeField] private float lineWidth = 0.06f;
        [SerializeField] private int sortingOrder = 100;

        private LineRenderer line;
        private MeshRenderer fill;
        private Material lineMaterial, fillMaterial;
        private Mesh fillMesh;
        private float elapsed;

        // Build the wedge and arc in the direction of the attack.
        public void Initialize(Vector2 direction, float range, float angle)
        {
            segments = Mathf.Max(1, segments);
            float centerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Vector3[] arc = new Vector3[segments + 1];

            for (int i = 0; i <= segments; i++)
            {
                float radians = (centerAngle - angle / 2f + angle * i / segments) * Mathf.Deg2Rad;
                arc[i] = new Vector3(Mathf.Cos(radians) * range, Mathf.Sin(radians) * range, 0f);
            }

            BuildFill(arc);
            BuildLine(arc);
            ApplyFade(1f);
            Destroy(gameObject, duration);
        }

        // A triangle fan from the player out to the arc fills the full swing area.
        private void BuildFill(Vector3[] arc)
        {
            var vertices = new Vector3[arc.Length + 1];
            vertices[0] = Vector3.zero;
            arc.CopyTo(vertices, 1);

            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 2;
                triangles[i * 3 + 2] = i + 1;
            }

            fillMesh = new Mesh { name = "Ruler Swing Fill" };
            fillMesh.vertices = vertices;
            fillMesh.triangles = triangles;
            fillMesh.RecalculateBounds();

            var child = new GameObject("Fill");
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = fillMesh;
            fill = child.AddComponent<MeshRenderer>();
            fillMaterial = new Material(Shader.Find("Sprites/Default"));
            fill.sharedMaterial = fillMaterial;
            fill.sortingOrder = sortingOrder;
        }

        private void BuildLine(Vector3[] arc)
        {
            line = GetComponent<LineRenderer>();
            if (line == null)
                line = gameObject.AddComponent<LineRenderer>();

            line.useWorldSpace = false;
            line.positionCount = arc.Length;
            line.SetPositions(arc);
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            line.sharedMaterial = lineMaterial;
            // Edge draws on top of the fill.
            line.sortingOrder = sortingOrder + 1;
        }

        // Fade the swing out over its lifetime.
        private void Update()
        {
            elapsed += Time.deltaTime;
            ApplyFade(1f - Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration)));
        }

        private void ApplyFade(float strength)
        {
            if (fillMaterial != null)
            {
                Color color = fillColor;
                color.a *= strength;
                fillMaterial.color = color;
            }

            if (line != null)
            {
                Color color = swingColor;
                color.a *= strength;
                line.startColor = color;
                line.endColor = color;
            }
        }

        // Runtime-created assets are not cleaned up with the GameObject, so free them here.
        private void OnDestroy()
        {
            if (fillMesh != null) Destroy(fillMesh);
            if (fillMaterial != null) Destroy(fillMaterial);
            if (lineMaterial != null) Destroy(lineMaterial);
        }
    }
}
