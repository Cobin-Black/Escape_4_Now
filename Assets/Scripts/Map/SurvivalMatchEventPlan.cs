// Chooses this match's event types and use limits without changing the saved Inspector settings.

using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Map
{
    //A seeded match catalog, separate from persistent Inspector settings and placement.
    public sealed class SurvivalMatchEventPlan
    {
        private sealed class Entry { public SurvivalEventRule rule; public int limit, used; }
        private readonly Dictionary<SurvivalEventType, Entry> entries = new Dictionary<SurvivalEventType, Entry>();
        public IEnumerable<SurvivalEventRule> SelectedRules { get { foreach (var entry in entries.Values) yield return entry.rule; } }
        public int TileBudget { get; }
        public bool IsSelected(SurvivalEventType type) => entries.ContainsKey(type);
        public int Limit(SurvivalEventType type) => entries.TryGetValue(type, out var entry) ? entry.limit : 0;
        public int Remaining(SurvivalEventType type) => entries.TryGetValue(type, out var entry) ? entry.limit - entry.used : 0;
        public bool CanUse(SurvivalEventRule rule) => rule != null && rule.enabled && rule.Supported && rule.weight > 0 && Remaining(rule.type) > 0;
        // Count a successful placement or activation against this match's limit.
        public bool Consume(SurvivalEventRule rule)
        {
            if (!CanUse(rule)) return false;
            entries[rule.type].used++;
            return true;
        }

        // Choose a limited mix of compatible event types, leaving enough ordinary map space.
        public SurvivalMatchEventPlan(SurvivalMapLayout map, SurvivalObjectiveLayout objectives, SurvivalEventSettings settings)
        {
            settings.Validate();
            var random = new System.Random(unchecked(map.Seed ^ 0x19AB2));
            var cells = new List<Vector2Int>();
            for (int y = 1; y < map.Height - 1; y++) for (int x = 1; x < map.Width - 1; x++)
            {
                var cell = new Vector2Int(x, y);
                if (objectives.IsWalkable(cell) && !objectives.IsReserved(cell)) cells.Add(cell);
            }
            var groups = new[] { settings.tileTypesPerMatch, settings.environmentalTypesPerMatch, settings.globalTypesPerMatch, settings.pickupTypesPerMatch };
            for (int group = 0; group < groups.Length; group++)
            {
                var pool = new List<SurvivalEventRule>();
                foreach (var rule in settings.events)
                    if (rule != null && rule.enabled && rule.Supported && rule.weight > 0 && rule.instancesPerMatch.y > 0
                        && (int)rule.Trigger == group && Compatible(rule, map, objectives, cells)) pool.Add(rule);
                int maximum = Mathf.Min(groups[group].y, pool.Count);
                int minimum = Mathf.Min(groups[group].x, maximum);
                int count = settings.useAllEnabledEventsForDebugging ? pool.Count : random.Next(minimum, maximum + 1);
                while (pool.Count > 0 && count > 0)
                {
                    float total = 0; foreach (var rule in pool) total += rule.weight;
                    float pick = (float)random.NextDouble() * total;
                    var chosen = pool[pool.Count - 1];
                    foreach (var rule in pool) { pick -= rule.weight; if (pick < 0) { chosen = rule; break; } }
                    pool.Remove(chosen);
                    int limit = settings.useAllEnabledEventsForDebugging ? chosen.instancesPerMatch.y
                        : random.Next(chosen.instancesPerMatch.x, chosen.instancesPerMatch.y + 1);
                    if (limit == 0) continue;
                    entries.Add(chosen.type, new Entry { rule = chosen, limit = limit });
                    count--;
                }
            }
            int target = settings.useAllEnabledEventsForDebugging ? settings.eventCount
                : random.Next(settings.eventTilesPerMatch.x, settings.eventTilesPerMatch.y + 1);
            int tileCapacity = 0;
            foreach (var entry in entries.Values)
                if (entry.rule.Trigger == SurvivalEventTrigger.Tile) tileCapacity += entry.limit;
            // Use the strictest of the configured count, density and selected event limits.
            TileBudget = Mathf.Min(settings.eventCount, target, tileCapacity, Mathf.FloorToInt(cells.Count * settings.maximumEventTileDensity));
        }

        // Leave out event types that have no suitable area or object in this generated map.
        private static bool Compatible(SurvivalEventRule rule, SurvivalMapLayout map, SurvivalObjectiveLayout objectives, List<Vector2Int> cells)
        {
            if (rule.Trigger == SurvivalEventTrigger.Global)
                return rule.type != SurvivalEventType.LocationShuffle || objectives.SurvivorCount > 1;
            if (rule.type == SurvivalEventType.CarAlarm)
            {
                foreach (var prop in map.Props)
                    if (prop.Kind == OutdoorPropKind.Car && map.GetArea(prop.X, prop.Y) == OutdoorAreaKind.Parking && rule.Allows(OutdoorAreaKind.Parking)) return true;
                return false;
            }
            foreach (var cell in cells)
            {
                if (!rule.Allows(map.GetArea(cell.x, cell.y))) continue;
                var surface = map.GetSurface(cell.x, cell.y);
                if (rule.type == SurvivalEventType.TrackBoost && surface != OutdoorSurface.Track) continue;
                if (rule.type == SurvivalEventType.PoolsideSlip && (surface != OutdoorSurface.Deck || map.GetArea(cell.x, cell.y) != OutdoorAreaKind.Pool)) continue;
                if (rule.type == SurvivalEventType.Mud && (surface != OutdoorSurface.Grass || map.IsProtectedRoute(cell.x, cell.y))) continue;
                if (rule.type == SurvivalEventType.FallenBranch && !map.IsMainPath(cell.x, cell.y)) continue;
                if (rule.type == SurvivalEventType.Sprinklers && map.GetArea(cell.x, cell.y) == OutdoorAreaKind.None) continue;
                if (rule.Trigger == SurvivalEventTrigger.Pickup && map.IsProtectedRoute(cell.x, cell.y)) continue;
                return true;
            }
            return false;
        }
    }
}
