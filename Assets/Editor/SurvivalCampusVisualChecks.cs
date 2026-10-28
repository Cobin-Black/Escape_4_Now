// Checks that campus decoration leaves the generated layout, walkable tiles and objective routes unchanged.

using System;
using System.Linq;
using System.Text;
using Escape4Now.Map;
using Escape4Now.GameModes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SurvivalCampusVisualChecks
{
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SurvivalMode.unity");
            var map = Object.FindFirstObjectByType<SurvivalMap>();
            var visual = map.GetComponent<SurvivalCampusVisuals>();
            Require(visual != null && visual.Settings != null, "Authored campus settings are assigned");
            for (int seed = 0; seed < 12; seed++)
            {
                visual.enabled = false;
                Require(map.GenerateWithSeed(seed), "Legacy rendering still generates");
                string before = Fingerprint(map.Layout);
                visual.enabled = true;
                Require(map.GenerateWithSeed(seed), "Polished rendering generates");
                Require(before == Fingerprint(map.Layout), "Visual toggle preserves every tile, prop, spawn and area");
                Require(map.Layout.Validate(out _), "Original campus connectivity remains valid");
                var details = map.GetComponentsInChildren<Transform>(true).First(item => item.name == "Campus surface details");
                Require(details.GetComponentsInChildren<Collider2D>(true).Length == 0 && details.GetComponentsInChildren<Collider>(true).Length == 0,
                    "Surface detail layer contains no blocking colliders");
                var rules = AssetDatabase.LoadAssetAtPath<SurvivalRulesSettings>("Assets/Settings/Survival/Match Rules.asset");
                for (int survivors = 1; survivors <= 4; survivors++)
                {
                    var objectives = new SurvivalObjectiveLayout(map.Layout, rules, survivors);
                    Require(objectives.ValidateConnectivity(null), "Puzzles, spawns and gates remain reachable after presentation");
                }
            }
            Debug.Log("CAMPUS VISUAL CHECKS PASSED: 12 identical gameplay layouts with visuals off/on, 48 connected objective layouts, no decorative colliders.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static string Fingerprint(SurvivalMapLayout map)
    {
        var text = new StringBuilder();
        for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++)text.Append((int)map.GetSurface(x,y)).Append(':').Append(map.IsWalkable(x,y)).Append(':').Append(map.IsProtectedRoute(x,y)).Append(';');
        foreach(var prop in map.Props)text.Append(prop.Kind).Append(':').Append(prop.X).Append(',').Append(prop.Y).Append(':').Append(prop.Width).Append(',').Append(prop.Height).Append(';');
        foreach(int spawn in map.PlayerSpawns)text.Append(spawn).Append(';');
        return text.ToString();
    }
    static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException("FAIL: "+message); }
}
