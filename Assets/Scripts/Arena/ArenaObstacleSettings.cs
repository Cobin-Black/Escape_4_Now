using System;
using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Arena
{
    public enum ArenaObstacleZone
    {
        MainFloor,
        Stage,
        Service
    }

    [Serializable]
    public sealed class ArenaObstaclePiece
    {
        public Vector2Int offset;
        public SurvivalPropVisual prefab;
    }

    [Serializable]
    public sealed class ArenaObstaclePattern
    {
        public string label;
        public ArenaObstacleZone zone;
        public ArenaPropFacing facing = ArenaPropFacing.TowardRoom;
        [Range(0, 3)]
        public int fixedQuarterTurns;
        [Tooltip("Main-floor groups stay near an edge instead of filling the central court.")]
        public bool edgeOnly = true;
        [Min(1)]
        public int edgeDepth = 3;
        [Min(0)]
        public float weight = 1;
        [Range(0, 2)]
        public int clearance = 1;
        public ArenaObstaclePiece[] pieces = Array.Empty<ArenaObstaclePiece>();
    }

    // Designers arrange small prop groups here; placement and route checks stay in ArenaObstaclePlan.
    [CreateAssetMenu(menuName = "Escape 4 Now/Arena/Obstacle Groups")]
    public sealed class ArenaObstacleSettings : ScriptableObject
    {
        [Header("Density")]
        public Vector2Int groupCount = new Vector2Int(3, 5);
        [Min(0)]
        public int maximumBlockedTiles = 12;
        [Range(0, .25f)]
        public float maximumFloorCoverage = .08f;
        [Min(1)]
        public int placementAttempts = 160;
        [Min(0)]
        public float groupSpacing = 3;
        [Header("Protected Main Routes")]
        [Min(0)]
        public int centerRouteWidth = 2;
        public bool rejectNewDeadEnds = true;
        [Header("Editable Groups and Prefabs")]
        public ArenaObstaclePattern[] patterns = Array.Empty<ArenaObstaclePattern>();
    }
}
