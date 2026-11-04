using UnityEngine;

namespace Escape4Now.Arena
{
    public enum ArenaPropFacing
    {
        TowardRoom,
        TowardStage,
        TowardAudience,
        Fixed
    }

    // Prefab fronts point down the logical Y axis. Choose a sensible facing before placing a group.
    public static class ArenaPropPlacement
    {
        public static int Facing(Vector2Int cell, ArenaLayout map, ArenaPropFacing rule, int fixedTurns = 0)
        {
            if (rule == ArenaPropFacing.Fixed)
                return (fixedTurns % 4 + 4) % 4;
            if (rule == ArenaPropFacing.TowardStage)
                return 2;
            if (rule == ArenaPropFacing.TowardAudience)
                return 0;
            Vector2 inward = new Vector2((map.Width - 1) * .5f, (map.Height - 1) * .5f) - cell;
            return Mathf.Abs(inward.x) > Mathf.Abs(inward.y) ? (inward.x > 0 ? 1 : 3) : (inward.y > 0 ? 2 : 0);
        }
    }
}
