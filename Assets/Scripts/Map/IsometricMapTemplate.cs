using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Map
{
    // Builds the floor and border walls on an isometric grid.
    [ExecuteAlways]
    public sealed class IsometricMapTemplate : MonoBehaviour
    {
        //Map size, tile shape, and floor colors.
        [SerializeField, Range(12, 40)] private int width = 18;
        [SerializeField, Range(12, 40)] private int height = 18;
        [SerializeField, Min(0.1f)] private float tileWidth = 1.2f;
        [SerializeField, Min(0.1f)] private float tileHeight = 0.6f;
        [SerializeField, Min(0f)] private float tileDepth = 0.28f;
        [SerializeField] private Color floorColor = new Color(0.02f, 0.02f, 0.02f, 1f);
        [SerializeField] private Color alternateFloorColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        [SerializeField] private Color rightSideColor = new Color(0.16f, 0.16f, 0.16f, 1f);
        [SerializeField] private Color leftSideColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        [SerializeField] private bool generateOnStart = true;

        //Exit placement and how much the front walls fade when a player stands behind them.
        [SerializeField] private bool hasExit = true;
        [SerializeField] private Vector2Int exitGridPosition = new Vector2Int(9, 0);
        [SerializeField] private Color exitColor = new Color(0.2f, 0.75f, 0.35f);
        [SerializeField, Range(0f, 1f)] private float nearWallFadedAlpha = 0.15f;

        //Shared drawing material and the last screen size.
        private Material tileMaterial;
        private int screenWidth;
        private int screenHeight;

        //Generated tiles by grid address and which addresses currently hold a player.
        private readonly Dictionary<Vector2Int, IsometricMapTile> tileLookup = new Dictionary<Vector2Int, IsometricMapTile>();
        private readonly HashSet<Vector2Int> occupiedPositions = new HashSet<Vector2Int>();

        //Addresses covered by obstacles, which players cannot walk onto.
        private readonly HashSet<Vector2Int> blockedPositions = new HashSet<Vector2Int>();

        //Tells obstacles and other listeners when a player changes tiles.
        public event System.Action OccupantsChanged;

        //Read-only sizes used by the player and other scripts.
        public int Width => width;
        public int Height => height;
        public float TileWidth => tileWidth;
        public float TileHeight => tileHeight;

        //Creates the map when the scene starts.
        private void Start()
        {
            if (generateOnStart)
            {
                GenerateBlankMap();
            }
        }

        //Checks map settings after changes in the Inspector.
        private void OnValidate()
        {
            ValidateSettings();
        }

        //Keeps map sizes within limits before creating tiles.
        private void ValidateSettings()
        {
            width = Mathf.Clamp(width, 12, 40);
            height = Mathf.Clamp(height, 12, 40);
            tileWidth = float.IsNaN(tileWidth) ? 1.2f : Mathf.Clamp(tileWidth, 0.1f, 10f);
            tileHeight = float.IsNaN(tileHeight) ? 0.6f : Mathf.Clamp(tileHeight, 0.1f, 10f);
            tileDepth = float.IsNaN(tileDepth) ? 0.28f : Mathf.Clamp(tileDepth, 0f, 10f);

            if (hasExit)
            {
                exitGridPosition = ClampToValidExitPosition(exitGridPosition);
            }
        }

        //Moves an exit request onto the nearest border tile that is not a corner.
        private Vector2Int ClampToValidExitPosition(Vector2Int cell)
        {
            int x = Mathf.Clamp(cell.x, 0, width - 1);
            int y = Mathf.Clamp(cell.y, 0, height - 1);

            int distanceToLeft = x;
            int distanceToRight = width - 1 - x;
            int distanceToBottom = y;
            int distanceToTop = height - 1 - y;
            int closest = Mathf.Min(Mathf.Min(distanceToLeft, distanceToRight), Mathf.Min(distanceToBottom, distanceToTop));

            bool snappedToHorizontalBorder = closest == distanceToBottom || closest == distanceToTop;
            if (snappedToHorizontalBorder)
            {
                y = closest == distanceToBottom ? 0 : height - 1;
                x = Mathf.Clamp(x, 1, width - 2);
            }
            else
            {
                x = closest == distanceToLeft ? 0 : width - 1;
                y = Mathf.Clamp(y, 1, height - 2);
            }

            return new Vector2Int(x, y);
        }

        //Converts a tile address into a position in the scene.
        public Vector3 GridToWorld(Vector2Int gridPosition)
        {
            float worldX = (gridPosition.x - gridPosition.y) * tileWidth * 0.5f;
            float worldY = (gridPosition.x + gridPosition.y) * tileHeight * 0.5f;
            return transform.position + new Vector3(worldX, worldY, 0f);
        }

        //Finds the nearest tile address, including positions outside the map.
        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            if (TryGetGridPosition(worldPosition, out Vector2Int gridPosition))
            {
                return gridPosition;
            }

            Vector3 localPosition = worldPosition - transform.position;
            float gridXMinusGridY = localPosition.x / (tileWidth * 0.5f);
            float gridXPlusGridY = localPosition.y / (tileHeight * 0.5f);
            int gridX = Mathf.RoundToInt((gridXMinusGridY + gridXPlusGridY) * 0.5f);
            int gridY = Mathf.RoundToInt((gridXPlusGridY - gridXMinusGridY) * 0.5f);

            return new Vector2Int(gridX, gridY);
        }

        //Finds the tile under a position and rejects positions outside the floor.
        public bool TryGetGridPosition(Vector3 worldPosition, out Vector2Int gridPosition)
        {
            // Check each tile and find the one the mouse is inside.
            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    Vector2Int currentGridPosition = new Vector2Int(x, y);
                    if (IsInsideTileFootprint(worldPosition, currentGridPosition))
                    {
                        gridPosition = currentGridPosition;
                        return true;
                    }
                }
            }

            gridPosition = Vector2Int.zero;
            return false;
        }

        //Checks whether the tile is inside the map.
        public bool IsInsideMap(Vector2Int gridPosition)
        {
            return gridPosition.x >= 0
                && gridPosition.x < width
                && gridPosition.y >= 0
                && gridPosition.y < height;
        }

        //Marks the outer tiles as walls, except where the exit replaces one.
        private bool IsWall(Vector2Int cell)
        {
            return IsBorder(cell) && !IsExit(cell);
        }

        //Checks whether a tile sits on the outer edge of the map.
        private bool IsBorder(Vector2Int cell)
        {
            return cell.x == 0 || cell.y == 0 || cell.x == width - 1 || cell.y == height - 1;
        }

        //Checks whether a tile is the exit that ends the game when reached.
        public bool IsExit(Vector2Int cell)
        {
            return hasExit && cell == exitGridPosition;
        }

        //Checks whether a tile is a floor tile inside the border walls.
        public bool IsInteriorFloor(Vector2Int cell)
        {
            return IsInsideMap(cell) && !IsBorder(cell);
        }

        //Allows movement on floor tiles inside the border walls, and onto the exit, unless an obstacle is there.
        public bool IsWalkable(Vector2Int cell)
        {
            return IsInsideMap(cell) && !IsWall(cell) && !blockedPositions.Contains(cell);
        }

        //Checks whether a player is standing on a tile.
        public bool IsOccupied(Vector2Int cell)
        {
            return occupiedPositions.Contains(cell);
        }

        //Checks whether an obstacle already covers a tile.
        public bool IsBlocked(Vector2Int cell)
        {
            return blockedPositions.Contains(cell);
        }

        //Marks a tile as covered by an obstacle. Returns false if another obstacle is already there.
        public bool RegisterBlockedPosition(Vector2Int cell)
        {
            return blockedPositions.Add(cell);
        }

        //Frees a tile when its obstacle moves or is removed.
        public void UnregisterBlockedPosition(Vector2Int cell)
        {
            blockedPositions.Remove(cell);
        }

        //Tracks where a player stands so the front walls beside it can fade, then refreshes them.
        public void SetOccupantPosition(Vector2Int previousPosition, Vector2Int newPosition)
        {
            occupiedPositions.Remove(previousPosition);
            occupiedPositions.Add(newPosition);
            RefreshNearWallVisibility();
            OccupantsChanged?.Invoke();
        }

        //Stops tracking a player, such as when it is destroyed.
        public void RemoveOccupant(Vector2Int position)
        {
            occupiedPositions.Remove(position);
            RefreshNearWallVisibility();
            OccupantsChanged?.Invoke();
        }

        //Fades the front-left and front-bottom border walls whenever a player stands directly behind them.
        private void RefreshNearWallVisibility()
        {
            foreach (KeyValuePair<Vector2Int, IsometricMapTile> entry in tileLookup)
            {
                Vector2Int cell = entry.Key;
                if (cell.x != 0 && cell.y != 0)
                {
                    continue;
                }

                bool playerBehindWall = false;
                if (cell.x == 0)
                {
                    playerBehindWall |= occupiedPositions.Contains(new Vector2Int(cell.x + 1, cell.y));
                }

                if (cell.y == 0)
                {
                    playerBehindWall |= occupiedPositions.Contains(new Vector2Int(cell.x, cell.y + 1));
                }

                entry.Value.SetWallAlpha(playerBehindWall ? nearWallFadedAlpha : 1f);
            }
        }

        //Finds a shortest route through open tiles or returns false if none exists.
        public bool TryFindPath(Vector2Int start, Vector2Int end, out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();

            if (!IsWalkable(start) || !IsWalkable(end) || start == end)
            {
                return false;
            }

            //Search each nearby tile once until the destination is reached.
            Queue<Vector2Int> pending = new Queue<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> previous = new Dictionary<Vector2Int, Vector2Int>();
            Vector2Int[] steps = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

            pending.Enqueue(start);
            previous[start] = start;

            while (pending.Count > 0)
            {
                Vector2Int cell = pending.Dequeue();
                if (cell == end)
                {
                    //Follow the saved steps backward, then put them in walking order.
                    Vector2Int step = end;
                    while (step != start)
                    {
                        path.Add(step);
                        step = previous[step];
                    }

                    path.Reverse();
                    return true;
                }
                foreach (Vector2Int step in steps)
                {
                    Vector2Int next = cell + step;
                    if (!IsWalkable(next) || previous.ContainsKey(next))
                    {
                        continue;
                    }

                    previous[next] = cell;
                    pending.Enqueue(next);
                }
            }
            return false;
        }

        //Checks whether a point is inside a tile's diamond shape.
        private bool IsInsideTileFootprint(Vector3 worldPosition, Vector2Int gridPosition)
        {
            Vector3 tileCenter = GridToWorld(gridPosition);
            float localX = Mathf.Abs(worldPosition.x - tileCenter.x);
            float localY = Mathf.Abs(worldPosition.y - tileCenter.y);

            return localX / (tileWidth * 0.5f) + localY / (tileHeight * 0.5f) <= 1f;
        }

        //Replaces the old tiles and fits the camera around the new map.
        public void GenerateBlankMap()
        {
            ValidateSettings();
            ClearGeneratedTiles();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    CreateTile(new Vector2Int(x, y));
                }
            }
            FitCamera();
        }

        //Creates one floor tile, its click area, and its border wall if needed.
        private void CreateTile(Vector2Int gridPosition)
        {
            GameObject tile = new GameObject($"Tile {gridPosition.x}, {gridPosition.y}");
            tile.transform.SetParent(transform, false);
            tile.transform.position = GridToWorld(gridPosition);

            MeshFilter meshFilter = tile.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateTileMesh(gridPosition);

            MeshRenderer meshRenderer = tile.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = CreateTileMaterial();
            meshRenderer.sortingOrder = -1000 - gridPosition.x - gridPosition.y;

            PolygonCollider2D tileCollider = tile.AddComponent<PolygonCollider2D>();
            tileCollider.points = CreateTileColliderPoints();
            tileCollider.isTrigger = true;

            IsometricMapTile mapTile = tile.AddComponent<IsometricMapTile>();
            mapTile.SetGridPosition(gridPosition);
            tileLookup[gridPosition] = mapTile;

            AddBorderWall(tile.transform, gridPosition, mapTile);
        }

        //Adds a low wall, or the exit, to tiles along the map border.
        private void AddBorderWall(Transform tile, Vector2Int cell, IsometricMapTile mapTile)
        {
            int order = 500 - (cell.x + cell.y) * 10;

            if (IsExit(cell))
            {
                // The exit sits in the same spot a wall would, so it uses the same low height.
                const float exitHeight = 0.22f;
                Color exitCapColor = Color.Lerp(exitColor, Color.white, 0.35f);
                CreateBlock(tile, "Exit", Vector2.zero, 0.98f, 0.98f, 0f, exitHeight, exitColor, order);
                CreateBlock(tile, "Exit cap", Vector2.zero, 1f, 1f, exitHeight, 0.06f, exitCapColor, order + 1);
                return;
            }

            if (IsWall(cell))
            {
                // All borders use the same low height to keep the floor visible.
                const float wallHeight = 0.22f;
                Renderer wall = CreateBlock(tile, "Wall", Vector2.zero, 0.98f, 0.98f, 0f, wallHeight,
                    new Color(0.33f, 0.36f, 0.37f), order);
                Renderer wallCap = CreateBlock(tile, "Wall cap", Vector2.zero, 1f, 1f, wallHeight, 0.06f,
                    new Color(0.48f, 0.5f, 0.49f), order + 1);

                // Only the front-left and front-bottom walls can ever hide a player, so only they need to fade.
                if (cell.x == 0 || cell.y == 0)
                {
                    mapTile.SetWallRenderers(wall, wallCap);
                }
            }
        }

        //Draws a raised box with a top and two shaded sides.
        private Renderer CreateBlock(Transform parent, string label, Vector2 offset, float sizeX,
            float sizeY, float bottom, float blockHeight, Color color, int order)
        {
            Vector3 x = new Vector3(tileWidth * 0.5f, tileHeight * 0.5f, 0f);
            Vector3 y = new Vector3(-tileWidth * 0.5f, tileHeight * 0.5f, 0f);
            Vector3 center = x * offset.x + y * offset.y + Vector3.up * bottom;
            Vector3 a = center + x * sizeX * 0.5f + y * sizeY * 0.5f;
            Vector3 b = center + x * sizeX * 0.5f - y * sizeY * 0.5f;
            Vector3 c = center - x * sizeX * 0.5f - y * sizeY * 0.5f;
            Vector3 d = center - x * sizeX * 0.5f + y * sizeY * 0.5f;
            Vector3 up = Vector3.up * blockHeight;
            Color right = color * 0.7f;
            Color left = color * 0.48f;
            right.a = left.a = 1f;
            Mesh mesh = new Mesh
            {
                vertices = new[] { a + up, b + up, c + up, d + up,
                    b + up, b, c, c + up, c + up, c, d, d + up },
                triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7, 8, 9, 10, 8, 10, 11 },
                colors = new[] { color, color, color, color, right, right, right, right, left, left, left, left }
            };
            mesh.RecalculateBounds();
            GameObject block = new GameObject(label);
            block.transform.SetParent(parent, false);
            block.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = block.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateTileMaterial();
            renderer.sortingOrder = order;
            return renderer;
        }

        //Fits the camera again when the game window changes size.
        private void Update()
        {
            bool screenSizeChanged = screenWidth != Screen.width || screenHeight != Screen.height;
            if (Application.isPlaying && screenSizeChanged)
            {
                FitCamera();
            }
        }

        //Centers the camera and keeps the full map in view.
        private void FitCamera()
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic)
            {
                return;
            }

            Vector3 center = (GridToWorld(Vector2Int.zero) + GridToWorld(new Vector2Int(width - 1, height - 1))) * 0.5f;
            camera.transform.position = new Vector3(center.x, center.y + 0.4f, camera.transform.position.z);
            float halfWidth = (width + height) * tileWidth * 0.25f;
            float halfHeight = (width + height) * tileHeight * 0.25f + 0.85f;
            camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.1f, camera.aspect)) + 0.45f;
            screenWidth = Screen.width;
            screenHeight = Screen.height;
        }

        //Builds the floor diamond and any visible outside edges.
        private Mesh CreateTileMesh(Vector2Int gridPosition)
        {
            Color topColor = (gridPosition.x + gridPosition.y) % 2 == 0 ? floorColor : alternateFloorColor;
            List<int> faces = new List<int> { 0, 1, 2, 0, 2, 3 };
            //Only the outside edge of the floor needs a visible side.
            if (gridPosition.y == 0)
            {
                faces.AddRange(new[] { 1, 4, 5, 1, 5, 2 });
            }

            if (gridPosition.x == 0)
            {
                faces.AddRange(new[] { 2, 5, 6, 2, 6, 3 });
            }

            Mesh mesh = new Mesh()
            {
                vertices = new[]
                {
                    new Vector3(0f, tileHeight * 0.5f, 0f),
                    new Vector3(tileWidth * 0.5f, 0f, 0f),
                    new Vector3(0f, -tileHeight * 0.5f, 0f),
                    new Vector3(-tileWidth * 0.5f, 0f, 0f),
                    new Vector3(tileWidth * 0.5f, -tileDepth, 0f),
                    new Vector3(0f, -tileHeight * 0.5f - tileDepth, 0f),
                    new Vector3(-tileWidth * 0.5f, -tileDepth, 0f)
                },
                triangles = faces.ToArray(),
                colors = new[]
                {
                    topColor,
                    topColor,
                    topColor,
                    topColor,
                    rightSideColor,
                    rightSideColor,
                    leftSideColor
                }
            };

            mesh.RecalculateBounds();
            return mesh;
        }

        // Reuses one material for the floor and walls.
        private Material CreateTileMaterial()
        {
            if (tileMaterial == null)
            {
                tileMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            return tileMaterial;
        }

        // Returns the four corners of a tile's click area.
        private Vector2[] CreateTileColliderPoints()
        {
            return new[]
            {
                new Vector2(0f, tileHeight * 0.5f),
                new Vector2(tileWidth * 0.5f, 0f),
                new Vector2(0f, -tileHeight * 0.5f),
                new Vector2(-tileWidth * 0.5f, 0f)
            };
        }

        // Removes old generated tiles and releases their mesh data.
        private void ClearGeneratedTiles()
        {
            tileLookup.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Tile ") && child.GetComponent<IsometricMapTile>() != null)
                {
                    foreach (MeshFilter filter in child.GetComponentsInChildren<MeshFilter>())
                    {
                        DestroyGeneratedObject(filter.sharedMesh);
                    }

                    child.gameObject.SetActive(false);
                    DestroyGeneratedObject(child.gameObject);
                }
            }

        }

        // Removes a generated object in either Play mode or edit mode.
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

        // Shows the grid outlines while editing the scene.
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.95f, 0.95f, 0.95f, 0.35f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    DrawTileOutline(GridToWorld(new Vector2Int(x, y)));
                }
            }
        }

        // Draws the top and side outlines of one tile.
        private void DrawTileOutline(Vector3 center)
        {
            Vector3 top = center + new Vector3(0f, tileHeight * 0.5f, 0f);
            Vector3 right = center + new Vector3(tileWidth * 0.5f, 0f, 0f);
            Vector3 bottom = center + new Vector3(0f, -tileHeight * 0.5f, 0f);
            Vector3 left = center + new Vector3(-tileWidth * 0.5f, 0f, 0f);
            Vector3 lowerRight = right + new Vector3(0f, -tileDepth, 0f);
            Vector3 lowerBottom = bottom + new Vector3(0f, -tileDepth, 0f);
            Vector3 lowerLeft = left + new Vector3(0f, -tileDepth, 0f);

            Gizmos.DrawLine(top, right);
            Gizmos.DrawLine(right, bottom);
            Gizmos.DrawLine(bottom, left);
            Gizmos.DrawLine(left, top);
            Gizmos.DrawLine(right, lowerRight);
            Gizmos.DrawLine(bottom, lowerBottom);
            Gizmos.DrawLine(left, lowerLeft);
            Gizmos.DrawLine(lowerRight, lowerBottom);
            Gizmos.DrawLine(lowerBottom, lowerLeft);
        }
    }
}

