//Checks that spawn selection uses existing space and leaves objectives reachable across seeds.
using System;
using System.Collections.Generic;
using Escape4Now.GameModes;
using Escape4Now.Map;
using UnityEditor;
using UnityEngine;

public static class SurvivalMonsterSpawnChecks
{
    public static void Run()
    {
        try
        {
            var rules = AssetDatabase.LoadAssetAtPath<SurvivalRulesSettings>("Assets/Settings/Survival/Match Rules.asset");
            //Persist the new Inspector defaults without resetting any existing designer values.
            EditorUtility.SetDirty(rules); AssetDatabase.SaveAssets();
            var areas = new HashSet<OutdoorAreaKind>(); var locations = new HashSet<Vector2Int>();
            int checks = 0;
            for (int seed = 0; seed < 128; seed++)
            {
                var map = new SurvivalMapLayout(seed);
                string terrain = Terrain(map);
                for (int survivors = 1; survivors <= 4; survivors++)
                {
                    var plan = new SurvivalObjectiveLayout(map,rules,survivors);
                    Require(Terrain(map) == terrain,"Choosing a monster spawn cannot change terrain or props");
                    Check(map,plan,rules);
                    locations.Add(plan.MonsterSpawn); areas.Add(map.GetArea(plan.MonsterSpawn.x,plan.MonsterSpawn.y));
                    if (seed < 4)
                        Require(new SurvivalObjectiveLayout(map,rules,survivors).MonsterSpawn == plan.MonsterSpawn,"Fixed seeds reproduce the spawn");
                    checks++;
                }
            }
            Require(areas.Count >= 3 && locations.Count > 40,"Monster spawns vary across areas and positions");
            var fixture = new SurvivalMapLayout(42); var objectives = new SurvivalObjectiveLayout(fixture,rules,2);
            var strict = UnityEngine.Object.Instantiate(rules); strict.minimumMonsterSpawnDistance = fixture.Width*fixture.Height;
            string before = Terrain(fixture);
            Require(!SurvivalMonsterSpawnSelector.TryChoose(fixture,objectives,strict,new System.Random(42),out _),"Impossible spacing safely rejects all candidates");
            Require(Terrain(fixture) == before,"Failed searches cannot clear terrain to force a spawn");
            UnityEngine.Object.DestroyImmediate(strict);
            Debug.Log("SURVIVAL MONSTER SPAWN CHECKS PASSED: "+checks+" layouts, "+areas.Count+" area types, "+locations.Count+" positions, footprints, distance, escape paths, deterministic seeds and no terrain mutation.");
            EditorApplication.Exit(0);
        }
        catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    public static void Check(SurvivalMapLayout map,SurvivalObjectiveLayout plan,SurvivalRulesSettings rules)
    {
        Require(plan.HasMonsterSpawn,"A valid procedural spawn was selected");
        var occupied = new HashSet<Vector2Int>();
        for(int y=0;y<rules.monsterFootprint.y;y++) for(int x=0;x<rules.monsterFootprint.x;x++)
        {
            var cell=plan.MonsterSpawn+new Vector2Int(x,y); occupied.Add(cell);
            Require(plan.IsWalkable(cell) && plan.IsReserved(cell) && plan.IsSpawn(cell),"Every footprint tile is walkable and protected from events");
            foreach(int spawn in map.PlayerSpawns)
                Require(SurvivalObjectiveLayout.Distance(cell,new Vector2Int(spawn%map.Width,spawn/map.Width))>=rules.minimumMonsterSpawnDistance,"Full footprint respects survivor separation");
            foreach(var prop in map.Props)
                Require(cell.x<prop.X || cell.y<prop.Y || cell.x>=prop.X+prop.Width || cell.y>=prop.Y+prop.Height,"Spawn cannot overlap a generated prop");
            foreach(var task in plan.Tasks)
            {
                Require(task.Cell!=cell,"Spawn avoids task objects");
                foreach(var space in task.InteractionCells) Require(space!=cell,"Spawn avoids task interactions");
            }
            foreach(var exit in plan.Exits) Require(exit!=cell,"Spawn avoids exit approaches");
        }
        Require(plan.ValidateConnectivity(occupied,false),"An occupied monster footprint leaves all important routes reachable");
    }
    private static string Terrain(SurvivalMapLayout map)
    {
        var values=new System.Text.StringBuilder();
        for(int y=0;y<map.Height;y++) for(int x=0;x<map.Width;x++)
            values.Append((int)map.GetSurface(x,y)).Append(':').Append((int)map.GetArea(x,y)).Append(':').Append(map.IsWalkable(x,y)).Append(';');
        foreach(var prop in map.Props) values.Append(prop.X).Append(',').Append(prop.Y).Append(',').Append(prop.Width).Append(',').Append(prop.Height).Append(';');
        return values.ToString();
    }
    private static void Require(bool condition,string message)
    { if(!condition) throw new InvalidOperationException("FAIL: "+message); }
}
