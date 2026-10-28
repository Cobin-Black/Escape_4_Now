// Finds routes on either map. Maps with movement costs can choose a cheaper route instead of the shortest one.

using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Map
{
    internal static class GridPathfinding
    {
        private static readonly Vector2Int[] Steps = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        internal static bool TryFindPath(IPlayerGridMap map, Vector2Int start, Vector2Int end,
            out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();
            if (!map.IsWalkable(start) || !map.IsWalkable(end) || start == end) return false;
            // Use cost-aware routing only when the map supplies costs; ordinary maps keep their existing route search.
            // Use cost-aware routing only when the map supplies costs; ordinary maps keep their existing route search.
            if (map is IWeightedPlayerGridMap weighted) return WeightedPath(map, weighted, start, end, out path);
            var pending = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            pending.Enqueue(start);
            previous[start] = start;
            while (pending.Count > 0)
            {
                Vector2Int cell = pending.Dequeue();
                if (cell == end)
                {
                    for (Vector2Int step = end; step != start; step = previous[step]) path.Add(step);
                    path.Reverse();
                    return true;
                }
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int next = cell + step;
                    if (!map.IsWalkable(next) || previous.ContainsKey(next)) continue;
                    previous[next] = cell;
                    pending.Enqueue(next);
                }
            }
            return false;
        }

        private sealed class SearchNode { public Vector2Int cell; public int cost, sequence; }
        // Compare the total movement cost to reach each tile, which lets a route avoid expensive mud or wet ground.
        // Compare the total movement cost to reach each tile, which lets a route avoid expensive mud or wet ground.
        private static bool WeightedPath(IPlayerGridMap map, IWeightedPlayerGridMap weighted,
            Vector2Int start, Vector2Int end, out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();
            var pending = new SortedSet<SearchNode>(Comparer<SearchNode>.Create((a, b) => a.cost != b.cost
                ? a.cost.CompareTo(b.cost) : a.sequence.CompareTo(b.sequence)));
            int sequence = 0;
            var costs = new Dictionary<Vector2Int, int> { [start] = 0 };
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            pending.Add(new SearchNode { cell = start, sequence = sequence++ });
            while (pending.Count > 0)
            {
                var node = pending.Min; pending.Remove(node);
                if (node.cost != costs[node.cell]) continue;
                if (node.cell == end)
                {
                    for (var step = end; step != start; step = previous[step]) path.Add(step);
                    path.Reverse(); return true;
                }
                foreach (var direction in Steps)
                {
                    var next = node.cell + direction; if (!map.IsWalkable(next)) continue;
                    int cost = node.cost + System.Math.Max(1, weighted.TraversalCost(next));
                    if (costs.TryGetValue(next, out int best) && best <= cost) continue;
                    costs[next] = cost; previous[next] = node.cell;
                    pending.Add(new SearchNode { cell = next, cost = cost, sequence = sequence++ });
                }
            }
            return false;
        }
    }
}
