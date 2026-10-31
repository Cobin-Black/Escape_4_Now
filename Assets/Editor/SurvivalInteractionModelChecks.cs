//Checks task deadlines and circuit power paths without depending on pointer timing.
using System;
using Escape4Now.Puzzles;
using UnityEditor;
using UnityEngine;

public static class SurvivalInteractionModelChecks
{
    public static void Run()
    {
        try
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:SurvivalPuzzleDefinition",new[]{"Assets/Settings/Survival"}))
            {
                var definition = AssetDatabase.LoadAssetAtPath<SurvivalPuzzleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                definition.Validate();
                var attempt = new PuzzleSession(definition,42);
                attempt.Tick(definition.timeLimitSeconds-.01f);
                Require(!attempt.Failed, "Attempt remains active before its deadline");
                float before = attempt.Elapsed; attempt.Tick(0);
                Require(attempt.Elapsed == before, "Zero elapsed time cannot consume the deadline");
                attempt.Tick(.02f);
                Require(attempt.TimedOut && attempt.Failed && !attempt.Solved, "Every puzzle type times out");
                var solved = new PuzzleSession(definition,37); SurvivalRulesChecks.Solve(solved);
                Require(solved.Solved, "Each generated puzzle remains solvable");
                solved.Tick(definition.timeLimitSeconds*2);
                Require(solved.Solved && !solved.Failed, "A solved puzzle cannot later time out during feedback");
                if (definition.type == SurvivalPuzzleType.Circuit)
                    Require(solved.PoweredCircuitDistances()[solved.EndCell] >= 0, "The power animation reaches the solved output");
                count++;
            }
            Debug.Log("SURVIVAL INTERACTION MODEL CHECKS PASSED: "+count+" puzzle types, deadlines, solvability and circuit power.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void Require(bool condition,string message)
    { if (!condition) throw new InvalidOperationException("FAIL: "+message); }
}
