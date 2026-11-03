using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Arena
{
    public sealed class ArenaEventState
    {
        public ArenaEventEntry Rule { get; internal set; }
        public bool Warning { get; internal set; } = true;
        public float WarningRemaining { get; internal set; }
        public float Elapsed { get; internal set; }
        public bool Ended { get; internal set; }

        public Vector2 Center, Direction = Vector2.right;
        public readonly List<Vector2Int> Cells = new List<Vector2Int>();
        internal Vector2Int blockCell, sourceCell;
        internal bool blocked, landed;
        internal ArenaMap.EquipmentView equipment;
        internal Vector3 originalPosition, originalScale;
        internal ArenaParticipant first, second;
        internal readonly HashSet<ArenaParticipant> hit = new HashSet<ArenaParticipant>();
        internal readonly Dictionary<ArenaParticipant, float> nextHit = new Dictionary<ArenaParticipant, float>();
        internal ArenaEventView View;
        internal readonly List<ArenaEventBall> balls = new List<ArenaEventBall>();
        internal int ballsSpawned;
        internal float nextBall;
    }

    internal sealed class ArenaEventBall
    {
        internal Vector2 position;
        internal float travel;
        internal GameObject visual;
        internal readonly HashSet<ArenaParticipant> hit = new HashSet<ArenaParticipant>();
    }
}
