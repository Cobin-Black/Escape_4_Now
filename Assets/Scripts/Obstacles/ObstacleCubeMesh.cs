using Escape4Now.Map;
using UnityEngine;
using UnityEngine.Serialization;

namespace Escape4Now.Obstacles
{
    //Grid direction an obstacle's front face points toward. South and West face the camera.
    public enum ObstacleFacing
    {
        South,
        West,
        North,
        East
    }

    //Draws a tall isometric box that sits on one map tile and rebuilds itself whenever a setting changes.
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ObstacleCubeMesh : MonoBehaviour
    {
        //Optional map to copy the tile size from so the obstacle footprint matches the floor.
        [SerializeField] private IsometricMapTemplate mapTemplate;
        [SerializeField, Min(0.1f)] private float tileWidth = 1.2f;
        [SerializeField, Min(0.1f)] private float tileHeight = 0.6f;

        //Box shape. Width runs along the front face and depth runs front to back, both measured in tiles.
        [SerializeField, Range(0.1f, 1f), FormerlySerializedAs("footprintX")] private float footprintWidth = 0.9f;
        [SerializeField, Range(0.1f, 1f), FormerlySerializedAs("footprintY")] private float footprintDepth = 0.9f;
        [SerializeField, Min(0.05f)] private float obstacleHeight = 1.2f;

        //Face colors. The sides are darkened by these amounts, and the front face uses its own color.
        [SerializeField] private Color topColor = new Color(0.45f, 0.42f, 0.4f, 1f);
        [SerializeField] private Color frontFaceColor = new Color(0.55f, 0.45f, 0.32f, 1f);
        [SerializeField, Range(0f, 1f)] private float rightSideShade = 0.7f;
        [SerializeField, Range(0f, 1f)] private float leftSideShade = 0.48f;

        //Set by ObstacleGridControl so the Inspector only has one place to change them.
        [SerializeField, HideInInspector] private ObstacleFacing facing = ObstacleFacing.South;
        [SerializeField, HideInInspector] private int sortingOrder;

        //Mesh and material owned by this obstacle only.
        private Mesh obstacleMesh;
        private Material obstacleMaterial;

        //Tile size currently in use, taken from the map when one is assigned.
        private float CurrentTileWidth => mapTemplate != null ? mapTemplate.TileWidth : tileWidth;
        private float CurrentTileHeight => mapTemplate != null ? mapTemplate.TileHeight : tileHeight;

        //Footprint along the grid's x and y axes after turning the box to face its direction.
        private bool FacesAlongX => facing == ObstacleFacing.West || facing == ObstacleFacing.East;
        private float SizeX => FacesAlongX ? footprintDepth : footprintWidth;
        private float SizeY => FacesAlongX ? footprintWidth : footprintDepth;

        //How far the highest point of the box sits above its base, in world units.
        public float TopEdgeHeight => obstacleHeight + CurrentTileHeight * 0.25f * (SizeX + SizeY);

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

        //Applies the map, facing, and drawing order chosen by ObstacleGridControl.
        public void Configure(IsometricMapTemplate map, ObstacleFacing newFacing, int newSortingOrder)
        {
            mapTemplate = map;
            facing = newFacing;
            sortingOrder = newSortingOrder;
            RebuildMesh();
        }

        //Returns the four base corners so a collider can match the footprint.
        public Vector2[] GetFootprintPoints()
        {
            GetFootprintCorners(out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d);
            return new Vector2[] { a, b, c, d };
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

            GetFootprintCorners(out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d);
            Vector3 up = Vector3.up * obstacleHeight;

            //The right side faces grid south and the left side faces grid west, so the front color lands on whichever one matches.
            Color right = ShadeSide(facing == ObstacleFacing.South ? frontFaceColor : topColor, rightSideShade);
            Color left = ShadeSide(facing == ObstacleFacing.West ? frontFaceColor : topColor, leftSideShade);

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

        //Finds the base corners: a is the back (top of screen), b right, c front (bottom of screen), d left.
        private void GetFootprintCorners(out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d)
        {
            //Grid directions on screen, matching IsometricMapTemplate.GridToWorld.
            Vector3 x = new Vector3(CurrentTileWidth * 0.5f, CurrentTileHeight * 0.5f, 0f) * SizeX * 0.5f;
            Vector3 y = new Vector3(-CurrentTileWidth * 0.5f, CurrentTileHeight * 0.5f, 0f) * SizeY * 0.5f;
            a = x + y;
            b = x - y;
            c = -x - y;
            d = -x + y;
        }

        //Darkens a side color while keeping its transparency.
        private static Color ShadeSide(Color color, float shade)
        {
            Color shaded = color * shade;
            shaded.a = color.a;
            return shaded;
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
