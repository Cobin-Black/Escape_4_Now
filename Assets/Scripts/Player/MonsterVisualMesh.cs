
using UnityEngine;

namespace Escape4Now.Player
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MonsterVisualMesh : MonoBehaviour
    {
        [Header("Tile Dimensions")]
        [SerializeField] private float tileWidth = 1.2f;
        [SerializeField] private float tileHeight = 0.6f;

        [Header("Appearance")]
        [SerializeField] private Color visualColor = Color.red;

        // Creates a diamond mesh that covers the Monster's four tiles.
        private void Awake()
        {
            if (!Application.isPlaying)
                return;

            Mesh mesh = new Mesh();
            mesh.name = "Monster Footprint Diamond";

            mesh.vertices = new Vector3[]
            {
                new Vector3(0f, tileHeight, 0f),
                new Vector3(tileWidth, 0f, 0f),
                new Vector3(0f, -tileHeight, 0f),
                new Vector3(-tileWidth, 0f, 0f)
            };

            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader != null)
            {
                Material material = new Material(shader);
                material.color = visualColor;
                meshRenderer.material = material;
            }

            meshRenderer.sortingOrder = 10;
        }

    }
}
