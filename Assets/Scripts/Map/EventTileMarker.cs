using UnityEngine;

namespace Escape4Now.Map
{
    //Draws the event tile's shape and label. MapEventController applies the effect.
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class EventTileMarker : MonoBehaviour
    {
        [SerializeField] private MapEventType eventType;
        [SerializeField] private Color markerColor = Color.white;
        [SerializeField] private TextMesh label;
        [SerializeField, Min(0.1f)] private float tileWidth = 1.2f;
        [SerializeField, Min(0.1f)] private float tileHeight = 0.6f;

        private Mesh markerMesh;
        private Material markerMaterial;
        private bool needsRedraw;

        public MapEventType EventType => eventType;

        //Shows the event tile in the game and while editing its reusable template.
        private void OnEnable()
        {
            DrawMarker();
        }

        //Marks the drawing for an update when its settings change in Unity.
        private void OnValidate()
        {
            needsRedraw = true;
        }

        //Refreshes the drawing only when a setting changed.
        private void Update()
        {
            if (!needsRedraw) return;
            needsRedraw = false;
            DrawMarker();
        }

        //Checks the size is valid, then fits the event marker to a map tile.
        public void Configure(float width, float height)
        {
            if (float.IsNaN(width) || float.IsInfinity(width) || width <= 0f
                || float.IsNaN(height) || float.IsInfinity(height) || height <= 0f) return;
            tileWidth = width;
            tileHeight = height;
            DrawMarker();
        }

        //Makes the colored diamond and draws its text on top.
        private void DrawMarker()
        {
            if (markerMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) return;
                markerMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
            if (markerMesh == null)
                markerMesh = new Mesh { name = "Event Diamond", hideFlags = HideFlags.DontSave };

            float halfWidth = Mathf.Max(0.1f, tileWidth) * 0.42f;
            float halfHeight = Mathf.Max(0.1f, tileHeight) * 0.42f;
            markerMesh.Clear();
            markerMesh.vertices = new[] { new Vector3(0f, halfHeight), new Vector3(halfWidth, 0f),
                new Vector3(0f, -halfHeight), new Vector3(-halfWidth, 0f) };
            markerMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            markerMesh.colors = new[] { markerColor, markerColor, markerColor, markerColor };
            markerMesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = markerMesh;
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = markerMaterial;
            renderer.sortingOrder = -900;

            if (label == null) return;
            if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MeshRenderer labelRenderer = label.GetComponent<MeshRenderer>();
            if (labelRenderer != null && label.font != null)
            {
                labelRenderer.sharedMaterial = label.font.material;
                labelRenderer.sortingOrder = -899;
            }
        }

        //Removes the temporary shape and appearance data created by this tile.
        private void OnDestroy()
        {
            ReleaseDrawing(markerMesh);
            ReleaseDrawing(markerMaterial);
        }

        //Cleans up temporary drawing data in Play mode or edit mode.
        private void ReleaseDrawing(Object drawing)
        {
            if (drawing == null) return;
            if (Application.isPlaying) Destroy(drawing);
            else DestroyImmediate(drawing);
        }
    }
}
