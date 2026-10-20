using System;
using System.Collections.Generic;
using Escape4Now.Map;

internal static class Program
{
    private static void Main(string[] args)
    {
        int count = args.Length > 0 ? int.Parse(args[0]) : 1000;
        var signatures = new HashSet<string>();
        var positions = new HashSet<string>();
        var optional = new HashSet<OutdoorAreaKind>();
        var sports = new HashSet<OutdoorCourtSport>();
        var props = new HashSet<OutdoorPropKind>();
        int pockets = 0;
        int retries = 0;
        for (int seed = 0; seed < count; seed++)
        {
            var map = new SurvivalMapLayout(seed);
            Check(map, new SurvivalGenerationSettings());
            if (seed < 30) CheckAlternateRoutes(map);
            signatures.Add(Signature(map));
            foreach (OutdoorArea area in map.Areas)
            {
                positions.Add($"{area.Kind}:{area.X},{area.Y}:{area.Width},{area.Height}");
                optional.Add(area.Kind);
                if (area.CourtSport != OutdoorCourtSport.Any) sports.Add(area.CourtSport);
                if (area.IsPocket) pockets++;
            }
            foreach (OutdoorPropPlacement prop in map.Props) props.Add(prop.Kind);
            if (map.AttemptsUsed > 1) retries++;
            if (seed < 50) Require(Signature(map) == Signature(new SurvivalMapLayout(seed)), "Fixed seeds repeat every tile, object, and spawn");
        }
        Check(new SurvivalMapLayout(int.MinValue), new SurvivalGenerationSettings());
        Check(new SurvivalMapLayout(int.MaxValue), new SurvivalGenerationSettings());
        Require(signatures.Count == count, "Seeds produce different arrangements");
        Require(optional.Count == 7 && positions.Count > Math.Min(count, 100), "All outdoor areas vary in position and size");
        Require(sports.Count == 2 && pockets > 0, "Both court styles and secondary courtyard pockets are procedural");
        if (count >= 100) foreach (OutdoorPropKind prop in Enum.GetValues(typeof(OutdoorPropKind)))
            if (prop != OutdoorPropKind.Fence) Require(props.Contains(prop), "Prefab rule can place " + prop);
        var optionalSettings = new SurvivalGenerationSettings();
        optionalSettings.CourtyardPocket.Chance = 0f;
        foreach (OutdoorAreaRule area in optionalSettings.Areas) if (!area.Required) area.Chance = 0f;
        var sparse = new SurvivalMapLayout(42, optionalSettings);
        Require(sparse.Areas.Count == 2, "Optional area probabilities are honored");
        var emptyObstacles = new SurvivalObstacleSettings { Rules = Array.Empty<OutdoorPropRule>() };
        var clear = new SurvivalMapLayout(42, null, emptyObstacles);
        Require(clear.Props.Count == 0 && clear.Validate(out _), "Empty obstacle rules preserve a connected map without optional props");
        var noChance = new SurvivalObstacleSettings();
        noChance.Rules = new[] { new OutdoorPropRule(OutdoorPropKind.Tree, new[] { OutdoorAreaKind.None }, 1, 3, chance: 0f) };
        Require(new SurvivalMapLayout(42, null, noChance).Props.Count == 0, "Spawner probabilities control placement");
        var decorativeTrees = new SurvivalObstacleSettings
        {
            Rules = new[] { new OutdoorPropRule(OutdoorPropKind.Tree, new[] { OutdoorAreaKind.None }, 2, 2,
                blocking: OutdoorBlockingMode.Decoration) }
        };
        var decorative = new SurvivalMapLayout(42, null, decorativeTrees);
        foreach (OutdoorPropPlacement prop in decorative.Props)
            Require(!prop.BlocksMovement && decorative.IsWalkable(prop.X, prop.Y), "Inspector blocking override controls physical walkability");
        var basketballOnly = new SurvivalGenerationSettings { TennisCourtChance = 0f };
        var tennisOnly = new SurvivalGenerationSettings { TennisCourtChance = 1f };
        for (int seed = 0; seed < 20; seed++)
        {
            foreach (OutdoorArea area in new SurvivalMapLayout(seed, basketballOnly).Areas)
                if (area.Kind == OutdoorAreaKind.Courts) Require(area.CourtSport == OutdoorCourtSport.Basketball, "Basketball-only Inspector chance is respected");
            foreach (OutdoorArea area in new SurvivalMapLayout(seed, tennisOnly).Areas)
                if (area.Kind == OutdoorAreaKind.Courts) Require(area.CourtSport == OutdoorCourtSport.Tennis, "Tennis-only Inspector chance is respected");
        }
        for (int players = 1; players <= 8; players++)
        {
            var settings = new SurvivalGenerationSettings { Width = 40, Height = 36, PlayerSpawnCount = players, MinimumSpawnDistance = 6 };
            Check(new SurvivalMapLayout(players, settings), settings);
        }
        bool rejected = false;
        try { _ = new SurvivalMapLayout(42, new SurvivalGenerationSettings { Width = 24, PathWidth = 4, GenerationAttempts = 2 }); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Impossible settings fail after bounded retries rather than return an invalid map");
        rejected = false;
        try { _ = new SurvivalMapLayout(42, new SurvivalGenerationSettings { Width = 0 }); }
        catch (ArgumentException) { rejected = true; }
        Require(rejected, "Invalid configuration is rejected");
        Console.WriteLine($"PASS: {count + 2} default seeds, {retries} retried layouts; all seven areas, both sports, courtyard pockets, every prop, parking bays, larger pools, protected field/garden routes, blocking overrides, connectivity, loops, dead ends, deterministic seeds, optional chances, and 1-8 spawn reservations.");
    }

    private static void Check(SurvivalMapLayout map, SurvivalGenerationSettings settings)
    {
        Require(map.Width == settings.Width && map.Height == settings.Height && map.Validate(out _), "Dimensions and built-in validation");
        var walkable = new HashSet<int>();
        for (int y = 0; y < map.Height; y++) for (int x = 0; x < map.Width; x++)
        {
            if (map.IsWalkable(x, y)) walkable.Add(y * map.Width + x);
            if (map.GetSurface(x, y) == OutdoorSurface.Water || map.GetSurface(x, y) == OutdoorSurface.Fence)
                Require(!map.IsWalkable(x, y), "Water and fences cannot be traversed");
            if (map.IsMainPath(x, y)) Require(map.IsWalkable(x, y), "Main paths stay clear of obstacles");
            if (map.GetSurface(x, y) == OutdoorSurface.Field) Require(map.IsWalkable(x, y), "Sports field core stays open");
            if (map.GetArea(x, y) == OutdoorAreaKind.Garden && map.GetSurface(x, y) == OutdoorSurface.Path)
                Require(map.IsWalkable(x, y), "Natural garden paths remain clear");
        }
        var reached = new HashSet<int>(); var queue = new Queue<int>();
        queue.Enqueue(map.PlayerSpawns[0]); reached.Add(map.PlayerSpawns[0]);
        while (queue.Count > 0) foreach (int next in map.GetNeighbours(queue.Dequeue())) if (reached.Add(next)) queue.Enqueue(next);
        Require(reached.SetEquals(walkable), "Independent flood fill reaches every walkable tile");
        var occupied = new HashSet<int>();
        foreach (OutdoorPropPlacement prop in map.Props)
        for (int y = prop.Y; y < prop.Y + prop.Height; y++) for (int x = prop.X; x < prop.X + prop.Width; x++)
        {
            Require(occupied.Add(y * map.Width + x), "Props never overlap");
            if (prop.BlocksMovement) Require(!map.IsWalkable(x, y), "Entire obstacle footprints are non-walkable");
        }
        foreach (OutdoorArea area in map.Areas)
        {
            Require(reached.Contains(area.AnchorCell) && area.Entrances.Count >= 2, "Every area has multiple reachable entrances");
            foreach (int gate in area.Entrances) Require(reached.Contains(gate), "All entrances remain open");
            if (area.Kind == OutdoorAreaKind.Pool)
            {
                Require(area.WaterWidth >= settings.MinimumPoolWaterWidth && area.WaterHeight >= settings.MinimumPoolWaterHeight,
                    "Pool water meets its Inspector size minimum rather than shrinking to a tiny blue patch");
                for (int y = area.WaterY; y < area.WaterY + area.WaterHeight; y++)
                for (int x = area.WaterX; x < area.WaterX + area.WaterWidth; x++)
                    Require(map.GetSurface(x, y) == OutdoorSurface.Water && !map.IsWalkable(x, y), "All pool water uses the authoritative blocked grid");
            }
            if (area.Kind == OutdoorAreaKind.Courts)
            {
                int hoops = 0, nets = 0;
                foreach (OutdoorPropPlacement prop in map.Props) if (area.Contains(prop.X, prop.Y))
                {
                    if (prop.Kind == OutdoorPropKind.BasketballHoop) hoops++;
                    if (prop.Kind == OutdoorPropKind.TennisNet)
                    {
                        nets++;
                        Require(area.Width >= area.Height ? prop.Height > prop.Width : prop.Width > prop.Height,
                            "Tennis nets cross the court rather than run along its length");
                    }
                    Require(prop.Kind != OutdoorPropKind.Bench, "Benches do not intrude on the playing surface");
                }
                Require(area.CourtSport == OutdoorCourtSport.Basketball ? hoops == 2 && nets == 0 : hoops == 0 && nets == 1,
                    "Court geometry matches its chosen sport");
            }
            if (area.Kind == OutdoorAreaKind.Maintenance)
                foreach (OutdoorPropKind kind in new[] { OutdoorPropKind.Shed, OutdoorPropKind.Crates, OutdoorPropKind.Dumpster })
                {
                    bool found = false;
                    foreach (OutdoorPropPlacement prop in map.Props) if (prop.Kind == kind && area.Contains(prop.X, prop.Y)) found = true;
                    Require(found, "Maintenance has its recognizable " + kind);
                }
            foreach (OutdoorArea other in map.Areas)
                if (!ReferenceEquals(area, other))
                    Require(area.X + area.Width <= other.X || other.X + other.Width <= area.X
                        || area.Y + area.Height <= other.Y || other.Y + other.Height <= area.Y, "Area bounds do not overlap");
        }
        foreach (OutdoorParkingBay bay in map.ParkingBays)
            Require(bay.Area.Kind == OutdoorAreaKind.Parking && bay.Area.Contains(bay.X, bay.Y)
                && bay.Area.Contains(bay.X + bay.Width - 1, bay.Y + bay.Height - 1), "Marked bays stay within the parking area");
        foreach (OutdoorPropPlacement prop in map.Props)
            if (prop.Kind == OutdoorPropKind.Car || prop.Kind == OutdoorPropKind.Bus)
            {
                bool inBay = false;
                foreach (OutdoorParkingBay bay in map.ParkingBays)
                    if (prop.X >= bay.X && prop.Y >= bay.Y && prop.X + prop.Width <= bay.X + bay.Width && prop.Y + prop.Height <= bay.Y + bay.Height) inBay = true;
                Require(inBay, "Vehicles align with marked bays and leave the reserved aisle open");
            }
        Require(map.LongestDeadEnd() <= settings.MaximumDeadEndLength, "No unavoidable long dead ends");
        var spawns = new HashSet<int>();
        foreach (int cell in map.PlayerSpawns)
        {
            Require(spawns.Add(cell) && reached.Contains(cell) && !occupied.Contains(cell), "Distinct player spawns use clear, reachable cells");
            foreach (int other in spawns)
                if (other != cell) Require(Math.Abs(cell % map.Width - other % map.Width) + Math.Abs(cell / map.Width - other / map.Width)
                    >= settings.MinimumSpawnDistance, "Player reservation separation is honored");
            for (int dy = -settings.SpawnClearance; dy <= settings.SpawnClearance; dy++)
            for (int dx = -settings.SpawnClearance; dx <= settings.SpawnClearance; dx++)
                Require(map.IsWalkable(cell % map.Width + dx, cell / map.Width + dy), "Spawn clearance is honored");
        }
    }

    private static string Signature(SurvivalMapLayout map)
    {
        var text = new System.Text.StringBuilder();
        for (int y = 0; y < map.Height; y++) for (int x = 0; x < map.Width; x++)
            text.Append((int)map.GetSurface(x, y)).Append(':').Append(map.IsWalkable(x, y) ? '+' : '-');
        foreach (OutdoorPropPlacement prop in map.Props) text.Append($"{prop.Kind}:{prop.X},{prop.Y}:{prop.Width},{prop.Height}:{prop.BlocksMovement}:{prop.QuarterTurns};");
        foreach (OutdoorArea area in map.Areas) text.Append($"{area.Kind}:{area.CourtSport}:{area.IsPocket}:{area.WaterX},{area.WaterY},{area.WaterWidth},{area.WaterHeight};");
        foreach (int spawn in map.PlayerSpawns) text.Append(spawn).Append(',');
        return text.ToString();
    }
    private static void CheckAlternateRoutes(SurvivalMapLayout map)
    {
        //Independently check that removing any main-path tile cannot disconnect the board.
        var discovery = new int[map.Width * map.Height];
        var low = new int[discovery.Length];
        var cuts = new HashSet<int>();
        int time = 0;
        void Visit(int cell, int parent)
        {
            discovery[cell] = low[cell] = ++time;
            int children = 0;
            foreach (int next in map.GetNeighbours(cell))
            {
                if (next == parent) continue;
                if (discovery[next] == 0)
                {
                    children++; Visit(next, cell); low[cell] = Math.Min(low[cell], low[next]);
                    if (parent >= 0 && low[next] >= discovery[cell]) cuts.Add(cell);
                }
                else low[cell] = Math.Min(low[cell], discovery[next]);
            }
            if (parent < 0 && children > 1) cuts.Add(cell);
        }
        Visit(map.PlayerSpawns[0], -1);
        foreach (int cut in cuts) Require(!map.IsMainPath(cut % map.Width, cut / map.Width), "Main path network has alternate routes, not single-tile bottlenecks");
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new Exception("FAIL: " + message); }
}
