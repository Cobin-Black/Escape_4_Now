using System;
using System.Collections.Generic;

namespace Escape4Now.Map
{
    public enum SchoolTileType { Wall, Floor }
    public enum SchoolRoomType { None, Library, ComputerLab, Classroom, ScienceLab }
    public enum SchoolSpaceType { None, Bathroom, Storage, JanitorCloset, StudyArea, Office, LockerArea }

    [Serializable]
    public sealed class SchoolMapGenerationSettings
    {
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Layout Validation")]
        [UnityEngine.Range(128, 512)]
        [UnityEngine.Tooltip("Maximum layout attempts before using the deterministic fallback seed.")]
#endif
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField]
#endif
        private int generationAttempts = 128;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Optional Secondary Room")]
        [UnityEngine.Range(0f, 1f)]
        [UnityEngine.Tooltip("Chance that a generated map includes one optional small school room.")]
#endif
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField]
#endif
        private float secondaryRoomChance = 0.5f;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(1, 1000)]
        [UnityEngine.Tooltip("Placement attempts for fitting the optional room into a valid map.")]
#endif
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField]
#endif
        private int secondaryRoomPlacementAttempts = 160;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Main Room Size")]
        [UnityEngine.Range(6, 9)]
        [UnityEngine.Tooltip("Smallest starting width and height for each major room, before room growth.")]
        [UnityEngine.SerializeField]
#endif
        private int mainRoomMinimumSize = 6;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Range(7, 9)]
        [UnityEngine.Tooltip("Largest starting width and height for each major room, before room growth.")]
        [UnityEngine.SerializeField]
#endif
        private int mainRoomMaximumSize = 9;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Hallway Refinement"), UnityEngine.SerializeField, UnityEngine.Range(1, 2)]
#endif
        private int hallwayWideningPasses = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(0, 4), UnityEngine.Tooltip("Remove tiny fully enclosed hallway wall islands without touching room boundaries.")]
#endif
        private int maximumHallwayWallIsland = 2;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(0f, 1f)]
#endif
        private float secondEntranceChance = .5f;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Min(1)]
#endif
        private int hallwayLengthWeight = 4, hallwayBendWeight = 6;

        public int GenerationAttempts { get => generationAttempts; set => generationAttempts = value; }
        public float SecondaryRoomChance { get => secondaryRoomChance; set => secondaryRoomChance = value; }
        public int SecondaryRoomPlacementAttempts { get => secondaryRoomPlacementAttempts; set => secondaryRoomPlacementAttempts = value; }
        public int MainRoomMinimumSize { get => mainRoomMinimumSize; set => mainRoomMinimumSize = value; }
        public int MainRoomMaximumSize { get => mainRoomMaximumSize; set => mainRoomMaximumSize = value; }
        public int HallwayWideningPasses => hallwayWideningPasses;
        public int MaximumHallwayWallIsland => maximumHallwayWallIsland;
        public float SecondEntranceChance => secondEntranceChance;
        public int HallwayLengthWeight => hallwayLengthWeight;
        public int HallwayBendWeight => hallwayBendWeight;

        public SchoolMapGenerationSettings Copy()
        {
            return new SchoolMapGenerationSettings
            {
                GenerationAttempts = Math.Max(128, Math.Min(512, generationAttempts)),
                SecondaryRoomChance = float.IsNaN(secondaryRoomChance) ? 0.5f
                    : Math.Max(0f, Math.Min(1f, secondaryRoomChance)),
                SecondaryRoomPlacementAttempts = Math.Max(1, Math.Min(1000, secondaryRoomPlacementAttempts)),
                MainRoomMinimumSize = Math.Max(6, Math.Min(9, mainRoomMinimumSize)),
                MainRoomMaximumSize = Math.Max(Math.Max(7, Math.Min(9, mainRoomMinimumSize)),
                    Math.Max(6, Math.Min(9, mainRoomMaximumSize))),
                hallwayWideningPasses = Math.Max(1, Math.Min(2, hallwayWideningPasses)),
                maximumHallwayWallIsland = Math.Max(0, Math.Min(4, maximumHallwayWallIsland)),
                secondEntranceChance = float.IsNaN(secondEntranceChance) ? .5f : Math.Max(0f, Math.Min(1f, secondEntranceChance)),
                hallwayLengthWeight = Math.Max(1, Math.Min(100, hallwayLengthWeight)),
                hallwayBendWeight = Math.Max(1, Math.Min(100, hallwayBendWeight))
            };
        }

        public bool IsValid => generationAttempts >= 128 && generationAttempts <= 512
            && !float.IsNaN(secondaryRoomChance) && !float.IsInfinity(secondaryRoomChance)
            && secondaryRoomChance >= 0f && secondaryRoomChance <= 1f
            && secondaryRoomPlacementAttempts >= 1 && secondaryRoomPlacementAttempts <= 1000
            && mainRoomMinimumSize >= 6 && mainRoomMinimumSize <= mainRoomMaximumSize
            && mainRoomMaximumSize >= 7 && mainRoomMaximumSize <= 9
            && hallwayWideningPasses >= 1 && hallwayWideningPasses <= 2
            && maximumHallwayWallIsland >= 0 && maximumHallwayWallIsland <= 4
            && secondEntranceChance >= 0f && secondEntranceChance <= 1f
            && hallwayLengthWeight >= 1 && hallwayLengthWeight <= 100 && hallwayBendWeight >= 1 && hallwayBendWeight <= 100;
    }

    //A small optional school space, separate from the four main rooms.
    public sealed class SchoolSecondarySpace
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public SchoolSpaceType Type { get; }

        //Stores only layout information; secondary spaces have no special game rules.
        public SchoolSecondarySpace(int x, int y, int width, int height, SchoolSpaceType type)
        { X = x; Y = y; Width = width; Height = height; Type = type; }
    }

    //Describes one room without depending on scene objects.
    public sealed class SchoolRoom
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public int CenterX => X + Width / 2;
        public int CenterY => Y + Height / 2;
        public SchoolRoomType Type { get; }
        public int CutWidth { get; }
        public int CutHeight { get; }
        public int CutCorner { get; }
        public bool IsLShaped => CutWidth > 0 && CutHeight > 0;

        //Stores the room's bounds and school theme.
        public SchoolRoom(int x, int y, int width, int height, SchoolRoomType type,
            int cutWidth = 0, int cutHeight = 0, int cutCorner = 0)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Type = type;
            CutWidth = cutWidth;
            CutHeight = cutHeight;
            CutCorner = cutCorner;
        }

        //An L-shaped room leaves one corner empty but keeps its center as floor.
        public bool Contains(int x, int y)
        {
            int column = x - X;
            int row = y - Y;
            if (column < 0 || row < 0 || column >= Width || row >= Height) return false;
            bool cutX = CutCorner % 2 == 0 ? column < CutWidth : column >= Width - CutWidth;
            bool cutY = CutCorner < 2 ? row < CutHeight : row >= Height - CutHeight;
            return !IsLShaped || !cutX || !cutY;
        }
    }

    //Builds and checks a small school floor plan using only grid data.
    public sealed class SchoolMapLayout
    {
        public const int Size = 25;
        public const int MinWalkableTiles = 344;
        public const int MaxWalkableTiles = 396;
        public const int MaxUnusedRegion = 24;
        private readonly bool[,] floor = new bool[Size, Size];
        private readonly SchoolRoomType[,] themes = new SchoolRoomType[Size, Size];
        private readonly int[,] doors = new int[Size, Size];
        private readonly SchoolSpaceType[,] spaces = new SchoolSpaceType[Size, Size];
        private readonly List<SchoolSecondarySpace> secondarySpaces = new List<SchoolSecondarySpace>();
        private readonly List<SchoolRoom> rooms = new List<SchoolRoom>();
        private readonly Random random;
        private readonly SchoolMapGenerationSettings settings;
        public IReadOnlyList<SchoolRoom> Rooms => rooms.AsReadOnly();
        public IReadOnlyList<SchoolSecondarySpace> SecondarySpaces => secondarySpaces.AsReadOnly();
        public int Seed { get; }

        //A seed makes a layout repeatable when testing a reported problem.
        public SchoolMapLayout(int seed, SchoolMapGenerationSettings generationSettings = null)
        {
            Seed = seed;
            random = new Random(seed);
            settings = generationSettings != null ? generationSettings.Copy() : new SchoolMapGenerationSettings();
            //Retry positions and shapes instead of accepting a crowded or mostly empty map.
            for (int attempt = 0; attempt < settings.GenerationAttempts; attempt++)
            {
                if (TryBuildLayout()) return;
            }
            //A repeatable backup still has to pass every rule. Never return a bad layout.
            random = new Random(0);
            for (int attempt = 0; attempt < settings.GenerationAttempts; attempt++)
                if (TryBuildLayout()) return;
            throw new InvalidOperationException("Could not build a valid school floor plan.");
        }

        //Keeps layout checks in one place for both normal generation and the backup.
        private bool TryBuildLayout()
        {
            ClearLayout();
            if (!BuildRooms()) return false;
            GrowRooms();
            ShapeRooms();
            if (!ConnectRooms()) return false;
            for (int pass = 0; pass < settings.HallwayWideningPasses; pass++) WidenHallways();
            ClearHallwayWallIslands();
            AddSecondarySpace();
            return AllFloorConnected() && HasGoodSpaceUsage();
        }

        //Separates hallway floor from major rooms and small secondary spaces.
        public bool IsHallway(int x, int y)
        {
            return GetTile(x, y) == SchoolTileType.Floor && themes[x, y] == SchoolRoomType.None
                && spaces[x, y] == SchoolSpaceType.None;
        }

        //Returns None for walls, hallways, and major rooms.
        public SchoolSpaceType GetSpaceType(int x, int y)
        {
            return GetTile(x, y) == SchoolTileType.Floor ? spaces[x, y] : SchoolSpaceType.None;
        }

        //Suggests a wall position for future decorations, never a walking tile or doorway.
        public bool IsHallwayDecorationWall(int x, int y)
        {
            if (x < 1 || y < 1 || x >= Size - 1 || y >= Size - 1 || floor[x, y]) return false;
            bool hallway = false;
            for (int i = 0; i < 4; i++)
            {
                int nextX = x + StepX[i], nextY = y + StepY[i];
                if (doors[nextX, nextY] != 0) return false;
                hallway |= IsHallway(nextX, nextY);
            }
            return hallway;
        }

        //Widens the original routes once, leaving walls and narrow door openings intact.
        private void WidenHallways()
        {
            var additions = new List<int>();
            for (int y = 1; y < Size - 1; y++)
                for (int x = 1; x < Size - 1; x++)
                {
                    if (floor[x, y] || !ClearForHallway(x, y)) continue;
                    for (int i = 0; i < 4; i++)
                        if (IsHallway(x + StepX[i], y + StepY[i]))
                        { additions.Add(y * Size + x); break; }
                }
            //Apply together so added tiles cannot keep expanding the hallway in this pass.
            foreach (int cell in additions) floor[cell % Size, cell / Size] = true;
        }

        private void ClearHallwayWallIslands()
        {
            if (settings.MaximumHallwayWallIsland == 0) return;
            var seen = new HashSet<int>();
            for (int y = 1; y < Size - 1; y++) for (int x = 1; x < Size - 1; x++)
            {
                int first = y * Size + x;
                if (floor[x, y] || !seen.Add(first)) continue;
                var island = new List<int>();
                var pending = new Queue<int>();
                pending.Enqueue(first);
                bool removable = true;
                while (pending.Count > 0)
                {
                    int cell = pending.Dequeue(), xx = cell % Size, yy = cell / Size;
                    island.Add(cell);
                    if (!ClearForHallway(xx, yy)) removable = false;
                    for (int d = 0; d < StepX.Length; d++)
                    {
                        int nx = xx + StepX[d], ny = yy + StepY[d];
                        if (nx <= 0 || ny <= 0 || nx >= Size - 1 || ny >= Size - 1) { removable = false; continue; }
                        if (floor[nx, ny]) { if (!IsHallway(nx, ny)) removable = false; continue; }
                        int next = ny * Size + nx;
                        if (seen.Add(next)) pending.Enqueue(next);
                    }
                }
                if (removable && island.Count <= settings.MaximumHallwayWallIsland)
                    foreach (int cell in island) floor[cell % Size, cell / Size] = true;
            }
        }

        //Occasionally uses a spare pocket beside a hallway for one small school space.
        private void AddSecondarySpace()
        {
            bool skipSecondaryRoom = settings.SecondaryRoomChance <= 0f
                || (settings.SecondaryRoomChance < 1f
                    && (settings.SecondaryRoomChance == 0.5f
                        ? random.Next(2) == 0
                        : random.NextDouble() >= settings.SecondaryRoomChance));
            if (skipSecondaryRoom) return;
            for (int attempt = 0; attempt < settings.SecondaryRoomPlacementAttempts; attempt++)
            {
                int width = random.Next(2, 4), height = random.Next(2, 4);
                if (width * height < 6) continue;
                int x = random.Next(1, Size - width), y = random.Next(1, Size - height);
                bool valid = true, connected = false;
                for (int row = y; row < y + height && valid; row++)
                    for (int column = x; column < x + width; column++)
                    {
                        if (!ClearForHallway(column, row) || floor[column, row]) { valid = false; break; }
                        for (int i = 0; i < 4; i++)
                            connected |= IsHallway(column + StepX[i], row + StepY[i]);
                    }
                if (!valid || !connected) continue;
                SchoolSpaceType type = (SchoolSpaceType)random.Next(1, 7);
                secondarySpaces.Add(new SchoolSecondarySpace(x, y, width, height, type));
                for (int row = y; row < y + height; row++)
                    for (int column = x; column < x + width; column++)
                    { floor[column, row] = true; spaces[column, row] = type; }
                return;
            }
        }

        //Checks floor coverage and distribution without forcing rooms into fixed regions.
        public bool HasGoodSpaceUsage()
        {
            int count = 0, smallestRoom = int.MaxValue;
            var areas = new int[4];
            foreach (SchoolRoom room in rooms)
            {
                int area = room.Width * room.Height - room.CutWidth * room.CutHeight;
                if (area > 125) return false;
                smallestRoom = Math.Min(smallestRoom, area);
            }
            //Keep at least one modest room, rather than making all four rooms huge.
            if (smallestRoom > 85) return false;
            for (int y = 1; y < Size - 1; y++)
                for (int x = 1; x < Size - 1; x++)
                    if (floor[x, y])
                    {
                        count++;
                        if (x != Size / 2 && y != Size / 2)
                            areas[(x > Size / 2 ? 1 : 0) + (y > Size / 2 ? 2 : 0)]++;
                    }
            if (count < MinWalkableTiles || count > MaxWalkableTiles) return false;
            //These are floor-coverage checks, not boxes that constrain room placement.
            foreach (int area in areas) if (area < 55) return false;
            return LargestUnusedRegion() <= MaxUnusedRegion;
        }

        //Ignores walls beside floors and measures connected unused space behind them.
        public int LargestUnusedRegion()
        {
            var unused = new bool[Size, Size];
            for (int y = 1; y < Size - 1; y++)
                for (int x = 1; x < Size - 1; x++)
                {
                    if (floor[x, y]) continue;
                    bool besideFloor = false;
                    for (int i = 0; i < 4; i++)
                        if (floor[x + StepX[i], y + StepY[i]]) besideFloor = true;
                    unused[x, y] = !besideFloor;
                }
            int largest = 0;
            var pending = new Queue<int>();
            for (int y = 1; y < Size - 1; y++)
                for (int x = 1; x < Size - 1; x++)
                {
                    if (!unused[x, y]) continue;
                    unused[x, y] = false;
                    pending.Enqueue(y * Size + x);
                    int count = 0;
                    while (pending.Count > 0)
                    {
                        int cell = pending.Dequeue();
                        count++;
                        for (int i = 0; i < 4; i++)
                        {
                            int nextX = cell % Size + StepX[i], nextY = cell / Size + StepY[i];
                            if (!unused[nextX, nextY]) continue;
                            unused[nextX, nextY] = false;
                            pending.Enqueue(nextY * Size + nextX);
                        }
                    }
                    largest = Math.Max(largest, count);
                }
            return largest;
        }

        //Clears only generated grid data before another bounded attempt.
        private void ClearLayout()
        {
            rooms.Clear();
            secondarySpaces.Clear();
            Array.Clear(floor, 0, floor.Length);
            Array.Clear(spaces, 0, spaces.Length);
            Array.Clear(themes, 0, themes.Length);
            Array.Clear(doors, 0, doors.Length);
        }

        //Treats an address outside the grid as a wall.
        public SchoolTileType GetTile(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Size && y < Size && floor[x, y]
                ? SchoolTileType.Floor : SchoolTileType.Wall;
        }

        //Hallways and walls have no room theme.
        public SchoolRoomType GetRoomType(int x, int y)
        {
            return GetTile(x, y) == SchoolTileType.Floor ? themes[x, y] : SchoolRoomType.None;
        }

        //Reports the room-facing direction of a doorway marked while carving a hallway.
        public bool IsDoorFacing(int x, int y, int dx, int dy)
        {
            if (GetTile(x, y) != SchoolTileType.Floor) return false;
            int direction = dx == 1 && dy == 0 ? 1 : dx == 0 && dy == 1 ? 2
                : dx == -1 && dy == 0 ? 3 : dx == 0 && dy == -1 ? 4 : 0;
            return direction != 0 && doors[x, y] == direction;
        }

        //Grows rooms toward spare space while leaving a wall gap and limiting their size.
        private void GrowRooms()
        {
            for (int pass = 0; pass < 8; pass++)
                for (int i = 0; i < rooms.Count; i++)
                {
                    SchoolRoom room = rooms[i];
                    int x = room.X, y = room.Y, width = room.Width, height = room.Height;
                    switch (random.Next(4))
                    {
                        case 0: x--; width++; break;
                        case 1: width++; break;
                        case 2: y--; height++; break;
                        default: height++; break;
                    }
                    if (x < 1 || y < 1 || x + width >= Size || y + height >= Size
                        || width > 12 || height > 12) continue;
                    bool overlaps = false;
                    for (int j = 0; j < rooms.Count; j++)
                    {
                        SchoolRoom other = rooms[j];
                        if (i != j && x <= other.X + other.Width + 3 && x + width + 3 >= other.X
                            && y <= other.Y + other.Height + 3 && y + height + 3 >= other.Y) overlaps = true;
                    }
                    if (!overlaps) rooms[i] = new SchoolRoom(x, y, width, height, room.Type);
                }
        }

        //Mixes rectangles and L shapes, then redraws their floor data after growing them.
        private void ShapeRooms()
        {
            Array.Clear(floor, 0, floor.Length);
            Array.Clear(themes, 0, themes.Length);
            int shapedRoom = random.Next(rooms.Count);
            for (int i = 0; i < rooms.Count; i++)
            {
                SchoolRoom room = rooms[i];
                if (i == shapedRoom || random.Next(3) == 0)
                    room = new SchoolRoom(room.X, room.Y, room.Width, room.Height, room.Type,
                        random.Next(2, (room.Width - 1) / 2 + 1),
                        random.Next(2, (room.Height - 1) / 2 + 1), random.Next(4));
                rooms[i] = room;
                for (int y = room.Y; y < room.Y + room.Height; y++)
                    for (int x = room.X; x < room.X + room.Width; x++)
                        if (room.Contains(x, y)) { floor[x, y] = true; themes[x, y] = room.Type; }
            }
        }

        //Leaves room for two hallway lanes and a wall on each side.
        private bool BuildRooms()
        {
            SchoolRoomType[] types = { SchoolRoomType.Library, SchoolRoomType.ComputerLab,
                SchoolRoomType.Classroom, SchoolRoomType.ScienceLab };
            for (int i = types.Length - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                SchoolRoomType saved = types[i];
                types[i] = types[other];
                types[other] = saved;
            }
            for (int attempt = 0; attempt < 500 && rooms.Count < 4; attempt++)
            {
                int width = random.Next(settings.MainRoomMinimumSize, settings.MainRoomMaximumSize + 1);
                int height = random.Next(settings.MainRoomMinimumSize, settings.MainRoomMaximumSize + 1);
                int x = random.Next(1, Size - width);
                int y = random.Next(1, Size - height);
                //Keep floor beside an outside wall for systems that need a boundary entrance.
                if (rooms.Count == 0)
                {
                    switch (random.Next(4))
                    {
                        case 0: x = 1; break;
                        case 1: x = Size - width - 1; break;
                        case 2: y = 1; break;
                        default: y = Size - height - 1; break;
                    }
                }
                bool overlaps = false;
                foreach (SchoolRoom room in rooms)
                    if (x <= room.X + room.Width + 3 && x + width + 3 >= room.X
                        && y <= room.Y + room.Height + 3 && y + height + 3 >= room.Y) overlaps = true;
                if (!overlaps) AddRoom(x, y, width, height, types[rooms.Count]);
            }
            return rooms.Count == 4;
        }

        //Fills a rectangle and records its school theme.
        private void AddRoom(int x, int y, int availableWidth, int availableHeight, SchoolRoomType type)
        {
            int width = availableWidth;
            int height = availableHeight;
            var room = new SchoolRoom(x, y, width, height, type);
            rooms.Add(room);
            for (int row = 0; row < height; row++)
                for (int column = 0; column < width; column++)
                {
                    floor[x + column, y + row] = true;
                    themes[x + column, y + row] = type;
                }
        }

        private static readonly int[] StepX = { 1, 0, -1, 0 };
        private static readonly int[] StepY = { 0, 1, 0, -1 };

        //A door sits between the room floor and the hallway approach tile.
        private struct Entrance
        {
            public int X, Y, Direction;
            public int Approach => (Y + StepY[Direction]) * Size + X + StepX[Direction];
        }

        //Builds one shared hallway network, then gives some rooms a second entrance.
        private bool ConnectRooms()
        {
            for (int i = 0; i < rooms.Count; i++)
                if (!ConnectEntrance(rooms[i], i == 0)) return false;
            foreach (SchoolRoom room in rooms)
                if (settings.SecondEntranceChance == .5f ? random.Next(2) == 0 : random.NextDouble() < settings.SecondEntranceChance)
                    ConnectEntrance(room, false);
            return true;
        }

        //Only the chosen doorway may touch room floor; the rest stays behind a wall gap.
        private bool ClearForHallway(int x, int y)
        {
            if (x < 1 || y < 1 || x >= Size - 1 || y >= Size - 1
                || themes[x, y] != SchoolRoomType.None) return false;
            for (int i = 0; i < 4; i++)
                if (themes[x + StepX[i], y + StepY[i]] != SchoolRoomType.None) return false;
            return true;
        }

        //Finds straight entrances away from corners and other doors.
        private List<Entrance> GetEntrances(SchoolRoom room)
        {
            var choices = new List<Entrance>();
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                {
                    if (!room.Contains(x, y)) continue;
                    for (int direction = 0; direction < 4; direction++)
                    {
                        int dx = StepX[direction], dy = StepY[direction];
                        int doorX = x + dx, doorY = y + dy;
                        if (!ClearForHallway(x + dx * 2, y + dy * 2)) continue;
                        //A solid room edge on both sides keeps the entrance off a corner.
                        if (!room.Contains(x - dy, y + dx) || !room.Contains(x + dy, y - dx)) continue;
                        if (themes[doorX, doorY] != SchoolRoomType.None || floor[doorX, doorY]) continue;
                        int neighbors = 0;
                        bool nearbyDoor = false;
                        for (int step = 0; step < 4; step++)
                            if (themes[doorX + StepX[step], doorY + StepY[step]] != SchoolRoomType.None) neighbors++;
                        for (int row = Math.Max(1, doorY - 3); row <= Math.Min(Size - 2, doorY + 3); row++)
                            for (int column = Math.Max(1, doorX - 3); column <= Math.Min(Size - 2, doorX + 3); column++)
                                if (doors[column, row] != 0 && Math.Abs(column - doorX) + Math.Abs(row - doorY) < 4)
                                    nearbyDoor = true;
                        if (neighbors == 1 && !nearbyDoor)
                            choices.Add(new Entrance { X = doorX, Y = doorY, Direction = direction });
                    }
                }
            return choices;
        }

        //Joins a room to the nearest shared corridor without cutting through another room.
        private bool ConnectEntrance(SchoolRoom room, bool first)
        {
            List<Entrance> choices = GetEntrances(room);
            if (choices.Count == 0) return false;
            int[] parent = new int[Size * Size];
            int[] distance = new int[Size * Size];
            for (int i = 0; i < parent.Length; i++) parent[i] = -1;
            var pending = new Queue<int>();
            if (!first)
            {
                for (int y = 1; y < Size - 1; y++)
                    for (int x = 1; x < Size - 1; x++)
                        if (floor[x, y] && ClearForHallway(x, y))
                        {
                            int cell = y * Size + x;
                            parent[cell] = cell;
                            pending.Enqueue(cell);
                        }
                //A consistent random direction order favors straight runs over zigzags.
                int directionStart = random.Next(4);
                while (pending.Count > 0)
                {
                    int cell = pending.Dequeue();
                    for (int offset = 0; offset < 4; offset++)
                    {
                        int direction = (directionStart + offset) % 4;
                        int x = cell % Size + StepX[direction], y = cell / Size + StepY[direction];
                        if (!ClearForHallway(x, y)) continue;
                        int next = y * Size + x;
                        if (parent[next] != -1) continue;
                        parent[next] = cell;
                        distance[next] = distance[cell] + 1;
                        pending.Enqueue(next);
                    }
                }
            }
            int best = -1, bestScore = int.MaxValue;
            for (int i = 0; i < choices.Count; i++)
            {
                int cell = choices[i].Approach;
                if (!first && parent[cell] < 0) continue;
                int length = first ? Math.Abs(cell % Size - Size / 2) + Math.Abs(cell / Size - Size / 2) : distance[cell];
                int bends = 0, lastStep = 0, cursor = cell;
                while (!first && parent[cursor] != cursor)
                {
                    int step = parent[cursor] - cursor;
                    if (lastStep != 0 && step != lastStep) bends++;
                    lastStep = step;
                    cursor = parent[cursor];
                }
                int score = length * settings.HallwayLengthWeight + bends * settings.HallwayBendWeight + random.Next(4);
                if (score < bestScore) { best = i; bestScore = score; }
            }
            if (best < 0) return false;
            Entrance entrance = choices[best];
            int current = entrance.Approach;
            floor[current % Size, current / Size] = true;
            while (!first && parent[current] != current)
            {
                current = parent[current];
                floor[current % Size, current / Size] = true;
            }
            floor[entrance.X, entrance.Y] = true;
            doors[entrance.X, entrance.Y] = (entrance.Direction + 2) % 4 + 1;
            return true;
        }

        //Flood-fills the floor and accepts it only when every floor tile is reachable.
        public bool AllFloorConnected()
        {
            int total = 0;
            int start = -1;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if (floor[x, y]) { total++; start = y * Size + x; }
            if (start < 0) return false;
            var pending = new Queue<int>();
            var visited = new HashSet<int>();
            pending.Enqueue(start);
            visited.Add(start);
            int[] dx = { 1, 0, -1, 0 };
            int[] dy = { 0, 1, 0, -1 };
            while (pending.Count > 0)
            {
                int cell = pending.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int x = cell % Size + dx[i];
                    int y = cell / Size + dy[i];
                    int next = y * Size + x;
                    if (GetTile(x, y) == SchoolTileType.Floor && visited.Add(next)) pending.Enqueue(next);
                }
            }
            return visited.Count == total;
        }
    }
}
