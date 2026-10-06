using System;
using System.Collections.Generic;

namespace Escape4Now.Map
{
    public enum SchoolTileType { Wall, Floor }
    public enum SchoolRoomType { None, Library, ComputerLab, Classroom, ScienceLab }

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

        //Stores the room's bounds and school theme.
        public SchoolRoom(int x, int y, int width, int height, SchoolRoomType type)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Type = type;
        }
    }

    //Builds and checks a small school floor plan using only grid data.
    public sealed class SchoolMapLayout
    {
        public const int Size = 18;
        private readonly bool[,] floor = new bool[Size, Size];
        private readonly SchoolRoomType[,] themes = new SchoolRoomType[Size, Size];
        private readonly List<SchoolRoom> rooms = new List<SchoolRoom>();
        private readonly Random random;
        public IReadOnlyList<SchoolRoom> Rooms => rooms.AsReadOnly();
        public int Seed { get; }

        //A seed makes a layout repeatable when testing a reported problem.
        public SchoolMapLayout(int seed)
        {
            Seed = seed;
            random = new Random(seed);
            BuildRooms();
            ConnectRooms();
            if (!AllFloorConnected()) throw new InvalidOperationException("The school floor is not connected.");
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

        //Uses different row splits on each side, so the four areas are not identical squares.
        private void BuildRooms()
        {
            int splitX = random.Next(8, 10);
            int leftY = random.Next(8, 10);
            int rightY = leftY == 8 ? 9 : 8;
            SchoolRoomType[] types = { SchoolRoomType.Library, SchoolRoomType.ComputerLab,
                SchoolRoomType.Classroom, SchoolRoomType.ScienceLab };
            for (int i = types.Length - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                SchoolRoomType saved = types[i];
                types[i] = types[other];
                types[other] = saved;
            }
            AddRoom(1, 1, splitX - 1, leftY - 1, types[0]);
            AddRoom(1, leftY + 1, splitX - 1, 16 - leftY, types[1]);
            AddRoom(splitX + 1, rightY + 1, 16 - splitX, 16 - rightY, types[2]);
            AddRoom(splitX + 1, 1, 16 - splitX, rightY - 1, types[3]);
        }

        //Fills each area with a 7-to-8-tile room, with an optional small corner cut.
        private void AddRoom(int x, int y, int availableWidth, int availableHeight, SchoolRoomType type)
        {
            int width = availableWidth;
            int height = availableHeight;
            var room = new SchoolRoom(x, y, width, height, type);
            rooms.Add(room);
            int notch = random.Next(0, 2);
            bool right = random.Next(2) == 0;
            bool top = random.Next(2) == 0;
            for (int row = 0; row < height; row++)
                for (int column = 0; column < width; column++)
                {
                    bool cornerX = right ? column >= width - notch : column < notch;
                    bool cornerY = top ? row >= height - notch : row < notch;
                    if (cornerX && cornerY) continue;
                    floor[x + column, y + row] = true;
                    themes[x + column, y + row] = type;
                }
        }

        //Three sides of a room loop always connect all four rooms; a fourth route is optional.
        private void ConnectRooms()
        {
            int missingSide = random.Next(4);
            for (int i = 0; i < rooms.Count; i++)
            {
                if (i == missingSide && random.Next(2) == 0) continue;
                SchoolRoom first = rooms[i];
                SchoolRoom second = rooms[(i + 1) % rooms.Count];
                //Vary the entrances without leaving large gaps around the rooms.
                int x = random.Next(first.X + 1, first.X + first.Width - 1);
                int y = random.Next(first.Y + 1, first.Y + first.Height - 1);
                int targetX = random.Next(second.X + 1, second.X + second.Width - 1);
                int targetY = random.Next(second.Y + 1, second.Y + second.Height - 1);
                bool horizontalFirst = random.Next(2) == 0;
                while (x != targetX || y != targetY)
                {
                    if ((horizontalFirst && x != targetX) || y == targetY)
                        x += Math.Sign(targetX - x);
                    else y += Math.Sign(targetY - y);
                    floor[x, y] = true;
                }
            }
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
