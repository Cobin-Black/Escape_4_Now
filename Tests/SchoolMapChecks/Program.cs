using System;
using System.Collections.Generic;
using System.Text;
using Escape4Now.Map;

//Runs repeatable floor-plan tests without needing a Unity scene or license.
internal static class Program
{
    //Checks many seeds, including the smallest and largest possible seed values.
    private static void Main()
    {
        var shapes = new HashSet<string>();
        var libraryPositions = new HashSet<int>();
        for (int seed = 0; seed < 10000; seed++)
        {
            SchoolMapLayout layout = CheckLayout(seed);
            shapes.Add(Snapshot(layout));
            for (int i = 0; i < layout.Rooms.Count; i++)
                if (layout.Rooms[i].Type == SchoolRoomType.Library) libraryPositions.Add(i);
        }
        CheckLayout(int.MinValue);
        CheckLayout(int.MaxValue);
        Require(shapes.Count > 9900, "Layout variety");
        Require(libraryPositions.Count == 4, "Themes change positions");
        Require(Snapshot(new SchoolMapLayout(123)) == Snapshot(new SchoolMapLayout(123)), "Same seed is repeatable");
        Console.WriteLine("PASS: 10,002 seeds: size, rooms, borders, centers, floor connectivity, and safe bounds.");
        Console.WriteLine("PASS: " + shapes.Count + " different layouts; shuffled themes; repeatable seeds.");
        Console.WriteLine("Legend: # wall, . hallway, L library, C computer lab, R classroom, S science lab");
        for (int seed = 1; seed <= 3; seed++)
        {
            Console.WriteLine("Seed " + seed);
            Console.WriteLine(Snapshot(new SchoolMapLayout(seed)));
        }
    }

    //Checks every tile using a second flood-fill, separate from the generator's validator.
    private static SchoolMapLayout CheckLayout(int seed)
    {
        var layout = new SchoolMapLayout(seed);
        Require(SchoolMapLayout.Size == 18 && layout.Rooms.Count == 4, "Fixed grid and four rooms");
        var types = new HashSet<SchoolRoomType>();
        foreach (SchoolRoom room in layout.Rooms)
        {
            Require(types.Add(room.Type) && room.Type != SchoolRoomType.None, "Unique room themes");
            Require(room.Width >= 7 && room.Width <= 8 && room.Height >= 7 && room.Height <= 8, "Larger room size bounds");
            Require(layout.GetTile(room.CenterX, room.CenterY) == SchoolTileType.Floor, "Floor at each room center");
            Require(layout.GetRoomType(room.CenterX, room.CenterY) == room.Type, "Correct center theme");
        }
        int floorCount = 0;
        int start = -1;
        for (int y = 0; y < 18; y++)
            for (int x = 0; x < 18; x++)
            {
                bool floor = layout.GetTile(x, y) == SchoolTileType.Floor;
                if (x == 0 || x == 17 || y == 0 || y == 17) Require(!floor, "Closed map boundary");
                if (floor) { floorCount++; start = y * 18 + x; }
                else Require(layout.GetRoomType(x, y) == SchoolRoomType.None, "Walls have no room type");
            }
        Require(floorCount >= 221 && floorCount < 256, "Most interior space is floor, with room dividers left");
        var queue = new Queue<int>();
        var seen = new HashSet<int> { start };
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            int x = cell % 18;
            int y = cell / 18;
            int[] neighbors = { cell - 18, cell + 18, x > 0 ? cell - 1 : -1, x < 17 ? cell + 1 : -1 };
            foreach (int next in neighbors)
                if (next >= 0 && next < 324 && layout.GetTile(next % 18, next / 18) == SchoolTileType.Floor
                    && seen.Add(next)) queue.Enqueue(next);
        }
        Require(seen.Count == floorCount && layout.AllFloorConnected(), "Every floor tile is reachable");
        Require(layout.GetTile(-1, 0) == SchoolTileType.Wall && layout.GetTile(18, 18) == SchoolTileType.Wall,
            "Out-of-bounds queries are safe");
        return layout;
    }

    //Makes a small readable floor plan and a stable value for comparing seeds.
    private static string Snapshot(SchoolMapLayout layout)
    {
        var text = new StringBuilder();
        for (int y = 17; y >= 0; y--)
        {
            for (int x = 0; x < 18; x++)
            {
                char tile = '#';
                if (layout.GetTile(x, y) == SchoolTileType.Floor)
                    tile = ".LCRS"[(int)layout.GetRoomType(x, y)];
                text.Append(tile);
            }
            text.AppendLine();
        }
        return text.ToString();
    }

    //Stops at the first failed rule so the failing seed can be investigated.
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
    }
}
