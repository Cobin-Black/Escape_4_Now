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
                // Stage props belong in the wings, leaving the middle/front open for chasing.
                if (pattern.edgeOnly && pattern.zone == ArenaObstacleZone.Stage && center.x > pattern.edgeDepth + 1 && center.x < map.Width - pattern.edgeDepth - 2)
                    continue;
                int turns;
                // Edge groups stand flush against a wall or the edge of their floor area (such as the court's sideline),
                // with their backs to it and facing into the room, so they look arranged instead of dropped on open floor.
                if (pattern.edgeOnly)
                {
                    if (!AgainstEdge(map, center, pattern, out turns))
                        continue;
                }
                else
                    turns = ArenaPropPlacement.Facing(center, map, pattern.facing, pattern.fixedQuarterTurns);
                var cells = new List<Vector2Int>();
                bool safe = true;
                foreach (var piece in pattern.pieces)
                {
                    var cell = center + Rotate(piece.offset, turns);
                    // Edge groups keep their clearance in front and to the sides, but sit right up against what is behind them.
                    bool clear = pattern.edgeOnly ? ClearExceptBehind(map, cell, pattern.clearance, Rotate(new Vector2Int(0, -1), turns))
                        : map.ClearSquare(cell, pattern.clearance);
                    if (piece.prefab == null || cells.Contains(cell) || !clear || !InZone(map, cell, pattern.zone) || Protected(map, cell, settings.centerRouteWidth))
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

        // Choose an obstacle pattern using its configured weight.
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

        // Finds a facing where every back-row piece has a wall (or the end of its zone) right behind it and open zone
        // floor in front. The room-facing direction is tried first; a Fixed pattern only tries its own facing.
        // Prefab fronts point down the logical Y axis, so turn 0 faces (0, -1) and each turn rotates a quarter.
        private static bool AgainstEdge(ArenaLayout map, Vector2Int center, ArenaObstaclePattern pattern, out int turns)
        {
            bool fixedFacing = pattern.facing == ArenaPropFacing.Fixed;
            int preferred = ArenaPropPlacement.Facing(center, map, fixedFacing ? ArenaPropFacing.Fixed : ArenaPropFacing.TowardRoom, pattern.fixedQuarterTurns);
            for (int step = 0; step < (fixedFacing ? 1 : 4); step++)
            {
                turns = (preferred + step) % 4;
                var front = Rotate(new Vector2Int(0, -1), turns);
                var cells = new List<Vector2Int>();
                foreach (var piece in pattern.pieces)
                    cells.Add(center + Rotate(piece.offset, turns));
                bool flush = true;
                foreach (var cell in cells)
                {
                    // Pieces stacked one behind another only need the back one against the edge.
                    // On the stage only a real wall or curtain counts, so nothing stands at the front lip facing backstage.
                    bool backOpen = !cells.Contains(cell - front) && map.Walkable(cell - front)
                        && (pattern.zone == ArenaObstacleZone.Stage || InZone(map, cell - front, pattern.zone));
                    bool frontClosed = !cells.Contains(cell + front) && (!map.Walkable(cell + front) || !InZone(map, cell + front, pattern.zone));
                    if (backOpen || frontClosed)
                    {
                        flush = false;
                        break;
                    }
                }
                if (flush)
                    return true;
            }
            turns = 0;
            return false;
        }

        // Like ArenaLayout.ClearSquare, but ignores the row behind the prop, where the wall or zone edge is.
        private static bool ClearExceptBehind(ArenaLayout map, Vector2Int cell, int radius, Vector2Int front)
        {
            if (!map.Walkable(cell))
                return false;
            for (int x = -radius; x <= radius; x++)
                for (int y = -radius; y <= radius; y++)
                {
                    var offset = new Vector2Int(x, y);
                    // Skip anything on the back side of the prop.
                    if (offset.x * front.x + offset.y * front.y < 0)
                        continue;
                    if (!map.Walkable(cell + offset))
                        return false;
                }
            return true;
        }

        // Check the floor type so equipment stays in the intended part of the room.
        private static bool InZone(ArenaLayout map, Vector2Int cell, ArenaObstacleZone zone)
        {
            var surface = map.surfaces[cell.x, cell.y];
            if (zone == ArenaObstacleZone.Stage)
                return surface == ArenaSurface.Stage;
            if (zone == ArenaObstacleZone.Service)
                return surface == ArenaSurface.Service;
            return surface == ArenaSurface.Court || surface == ArenaSurface.Wood || surface == ArenaSurface.Carpet;
        }

        // Keep obstacle placement away from the map's reserved movement routes.
        private static bool Protected(ArenaLayout map, Vector2Int cell, int width)
        {
            if (width <= 0)
                return false;
            int firstX = (map.Width - width) / 2, firstY = (map.Height - width) / 2;
            return cell.x >= firstX && cell.x < firstX + width || cell.y >= firstY && cell.y < firstY + width;
        }

        // Count open neighboring cells when checking whether a route has enough space.
        private static int Degree(ArenaLayout map, Vector2Int cell)
        {
            int count = 0;
            foreach (var d in ArenaLayout.Directions)
                if (map.Walkable(cell + d))
                    count++;
            return count;
        }

        // Rotate an obstacle's cell offset by a whole number of quarter turns.
        private static Vector2Int Rotate(Vector2Int point, int turns)
        {
            for (int i = 0; i < turns; i++)
                point = new Vector2Int(-point.y, point.x);
            return point;
        }
    }
}
