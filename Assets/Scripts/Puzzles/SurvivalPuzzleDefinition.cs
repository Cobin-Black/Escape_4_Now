// Inspector choices for one puzzle type, including its prefab, allowed areas, difficulty and matching symbols.

using System;
using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Puzzles
{
    public enum SurvivalPuzzleType
    {
        MatchWires,
        ButtonSequence,
        Circuit,
        EnterCode,
        PowerSwitches,
        SortSupplies,
        ArrangeNumbers,
        Batteries,
        Breaker
    }

    [CreateAssetMenu(menuName = "Escape 4 Now/Survival/Puzzle Definition")]
    public sealed class SurvivalPuzzleDefinition : ScriptableObject
    {
        [Header("Pool and Placement")]
        public bool enabledInPool = true;
        public SurvivalPuzzleType type;
        public string title;
        [Tooltip("Short label above the station on the map. Leave empty to use the full puzzle title.")]
        public string mapLabel;
        public string MapLabel => string.IsNullOrWhiteSpace(mapLabel) ? title : mapLabel;
        [TextArea]
        public string instruction;
        public SurvivalWorldMarker stationPrefab;
        public OutdoorAreaKind[] compatibleAreas;
        [Header("Attempt Time Limit")]
        [Min(1f), Tooltip("Seconds available for this attempt, including any sequence preview. Pause stops this countdown.")]
        public float timeLimitSeconds = 15f;
        [Header("Short Puzzle Difficulty")]
        [Range(2, 6)]
        public int wireCount = 4;
        [Range(3, 5)]
        public int sequenceLength = 4;
        [Range(2, 6)]
        public int sequenceButtonCount = 4;
        [Range(2, 4)]
        public int circuitSize = 3;
        [Range(2, 5)]
        public int codeLength = 4;
        [Range(2, 6)]
        public int switchCount = 4;
        [Range(2, 6)]
        public int supplyCount = 4;
        [Range(3, 6)]
        public int numberCount = 5;
        [Range(2, 6)]
        public int batteryCount = 4;
        [Range(2, 6)]
        public int breakerCount = 4;
        [Min(.1f)]
        public float sequenceFlashSeconds = .4f;
        [Min(.1f)]
        public float sequenceGapSeconds = .2f;
        [Header("Visible Matching Keys")]
        public Color[] colors;
        public string[] symbols;
        [Tooltip("Optional native UI icon prefabs, indexed like Symbols and Colors. Used by supplies and batteries.")]
        public GameObject[] matchingIconPrefabs;
        public bool Allows(OutdoorAreaKind area) => compatibleAreas != null && Array.IndexOf(compatibleAreas, area) >= 0;
        public int Count => type == SurvivalPuzzleType.MatchWires ? wireCount : type == SurvivalPuzzleType.SortSupplies ? supplyCount : type == SurvivalPuzzleType.Batteries ? batteryCount : type == SurvivalPuzzleType.ArrangeNumbers ? numberCount : type == SurvivalPuzzleType.Breaker ? breakerCount : type == SurvivalPuzzleType.ButtonSequence ? sequenceButtonCount : switchCount;

        public void Validate()
        {
            if (timeLimitSeconds < 1f || float.IsNaN(timeLimitSeconds) || float.IsInfinity(timeLimitSeconds))
                throw new InvalidOperationException("Assign a positive task time limit on Survival puzzle '" + name + "'.");
            if (stationPrefab == null)
                throw new InvalidOperationException("Assign Station Prefab on Survival puzzle '" + name + "'.");
            if (compatibleAreas == null || compatibleAreas.Length == 0)
                throw new InvalidOperationException("Assign Compatible Areas on Survival puzzle '" + name + "'.");
            if (Count < 2 || Count > 6 || circuitSize < 2 || circuitSize > 4 || codeLength < 2 || codeLength > 5 || sequenceLength < 3 || sequenceLength > 5)
                throw new InvalidOperationException("Check difficulty/count ranges on Survival puzzle '" + name + "'.");
            if (colors == null || colors.Length < 6 || symbols == null || symbols.Length < colors.Length)
                throw new InvalidOperationException("Assign at least six matching Colors and Symbols on Survival puzzle '" + name + "'.");
        }
    }
}
