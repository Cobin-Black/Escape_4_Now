using System;
using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Arena
{
    // Both rooms keep their identity. Randomness changes equipment and door locations, not the whole building.
    public sealed class ArenaLayout
    {
        public readonly bool[, ] blocked;
        public readonly ArenaSurface[, ] surfaces;
        public readonly List<Vector2Int> seats = new List<Vector2Int>();
        public readonly List<Vector2Int> props = new List<Vector2Int>();
        public readonly List<ArenaPlacedObstacle> groupedProps = new List<ArenaPlacedObstacle>();
        public readonly Dictionary<Vector2Int, int> fixtures = new Dictionary<Vector2Int, int>();
        public readonly int Width, Height;
        public ArenaLayout(ArenaMapSettings settings, System.Random random)
        {
            Width = Mathf.Max(20, settings.size.x);
            Height = Mathf.Max(20, settings.size.y);
            blocked = new bool[Width, Height];
            surfaces = new ArenaSurface[Width, Height];
            int depth = Mathf.Clamp(settings.sideRoomDepth, 3, Height / 4);
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    blocked[x, y] = x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
                    surfaces[x, y] = settings.room == ArenaRoom.Gymnasium ? ArenaSurface.Wood : ArenaSurface.Carpet;
                    if (y < depth || y >= Height - depth)
                        surfaces[x, y] = ArenaSurface.Service;
                }

            // Two entrances per side room provide routes through the back rooms and around the main floor.
            foreach (int wallY in new[]
            {
                depth,
                Height - depth - 1
            }

            )
            {
                int opening = Mathf.Clamp(settings.aisleWidth, 2, 4);
                int a = random.Next(3, Width / 3);
                int b = random.Next(Width * 2 / 3, Width - opening - 2);
                for (int x = 1; x < Width - 1; x++)
                    blocked[x, wallY] = !(x >= a && x < a + opening || x >= b && x < b + opening);
                int middle = Width / 2;
                for (int y = wallY < Height / 2 ? 1 : wallY + 1; y < (wallY < Height / 2 ? wallY : Height - 1); y++)
                    blocked[middle, y] = y != (wallY < Height / 2 ? depth / 2 : Height - depth / 2);
            }

            if (settings.room == ArenaRoom.Gymnasium)
            {
                for (int x = 4; x < Width - 4; x++)
                    for (int y = depth + 2; y < Height - depth - 2; y++)
                        surfaces[x, y] = ArenaSurface.Court;
            }
            else
            {
                int stageStart = Height - depth - Mathf.Clamp(settings.stageDepth, 3, 7);
                for (int x = 2; x < Width - 2; x++)
                    for (int y = stageStart; y < Height - depth - 1; y++)
                        surfaces[x, y] = ArenaSurface.Stage;
                int aisle = Mathf.Clamp(settings.aisleWidth, 2, 4);
                int centerStart = (Width - aisle) / 2, centerEnd = centerStart + aisle;
                int block = Mathf.Clamp(settings.seatingBlockWidth, 2, centerStart - aisle - 1);
                int rowStep = Mathf.Max(1, settings.seatingRowSpacing);
                int sectionDepth = Mathf.Max(1, settings.seatingRowsPerSection) * rowStep;
                // Two balanced audience blocks face the stage. Cross aisles separate short row groups.
                for (int y = depth + 2; y < stageStart - aisle; y++)
                {
                    int row = (y - depth - 2) % (sectionDepth + aisle);
                    if (row >= sectionDepth || row % rowStep != 0)
                        continue;
                    for (int x = centerStart - block; x < centerEnd + block; x++)
                    {
                        if (x >= centerStart && x < centerEnd)
                            continue;
                        var cell = new Vector2Int(x, y);
                        blocked[x, y] = true;
                        seats.Add(cell);
                    }
                }
            }

            if (settings.room == ArenaRoom.Gymnasium)
            {
                Fixture(new Vector2Int(4, Height / 2), 0);
                Fixture(new Vector2Int(Width - 5, Height / 2), 0);
                Fixture(new Vector2Int(2, Height / 2 - 3), 1);
                Fixture(new Vector2Int(2, Height / 2 + 3), 1);
                Fixture(new Vector2Int(Width - 3, Height / 2 - 3), 2);
                Fixture(new Vector2Int(Width - 3, Height / 2 + 3), 2);
            }

            int count = random.Next(Mathf.Max(0, settings.equipmentCount.x), Mathf.Max(settings.equipmentCount.x, settings.equipmentCount.y) + 1);
            for (int attempt = 0; attempt < count * 20 && props.Count < count; attempt++)
            {
                var cell = new Vector2Int(random.Next(2, Width - 2), random.Next(2, Height - 2));
                // Equipment belongs in service rooms or along the perimeter, leaving the court and main aisles open.
                if (surfaces[cell.x, cell.y] != ArenaSurface.Service && cell.x > 3 && cell.x < Width - 4)
                    continue;
                int clearance = surfaces[cell.x, cell.y] == ArenaSurface.Service ? 1 : Mathf.Clamp(settings.routeClearance, 1, 2);
                if (!Walkable(cell) || !ClearSquare(cell, clearance))
                    continue;
                blocked[cell.x, cell.y] = true;
                if (!Connected())
                {
                    blocked[cell.x, cell.y] = false;
                    continue;
                }

                props.Add(cell);
            }

            ArenaObstaclePlan.Apply(this, settings.obstacleGroups, random);
        }

        private void Fixture(Vector2Int cell, int kind)
        {
            if (!ClearSquare(cell, 1))
                return;
            blocked[cell.x, cell.y] = true;
            if (!Connected())
            {
                blocked[cell.x, cell.y] = false;
                return;
            }

            fixtures.Add(cell, kind);
        }

        public bool Walkable(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height && !blocked[cell.x, cell.y];
        // Check clearance against the existing walls and equipment before placing anything.
        public bool ClearSquare(Vector2Int cell, int radius)
        {
            for (int x = -radius; x <= radius; x++)
                for (int y = -radius; y <= radius; y++)
                    if (!Walkable(cell + new Vector2Int(x, y)))
                        return false;
            return true;
        }

        public List<Vector2Int> Cells()
        {
            var result = new List<Vector2Int>();
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (!blocked[x, y])
                        result.Add(new Vector2Int(x, y));
            return result;
        }

        // Visit every walkable tile. A missing tile means a route was cut off.
        public bool Connected()
        {
            var cells = Cells();
            if (cells.Count == 0)
                return false;
            var seen = new HashSet<Vector2Int>
            {
                cells[0]
            };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(cells[0]);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var d in Directions)
                    if (Walkable(cell + d) && seen.Add(cell + d))
                        queue.Enqueue(cell + d);
            }

            return seen.Count == cells.Count;
        }

        // Choose separated open tiles from the completed layout, never by fixed coordinates.
        public bool TrySpawns(int count, ArenaMapSettings settings, System.Random random, out List<Vector2Int> result)
        {
            var candidates = Cells();
            Shuffle(candidates, random);
            result = new List<Vector2Int>();
            int tested = 0;
            foreach (var cell in candidates)
            {
                if (++tested > Mathf.Max(count, settings.spawnAttempts))
                    break;
                int open = 0;
                foreach (var d in Directions)
                    if (Walkable(cell + d))
                        open++;
                if (open < settings.minimumOpenNeighbours || !Fits(cell, settings.playerRadius))
                    continue;
                bool distant = true;
                foreach (var other in result)
                    if (Vector2.Distance(cell, other) < settings.minimumSpawnDistance)
                        distant = false;
                if (!distant)
                    continue;
                result.Add(cell);
                if (result.Count == count)
                    return true;
            }

            return false;
        }

        // Circle against nearby solid tile squares; used for spawns, walking, sliding and knockback.
        public bool Fits(Vector2 point, float radius)
        {
            for (int x = Mathf.FloorToInt(point.x - radius + .5f); x <= Mathf.FloorToInt(point.x + radius + .5f); x++)
                for (int y = Mathf.FloorToInt(point.y - radius + .5f); y <= Mathf.FloorToInt(point.y + radius + .5f); y++)
                {
                    if (Walkable(new Vector2Int(x, y)))
                        continue;
                    Vector2 nearest = new Vector2(Mathf.Clamp(point.x, x - .5f, x + .5f), Mathf.Clamp(point.y, y - .5f, y + .5f));
                    if ((nearest - point).sqrMagnitude < radius * radius)
                        return false;
                }

            return true;
        }

        public static void Shuffle<T>(IList<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T value = list[i];
                list[i] = list[j];
                list[j] = value;
            }
        }

        public static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };
    }
}
