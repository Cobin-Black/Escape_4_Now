using System;
using System.Collections.Generic;
using System.Text;
using Escape4Now.Map;
using Escape4Now.Obstacles;
using Escape4Now.TurnSystem;

//Runs repeatable floor-plan tests without needing a Unity scene or license.
internal static class Program
{
    //Checks many seeds, including the smallest and largest possible seed values.
    private static void Main()
    {
        CheckTurnOrders();
        var shapes = new HashSet<string>();
        var libraryPositions = new HashSet<int>();
        int minFloor = 625, maxFloor = 0, totalFloor = 0, multipleEntrances = 0;
        var areaUsage = new int[4];
        int withSecondary = 0, withWideHallway = 0;
        var spaceTypes = new HashSet<SchoolSpaceType>();
        var assignments = new HashSet<string>();
        var spawnLocations = new HashSet<int>();
        var furnitureChoices = new HashSet<SchoolFurnitureKind>();
        var facingChoices = new HashSet<ObstacleFacing>();
        var furnishedSpaces = new HashSet<SchoolSpaceType>();
        var patterns = new Dictionary<SchoolRoomType, HashSet<SchoolFurniturePattern>>();
        for (int seed = 0; seed < 10000; seed++)
        {
            SchoolMapLayout layout = CheckLayout(seed);
            if (seed < 1000)
            {
                SchoolFurniturePlan plan = CheckFurniture(layout);
                foreach (SchoolSecondarySpace space in layout.SecondarySpaces) furnishedSpaces.Add(space.Type);
                var assignment = new StringBuilder();
                foreach (int cell in plan.Spawns)
                {
                    assignment.Append((int)layout.GetRoomType(cell % 25, cell / 25));
                    spawnLocations.Add(cell);
                }
                assignments.Add(assignment.ToString());
                foreach (SchoolFurniturePlacement item in plan.Furniture)
                { furnitureChoices.Add(item.Kind); facingChoices.Add(item.Facing); }
                foreach (SchoolRoomArrangement arrangement in plan.Arrangements)
                {
                    if (!patterns.TryGetValue(arrangement.Room, out var choices))
                        patterns[arrangement.Room] = choices = new HashSet<SchoolFurniturePattern>();
                    choices.Add(arrangement.Pattern);
                }
            }
            if (layout.SecondarySpaces.Count > 0) withSecondary++;
            foreach (SchoolSecondarySpace space in layout.SecondarySpaces) spaceTypes.Add(space.Type);
            bool widened = false;
            int count = 0;
            var entrances = new Dictionary<SchoolRoomType, int>();
            for (int y = 0; y < 25; y++)
                for (int x = 0; x < 25; x++)
                {
                    if (layout.GetTile(x, y) == SchoolTileType.Floor)
                    {
                        count++;
                        if (x != 12 && y != 12) areaUsage[(x > 12 ? 1 : 0) + (y > 12 ? 2 : 0)]++;
                    }
                    widened |= layout.IsHallway(x, y) && layout.IsHallway(x + 1, y)
                        && layout.IsHallway(x, y + 1) && layout.IsHallway(x + 1, y + 1);
                    foreach (var step in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
                        if (layout.IsDoorFacing(x, y, step.Item1, step.Item2))
                        {
                            SchoolRoomType type = layout.GetRoomType(x + step.Item1, y + step.Item2);
                            Require(type != SchoolRoomType.None && layout.GetRoomType(x, y) == SchoolRoomType.None,
                                "Doors face rooms from hallways");
                            entrances.TryGetValue(type, out int previous);
                            entrances[type] = previous + 1;
                        }
                }
            foreach (int entrancesInRoom in entrances.Values)
                if (entrancesInRoom > 1) { multipleEntrances++; break; }
            Require(entrances.Count == 4, "Each room has a doorway");
            foreach (int entrancesInRoom in entrances.Values)
                Require(entrancesInRoom >= 1 && entrancesInRoom <= 2, "Only one or two doors per room");
            minFloor = Math.Min(minFloor, count);
            maxFloor = Math.Max(maxFloor, count);
            totalFloor += count;
            if (widened) withWideHallway++;
            shapes.Add(Snapshot(layout).Replace('L', '.').Replace('C', '.').Replace('R', '.').Replace('S', '.'));
            for (int i = 0; i < layout.Rooms.Count; i++)
                if (layout.Rooms[i].Type == SchoolRoomType.Library) libraryPositions.Add(i);
        }
        CheckLayout(int.MinValue);
        CheckLayout(int.MaxValue);
        foreach (var roomPatterns in patterns)
            Require(roomPatterns.Value.Count >= 3, "Each main room uses at least three seeded arrangement patterns");
        CheckPatternConfiguration();
        Require(assignments.Count == 24 && spawnLocations.Count > 100 && furnitureChoices.Count == 22,
            "All player-room orders, varied spawn tiles, and all furniture kinds occur");
        Require(facingChoices.Count == 4, "Obstacles use all four grid directions across different maps");
        Require(furnishedSpaces.Count == 6, "All six small-room types were checked with matching obstacles");
        Console.WriteLine("PASS: All six small-room types have 1-2 matching obstacles and exactly one reachable reward tile.");
        Console.WriteLine("PASS: 1,000 obstacle orientations: clear fronts, wall alignment, desks facing the board, and repeatable directions.");
        Console.WriteLine("PASS: 1,000 furniture plans: safe spawns, allowed pools, spacing, connectivity, and repeatable placement.");
        Require(shapes.Count > 9900, "Layout variety");
        Require(libraryPositions.Count == 4, "Themes change positions");
        Require(withSecondary > 0 && withSecondary < 10000 && spaceTypes.Count == 6, "Secondary spaces vary and are optional");
        Require(withWideHallway == 10000, "Every map has wider hallway space");
        Console.WriteLine($"Environment: {withSecondary} maps with secondary spaces; {withWideHallway} with wider hallways.");
        Require(multipleEntrances > 9000, "Most layouts offer rooms with more than one entrance");
        Console.WriteLine($"Floor coverage: {minFloor} to {maxFloor}; average {totalFloor / 10000}. Multiple entrances: {multipleEntrances} layouts.");
        Require(minFloor >= 344 && maxFloor <= 396, "Every map uses 65 to 75 percent of its interior");
        foreach (int usage in areaUsage)
            Require(usage > totalFloor * 0.20 && usage < totalFloor * 0.28, "No map quarter stays consistently unused");
        Require(Snapshot(new SchoolMapLayout(123)) == Snapshot(new SchoolMapLayout(123)), "Same seed is repeatable");
        Require(EnvironmentSnapshot(new SchoolMapLayout(123)) == EnvironmentSnapshot(new SchoolMapLayout(123)),
            "Environment metadata repeats with its seed");
        var noSecondarySettings = new SchoolMapGenerationSettings { SecondaryRoomChance = 0f };
        Require(new SchoolMapLayout(123, noSecondarySettings).SecondarySpaces.Count == 0,
            "Inspector setting can turn off optional secondary rooms");
        var compactRoomSettings = new SchoolMapGenerationSettings
        {
            MainRoomMinimumSize = 6,
            MainRoomMaximumSize = 7
        };
        Require(new SchoolMapLayout(456, compactRoomSettings).HasGoodSpaceUsage(),
            "Inspector room-size settings still produce a valid map");
        var compactFurniture = new SchoolFurnitureSettings
        {
            LibraryShelvesMinimum = 2,
            LibraryShelvesMaximum = 2,
            LibraryTablesMaximum = 0,
            LibraryCartsMaximum = 0,
            HallwayObjectsMinimum = 1,
            HallwayObjectsMaximum = 1
        };
        var customPlan = new SchoolFurniturePlan(new SchoolMapLayout(123), compactFurniture);
        int shelfCount = 0;
        foreach (SchoolFurniturePlacement placement in customPlan.Furniture)
            if (placement.Kind == SchoolFurnitureKind.Bookshelf) shelfCount++;
        Require(shelfCount == 2, "Inspector furniture settings control the library shelf count");
        Console.WriteLine("PASS: 10,002 seeds: size, rooms, borders, centers, floor connectivity, and safe bounds.");
        Console.WriteLine("PASS: Map-generation and furniture settings preserve their placement rules.");
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
        Require(SchoolMapLayout.Size == 25 && layout.Rooms.Count == 4, "Fixed grid and four rooms");
        var types = new HashSet<SchoolRoomType>();
        bool hasLShape = false;
        int smallestRoom = int.MaxValue;
        foreach (SchoolRoom room in layout.Rooms)
        {
            hasLShape |= room.IsLShaped;
            int roomArea = room.Width * room.Height - room.CutWidth * room.CutHeight;
            Require(roomArea <= 125, "No oversized room");
            smallestRoom = Math.Min(smallestRoom, roomArea);
            Require(types.Add(room.Type) && room.Type != SchoolRoomType.None, "Unique room themes");
            Require(room.Width >= 6 && room.Width <= 12 && room.Height >= 6 && room.Height <= 12, "Larger room size bounds");
            Require(layout.GetTile(room.CenterX, room.CenterY) == SchoolTileType.Floor, "Floor at each room center");
            Require(layout.GetRoomType(room.CenterX, room.CenterY) == room.Type, "Correct center theme");
            Require(room.X > 0 && room.Y > 0 && room.X + room.Width < SchoolMapLayout.Size
                && room.Y + room.Height < SchoolMapLayout.Size, "Rooms stay inside the border");
            bool entrance = false;
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                {
                    Require(layout.GetRoomType(x, y) == (room.Contains(x, y) ? room.Type : SchoolRoomType.None),
                        "Room floor matches its rectangle or L shape");
                    if (!room.Contains(x, y)) continue;
                    foreach (var offset in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        if (layout.GetTile(x + offset.Item1, y + offset.Item2) == SchoolTileType.Floor
                            && layout.GetRoomType(x + offset.Item1, y + offset.Item2) == SchoolRoomType.None)
                            entrance = true;
                }
            Require(entrance, "Every room has a hallway entrance");
            foreach (SchoolRoom other in layout.Rooms)
                if (!ReferenceEquals(room, other))
                    Require(room.X + room.Width < other.X || other.X + other.Width < room.X
                        || room.Y + room.Height < other.Y || other.Y + other.Height < room.Y,
                        "Room rectangles do not overlap and leave a wall gap");
        }
        int floorCount = 0;
        Require(hasLShape, "Every layout has at least one shaped room");
        Require(smallestRoom <= 85, "Not all four rooms are huge");
        var quarterFloors = new int[4];
        int start = -1;
        for (int y = 0; y < 25; y++)
            for (int x = 0; x < 25; x++)
            {
                bool floor = layout.GetTile(x, y) == SchoolTileType.Floor;
                if (x == 0 || x == 24 || y == 0 || y == 24) Require(!floor, "Closed map boundary");
                if (floor)
                {
                    floorCount++;
                    start = y * 25 + x;
                    if (x != 12 && y != 12) quarterFloors[(x > 12 ? 1 : 0) + (y > 12 ? 2 : 0)]++;
                }
                else Require(layout.GetRoomType(x, y) == SchoolRoomType.None, "Walls have no room type");
                if (floor && layout.GetRoomType(x, y) == SchoolRoomType.None)
                    foreach (var step in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
                    {
                        if (layout.GetRoomType(x + step.Item1, y + step.Item2) != SchoolRoomType.None)
                            Require(layout.IsDoorFacing(x, y, step.Item1, step.Item2), "Hallways touch rooms only at doors");
                        if (!layout.IsDoorFacing(x, y, step.Item1, step.Item2)) continue;
                        for (int row = Math.Max(0, y - 3); row <= Math.Min(24, y + 3); row++)
                            for (int col = Math.Max(0, x - 3); col <= Math.Min(24, x + 3); col++)
                                if ((col != x || row != y) && Math.Abs(col - x) + Math.Abs(row - y) < 4)
                                    foreach (var facing in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
                                        Require(!layout.IsDoorFacing(col, row, facing.Item1, facing.Item2), "Doors stay apart");
                    }
            }
        Require(floorCount >= 344 && floorCount <= 396, "Interior coverage stays in target range");
        foreach (int count in quarterFloors) Require(count >= 55, "Floor is distributed across this map");
        Require(layout.HasGoodSpaceUsage(), "Space checks accept the layout");
        CheckUnusedSpace(layout);
        CheckEnvironment(layout);
        var queue = new Queue<int>();
        var seen = new HashSet<int> { start };
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            int x = cell % 25;
            int y = cell / 25;
            int[] neighbors = { cell - 25, cell + 25, x > 0 ? cell - 1 : -1, x < 24 ? cell + 1 : -1 };
            foreach (int next in neighbors)
                if (next >= 0 && next < 625 && layout.GetTile(next % 25, next / 25) == SchoolTileType.Floor
                    && seen.Add(next)) queue.Enqueue(next);
        }
        Require(seen.Count == floorCount && layout.AllFloorConnected(), "Every floor tile is reachable");
        Require(layout.GetTile(-1, 0) == SchoolTileType.Wall && layout.GetTile(25, 25) == SchoolTileType.Wall,
            "Out-of-bounds queries are safe");
        return layout;
    }

    //Checks that optional spaces and decoration anchors do not cover room floors or paths.
    private static void CheckEnvironment(SchoolMapLayout layout)
    {
        Require(layout.SecondarySpaces.Count <= 1, "At most one small secondary space");
        var cells = new HashSet<(int, int)>();
        foreach (SchoolSecondarySpace space in layout.SecondarySpaces)
        {
            Require(space.Width >= 2 && space.Width <= 3 && space.Height >= 2 && space.Height <= 3,
                "Secondary spaces stay small");
            bool connected = false;
            for (int y = space.Y; y < space.Y + space.Height; y++)
                for (int x = space.X; x < space.X + space.Width; x++)
                {
                    Require(cells.Add((x, y)) && layout.GetTile(x, y) == SchoolTileType.Floor
                        && layout.GetSpaceType(x, y) == space.Type && layout.GetRoomType(x, y) == SchoolRoomType.None
                        && !layout.IsHallway(x, y), "Separate walkable secondary metadata");
                    connected |= layout.IsHallway(x + 1, y) || layout.IsHallway(x - 1, y)
                        || layout.IsHallway(x, y + 1) || layout.IsHallway(x, y - 1);
                }
            Require(connected, "Secondary space opens onto a hallway");
        }
        for (int y = 0; y < 25; y++)
            for (int x = 0; x < 25; x++)
            {
                Require((layout.GetSpaceType(x, y) != SchoolSpaceType.None) == cells.Contains((x, y)),
                    "Secondary metadata matches its bounds");
                if (!layout.IsHallwayDecorationWall(x, y)) continue;
                Require(layout.GetTile(x, y) == SchoolTileType.Wall, "Decoration anchor never blocks walking floor");
                Require(layout.IsHallway(x + 1, y) || layout.IsHallway(x - 1, y)
                    || layout.IsHallway(x, y + 1) || layout.IsHallway(x, y - 1), "Decoration faces a hallway");
            }
        Require(!layout.IsHallway(-1, 0) && !layout.IsHallwayDecorationWall(25, 25)
            && layout.GetSpaceType(-1, 0) == SchoolSpaceType.None, "Environment queries handle invalid coordinates");
    }

    //Includes decorative space types in repeatability checks, not just floor geometry.
    private static string EnvironmentSnapshot(SchoolMapLayout layout)
    {
        var text = new StringBuilder();
        for (int y = 0; y < 25; y++)
            for (int x = 0; x < 25; x++) text.Append((int)layout.GetSpaceType(x, y));
        return text.ToString();
    }

    //Checks the final occupied board, including all reserved starts, with a separate flood fill.
    private static SchoolFurniturePlan CheckFurniture(SchoolMapLayout layout, SchoolFurnitureSettings configuration = null)
    {
        configuration ??= new SchoolFurnitureSettings();
        var plan = new SchoolFurniturePlan(layout, configuration);
        var repeat = new SchoolFurniturePlan(layout, configuration);
        var starts = new HashSet<int>(plan.Spawns);
        var themes = new HashSet<SchoolRoomType>();
        var blocked = new HashSet<int>();
        var counts = new Dictionary<SchoolRoomType, int>();
        Require(starts.Count == 4, "Four distinct starts");
        for (int i = 0; i < 4; i++)
        {
            int cell = plan.Spawns[i];
            SchoolRoomType type = layout.GetRoomType(cell % 25, cell / 25);
            Require(type != SchoolRoomType.None && themes.Add(type), "A different main room for each player");
            Require(cell == repeat.Spawns[i], "Host and guest get identical starts");
        }
        Require(plan.Furniture.Count == repeat.Furniture.Count, "Repeatable furniture count");
        for (int i = 0; i < plan.Furniture.Count; i++)
        {
            SchoolFurniturePlacement item = plan.Furniture[i];
            int x = item.Cell % 25, y = item.Cell / 25;
            Require(blocked.Add(item.Cell) && !starts.Contains(item.Cell)
                && layout.GetTile(x, y) == SchoolTileType.Floor, "Furniture does not overlap players, walls, or other objects");
            SchoolRoomType type = layout.GetRoomType(x, y);
            SchoolSpaceType space = layout.GetSpaceType(x, y);
            var pool = space == SchoolSpaceType.None ? SchoolFurniturePlan.Pool(type) : SchoolFurniturePlan.SpecialPool(space);
            Require(Array.IndexOf(pool, item.Kind) >= 0, "Obstacle belongs to its room pool");
            if (type == SchoolRoomType.None && space == SchoolSpaceType.None) Require(layout.IsHallway(x, y), "Hallway objects stay in hallways");
            if (space == SchoolSpaceType.None) { counts.TryGetValue(type, out int count); counts[type] = count + 1; }
            Require(item.Cell == repeat.Furniture[i].Cell && item.Kind == repeat.Furniture[i].Kind
                && item.Facing == repeat.Furniture[i].Facing,
                "Host and guest get identical furniture");
        }
        CheckFacing(layout, plan, blocked);
        Require(plan.Arrangements.Count == 4 && repeat.Arrangements.Count == 4, "Each room records its chosen layout");
        for (int i = 0; i < plan.Arrangements.Count; i++)
            Require(plan.Arrangements[i].Pattern == repeat.Arrangements[i].Pattern
                && plan.Arrangements[i].FrontWall == repeat.Arrangements[i].FrontWall, "Pattern and orientation repeat with the seed");
        foreach (int cell in plan.RoomRoutes)
            Require(!blocked.Contains(cell), "Furniture never blocks a reserved door-to-center route or center aisle");
        foreach (SchoolFurniturePlacement item in plan.Furniture)
            if (item.Kind == SchoolFurnitureKind.Whiteboard)
            {
                bool wall = false;
                foreach (var step in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    wall |= layout.GetTile(item.Cell % 25 + step.Item1, item.Cell / 25 + step.Item2) == SchoolTileType.Wall;
                Require(wall, "Classroom whiteboard is placed against a wall");
            }
        foreach (SchoolRoomArrangement arrangement in plan.Arrangements)
            if (arrangement.Pattern == SchoolFurniturePattern.CenterAisle)
            {
                SchoolRoom room = null;
                foreach (SchoolRoom candidate in layout.Rooms) if (candidate.Type == arrangement.Room) room = candidate;
                SchoolRoomPatternSettings profile = Array.Find(configuration.RoomPatterns, value => value.Room == arrangement.Room);
                bool acrossX = arrangement.FrontWall == ObstacleFacing.North || arrangement.FrontWall == ObstacleFacing.South;
                int start = ((acrossX ? room.Width : room.Height) - profile.AisleWidth) / 2;
                foreach (int cell in blocked)
                    if (layout.GetRoomType(cell % 25, cell / 25) == arrangement.Room)
                    {
                        int coordinate = acrossX ? cell % 25 - room.X : cell / 25 - room.Y;
                        Require(coordinate < start || coordinate >= start + profile.AisleWidth, "The entire configured center aisle stays clear");
                    }
            }
        for (int y = 1; y < 24; y++) for (int x = 1; x < 24; x++)
        {
            int cell = y * 25 + x;
            if (layout.GetTile(x, y) != SchoolTileType.Floor || blocked.Contains(cell) || layout.GetSpaceType(x, y) != SchoolSpaceType.None) continue;
            int original = 0, remaining = 0;
            foreach (int next in new[] { cell - 1, cell + 1, cell - 25, cell + 25 })
                if (layout.GetTile(next % 25, next / 25) == SchoolTileType.Floor)
                { original++; if (!blocked.Contains(next)) remaining++; }
            Require(!configuration.PreventNewDeadEnds || original <= 1 || remaining != 1, "Furniture creates no new one-tile dead-end traps");
        }
        foreach (SchoolRoomType theme in themes)
        {
            int maximum = theme == SchoolRoomType.Library ? configuration.LibraryShelvesMaximum + configuration.LibraryTablesMaximum + configuration.LibraryCartsMaximum
                : theme == SchoolRoomType.ComputerLab ? configuration.ComputerDesksMaximum + configuration.PrintersMaximum
                : theme == SchoolRoomType.Classroom ? configuration.ClassroomDesksMaximum + configuration.WhiteboardCount + configuration.TeacherDesksMaximum
                : configuration.ScienceTablesMaximum + configuration.ScienceCabinetsMaximum + configuration.ScienceSinksMaximum;
            Require(counts.ContainsKey(theme) && counts[theme] >= (theme == SchoolRoomType.Classroom ? 4 : 2)
                && counts[theme] <= maximum, "Required and optional room counts");
            var kinds = new Dictionary<SchoolFurnitureKind, int>();
            foreach (SchoolFurniturePlacement item in plan.Furniture)
                if (layout.GetRoomType(item.Cell % 25, item.Cell / 25) == theme)
                { kinds.TryGetValue(item.Kind, out int number); kinds[item.Kind] = number + 1; }
            int main = kinds[SchoolFurniturePlan.Pool(theme)[0]];
            int mainMaximum = theme == SchoolRoomType.Library ? configuration.LibraryShelvesMaximum
                : theme == SchoolRoomType.ComputerLab ? configuration.ComputerDesksMaximum
                : theme == SchoolRoomType.Classroom ? configuration.ClassroomDesksMaximum : configuration.ScienceTablesMaximum;
            Require(main >= (theme == SchoolRoomType.Classroom ? 3 : 2)
                && main <= mainMaximum, "Required signature furniture counts");
            if (theme == SchoolRoomType.Classroom) Require(kinds[SchoolFurnitureKind.Whiteboard] == configuration.WhiteboardCount, "Configured classroom whiteboards");
            foreach (var entry in kinds)
                if (entry.Key != SchoolFurniturePlan.Pool(theme)[0])
                    Require(entry.Value <= (entry.Key == SchoolFurnitureKind.ReadingTable ? configuration.LibraryTablesMaximum
                        : entry.Key == SchoolFurnitureKind.Whiteboard ? configuration.WhiteboardCount : 1), "Optional furniture stays sparse");
        }
        if (counts.TryGetValue(SchoolRoomType.None, out int hallwayCount)) Require(hallwayCount <= 2, "Sparse hallways");
        int specialCount = 0;
        foreach (SchoolSecondarySpace space in layout.SecondarySpaces)
        {
            SchoolFurnitureKind[] pool = SchoolFurniturePlan.SpecialPool(space.Type);
            Require(pool.Length > 0, "Every small-room type has an obstacle pool");
            specialCount++;
            int obstacles = 0, rewards = 0;
            bool hasSignature = false;
            foreach (SchoolFurniturePlacement item in plan.Furniture)
                if (layout.GetSpaceType(item.Cell % 25, item.Cell / 25) == space.Type)
                {
                    obstacles++;
                    hasSignature |= item.Kind == pool[0];
                    foreach (var step in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int x = item.Cell % 25 + step.Item1, y = item.Cell / 25 + step.Item2;
                        Require(layout.GetTile(x, y) != SchoolTileType.Floor || layout.GetSpaceType(x, y) == space.Type,
                            "Special-room entrances stay clear");
                    }
                }
            foreach (int reward in plan.SpecialItems)
                if (layout.GetSpaceType(reward % 25, reward / 25) == space.Type)
                { rewards++; Require(!blocked.Contains(reward), "Special reward is not inside an obstacle"); }
            Require(obstacles >= 1 && obstacles <= 2, "Small rooms have one or two matching obstacles");
            Require(space.Type == SchoolSpaceType.Storage || hasSignature, "Small room has an object that identifies it");
            Require(rewards == 1, "Every generated small room reserves exactly one reward");
        }
        Require(plan.SpecialItems.Count == specialCount && repeat.SpecialItems.Count == specialCount, "Only generated special rooms receive rewards");
        for (int i = 0; i < specialCount; i++) Require(plan.SpecialItems[i] == repeat.SpecialItems[i], "Repeatable reward positions");
        foreach (int cell in starts)
            foreach (int other in blocked)
                Require(Math.Abs(cell % 25 - other % 25) + Math.Abs(cell / 25 - other / 25) > 1, "Spawn has breathing room");
        var doorClearance = new HashSet<int>(blocked);
        doorClearance.UnionWith(starts);
        foreach (int cell in doorClearance)
            for (int y = cell / 25 - 2; y <= cell / 25 + 2; y++)
                for (int x = cell % 25 - 2; x <= cell % 25 + 2; x++)
                {
                    if (Math.Abs(x - cell % 25) + Math.Abs(y - cell / 25) > 2) continue;
                    foreach (var step in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
                        Require(!layout.IsDoorFacing(x, y, step.Item1, step.Item2), "Door approaches stay clear");
                }
        var seen = new HashSet<int> { plan.Spawns[0] };
        var queue = new Queue<int>();
        queue.Enqueue(plan.Spawns[0]);
        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            foreach (int next in new[] { cell - 1, cell + 1, cell - 25, cell + 25 })
                if (next >= 0 && next < 625 && !blocked.Contains(next)
                    && layout.GetTile(next % 25, next / 25) == SchoolTileType.Floor && seen.Add(next)) queue.Enqueue(next);
        }
        for (int y = 0; y < 25; y++)
            for (int x = 0; x < 25; x++)
                if (layout.GetTile(x, y) == SchoolTileType.Floor && !blocked.Contains(y * 25 + x))
                    Require(seen.Contains(y * 25 + x), "All remaining floor stays reachable after furniture");
        return plan;
    }

    private static void CheckPatternConfiguration()
    {
        var dense = new SchoolFurnitureSettings
        {
            LibraryShelvesMaximum = 5, LibraryTablesMaximum = 3, ComputerDesksMaximum = 7,
            ClassroomDesksMaximum = 10, ScienceTablesMaximum = 6
        };
        dense.RoomPatterns[2] = new SchoolRoomPatternSettings(SchoolRoomType.Classroom, 4, 0, 3, 3, aisleWidth: 2);
        int smallestCount = int.MaxValue, largestCount = 0;
        double smallTotal = 0, largeTotal = 0;
        int smallRooms = 0, largeRooms = 0;
        for (int seed = 0; seed < 256; seed++)
        {
            var layout = new SchoolMapLayout(seed);
            SchoolFurniturePlan plan = CheckFurniture(layout, dense);
            foreach (SchoolRoom room in layout.Rooms)
            {
                int count = 0;
                foreach (SchoolFurniturePlacement item in plan.Furniture)
                    if (layout.GetRoomType(item.Cell % 25, item.Cell / 25) == room.Type) count++;
                int area = room.Width * room.Height - room.CutWidth * room.CutHeight;
                if (area <= 65) { smallTotal += count; smallRooms++; }
                if (area >= 95) { largeTotal += count; largeRooms++; }
                if (room.Type == SchoolRoomType.Classroom) { smallestCount = Math.Min(smallestCount, count); largestCount = Math.Max(largestCount, count); }
            }
        }
        Require(smallRooms > 0 && largeRooms > 0 && largeTotal / largeRooms > smallTotal / smallRooms,
            "Larger rooms receive more furniture on average");
        Require(largestCount - smallestCount >= 3, "Scene-density classroom counts vary meaningfully");
        foreach (SchoolFurniturePattern pattern in Enum.GetValues(typeof(SchoolFurniturePattern)))
        {
            var forced = new SchoolFurnitureSettings();
            for (int i = 0; i < forced.RoomPatterns.Length; i++)
                forced.RoomPatterns[i] = new SchoolRoomPatternSettings(forced.RoomPatterns[i].Room,
                    pattern == SchoolFurniturePattern.Rows ? 1 : 0, pattern == SchoolFurniturePattern.Perimeter ? 1 : 0,
                    pattern == SchoolFurniturePattern.Groups ? 1 : 0, pattern == SchoolFurniturePattern.CenterAisle ? 1 : 0);
            var plan = CheckFurniture(new SchoolMapLayout(42), forced);
            foreach (SchoolRoomArrangement arrangement in plan.Arrangements)
                Require(arrangement.Pattern == pattern, "Zero pattern weights are respected instead of silently re-enabled");
        }
        var invalid = new SchoolFurnitureSettings();
        invalid.RoomPatterns[0] = new SchoolRoomPatternSettings(SchoolRoomType.Library, 0, 0, 0, 0);
        bool rejected = false;
        try { new SchoolFurniturePlan(new SchoolMapLayout(42), invalid); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "An all-disabled room pattern is rejected clearly");
        Console.WriteLine("PASS: weighted pattern variety, forced patterns, reserved routes, density scaling, 256 denser scene plans, and invalid Inspector settings.");
    }

    //Checks the chosen front against every clear direction on the finished board.
    private static void CheckFacing(SchoolMapLayout map, SchoolFurniturePlan plan, HashSet<int> blocked)
    {
        int board = -1;
        foreach (SchoolFurniturePlacement item in plan.Furniture)
            if (item.Kind == SchoolFurnitureKind.Whiteboard) board = item.Cell;
        var steps = new[] { (0, -1), (-1, 0), (0, 1), (1, 0) };
        foreach (SchoolFurniturePlacement item in plan.Furniture)
        {
            int x = item.Cell % 25, y = item.Cell / 25;
            int direction = (int)item.Facing;
            Require(direction >= 0 && direction < 4, "Only grid-aligned directions");
            var front = steps[direction];
            Require(Clear(front), "Every obstacle faces clear floor in its own area");
            int targetScore = front.Item1 * (board % 25 - x) + front.Item2 * (board / 25 - y);
            int wallDistance = WallDistance(front);
            foreach (var step in steps)
            {
                if (!Clear(step)) continue;
                if (item.Kind == SchoolFurnitureKind.StudentDesk)
                    Require(targetScore >= step.Item1 * (board % 25 - x) + step.Item2 * (board / 25 - y),
                        "Desk faces toward the whiteboard using a clear direction");
                else Require(wallDistance <= WallDistance(step), "Nearest suitable wall stays behind the object");
            }

            bool SameArea(int otherX, int otherY) => map.GetTile(otherX, otherY) == SchoolTileType.Floor
                && map.GetRoomType(otherX, otherY) == map.GetRoomType(x, y)
                && map.GetSpaceType(otherX, otherY) == map.GetSpaceType(x, y);
            bool Clear((int dx, int dy) step) => SameArea(x + step.dx, y + step.dy)
                && !blocked.Contains((y + step.dy) * 25 + x + step.dx);
            int WallDistance((int dx, int dy) step)
            {
                for (int i = 1; i < 25; i++)
                {
                    int otherX = x - step.dx * i, otherY = y - step.dy * i;
                    if (map.GetTile(otherX, otherY) == SchoolTileType.Wall) return i;
                    if (!SameArea(otherX, otherY)) break;
                }
                return 25;
            }
        }
    }

    //Independently groups unused cells after excluding the wall layer beside floor.
    private static void CheckUnusedSpace(SchoolMapLayout layout)
    {
        var empty = new HashSet<(int x, int y)>();
        var steps = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        for (int y = 1; y < 24; y++)
            for (int x = 1; x < 24; x++)
            {
                bool unused = layout.GetTile(x, y) == SchoolTileType.Wall;
                foreach (var step in steps)
                    unused &= layout.GetTile(x + step.Item1, y + step.Item2) == SchoolTileType.Wall;
                if (unused) empty.Add((x, y));
            }
        int largest = 0;
        while (empty.Count > 0)
        {
            var iterator = empty.GetEnumerator();
            iterator.MoveNext();
            var start = iterator.Current;
            iterator.Dispose();
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue(start);
            empty.Remove(start);
            int count = 0;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                count++;
                foreach (var step in steps)
                {
                    var next = (cell.x + step.Item1, cell.y + step.Item2);
                    if (empty.Remove(next)) queue.Enqueue(next);
                }
            }
            largest = Math.Max(largest, count);
        }
        Require(largest <= 24 && layout.LargestUnusedRegion() == largest, "No large unused region behind the walls");
    }

    //Makes a small readable floor plan and a stable value for comparing seeds.
    private static string Snapshot(SchoolMapLayout layout)
    {
        var text = new StringBuilder();
        for (int y = 24; y >= 0; y--)
        {
            for (int x = 0; x < 25; x++)
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

    private static void CheckTurnOrders()
    {
        foreach (int count in new[] { 2, 3, 4 })
        {
            var permutations = new HashSet<string>();
            var startingPlayers = new HashSet<int>();
            for (int seed = 0; seed < 10000; seed++)
            {
                int[] order = PlayerTurnOrder.Create(count, seed);
                Require(PlayerTurnOrder.IsValid(order, count), "Every participant occurs exactly once");
                Require(string.Join(",", order) == string.Join(",", PlayerTurnOrder.Create(count, seed)),
                    "Fixed match seeds reproduce the complete order");
                permutations.Add(string.Join(",", order));
                startingPlayers.Add(order[0]);
            }
            int expectedPermutations = count == 2 ? 2 : count == 3 ? 6 : 24;
            Require(permutations.Count == expectedPermutations && startingPlayers.Count == count,
                "Every order and starting identity is possible");
            foreach (int seed in new[] { int.MinValue, int.MaxValue })
                Require(PlayerTurnOrder.IsValid(PlayerTurnOrder.Create(count, seed), count), "Extreme seeds are valid");
            int[] originalOrder = PlayerTurnOrder.Create(count, 42, false);
            for (int i = 0; i < count; i++) Require(originalOrder[i] == i, "Inspector opt-out preserves identity order");
        }
        Require(PlayerTurnOrder.Create(0, 42).Length == 0 && PlayerTurnOrder.Create(1, 42)[0] == 0,
            "Empty setup and solo tutorial remain safe");
        foreach (int[] invalid in new[] { null, new[] { 0 }, new[] { 0, 0 }, new[] { -1, 1 }, new[] { 0, 2 } })
            Require(!PlayerTurnOrder.IsValid(invalid, 2), "Malformed network turn sequences are rejected");
        Console.WriteLine("PASS: 30,000 seeded turn orders for 2-4 players; all permutations, repeatability, solo fallback, and validation.");
    }

    //Stops at the first failed rule so the failing seed can be investigated.
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
    }
}
