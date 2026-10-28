// Chooses safe locations for standard event tiles, keeping tasks, spawns and exits protected.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Map
{
    //Moving events must not become unavoidable barriers to tasks or exits.
    public sealed class SurvivalEventLayout
    {
        private readonly Dictionary<Vector2Int, SurvivalEventRule> tiles = new Dictionary<Vector2Int, SurvivalEventRule>();
        private readonly HashSet<Vector2Int> forced = new HashSet<Vector2Int>();
        public IReadOnlyDictionary<Vector2Int, SurvivalEventRule> Tiles => tiles;
        public IEnumerable<Vector2Int> ForcedMovementCells => forced;

        public static bool MovesPlayers(SurvivalEventType type) => type == SurvivalEventType.Warp || type == SurvivalEventType.AreaSwitch || type == SurvivalEventType.LocationSwitch || type == SurvivalEventType.LocationShuffle;
        // Place only standard event tiles here; outdoor regions, objects and globals have their own triggers.
        public SurvivalEventLayout(SurvivalMapLayout map, SurvivalObjectiveLayout objectives, SurvivalEventSettings settings, SurvivalMatchEventPlan plan = null)
        {
            settings.Validate();
            plan = plan ?? new SurvivalMatchEventPlan(map, objectives, settings);
            var random = new System.Random(unchecked(map.Seed ^ 0x47B3E2));
            var candidates = new List<Vector2Int>();
            for (int y = 1; y < map.Height - 1; y++)
                for (int x = 1; x < map.Width - 1; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (objectives.IsWalkable(cell) && !objectives.IsReserved(cell))
                        candidates.Add(cell);
                }

            SurvivalObjectiveLayout.Shuffle(candidates, random);
            foreach (var cell in candidates)
            {
                if (tiles.Count >= plan.TileBudget)
                    break;
                bool spaced = true;
                foreach (var placed in tiles.Keys) if (SurvivalObjectiveLayout.Distance(placed, cell) < settings.minimumEventTileDistance) spaced = false;
                if (!spaced) continue;
                var pool = new List<SurvivalEventRule>();
                foreach (var rule in settings.events)
                {
                    if (rule == null || !plan.CanUse(rule) || rule.Trigger != SurvivalEventTrigger.Tile || !rule.Allows(map.GetArea(cell.x, cell.y)))
                        continue;
                    if (MovesPlayers(rule.type))
                    {
                        forced.Add(cell);
                        // Treat a forced-movement tile as unavailable while checking routes, so a required path cannot force endless teleporting.
                        bool valid = objectives.ValidateConnectivity(forced);
                        forced.Remove(cell);
                        if (!valid)
                            continue;
                    }

                    pool.Add(rule);
                }

                float total = 0;
                foreach (var rule in pool)
                    total += rule.weight;
                float pick = (float)random.NextDouble() * total;
                foreach (var rule in pool)
                {
                    pick -= rule.weight;
                    if (pick >= 0)
                        continue;
                    tiles.Add(cell, rule);
                    plan.Consume(rule);
                    if (MovesPlayers(rule.type))
                        forced.Add(cell);
                    break;
                }
            }
        }
    }
}
