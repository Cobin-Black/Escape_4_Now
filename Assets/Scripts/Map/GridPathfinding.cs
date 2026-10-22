using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Map
{
    internal static class GridPathfinding
    {
        internal static bool TryFindPath(IPlayerGridMap map, Vector2Int start, Vector2Int end,
            out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();
            if (!map.IsWalkable(start) || !map.IsWalkable(end) || start == end) return false;
            var pending = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            Vector2Int[] steps = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
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
                foreach (Vector2Int step in steps)
                {
                    Vector2Int next = cell + step;
                    if (!map.IsWalkable(next) || previous.ContainsKey(next)) continue;
                    previous[next] = cell;
                    pending.Enqueue(next);
                }
            }
            return false;
        }
    }
}
