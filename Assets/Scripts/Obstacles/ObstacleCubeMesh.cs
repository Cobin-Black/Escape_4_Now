using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Obstacles
{
    //Draws a tall isometric box that sits on one map tile and rebuilds itself whenever a setting changes.
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ObstacleCubeMesh : MonoBehaviour
    {
        //Optional map to copy the tile size from so the obstacle footprint matches the floor.
        [SerializeField] private IsometricMapTemplate mapTemplate;
        [SerializeField, Min(0.1f)] private float tileWidth = 1.2f;
        [SerializeField, Min(0.1f)] private float tileHeight = 0.6f;

        //Box shape, measured in tiles for the footprint and in world units for the height.
        [SerializeField, Range(0.1f, 1f)] private float footprintX = 0.9f;
        [SerializeField, Range(0.1f, 1f)] private float footprintY = 0.9f;
        [SerializeField, Min(0.05f)] private float obstacleHeight = 1.2f;

        //Face colors. The sides are the top color darkened by these amounts.
        [SerializeField] private Color topColor = new Color(0.45f, 0.42f, 0.4f, 1f);
        [SerializeField, Range(0f, 1f)] private float rightSideShade = 0.7f;
        [SerializeField, Range(0f, 1f)] private float leftSideShade = 0.48f;
        [SerializeField] private int sortingOrder = 0;

        //Mesh and material owned by this obstacle only.
        private Mesh obstacleMesh;
        private Material obstacleMaterial;

        //Builds the mesh when the object loads, in both edit mode and Play mode.
        private void OnEnable()
        {
            RebuildMesh();
        }

        //Rebuilds the mesh as soon as a value changes in the Inspector.
        private void OnValidate()
        {
            RebuildMesh();
        }

        //Releases the generated mesh and material when the obstacle is removed.
        private void OnDestroy()
        {
            DestroyGeneratedObject(obstacleMesh);
            DestroyGeneratedObject(obstacleMaterial);
        }

        //Draws the top and the two camera-facing sides of the box.
        public void RebuildMesh()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshFilter == null || meshRenderer == null)
            {
                return;
            }

            float width = mapTemplate != null ? mapTemplate.TileWidth : tileWidth;
            float height = mapTemplate != null ? mapTemplate.TileHeight : tileHeight;

            //Grid directions on screen, matching IsometricMapTemplate.GridToWorld.
            Vector3 x = new Vector3(width * 0.5f, height * 0.5f, 0f);
            Vector3 y = new Vector3(-width * 0.5f, height * 0.5f, 0f);
            Vector3 a = x * footprintX * 0.5f + y * footprintY * 0.5f;
            Vector3 b = x * footprintX * 0.5f - y * footprintY * 0.5f;
            Vector3 c = -x * footprintX * 0.5f - y * footprintY * 0.5f;
            Vector3 d = -x * footprintX * 0.5f + y * footprintY * 0.5f;
            Vector3 up = Vector3.up * obstacleHeight;

            Color right = topColor * rightSideShade;
            Color left = topColor * leftSideShade;
            right.a = left.a = topColor.a;

            if (obstacleMesh == null)
            {
                obstacleMesh = new Mesh { name = "Obstacle Cube", hideFlags = HideFlags.DontSave };
            }

            obstacleMesh.Clear();
            obstacleMesh.vertices = new[] { a + up, b + up, c + up, d + up,
                b + up, b, c, c + up, c + up, c, d, d + up };
            obstacleMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7, 8, 9, 10, 8, 10, 11 };
            obstacleMesh.colors = new[] { topColor, topColor, topColor, topColor,
                right, right, right, right, left, left, left, left };
            obstacleMesh.RecalculateBounds();
            meshFilter.sharedMesh = obstacleMesh;

            if (obstacleMaterial == null)
            {
                obstacleMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            }

            meshRenderer.sharedMaterial = obstacleMaterial;
            meshRenderer.sortingOrder = sortingOrder;
        }

        //Removes a generated object in either Play mode or edit mode.
        private void DestroyGeneratedObject(Object generatedObject)
        {
            if (generatedObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(generatedObject);
            }
            else
            {
                DestroyImmediate(generatedObject);
            }
        }
    }
}
