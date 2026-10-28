// Inspector settings for Survival task counts, puzzle choices, exits, protected spaces and result wording.

using System;
using System.Collections.Generic;
using Escape4Now.Map;
using Escape4Now.Puzzles;
using UnityEngine;

namespace Escape4Now.GameModes
{
    public enum SurvivalRole
    {
        Survivor,
        Monster
    }

    public enum SurvivorOutcome
    {
        Active,
        Escaped,
        Dead,
        TimedOut
    }

    [Serializable]
    public sealed class SurvivorTaskCount
    {
        [Range(1, 4)]
        public int survivors = 1;
        [Min(1), Tooltip("Number of different puzzle types the team must complete. Each type counts once.")]
        public int required = 2;
        [Min(1)]
        public int extraStations = 1;
    }

    [CreateAssetMenu(menuName = "Escape 4 Now/Survival/Match Rules")]
    public sealed class SurvivalRulesSettings : ScriptableObject
    {
        [Header("Shared Objectives by Survivor Count")]
        public SurvivorTaskCount[] taskCounts = new SurvivorTaskCount[0];
        [Header("Puzzle Pool")]
        public SurvivalPuzzleDefinition[] puzzles = new SurvivalPuzzleDefinition[0];
        [Header("Evacuation Points")]
        [Min(2)]
        public int escapeCount = 2;
        [Min(1)]
        public int minimumExitSeparation = 12;
        public SurvivalWorldMarker exitPrefab;
        public string exitLabel = "EXIT";
        [Header("Task Interaction and Reservations")]
        [Range(1, 2)]
        public int interactionRange = 1;
        [Range(0, 3)]
        public int taskProtectionRadius = 1;
        [Min(0)]
        public int spawnClearance = 1;
        [Tooltip("Reserved monster footprint in grid tiles. The current MonsterCharacter from main occupies 2 by 2 tiles.")]
        public Vector2Int monsterFootprint = new Vector2Int(2, 2);
        [Min(1)]
        public int minimumTaskSeparation = 5;
        [Header("Validation Limits")]
        [Min(1)]
        public int placementAttempts = 128;
        [Min(1)]
        public int candidatesPerTask = 128;
        [Min(1)]
        public int mapRegenerationAttempts = 8;
        [Header("HUD and Results")]
        public string objectiveFormat = "Puzzles {0}/{1}. Unlock the exits, then escape.";
        public string unlockedObjective = "Exits unlocked. Reach an exit to escape.";
        public string resultTitle = "MATCH RESULTS";
        public string escapedLabel = "ESCAPED";
        public string deadLabel = "ELIMINATED";
        public string timeoutLabel = "DID NOT ESCAPE";
        public string noneLabel = "None";
        // Read the Inspector row for this survivor count rather than calculating a fixed task budget.
        public SurvivorTaskCount ForSurvivors(int count)
        {
            foreach (var rule in taskCounts)
                if (rule != null && rule.survivors == count)
                    return rule;
            throw new InvalidOperationException("Assign Survival task counts for " + count + " survivors.");
        }

        // Reject settings that cannot provide reachable tasks, two exits and an extra puzzle choice.
        public void Validate(int survivors)
        {
            if (monsterFootprint.x < 1 || monsterFootprint.y < 1) throw new InvalidOperationException("Assign a positive monster footprint.");
            var count = ForSurvivors(survivors);
            if (count.required < 1 || count.extraStations < 1 || escapeCount < 2 || exitPrefab == null || placementAttempts < 1 || candidatesPerTask < 1 || mapRegenerationAttempts < 1 || interactionRange < 1 || puzzles == null || puzzles.Length == 0)
                throw new InvalidOperationException("Assign valid Survival rules, extra tasks, two exits and puzzle prefabs.");
            var types = new HashSet<SurvivalPuzzleType>();
            foreach (var puzzle in puzzles)
                if (puzzle != null && puzzle.enabledInPool)
                {
                    puzzle.Validate();
                    types.Add(puzzle.type);
                }
            if (types.Count <= count.required)
                throw new InvalidOperationException("Enable at least one more distinct puzzle type than the required puzzle count, so completed types count once and another option remains available.");
        }
    }
}
