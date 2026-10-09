using System;
using System.Collections.Generic;

namespace Escape4Now.Map
{
    public enum SchoolFurnitureKind
    {
        Bookshelf, ReadingTable, BookCart, ComputerDesk, Printer,
        StudentDesk, TeacherDesk, Whiteboard, LabTable, Cabinet, LabSink,
        Lockers, Bench, WaterFountain, TrashCan,
        StorageShelf, StorageBoxes, SupplyCart, Crate, MopBucket, CleaningCart, CleaningCabinet
    }

    //Each object uses one tile. Its prefab stays inside that tile's footprint.
    public sealed class SchoolFurniturePlacement
    {
        public int Cell { get; }
        public SchoolFurnitureKind Kind { get; }
        public SchoolFurniturePlacement(int cell, SchoolFurnitureKind kind) { Cell = cell; Kind = kind; }
    }

    //Makes the same spawn and furniture choices on every computer using the map seed.
    public sealed class SchoolFurniturePlan
    {
        private readonly SchoolMapLayout map;
        private readonly Random random;
        private readonly List<int> spawns = new List<int>();
        private readonly List<SchoolFurniturePlacement> furniture = new List<SchoolFurniturePlacement>();
        private readonly HashSet<int> blocked = new HashSet<int>();
        private readonly List<int> specialItems = new List<int>();
        private static readonly int[] Dx = { 1, 0, -1, 0 };
        private static readonly int[] Dy = { 0, 1, 0, -1 };
        public IReadOnlyList<int> Spawns => spawns.AsReadOnly();
        public IReadOnlyList<SchoolFurniturePlacement> Furniture => furniture.AsReadOnly();
        public IReadOnlyList<int> SpecialItems => specialItems.AsReadOnly();

        //Finish furniture first, then choose a clear start in each shuffled room.
        public SchoolFurniturePlan(SchoolMapLayout layout)
        {
            map = layout ?? throw new ArgumentNullException(nameof(layout));
            random = new Random(unchecked(layout.Seed ^ 0x51A43C7));
            foreach (SchoolRoom room in map.Rooms)
            {
                List<int> cells = RoomCells(room.Type);
                switch (room.Type)
                {
                    case SchoolRoomType.Library:
                        Required(cells, SchoolFurnitureKind.Bookshelf, 2, 3);
                        Place(cells, new[] { SchoolFurnitureKind.ReadingTable }, random.Next(3));
                        Place(cells, new[] { SchoolFurnitureKind.BookCart }, random.Next(2));
                        break;
                    case SchoolRoomType.ComputerLab:
                        Required(cells, SchoolFurnitureKind.ComputerDesk, 2, 4);
                        Place(cells, new[] { SchoolFurnitureKind.Printer }, random.Next(2));
                        break;
                    case SchoolRoomType.Classroom:
                        Required(cells, SchoolFurnitureKind.Whiteboard, 1, 1);
                        Required(cells, SchoolFurnitureKind.StudentDesk, 3, 5);
                        Place(cells, new[] { SchoolFurnitureKind.TeacherDesk }, random.Next(2));
                        break;
                    case SchoolRoomType.ScienceLab:
                        Required(cells, SchoolFurnitureKind.LabTable, 2, 4);
                        Place(cells, new[] { SchoolFurnitureKind.Cabinet }, random.Next(2));
                        Place(cells, new[] { SchoolFurnitureKind.LabSink }, random.Next(2));
                        break;
                }
            }
            PlaceSpecialRooms();
            var hallways = new List<int>();
            for (int y = 1; y < SchoolMapLayout.Size - 1; y++)
                for (int x = 1; x < SchoolMapLayout.Size - 1; x++)
                    if (map.IsHallway(x, y) && ClearOfDoors(x, y) && BesideWall(x, y)) hallways.Add(y * SchoolMapLayout.Size + x);
            Place(hallways, Pool(SchoolRoomType.None), random.Next(1, 3));
            var rooms = new List<SchoolRoom>(map.Rooms);
            Shuffle(rooms);
            foreach (SchoolRoom room in rooms)
            {
                List<int> cells = SpawnCells(room.Type);
                if (cells.Count == 0) throw new InvalidOperationException("A school room has no safe player start.");
                spawns.Add(cells[random.Next(cells.Count)]);
            }
        }

        //Small optional rooms get matching obstacles and one reserved floor reward.
        private void PlaceSpecialRooms()
        {
            foreach (SchoolSecondarySpace space in map.SecondarySpaces)
            {
                SchoolFurnitureKind[] pool = SpecialPool(space.Type);
                if (pool.Length == 0) continue;
                var cells = new List<int>();
                var corners = new List<int>();
                for (int y = space.Y; y < space.Y + space.Height; y++)
                    for (int x = space.X; x < space.X + space.Width; x++)
                    {
                        int cell = y * SchoolMapLayout.Size + x;
                        cells.Add(cell);
                        bool entrance = false;
                        for (int i = 0; i < 4; i++)
                            if (map.GetTile(x + Dx[i], y + Dy[i]) == SchoolTileType.Floor
                                && map.GetSpaceType(x + Dx[i], y + Dy[i]) != space.Type) entrance = true;
                        if (!entrance && ClearOfDoors(x, y)) corners.Add(cell);
                    }
                //Cleaning equipment identifies the closet before any optional pieces are added.
                Place(corners, space.Type == SchoolSpaceType.JanitorCloset ? new[] { SchoolFurnitureKind.MopBucket } : pool, 1);
                Place(corners, pool, random.Next(2));
                cells.RemoveAll(cell => blocked.Contains(cell));
                if (cells.Count == 0) throw new InvalidOperationException("A special room has no space for its item.");
                specialItems.Add(cells[random.Next(cells.Count)]);
            }
        }

        //Storage and cleaning objects stay in their own small rooms.
        public static SchoolFurnitureKind[] SpecialPool(SchoolSpaceType type)
        {
            if (type == SchoolSpaceType.Storage) return new[] { SchoolFurnitureKind.StorageShelf, SchoolFurnitureKind.StorageBoxes,
                SchoolFurnitureKind.Cabinet, SchoolFurnitureKind.SupplyCart, SchoolFurnitureKind.Crate };
            if (type == SchoolSpaceType.JanitorCloset) return new[] { SchoolFurnitureKind.MopBucket, SchoolFurnitureKind.CleaningCart,
                SchoolFurnitureKind.StorageShelf, SchoolFurnitureKind.TrashCan, SchoolFurnitureKind.CleaningCabinet };
            return new SchoolFurnitureKind[0];
        }

        //Required objects identify the room. Never silently accept a room without them.
        private void Required(List<int> cells, SchoolFurnitureKind kind, int minimum, int maximum)
        {
            if (Place(cells, new[] { kind }, random.Next(minimum, maximum + 1)) < minimum)
                throw new InvalidOperationException("A school room cannot fit its required furniture.");
        }

        //Leaves a clear tile around players and keeps starts away from doorways.
        private List<int> SpawnCells(SchoolRoomType type)
        {
            List<int> cells = RoomCells(type);
            cells.RemoveAll(cell => { foreach (int other in blocked) if (Distance(cell, other) <= 1) return true; return false; });
            return cells;
        }

        //Decorations stay beside walls instead of filling open hallway intersections.
        private bool BesideWall(int x, int y)
        {
            for (int i = 0; i < 4; i++)
                if (map.GetTile(x + Dx[i], y + Dy[i]) == SchoolTileType.Wall) return true;
            return false;
        }

        //Keeps each room's choices separate from every other room and the hallway.
        public static SchoolFurnitureKind[] Pool(SchoolRoomType type)
        {
            switch (type)
            {
                case SchoolRoomType.Library: return new[] { SchoolFurnitureKind.Bookshelf, SchoolFurnitureKind.ReadingTable, SchoolFurnitureKind.BookCart };
                case SchoolRoomType.ComputerLab: return new[] { SchoolFurnitureKind.ComputerDesk, SchoolFurnitureKind.Printer };
                case SchoolRoomType.Classroom: return new[] { SchoolFurnitureKind.StudentDesk, SchoolFurnitureKind.TeacherDesk, SchoolFurnitureKind.Whiteboard };
                case SchoolRoomType.ScienceLab: return new[] { SchoolFurnitureKind.LabTable, SchoolFurnitureKind.Cabinet, SchoolFurnitureKind.LabSink };
                default: return new[] { SchoolFurnitureKind.Lockers, SchoolFurnitureKind.Bench, SchoolFurnitureKind.WaterFountain, SchoolFurnitureKind.TrashCan };
            }
        }

        //Uses the room's real floor shape, including L-shaped rooms, and avoids door approaches.
        private List<int> RoomCells(SchoolRoomType type)
        {
            var cells = new List<int>();
            for (int y = 1; y < SchoolMapLayout.Size - 1; y++)
                for (int x = 1; x < SchoolMapLayout.Size - 1; x++)
                    if (map.GetRoomType(x, y) == type && ClearOfDoors(x, y)) cells.Add(y * SchoolMapLayout.Size + x);
            return cells;
        }

        //Leaves a two-step gap around doorways for turning and entering rooms.
        private bool ClearOfDoors(int x, int y)
        {
            for (int row = y - 2; row <= y + 2; row++)
                for (int column = x - 2; column <= x + 2; column++)
                {
                    if (Math.Abs(column - x) + Math.Abs(row - y) > 2) continue;
                    for (int i = 0; i < 4; i++)
                        if (map.IsDoorFacing(column, row, Dx[i], Dy[i])) return false;
                }
            return true;
        }

        //Tries shuffled tiles once and skips objects when no safe position remains.
        private int Place(List<int> cells, SchoolFurnitureKind[] pool, int count)
        {
            int placed = 0;
            Shuffle(cells);
            foreach (int cell in cells)
            {
                if (count == 0) break;
                if (specialItems.Contains(cell)) continue;
                bool clear = true;
                foreach (SchoolRoom room in map.Rooms)
                    if (Distance(cell, room.CenterY * SchoolMapLayout.Size + room.CenterX) <= 1) clear = false;
                bool hallway = map.IsHallway(cell % SchoolMapLayout.Size, cell / SchoolMapLayout.Size);
                foreach (int other in blocked) if (Distance(cell, other) <= (hallway ? 2 : 1)) clear = false;
                if (!clear) continue;
                blocked.Add(cell);
                if (!AllRemainingFloorConnected()) { blocked.Remove(cell); continue; }
                SchoolRoomType type = map.GetRoomType(cell % SchoolMapLayout.Size, cell / SchoolMapLayout.Size);
                if (type != SchoolRoomType.None && SpawnCells(type).Count == 0) { blocked.Remove(cell); continue; }
                furniture.Add(new SchoolFurniturePlacement(cell, pool[random.Next(pool.Length)]));
                placed++;
                count--;
            }
            return placed;
        }

        //Tests all remaining floor, not just the four room centers.
        private bool AllRemainingFloorConnected()
        {
            int start = map.Rooms[0].CenterY * SchoolMapLayout.Size + map.Rooms[0].CenterX;
            var seen = new HashSet<int> { start };
            var pending = new Queue<int>();
            pending.Enqueue(start);
            while (pending.Count > 0)
            {
                int cell = pending.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int x = cell % SchoolMapLayout.Size + Dx[i], y = cell / SchoolMapLayout.Size + Dy[i];
                    int next = y * SchoolMapLayout.Size + x;
                    if (map.GetTile(x, y) == SchoolTileType.Floor && !blocked.Contains(next) && seen.Add(next)) pending.Enqueue(next);
                }
            }
            for (int y = 1; y < SchoolMapLayout.Size - 1; y++)
                for (int x = 1; x < SchoolMapLayout.Size - 1; x++)
                {
                    int cell = y * SchoolMapLayout.Size + x;
                    if (map.GetTile(x, y) == SchoolTileType.Floor && !blocked.Contains(cell) && !seen.Contains(cell)) return false;
                }
            return true;
        }

        //Measures tile spacing without depending on Unity objects.
        private static int Distance(int a, int b) => Math.Abs(a % SchoolMapLayout.Size - b % SchoolMapLayout.Size)
            + Math.Abs(a / SchoolMapLayout.Size - b / SchoolMapLayout.Size);

        //A shuffled list avoids always trying the same corner of a room first.
        private void Shuffle<T>(List<T> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                T saved = values[i]; values[i] = values[other]; values[other] = saved;
            }
        }
    }
}
