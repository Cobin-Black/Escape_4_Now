using UnityEngine;

namespace Escape4Now.Arena
{
    // Keep the existing footprint checks, then slide along the actual contacted wall/player surface.
    public static class ArenaMovementCollision
    {
        public static Vector2 Step(ArenaParticipant actor, Vector2 start, Vector2 delta, int contacts, int searches, float skin)
        {
            var match = actor.Match;
            var map = match.Map;
            for (int contact = 0; contact < contacts && delta.sqrMagnitude > .00000001f; contact++)
            {
                if (match.CanOccupy(actor, start + delta))
                    return start + delta;
                float low = 0, high = 1;
                for (int i = 0; i < searches; i++)
                {
                    float mid = (low + high) * .5f;
                    if (match.CanOccupy(actor, start + delta * mid))
                        low = mid;
                    else
                        high = mid;
                }

                Vector2 normal = Normal(actor, start + delta * high);
                float safe = Mathf.Max(0, low - skin / Mathf.Max(skin, delta.magnitude));
                start += delta * safe;
                if (normal.sqrMagnitude < .000001f)
                    break;
                // Normals transform differently from positions under the isometric projection.
                Vector2 screenNormal = new Vector2((normal.x - normal.y) / map.Settings.tileSize.x, (normal.x + normal.y) / map.Settings.tileSize.y).normalized;
                Vector2 remaining = map.ScreenDirection(delta * (1 - safe));
                float inward = Vector2.Dot(remaining, screenNormal);
                if (inward >= 0)
                    break;
                remaining -= screenNormal * inward;
                delta = map.GridDirection(remaining);
            }

            return start;
        }

        private static Vector2 Normal(ArenaParticipant actor, Vector2 point)
        {
            var match = actor.Match;
            float radius = match.Map.Settings.playerRadius;
            Vector2 normal = Vector2.zero;
            for (int x = Mathf.FloorToInt(point.x - radius + .5f); x <= Mathf.FloorToInt(point.x + radius + .5f); x++)
                for (int y = Mathf.FloorToInt(point.y - radius + .5f); y <= Mathf.FloorToInt(point.y + radius + .5f); y++)
                {
                    if (match.Map.Layout.Walkable(new Vector2Int(x, y)))
                        continue;
                    Vector2 nearest = new Vector2(Mathf.Clamp(point.x, x - .5f, x + .5f), Mathf.Clamp(point.y, y - .5f, y + .5f));
                    Vector2 offset = point - nearest;
                    if (offset.sqrMagnitude < radius * radius)
                        normal += offset.normalized;
                }

            foreach (var other in match.Players)
                if (other != actor && other.Alive && (point - other.Position).sqrMagnitude < radius * radius * 4)
                    normal += (point - other.Position).normalized;
            return normal.normalized;
        }
    }
}
