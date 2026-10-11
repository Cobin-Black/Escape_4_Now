using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Map
{
    //Draws a connected school floor plan using the existing diamond-shaped tiles.
    public sealed class IsometricMapTemplate : MonoBehaviour
    {
        //Map size, tile shape, and floor colors.
        [SerializeField, HideInInspector] private int width = SchoolMapLayout.Size;
        [SerializeField, HideInInspector] private int height = SchoolMapLayout.Size;
        [SerializeField, Min(0.1f)] private float tileWidth = 1.2f;
        [SerializeField, Min(0.1f)] private float tileHeight = 0.6f;
        [SerializeField, Min(0f)] private float tileDepth = 0.28f;
        [SerializeField] private Color floorColor = new Color(0.02f, 0.02f, 0.02f, 1f);
        [SerializeField] private Color alternateFloorColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        [SerializeField] private Color rightSideColor = new Color(0.16f, 0.16f, 0.16f, 1f);
        [SerializeField] private Color leftSideColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        [SerializeField] private bool generateOnStart = true;
        //A prefab is a reusable template. The map makes a copy for each grid space.
        [SerializeField] private IsometricMapTile floorTilePrefab;

        //Exit placement and how much the front walls fade when a player stands behind them.
        [SerializeField] private bool hasExit = false;
        [SerializeField] private Vector2Int exitGridPosition = new Vector2Int(9, 0);
        [SerializeField] private Color exitColor = new Color(0.2f, 0.75f, 0.35f);
        [SerializeField, Range(0f, 1f)] private float nearWallFadedAlpha = 0.15f;

        //Shared drawing material and the last screen size.
        private Material tileMaterial;
        private int screenWidth;
        private int screenHeight;
        private SchoolMapLayout layout;

        //Generated tiles by grid address and which addresses currently hold a player.
        private readonly Dictionary<Vector2Int, IsometricMapTile> tileLookup = new Dictionary<Vector2Int, IsometricMapTile>();
        private readonly HashSet<Vector2Int> occupiedPositions = new HashSet<Vector2Int>();

        //Addresses covered by obstacles, which players cannot walk onto.
        private readonly HashSet<Vector2Int> blockedPositions = new HashSet<Vector2Int>();

        //Tells obstacles and other listeners when a player changes tiles.
        public event System.Action OccupantsChanged;

        //Read-only sizes used by the player and other scripts.
        public int Width => SchoolMapLayout.Size;
        public int Height => SchoolMapLayout.Size;
        public float TileWidth => tileWidth;
        public float TileHeight => tileHeight;

        //Shares read-only room bounds and themes with other systems.
        public IReadOnlyList<SchoolRoom> Rooms { get { PrepareLayout(); return layout.Rooms; } }
        public int LayoutSeed { get { PrepareLayout(); return layout.Seed; } }

        //Creates grid data before player placement. Drawing later uses this same layout.
        public void PrepareLayout()
        {
            if (layout != null) return;
            ValidateSettings();
            layout = new SchoolMapLayout(System.Guid.NewGuid().GetHashCode());
        }

        //Reports the base floor or wall without adding any gameplay objects.
        public SchoolTileType GetTileType(Vector2Int cell)
        {
            PrepareLayout();
            return layout.GetTile(cell.x, cell.y);
        }

        //Returns the school room theme, or None for a hallway or wall.
        public SchoolRoomType GetRoomType(Vector2Int cell)
        {
            PrepareLayout();
            return layout.GetRoomType(cell.x, cell.y);
        }

        //Returns open floor addresses. The caller can reserve chosen tiles for its own objects.
        public List<Vector2Int> GetAvailableFloorTiles(ISet<Vector2Int> reserved = null)
        {
            PrepareLayout();
            var cells = new List<Vector2Int>();
            for (int y = 1; y < height - 1; y++)
                for (int x = 1; x < width - 1; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (IsWalkable(cell) && !IsExit(cell) && !IsOccupied(cell)
                        && (reserved == null || !reserved.Contains(cell))) cells.Add(cell);
                }
            return cells;
        }

        //Fails cleanly when every valid floor tile has already been taken.
        public bool TryGetRandomFloor(out Vector2Int cell, ISet<Vector2Int> reserved = null)
        {
            List<Vector2Int> cells = GetAvailableFloorTiles(reserved);
            cell = default;
            if (cells.Count == 0) return false;
            cell = cells[Random.Range(0, cells.Count)];
            return true;
        }

        //Creates the tiles when Play mode starts, not while editing the scene.
        private System.Collections.IEnumerator Start()
        {
            if (generateOnStart)
            {
                GenerateBlankMap();
            }
            //Wait for existing spawners before choosing a clear approach to the door.
            MapEventController events = GetComponent<MapEventController>();
            Escape4Now.Items.ItemSpawner items = GetComponent<Escape4Now.Items.ItemSpawner>();
            yield return new WaitUntil(() =>
                (events == null || !events.isActiveAndEnabled || events.EventsPlaced)
                && (items == null || !items.isActiveAndEnabled || items.HasSpawnedItems));
            yield return null;
            AddExitDoor();
        }

        //Places the door on a clear outside wall and connects it to the existing exit rules.
        private void AddExitDoor()
        {
            var candidates = new List<Vector2Int>();
            MapEventController events = GetComponent<MapEventController>();
            Escape4Now.Items.Item[] items = FindObjectsByType<Escape4Now.Items.Item>(FindObjectsSortMode.None);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!IsBorder(cell) || ((x == 0 || x == width - 1) && (y == 0 || y == height - 1))) continue;
                    Vector2Int inside = new Vector2Int(Mathf.Clamp(x, 1, width - 2), Mathf.Clamp(y, 1, height - 2));
                    if (!IsInteriorFloor(inside) || !IsWalkable(inside) || IsOccupied(inside)
                        || (events != null && events.HasEvent(inside))) continue;
                    bool hasItem = false;
                    foreach (Escape4Now.Items.Item item in items)
                        if (item.IsAtPosition(inside)) { hasItem = true; break; }
                    if (!hasItem && tileLookup.ContainsKey(cell)) candidates.Add(cell);
                }
            if (candidates.Count == 0) return;
            Vector2Int chosen = candidates[Random.Range(0, candidates.Count)];
            exitGridPosition = chosen;
            hasExit = true;
            Transform tile = tileLookup[chosen].transform;
            //Replace only this tile's raised wall, keeping its floor and click area.
            tileLookup[chosen].SetWallRenderers(null, null);
            foreach (Transform child in tile)
            {
                if (child.name != "Wall" && child.name != "Wall cap"
                    && child.name != "Exit" && child.name != "Exit cap") continue;
                child.gameObject.SetActive(false);
                MeshFilter wallMesh = child.GetComponent<MeshFilter>();
                if (wallMesh != null && wallMesh.sharedMesh != null) Destroy(wallMesh.sharedMesh);
                Destroy(child.gameObject);
            }
            MeshFilter floorMesh = tile.GetComponent<MeshFilter>();
            Destroy(floorMesh.sharedMesh);
            floorMesh.sharedMesh = CreateTileMesh(chosen);
            bool alongX = chosen.x == 0 || chosen.x == width - 1;
            Vector2 across = alongX ? Vector2.up : Vector2.right;
            int order = 500 - (chosen.x + chosen.y) * 10;
            Color frame = new Color(0.2f, 0.24f, 0.23f);
            CreateBlock(tile, "Exit door post", across * 0.44f, 0.12f, 0.12f, 0f, 0.85f, frame, order + 2);
            CreateBlock(tile, "Exit door post", across * -0.44f, 0.12f, 0.12f, 0f, 0.85f, frame, order + 2);
            CreateBlock(tile, "Exit door", Vector2.zero, alongX ? 0.09f : 0.76f,
                alongX ? 0.76f : 0.09f, 0f, 0.8f, exitColor, order + 3);
            CreateBlock(tile, "Exit door header", Vector2.zero, alongX ? 0.14f : 1f,
                alongX ? 1f : 0.14f, 0.8f, 0.12f, frame, order + 4);
            var sign = new GameObject("Exit sign");
            sign.transform.SetParent(tile, false);
            sign.transform.localPosition = new Vector3(0f, 1.07f, -0.1f);
            TextMesh text = sign.AddComponent<TextMesh>();
            text.text = "EXIT";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 40;
            text.characterSize = 0.08f;
            text.anchor = TextAnchor.MiddleCenter;
            MeshRenderer renderer = sign.GetComponent<MeshRenderer>();
            if (text.font != null) renderer.sharedMaterial = text.font.material;
            renderer.sortingOrder = order + 5;
        }

        //Checks map settings after changes in the Inspector.
        private void OnValidate()
        {
            ValidateSettings();
        }

        //Keeps map sizes within limits before creating tiles.
        private void ValidateSettings()
        {
            width = height = SchoolMapLayout.Size;
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

        //Turns a grid address, such as (2, 3), into a position on the game map.
        public Vector3 GridToWorld(Vector2Int gridPosition)
        {
            float worldX = (gridPosition.x - gridPosition.y) * tileWidth * 0.5f;
            float worldY = (gridPosition.x + gridPosition.y) * tileHeight * 0.5f;
            return transform.position + new Vector3(worldX, worldY, 0f);
        }

        //Turns a position on the map into its nearest grid address.
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

        //Finds which tile contains a position. Returns false if it is off the map.
        public bool TryGetGridPosition(Vector3 worldPosition, out Vector2Int gridPosition)
        {
            // Check each diamond to find the one containing the supplied position.
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

        //Includes room walls and simple solid blocks as well as the outside border.
        private bool IsWall(Vector2Int cell)
        {
            return GetTileType(cell) == SchoolTileType.Wall && !IsExit(cell);
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
            return IsInsideMap(cell) && !IsBorder(cell) && GetTileType(cell) == SchoolTileType.Floor;
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

        //Fades a low wall when a player stands directly behind it, including room walls.
        private void RefreshNearWallVisibility()
        {
            foreach (KeyValuePair<Vector2Int, IsometricMapTile> entry in tileLookup)
            {
                Vector2Int cell = entry.Key;
                if (!IsWall(cell))
                {
                    continue;
                }

                bool playerBehindWall = occupiedPositions.Contains(new Vector2Int(cell.x + 1, cell.y))
                    || occupiedPositions.Contains(new Vector2Int(cell.x, cell.y + 1));

                entry.Value.SetWallAlpha(playerBehindWall ? nearWallFadedAlpha : 1f);
            }
        }

        //Finds a shortest path around walls and obstacles, or reports that no path exists.
        public bool TryFindPath(Vector2Int start, Vector2Int end, out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();

            if (!IsWalkable(start) || !IsWalkable(end) || start == end)
            {
                return false;
            }

            //Check nearby tiles first and remember where each step came from.
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
                    //Trace the steps back to the start, then reverse them into walking order.
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

        //Keeps the old method name for scene compatibility, but now draws the school layout.
        public void GenerateBlankMap()
        {
            //Do not build tiles while editing, so they are not saved in the shared scene.
            if (!Application.isPlaying) return;
            if (floorTilePrefab == null || !floorTilePrefab.gameObject.activeSelf
                || floorTilePrefab.GetComponent<MeshFilter>() == null
                || floorTilePrefab.GetComponent<MeshRenderer>() == null
                || floorTilePrefab.GetComponent<PolygonCollider2D>() == null)
            {
                Debug.LogError("Assign a floor tile prefab with a mesh, renderer, and polygon collider.", this);
                return;
            }

            PrepareLayout();
            ClearGeneratedTiles();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    CreateTile(new Vector2Int(x, y));
                }
            }
            AddRoomLabels();
            RefreshNearWallVisibility();
            FitCamera();
        }

        //Labels the four areas without adding furniture or gameplay rules.
        private void AddRoomLabels()
        {
            foreach (SchoolRoom room in Rooms)
            {
                var center = new Vector2Int(room.CenterX, room.CenterY);
                var labelObject = new GameObject("Room Label");
                labelObject.transform.SetParent(tileLookup[center].transform, false);
                labelObject.transform.localPosition = new Vector3(0f, -0.35f, -0.1f);
                TextMesh label = labelObject.AddComponent<TextMesh>();
                label.text = room.Type == SchoolRoomType.ComputerLab ? "COMPUTER LAB"
                    : room.Type == SchoolRoomType.ScienceLab ? "SCIENCE LAB" : room.Type.ToString().ToUpperInvariant();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 40;
                label.characterSize = 0.09f;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.color = Color.Lerp(RoomColor(room.Type), Color.white, 0.65f);
                MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
                if (label.font != null) renderer.sharedMaterial = label.font.material;
                //Area names stay readable over the low wall blocks.
                renderer.sortingOrder = 1000;
            }
        }

        //Gives each school area a quiet identifying floor color.
        private static Color RoomColor(SchoolRoomType room)
        {
            switch (room)
            {
                case SchoolRoomType.Library: return new Color(0.19f, 0.35f, 0.29f);
                case SchoolRoomType.ComputerLab: return new Color(0.2f, 0.3f, 0.43f);
                case SchoolRoomType.Classroom: return new Color(0.43f, 0.32f, 0.17f);
                case SchoolRoomType.ScienceLab: return new Color(0.36f, 0.23f, 0.33f);
                default: return new Color(0.19f, 0.2f, 0.22f);
            }
        }

        //Copies the tile template, sets its position and color, and adds a border if needed.
        private void CreateTile(Vector2Int gridPosition)
        {
            IsometricMapTile mapTile = Instantiate(floorTilePrefab, transform);
            GameObject tile = mapTile.gameObject;
            tile.name = $"Tile {gridPosition.x}, {gridPosition.y}";
            tile.transform.position = GridToWorld(gridPosition);

            MeshFilter meshFilter = tile.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateTileMesh(gridPosition);

            MeshRenderer meshRenderer = tile.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = CreateTileMaterial();
            meshRenderer.sortingOrder = -1000 - gridPosition.x - gridPosition.y;

            PolygonCollider2D tileCollider = tile.GetComponent<PolygonCollider2D>();
            tileCollider.points = CreateTileColliderPoints();
            tileCollider.isTrigger = true;

            mapTile.SetGridPosition(gridPosition);
            tileLookup[gridPosition] = mapTile;

            AddBorderWall(tile.transform, gridPosition, mapTile);
            AddVisualDoor(tile.transform, gridPosition);
        }

        //Checks whether a floor tile is being used as a doorway into a room.
        public bool IsDoorway(Vector2Int cell)
        {
            if (GetTileType(cell) != SchoolTileType.Floor
                || GetRoomType(cell) != SchoolRoomType.None)
            {
                return false;
            }

            Vector2Int[] directions =
            {
        Vector2Int.right,
        Vector2Int.up,
        Vector2Int.left,
        Vector2Int.down
    };

            foreach (Vector2Int direction in directions)
            {
                if (GetRoomType(cell + direction) != SchoolRoomType.None)
                {
                    return true;
                }
            }

            return false;
        }

        //Adds an open door at a room entrance. It has no collider or opening rules.
        private void AddVisualDoor(Transform tile, Vector2Int cell)
        {
            if (GetTileType(cell) != SchoolTileType.Floor || GetRoomType(cell) != SchoolRoomType.None) return;
            foreach (Vector2Int direction in new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down })
            {
                if (GetRoomType(cell + direction) == SchoolRoomType.None) continue;
                Vector2 across = new Vector2(-direction.y, direction.x);
                bool alongX = direction.x != 0;
                int order = 500 - (cell.x + cell.y) * 10;
                Color frame = new Color(0.26f, 0.3f, 0.29f);
                CreateBlock(tile, "Door post", across * 0.43f, 0.1f, 0.1f, 0f, 0.65f, frame, order);
                CreateBlock(tile, "Door post", across * -0.43f, 0.1f, 0.1f, 0f, 0.65f, frame, order);
                CreateBlock(tile, "Door frame", Vector2.zero, alongX ? 0.1f : 0.96f,
                    alongX ? 0.96f : 0.1f, 0.65f, 0.08f, frame, order + 1);
                Vector2 offset = across * 0.37f + new Vector2(direction.x, direction.y) * 0.25f;
                CreateBlock(tile, "Open door", offset, alongX ? 0.65f : 0.07f,
                    alongX ? 0.07f : 0.65f, 0.02f, 0.58f, new Color(0.12f, 0.38f, 0.34f), order);
                return;
            }
        }

        //Draws the outer border and thin walls around the rooms.
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
                if (!IsBorder(cell))
                {
                    AddRoomWallEdges(tile, cell, order);
                    return;
                }
                // All borders use the same low height to keep the floor visible.
                const float wallHeight = 0.22f;
                Renderer wall = CreateBlock(tile, "Wall", Vector2.zero, 0.98f, 0.98f, 0f, wallHeight,
                    new Color(0.18f, 0.22f, 0.21f), order);
                Renderer wallCap = CreateBlock(tile, "Wall cap", Vector2.zero, 1f, 1f, wallHeight, 0.06f,
                    new Color(0.28f, 0.32f, 0.3f), order + 1);

                //Room walls can also stand in front of a player.
                mapTile.SetWallRenderers(wall, wallCap);
            }
        }

        //Only raises the edges next to a floor, leaving unused areas flat and dark.
        private void AddRoomWallEdges(Transform tile, Vector2Int cell, int order)
        {
            foreach (Vector2Int direction in new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down })
            {
                if (GetTileType(cell + direction) != SchoolTileType.Floor) continue;
                Vector2 offset = new Vector2(direction.x, direction.y) * 0.44f;
                float sizeX = direction.x != 0 ? 0.12f : 1f;
                float sizeY = direction.y != 0 ? 0.12f : 1f;
                CreateBlock(tile, "Room wall", offset, sizeX, sizeY, 0f, 0.22f,
                    new Color(0.18f, 0.22f, 0.21f), order);
                CreateBlock(tile, "Room wall cap", offset, sizeX, sizeY, 0.22f, 0.04f,
                    new Color(0.28f, 0.32f, 0.3f), order + 1);
            }
        }

        //Makes a raised block from triangles, with darker sides to show its depth.
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

        //Makes the tile shape from triangles and alternates colors for the checkerboard.
        private Mesh CreateTileMesh(Vector2Int gridPosition)
        {
            Color topColor = (gridPosition.x + gridPosition.y) % 2 == 0 ? floorColor : alternateFloorColor;
            if (GetTileType(gridPosition) == SchoolTileType.Wall)
                topColor = new Color(0.045f, 0.05f, 0.055f);
            if (IsExit(gridPosition)) topColor = RoomColor(SchoolRoomType.None);
            if (GetTileType(gridPosition) == SchoolTileType.Floor)
            {
                topColor = RoomColor(GetRoomType(gridPosition));
                if ((gridPosition.x + gridPosition.y) % 2 == 0) topColor *= 0.86f;
                topColor.a = 1f;
            }
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

        // Removes only generated tiles and their shapes before building the grid again.
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

        //Releases only the generated floor shapes and shared drawing material.
        private void OnDestroy()
        {
            ClearGeneratedTiles();
            DestroyGeneratedObject(tileMaterial);
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

        // Shows guide lines in the editor without creating saved tiles.
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

