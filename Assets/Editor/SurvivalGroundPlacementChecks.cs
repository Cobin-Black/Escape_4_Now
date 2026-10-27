// Checks many seeds and player counts to make sure ground effects stay connected and avoid reserved tiles.

using System;
using System.Collections.Generic;
using Escape4Now.GameModes;
using Escape4Now.Map;
using UnityEditor;
using UnityEngine;

public static class SurvivalGroundPlacementChecks
{
    public static void Run()
    {
        try
        {
            var rules = AssetDatabase.LoadAssetAtPath<SurvivalRulesSettings>("Assets/Settings/Survival/Match Rules.asset");
            var settings = AssetDatabase.LoadAssetAtPath<SurvivalEventSettings>("Assets/Settings/Survival/Event Settings.asset");
            int checkedRegions = 0;
            for (int seed = 0; seed < 64; seed++)
            {
                var map = new SurvivalMapLayout(seed, new SurvivalGenerationSettings());
                for (int survivors = 1; survivors <= 4; survivors++)
                {
                    var objectives = new SurvivalObjectiveLayout(map, rules, survivors);
                    var events = new SurvivalEventLayout(map, objectives, settings);
                    var candidates = new List<Vector2Int>();
                    for (int y = 0; y < map.Height; y++)
                        for (int x = 0; x < map.Width; x++)
                        {
                            var cell = new Vector2Int(x,y);
                            if (map.GetSurface(x,y) == OutdoorSurface.Grass && !events.Tiles.ContainsKey(cell)
                                && SurvivalEnvironmentalPlacement.IsEligible(map, objectives, cell, true)) candidates.Add(cell);
                        }
                    if (candidates.Count == 0) continue;
                    var patch = SurvivalEnvironmentalPlacement.NaturalPatch(candidates[seed % candidates.Count], candidates,
                        settings.mudPatchTiles.y, new System.Random(seed));
                    var repeat = SurvivalEnvironmentalPlacement.NaturalPatch(candidates[seed % candidates.Count], candidates,
                        settings.mudPatchTiles.y, new System.Random(seed));
                    Require(patch.Count == repeat.Count && patch.TrueForAll(repeat.Contains), "Fixed seeds reproduce ground regions");
                    Require(patch.Count > 0 && patch.Count <= settings.mudPatchTiles.y, "Region size respects its configured budget");
                    var visited = new HashSet<Vector2Int> { patch[0] };
                    var pending = new Queue<Vector2Int>(); pending.Enqueue(patch[0]);
                    while (pending.Count > 0)
                    {
                        var cell = pending.Dequeue();
                        foreach (var step in SurvivalObjectiveLayout.Steps)
                            if (patch.Contains(cell + step) && visited.Add(cell + step)) pending.Enqueue(cell + step);
                    }
                    Require(visited.Count == patch.Count, "A region remains connected");
                    foreach (var cell in patch)
                        Require(!objectives.IsReserved(cell) && !events.Tiles.ContainsKey(cell) && !map.IsProtectedRoute(cell.x,cell.y),
                            "Ground excludes tasks, exits, spawns, events and protected routes");
                    Require(objectives.ValidateConnectivity(null), "Original task and exit connectivity is preserved");
                    checkedRegions++;
                }
            }
            Debug.Log("SURVIVAL GROUND PLACEMENT CHECKS PASSED: " + checkedRegions + " seeded objective layouts across 1–4 survivors; connected, protected, reproducible patches.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("FAIL: " + message); }
}
