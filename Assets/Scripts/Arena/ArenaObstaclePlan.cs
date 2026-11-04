using System.Collections.Generic;
using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Arena
{
    public sealed class ArenaPlacedObstacle
    {
        public Vector2Int cell;
        public SurvivalPropVisual prefab;
        public int quarterTurns;
    }

    // Place recognizable groups instead of filling the room with unrelated random props.
    public static class ArenaObstaclePlan
    {
        public static void Apply(ArenaLayout map, ArenaObstacleSettings settings, System.Random random)
        {
            if (settings == null || settings.patterns.Length == 0)
                return;
            int budget = Mathf.Min(settings.maximumBlockedTiles, Mathf.FloorToInt(map.Cells().Count * settings.maximumFloorCoverage));
            int desired = random.Next(Mathf.Max(0, settings.groupCount.x), Mathf.Max(settings.groupCount.x, settings.groupCount.y) + 1);
            var centers = new List<Vector2Int>();
            var candidates = map.Cells();
            if (candidates.Count == 0)
                return;
            ArenaLayout.Shuffle(candidates, random);
            for (int attempt = 0; attempt < settings.placementAttempts && centers.Count < desired && budget > 0; attempt++)
            {
                var pattern = Choose(settings.patterns, random);
                if (pattern == null || pattern.pieces.Length == 0 || pattern.pieces.Length > budget)
                    continue;
                var center = candidates[random.Next(candidates.Count)];
                bool spaced = true;
                foreach (var previous in centers)
                    if (Vector2.Distance(center, previous) < settings.groupSpacing)
                        spaced = false;
                if (!spaced)
                    continue;
                if (pattern.edgeOnly && pattern.zone == ArenaObstacleZone.MainFloor && center.x > pattern.edgeDepth + 1 && center.x < map.Width - pattern.edgeDepth - 2 && center.y > pattern.edgeDepth + 4 && center.y < map.Height - pattern.edgeDepth - 5)
                    continue;
                // Stage props belong in the wings, leaving the middle/front open for chasing.
                if (pattern.edgeOnly && pattern.zone == ArenaObstacleZone.Stage && center.x > pattern.edgeDepth + 1 && center.x < map.Width - pattern.edgeDepth - 2)
                    continue;
                int turns = ArenaPropPlacement.Facing(center, map, pattern.facing, pattern.fixedQuarterTurns);
                var cells = new List<Vector2Int>();
                bool safe = true;
                foreach (var piece in pattern.pieces)
                {
                    var cell = center + Rotate(piece.offset, turns);
                    if (piece.prefab == null || cells.Contains(cell) || !map.ClearSquare(cell, pattern.clearance) || !InZone(map, cell, pattern.zone) || Protected(map, cell, settings.centerRouteWidth))
                    {
                        safe = false;
                        break;
                    }

                    cells.Add(cell);
                }

                if (!safe)
                    continue;
                // Try the whole group as one edit. Roll it back if it cuts a route or creates a new dead end.
                var oldDegrees = new Dictionary<Vector2Int, int>();
                foreach (var cell in cells)
                    foreach (var direction in ArenaLayout.Directions)
                        if (map.Walkable(cell + direction))
                            oldDegrees[cell + direction] = Degree(map, cell + direction);
                foreach (var cell in cells)
                    map.blocked[cell.x, cell.y] = true;
                safe = map.Connected();
                if (safe && settings.rejectNewDeadEnds)
                    foreach (var pair in oldDegrees)
                        if (map.Walkable(pair.Key) && pair.Value >= 2 && Degree(map, pair.Key) < 2)
                            safe = false;
                if (!safe)
                {
                    foreach (var cell in cells)
                        map.blocked[cell.x, cell.y] = false;
                    continue;
                }

                for (int i = 0; i < cells.Count; i++)
                    map.groupedProps.Add(new ArenaPlacedObstacle { cell = cells[i], prefab = pattern.pieces[i].prefab, quarterTurns = turns });
                budget -= cells.Count;
                centers.Add(center);
            }
        }

        private static ArenaObstaclePattern Choose(ArenaObstaclePattern[] patterns, System.Random random)
        {
            float total = 0;
            foreach (var pattern in patterns)
                if (pattern != null)
                    total += Mathf.Max(0, pattern.weight);
            if (total <= 0)
                return null;
            double choice = random.NextDouble() * total;
            foreach (var pattern in patterns)
                if (pattern != null && (choice -= Mathf.Max(0, pattern.weight)) <= 0)
                    return pattern;
            return null;
        }

        private static bool InZone(ArenaLayout map, Vector2Int cell, ArenaObstacleZone zone)
        {
            var surface = map.surfaces[cell.x, cell.y];
            if (zone == ArenaObstacleZone.Stage)
                return surface == ArenaSurface.Stage;
            if (zone == ArenaObstacleZone.Service)
                return surface == ArenaSurface.Service;
            return surface == ArenaSurface.Court || surface == ArenaSurface.Wood || surface == ArenaSurface.Carpet;
        }

        private static bool Protected(ArenaLayout map, Vector2Int cell, int width)
        {
            if (width <= 0)
                return false;
            int firstX = (map.Width - width) / 2, firstY = (map.Height - width) / 2;
            return cell.x >= firstX && cell.x < firstX + width || cell.y >= firstY && cell.y < firstY + width;
        }

        private static int Degree(ArenaLayout map, Vector2Int cell)
        {
            int count = 0;
            foreach (var d in ArenaLayout.Directions)
                if (map.Walkable(cell + d))
                    count++;
            return count;
        }

        private static Vector2Int Rotate(Vector2Int point, int turns)
        {
            for (int i = 0; i < turns; i++)
                point = new Vector2Int(-point.y, point.x);
            return point;
        }
    }
}
