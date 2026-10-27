// Converts an obstacle's grid facing into the direction used for its isometric drawing.

using System;
using System.Collections.Generic;
using Escape4Now.Map;

namespace Escape4Now.Obstacles
{
    //Grid directions for the front of an obstacle. Keep these values for saved prefabs.
    public enum ObstacleFacing { South, West, North, East }

    //Chooses a direction from the finished floor plan without moving any objects.
    public static class ObstacleOrientation
    {
        //Returns one tile step in the chosen direction.
        public static void GetStep(ObstacleFacing facing, out int x, out int y)
        {
            x = facing == ObstacleFacing.West ? -1 : facing == ObstacleFacing.East ? 1 : 0;
            y = facing == ObstacleFacing.South ? -1 : facing == ObstacleFacing.North ? 1 : 0;
        }

        //Prefers a wall behind the object and clear floor in front. Desks can face a target.
        public static ObstacleFacing Choose(SchoolMapLayout map, int cell, ISet<int> blocked, int target = -1)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (cell < 0 || cell >= SchoolMapLayout.Size * SchoolMapLayout.Size
                || map.GetTile(cell % SchoolMapLayout.Size, cell / SchoolMapLayout.Size) != SchoolTileType.Floor)
                throw new ArgumentOutOfRangeException(nameof(cell));
            int x = cell % SchoolMapLayout.Size, y = cell / SchoolMapLayout.Size;
            bool hasTarget = target >= 0 && target < SchoolMapLayout.Size * SchoolMapLayout.Size;
            int bestScore = int.MinValue;
            ObstacleFacing best = ObstacleFacing.South;
            for (int i = 0; i < 4; i++)
            {
                var facing = (ObstacleFacing)i;
                GetStep(facing, out int dx, out int dy);
                int clear = 0;
                for (int distance = 1; distance <= 3; distance++)
                {
                    int frontX = x + dx * distance, frontY = y + dy * distance;
                    if (!SameArea(map, x, y, frontX, frontY)
                        || (blocked != null && blocked.Contains(frontY * SchoolMapLayout.Size + frontX))) break;
                    clear++;
                }
                if (clear == 0) continue;
                int wallDistance = SchoolMapLayout.Size;
                for (int distance = 1; distance < SchoolMapLayout.Size; distance++)
                {
                    int backX = x - dx * distance, backY = y - dy * distance;
                    if (map.GetTile(backX, backY) == SchoolTileType.Wall) { wallDistance = distance; break; }
                    if (!SameArea(map, x, y, backX, backY)) break;
                }
                int score = -wallDistance * 10 + clear;
                if (hasTarget)
                    score += 1000 * (dx * (target % SchoolMapLayout.Size - x) + dy * (target / SchoolMapLayout.Size - y));
                //Ties keep the first direction, so the same seed looks the same on every computer.
                if (score > bestScore) { bestScore = score; best = facing; }
            }
            return best;
        }

        //A clear front stays inside its room or hallway, not across a doorway.
        private static bool SameArea(SchoolMapLayout map, int x, int y, int otherX, int otherY)
        {
            return map.GetTile(otherX, otherY) == SchoolTileType.Floor
                && map.GetRoomType(x, y) == map.GetRoomType(otherX, otherY)
                && map.GetSpaceType(x, y) == map.GetSpaceType(otherX, otherY);
        }
    }
}
