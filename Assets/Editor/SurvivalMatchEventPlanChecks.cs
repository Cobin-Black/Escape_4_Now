// Checks event variety, occurrence limits and repeatable choices when using a fixed seed.

using System;
using System.Collections.Generic;
using System.Linq;
using Escape4Now.Map;
using UnityEngine;

public static class SurvivalMatchEventPlanChecks
{
    private static readonly HashSet<string> catalogs = new HashSet<string>();
    private static readonly Dictionary<SurvivalEventType, int> frequencies = new Dictionary<SurvivalEventType, int>();
    public static void Check(SurvivalMapLayout map, SurvivalObjectiveLayout objectives, SurvivalEventSettings settings,
        SurvivalMatchEventPlan plan, SurvivalEventLayout layout)
    {
        catalogs.Add(Catalog(plan));
        foreach (var rule in plan.SelectedRules)
            frequencies[rule.type] = Frequency(rule.type) + 1;
        Require(layout.Tiles.Count <= plan.TileBudget, "Match tile budget");
        var ranges = new[] { settings.tileTypesPerMatch, settings.environmentalTypesPerMatch, settings.globalTypesPerMatch, settings.pickupTypesPerMatch };
        foreach (var group in plan.SelectedRules.GroupBy(rule => rule.Trigger))
            Require(group.Count() <= ranges[(int)group.Key].y, "Selected type limits");
        foreach (var rule in plan.SelectedRules)
        {
            Require(rule.enabled && rule.Supported && plan.Limit(rule.type) >= rule.instancesPerMatch.x && plan.Limit(rule.type) <= rule.instancesPerMatch.y,
                "Enabled supported events with valid quotas");
            if (rule.Trigger == SurvivalEventTrigger.Tile)
                Require(layout.Tiles.Values.Count(tile => tile.type == rule.type) <= plan.Limit(rule.type), "Per-type floor quota");
        }
        foreach (var tile in layout.Tiles)
        {
            Require(plan.IsSelected(tile.Value.type), "Only selected types placed");
            foreach (var other in layout.Tiles)
                if (tile.Key != other.Key) Require(Mathf.Abs(tile.Key.x - other.Key.x) + Mathf.Abs(tile.Key.y - other.Key.y) >= settings.minimumEventTileDistance, "Floor spacing");
        }
        var repeat = new SurvivalMatchEventPlan(map, objectives, settings);
        Require(Catalog(plan) == Catalog(repeat), "Fixed seed type and quota reproducibility");
        var repeatedLayout = new SurvivalEventLayout(map, objectives, settings, repeat);
        Require(layout.Tiles.Count == repeatedLayout.Tiles.Count && layout.Tiles.All(pair => repeatedLayout.Tiles.TryGetValue(pair.Key, out var rule) && rule.type == pair.Value.type), "Fixed seed placements");
    }
    public static string Catalog(SurvivalMatchEventPlan plan) => string.Join(";", plan.SelectedRules.Select(rule => rule.type + ":" + plan.Limit(rule.type)));
    public static void VerifyVariety()
    {
        Require(catalogs.Count > 100, "Enabled event pool produces varied matches");
        Require(Frequency(SurvivalEventType.Freeze) < Frequency(SurvivalEventType.AdrenalineRush), "Freeze is less common than a small tile event");
        Require(Frequency(SurvivalEventType.Blackout) < Frequency(SurvivalEventType.SchoolBell), "Blackout is less common than School Bell");
        Require(Frequency(SurvivalEventType.LocationShuffle) < Frequency(SurvivalEventType.SchoolBell), "Location Shuffle is rare");
        Require(Frequency(SurvivalEventType.FallenBranch) < Frequency(SurvivalEventType.Sprinklers), "Blocking branches are rare");
        Debug.Log("SURVIVAL EVENT PLAN CHECKS PASSED: " + catalogs.Count + " distinct catalogs, seeded quotas, spacing, selection and strong-event rarity.");
    }
    private static int Frequency(SurvivalEventType type) => frequencies.TryGetValue(type, out var count) ? count : 0;
    private static void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException("FAIL: " + label); }
}
