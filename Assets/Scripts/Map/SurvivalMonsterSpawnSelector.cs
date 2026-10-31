//Finds an existing safe space for the monster. It never changes terrain, props or paths.
using System;
using System.Collections.Generic;
using Escape4Now.GameModes;
using UnityEngine;

namespace Escape4Now.Map
{
    public static class SurvivalMonsterSpawnSelector
    {
        public static bool TryChoose(SurvivalMapLayout map, SurvivalObjectiveLayout objectives,
            SurvivalRulesSettings settings, System.Random random, out Vector2Int spawn)
        {
            var candidates = new List<Vector2Int>();
            for (int y = 1; y < map.Height - 1; y++)
                for (int x = 1; x < map.Width - 1; x++) candidates.Add(new Vector2Int(x,y));
            SurvivalObjectiveLayout.Shuffle(candidates, random);
            //Choose between areas as well as tiles, so a large Field does not always win.
            var preferred = new Dictionary<OutdoorAreaKind,List<Vector2Int>>();
            var fallback = new Dictionary<OutdoorAreaKind,List<Vector2Int>>();
            int batch = Math.Min(settings.monsterSpawnAttempts,candidates.Count);
            for (int pass = 0; pass < 2; pass++)
            {
                int start = pass == 0 ? 0 : batch;
                int end = pass == 0 ? batch : candidates.Count;
                for (int i = start; i < end; i++)
                {
                    var cell = candidates[i];
                    if (!IsValid(map,objectives,settings,cell)) continue;
                    var area = map.GetArea(cell.x,cell.y);
                    var pool = settings.preferredMonsterSpawnAreas != null
                        && Array.IndexOf(settings.preferredMonsterSpawnAreas,area) >= 0 ? preferred : fallback;
                    if (!pool.TryGetValue(area,out var cells)) pool.Add(area,cells = new List<Vector2Int>());
                    cells.Add(cell);
                }
                if (preferred.Count > 0) break;
            }
            var chosen = preferred.Count > 0 ? preferred : fallback;
            if (chosen.Count == 0) { spawn = default; return false; }
            var areas = new List<OutdoorAreaKind>(chosen.Keys);
            var positions = chosen[areas[random.Next(areas.Count)]];
            spawn = positions[random.Next(positions.Count)];
            return true;
        }

        public static bool IsValid(SurvivalMapLayout map, SurvivalObjectiveLayout objectives,
            SurvivalRulesSettings settings, Vector2Int anchor)
        {
            int radius = settings.monsterSpawnClearanceRadius;
            for (int y = -radius; y < settings.monsterFootprint.y + radius; y++)
                for (int x = -radius; x < settings.monsterFootprint.x + radius; x++)
                {
                    var cell = anchor + new Vector2Int(x,y);
                    if (!objectives.IsWalkable(cell) || objectives.IsReserved(cell)) return false;
                }
            //Measure from the nearest occupied monster tile, not just its bottom-left anchor.
            foreach (int index in map.PlayerSpawns)
            {
                int x = index % map.Width, y = index / map.Width;
                int dx = Math.Max(Math.Max(anchor.x-x,x-anchor.x-settings.monsterFootprint.x+1),0);
                int dy = Math.Max(Math.Max(anchor.y-y,y-anchor.y-settings.monsterFootprint.y+1),0);
                if (dx+dy < settings.minimumMonsterSpawnDistance) return false;
            }
            int directions = 0;
            foreach (var step in SurvivalObjectiveLayout.Steps)
                if (Fits(objectives,settings,anchor+step)) directions++;
            if (directions < settings.minimumMonsterOpenDirections || !HasEscape(map,objectives,settings,anchor)) return false;
            //Even while occupied, this footprint must leave survivors a route to every task and exit.
            var occupied = new HashSet<Vector2Int>();
            for (int y = 0; y < settings.monsterFootprint.y; y++)
                for (int x = 0; x < settings.monsterFootprint.x; x++) occupied.Add(anchor+new Vector2Int(x,y));
            return objectives.ValidateConnectivity(occupied,false);
        }

        private static bool Fits(SurvivalObjectiveLayout objectives, SurvivalRulesSettings settings, Vector2Int anchor)
        {
            for (int y = 0; y < settings.monsterFootprint.y; y++)
                for (int x = 0; x < settings.monsterFootprint.x; x++)
                    if (!objectives.IsWalkable(anchor+new Vector2Int(x,y))) return false;
            return true;
        }

        //Search positions the whole monster can occupy, rather than a path for a one-tile actor.
        private static bool HasEscape(SurvivalMapLayout map, SurvivalObjectiveLayout objectives,
            SurvivalRulesSettings settings, Vector2Int start)
        {
            var seen = new HashSet<Vector2Int> { start };
            var pending = new Queue<Vector2Int>(); pending.Enqueue(start);
            bool reachedMainPath = false;
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                reachedMainPath |= map.IsMainPath(cell.x,cell.y);
                if (reachedMainPath && seen.Count >= settings.minimumMonsterReachablePositions) return true;
                foreach (var step in SurvivalObjectiveLayout.Steps)
                {
                    var next = cell+step;
                    if (!seen.Contains(next) && Fits(objectives,settings,next)) { seen.Add(next); pending.Enqueue(next); }
                }
            }
            return false;
        }
    }
}
