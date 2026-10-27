// Places tasks, exits and spawn reservations on the generated campus, then checks that survivors can reach them.

using System;
using System.Collections.Generic;
using Escape4Now.GameModes;
using Escape4Now.Puzzles;
using UnityEngine;

namespace Escape4Now.Map
{
    public sealed class SurvivalTaskPlacement
    {
        public SurvivalPuzzleDefinition Definition { get; }
        public Vector2Int Cell { get; }
        public IReadOnlyList<Vector2Int> InteractionCells { get; }
        public bool Completed { get; internal set; }

        internal SurvivalTaskPlacement(SurvivalPuzzleDefinition definition, Vector2Int cell, List<Vector2Int> spaces)
        {
            Definition = definition;
            Cell = cell;
            InteractionCells = spaces.AsReadOnly();
        }
    }

    //An overlay on the existing campus, not a second map generator.
    public sealed class SurvivalObjectiveLayout
    {
        public static readonly Vector2Int[] Steps =
        {
            Vector2Int.right,
            Vector2Int.up,
            Vector2Int.left,
            Vector2Int.down
        };
        private readonly SurvivalMapLayout map;
        private readonly SurvivalRulesSettings settings;
        private readonly HashSet<Vector2Int> objects = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> reserved = new HashSet<Vector2Int>();
        private readonly List<SurvivalTaskPlacement> tasks = new List<SurvivalTaskPlacement>();
        private readonly List<Vector2Int> exits = new List<Vector2Int>();
        public IReadOnlyList<SurvivalTaskPlacement> Tasks => tasks.AsReadOnly();
        public IReadOnlyList<Vector2Int> Exits => exits.AsReadOnly();
        public Vector2Int MonsterSpawn { get; private set; }
        public int Required { get; }
        public int SurvivorCount { get; }

        public bool IsReserved(Vector2Int cell) => reserved.Contains(cell);
        public bool IsTaskObject(Vector2Int cell) => objects.Contains(cell);
        public bool IsWalkable(Vector2Int cell) => map.IsWalkable(cell.x, cell.y) && !objects.Contains(cell);
        public bool IsSpawn(Vector2Int cell) => cell == MonsterSpawn || ContainsIndex(map.PlayerSpawns, cell);
        private bool ContainsIndex(IReadOnlyList<int> indices, Vector2Int cell)
        {
            foreach (int index in indices)
                if (cell == Cell(index))
                    return true;
            return false;
        }

        private Vector2Int Cell(int index) => new Vector2Int(index % map.Width, index / map.Width);
        public static int Distance(Vector2Int a, Vector2Int b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
        // Reserve spawns first, then try placing opposite exits and enough reachable puzzle stations.
        public SurvivalObjectiveLayout(SurvivalMapLayout map, SurvivalRulesSettings settings, int survivors)
        {
            this.map = map;
            this.settings = settings;
            SurvivorCount = survivors;
            settings.Validate(survivors);
            var count = settings.ForSurvivors(survivors);
            Required = count.required;
            var random = new System.Random(unchecked(map.Seed ^ 0x17A5721));
            for (int attempt = 0; attempt < settings.placementAttempts; attempt++)
            {
                tasks.Clear();
                exits.Clear();
                objects.Clear();
                reserved.Clear();
                foreach (var prop in map.Props)
                    for (int y = prop.Y; y < prop.Y + prop.Height; y++)
                        for (int x = prop.X; x < prop.X + prop.Width; x++)
                            reserved.Add(new Vector2Int(x, y));
                foreach (int spawn in map.PlayerSpawns)
                    Reserve(Cell(spawn), settings.spawnClearance);
                var available = AllCells();
                Shuffle(available, random);
                bool monsterFound = false;
                int bestDistance = -1;
                foreach (var cell in available)
                {
                    bool fits = true;
                    for (int y = 0; y < settings.monsterFootprint.y; y++)
                        for (int x = 0; x < settings.monsterFootprint.x; x++)
                        {
                            var part = cell + new Vector2Int(x, y);
                            if (!IsWalkable(part) || reserved.Contains(part)) fits = false;
                        }
                    if (!fits)
                        continue;
                    int distance = int.MaxValue;
                    foreach (int spawn in map.PlayerSpawns)
                        distance = Math.Min(distance, Distance(cell, Cell(spawn)));
                    if (distance <= bestDistance)
                        continue;
                    bestDistance = distance;
                    MonsterSpawn = cell;
                    monsterFound = true;
                }

                if (!monsterFound)
                    continue;
                for (int y = 0; y < settings.monsterFootprint.y; y++)
                    for (int x = 0; x < settings.monsterFootprint.x; x++)
                        Reserve(MonsterSpawn + new Vector2Int(x, y), settings.spawnClearance);
                int exitSide = random.Next(4);
                for (int exitIndex = 0; exitIndex < settings.escapeCount; exitIndex++)
                {
                    if (exitIndex > 0 && exitIndex % 2 == 0) exitSide = random.Next(4);
                    // Each pair of exits uses opposite sides, so one camped gate does not remove the other escape option.
                    int side = exitIndex % 2 == 0 ? exitSide : exitSide ^ 1;
                    foreach (var cell in available)
                    {
                        if (reserved.Contains(cell) || !OnExitSide(cell, side)) continue;
                        bool far = true;
                        foreach (var exit in exits)
                            if (Distance(exit, cell) < settings.minimumExitSeparation) far = false;
                        if (!far) continue;
                        exits.Add(cell);
                        Reserve(cell, settings.taskProtectionRadius);
                        break;
                    }
                    if (exits.Count != exitIndex + 1) break;
                }

                if (exits.Count != settings.escapeCount)
                    continue;
                var pool = new List<SurvivalPuzzleDefinition>();
                foreach (var definition in settings.puzzles)
                    if (definition != null && definition.enabledInPool && CompatibleExists(definition))
                        pool.Add(definition);
                Shuffle(pool, random);
                var usedTypes = new HashSet<SurvivalPuzzleType>();
                int target = count.required + count.extraStations;
                for (int i = 0; i < target; i++)
                {
                    bool placed = false;
                    // Try unused puzzle types first. Reuse a type only when another unique type cannot be placed.
                    for (int pass = 0; pass < 2 && !placed; pass++)
                        foreach (var definition in pool)
                        {
                            if (pass == 0 && usedTypes.Contains(definition.type))
                                continue;
                            if (TryPlace(definition, available, random))
                            {
                                usedTypes.Add(definition.type);
                                placed = true;
                                break;
                            }
                        }

                    if (!placed)
                        break;
                }

                if (tasks.Count == target && usedTypes.Count > Required && ValidateConnectivity(null))
                    return;
            }

            throw new InvalidOperationException("Could not place reachable Survival tasks and exits with at least one extra puzzle type. Check puzzle compatibility, clearances and count settings.");
        }

        // Solving one type completes all its duplicate stations; the same type cannot add to the team count twice.
        public bool TryCompletePuzzleType(SurvivalTaskPlacement task)
        {
            if (task == null || !tasks.Contains(task)) return false;
            foreach (var station in tasks)
                if (station.Definition.type == task.Definition.type && station.Completed) return false;
            foreach (var station in tasks)
                if (station.Definition.type == task.Definition.type) station.Completed = true;
            return true;
        }

        private bool CompatibleExists(SurvivalPuzzleDefinition definition)
        {
            foreach (var area in map.Areas)
                if (definition.Allows(area.Kind))
                    return true;
            return definition.Allows(OutdoorAreaKind.None);
        }

        private bool OnExitSide(Vector2Int cell, int side)
        {
            if (side < 2)
                return cell.x == (side == 0 ? 1 : map.Width - 2) && cell.y > 1 && cell.y < map.Height - 2;
            return cell.y == (side == 2 ? 1 : map.Height - 2) && cell.x > 1 && cell.x < map.Width - 2;
        }

        // Convert a walkable exit approach tile into the matching gate position on the outer fence.
        public static Vector2Int ExitBoundary(Vector2Int approach, int width, int height)
        {
            if (approach.x == 1) return new Vector2Int(0, approach.y);
            if (approach.x == width - 2) return new Vector2Int(width - 1, approach.y);
            if (approach.y == 1) return new Vector2Int(approach.x, 0);
            if (approach.y == height - 2) return new Vector2Int(approach.x, height - 1);
            throw new ArgumentException("Exit approaches must adjoin the campus perimeter.");
        }

        private List<Vector2Int> AllCells()
        {
            var result = new List<Vector2Int>();
            for (int y = 1; y < map.Height - 1; y++)
                for (int x = 1; x < map.Width - 1; x++)
                    if (map.IsWalkable(x, y))
                        result.Add(new Vector2Int(x, y));
            return result;
        }

        // Test the station as a blocked tile before accepting it, so its object cannot cut off another objective.
        private bool TryPlace(SurvivalPuzzleDefinition definition, List<Vector2Int> candidates, System.Random random)
        {
            Shuffle(candidates, random);
            //Prefer two or more interaction sides before accepting a single safe side.
            for (int sides = 2; sides >= 1; sides--)
            {
                int tried = 0;
                foreach (var cell in candidates)
                {
                    if (reserved.Contains(cell) || !definition.Allows(map.GetArea(cell.x, cell.y)))
                        continue;
                    bool far = true;
                    foreach (var task in tasks)
                        if (Distance(cell, task.Cell) < settings.minimumTaskSeparation)
                            far = false;
                    if (!far || ++tried > settings.candidatesPerTask)
                        continue;
                    var spaces = new List<Vector2Int>();
                    for (int y = -settings.interactionRange; y <= settings.interactionRange; y++)
                        for (int x = -settings.interactionRange; x <= settings.interactionRange; x++)
                        {
                            var space = cell + new Vector2Int(x, y);
                            if (space == cell || Distance(space, cell) > settings.interactionRange)
                                continue;
                            if (IsWalkable(space) && !reserved.Contains(space))
                                spaces.Add(space);
                        }

                    if (spaces.Count < sides)
                        continue;
                    bool adjacent = false;
                    foreach (var space in spaces)
                        if (Distance(space, cell) == 1)
                            adjacent = true;
                    if (!adjacent)
                        continue;
                    objects.Add(cell);
                    if (!ValidateConnectivity(null))
                    {
                        objects.Remove(cell);
                        continue;
                    }

                    tasks.Add(new SurvivalTaskPlacement(definition, cell, spaces));
                    Reserve(cell, settings.taskProtectionRadius);
                    foreach (var space in spaces)
                        Reserve(space, settings.taskProtectionRadius);
                    return true;
                }
            }

            return false;
        }

        // Keep later tasks and events away from this cell and its configured clearance.
        private void Reserve(Vector2Int cell, int radius)
        {
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (Math.Abs(x) + Math.Abs(y) <= radius)
                        reserved.Add(cell + new Vector2Int(x, y));
        }

        // Search from the survivor spawns and check that task interaction spaces and exits are still reachable.
        public bool ValidateConnectivity(ISet<Vector2Int> extraBlocked)
        {
            var visited = new HashSet<Vector2Int>();
            var pending = new Queue<Vector2Int>();
            var start = Cell(map.PlayerSpawns[0]);
            if (!IsWalkable(start) || extraBlocked != null && extraBlocked.Contains(start))
                return false;
            visited.Add(start);
            pending.Enqueue(start);
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                foreach (var step in Steps)
                {
                    var next = cell + step;
                    if (!IsWalkable(next) || extraBlocked != null && extraBlocked.Contains(next) || !visited.Add(next))
                        continue;
                    pending.Enqueue(next);
                }
            }

            foreach (var cell in AllCells())
                if (IsWalkable(cell) && (extraBlocked == null || !extraBlocked.Contains(cell)) && !visited.Contains(cell))
                    return false;
            foreach (var task in tasks)
            {
                bool reachable = false;
                foreach (var cell in task.InteractionCells)
                    if (visited.Contains(cell))
                        reachable = true;
                if (!reachable)
                    return false;
            }

            foreach (var exit in exits)
                if (!visited.Contains(exit))
                    return false;
            return visited.Contains(MonsterSpawn);
        }

        public static void Shuffle<T>(IList<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T saved = list[i];
                list[i] = list[j];
                list[j] = saved;
            }
        }
    }
}
