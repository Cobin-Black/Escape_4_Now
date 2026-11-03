using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Arena
{
    // Event changes use the existing room connectivity and circle-fit checks, including all other active blocks.
    public static class ArenaEventSafety
    {
        public static bool ClearOfPlayers(ArenaMatch match, Vector2Int cell, float clearance)
        {
            foreach (var player in match.Players)
                if (player.Alive)
                {
                    Vector2 nearest = new Vector2(Mathf.Clamp(player.Position.x, cell.x - .5f, cell.x + .5f), Mathf.Clamp(player.Position.y, cell.y - .5f, cell.y + .5f));
                    if (Vector2.Distance(nearest, player.Position) < match.Map.Settings.playerRadius + clearance)
                        return false;
                }

            return true;
        }

        // Temporarily test the proposed edit, then restore the map even when validation fails.
        public static bool CanChange(ArenaMatch match, IReadOnlyList<Vector2Int> blocks, Vector2Int? open = null)
        {
            var layout = match.Map.Layout;
            var previous = new Dictionary<Vector2Int, bool>();
            try
            {
                if (open.HasValue)
                {
                    previous[open.Value] = layout.blocked[open.Value.x, open.Value.y];
                    layout.blocked[open.Value.x, open.Value.y] = false;
                }

                foreach (var cell in blocks)
                {
                    if (!layout.Walkable(cell) || !ClearOfPlayers(match, cell, 0))
                        return false;
                    previous[cell] = layout.blocked[cell.x, cell.y];
                    layout.blocked[cell.x, cell.y] = true;
                }

                if (!layout.Connected())
                    return false;
                foreach (var player in match.Players)
                    if (player.Alive && !layout.Fits(player.Position, match.Map.Settings.playerRadius))
                        return false;
                return true;
            }
            finally
            {
                foreach (var pair in previous)
                    layout.blocked[pair.Key.x, pair.Key.y] = pair.Value;
            }
        }

        public static bool ClearLine(ArenaMatch match, Vector2 a, Vector2 b, float radius = .05f)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / Mathf.Max(.05f, match.Map.Settings.playerRadius)));
            for (int i = 0; i <= steps; i++)
                if (!match.Map.Layout.Fits(Vector2.Lerp(a, b, i / (float)steps), radius))
                    return false;
            return true;
        }

        // Validate both existing positions before moving either human player.
        public static bool Swap(ArenaMatch match, ArenaParticipant a, ArenaParticipant b)
        {
            if (a == null || b == null || !a.Alive || !b.Alive || a == b)
                return false;
            var participants = new[]
            {
                a,
                b
            };
            var positions = new[]
            {
                a.Position,
                b.Position
            };
            float spacing = match.Map.Settings.playerRadius * 2;
            return Escape4Now.Map.SharedPositionEffects.TryPermute(positions, new[] { 1, 0 }, (i, point) =>
            {
                if (!match.Map.Layout.Fits(point, match.Map.Settings.playerRadius))
                    return false;
                foreach (var other in match.Players)
                    if (other.Alive && other != a && other != b && Vector2.Distance(point, other.Position) < spacing)
                        return false;
                return true;
            }, (i, point) => participants[i].Place(point));
        }
    }
}
