using System;
using UnityEngine;

namespace Escape4Now.Arena
{
    // Keep the old numeric IDs so old pickup entries can be detected and disabled during migration.
    public enum ArenaEventKind
    {
        SpeedBoost = 0,
        Shield = 1,
        Slow = 2,
        Teleport = 3,
        PlayerSwap = 4,
        PotatoShuffle = 5,
        ExtraTime = 6,
        TimeCut = 7,
        SlipperyFloor = 8,
        BallBarrage,
        BleachersMove,
        LightsOut,
        HazardZone,
        FallingEquipment,
        KnockbackWave,
        CourtShuffle,
        LockerRoomOpens,
        Spotlight,
        CurtainDrop,
        Blackout,
        StageDoorOpens,
        SeatBlock,
        SoundBlast,
        FallingProp,
        MovingStageLights
    }

    [Serializable]
    public sealed class ArenaEventEntry
    {
        public bool enabled = true;
        public ArenaEventKind kind;
        public string label = "Event";
        [Min(0)]
        public float weight = 1;
        [Min(1)]
        public int maximumPerMatch = 2;
        [Min(0)]
        public float cooldown = 18;
        [Header("Warning and Duration")]
        [Min(.1f)]
        public float warningSeconds = 2.5f;
        [Min(.1f)]
        public float duration = 6;
        [Header("Area and Effect")]
        [Min(.5f)]
        public float radius = 2.5f;
        [Min(1)]
        public int zoneTiles = 9;
        public float amount = 1.5f;
        [Min(0)]
        public int damage;
        [Min(.1f)]
        public float hitInterval = 1.5f;
        [Min(.1f)]
        public float steeringResponse = 2;
        [Range(.1f, 1)]
        public float lightMovementFactor = .8f;
        [Header("Basketballs / Waves")]
        [Min(1)]
        public int ballCount = 3;
        [Min(.1f)]
        public float ballSpeed = 5;
        [Min(.1f)]
        public float ballInterval = .65f;
        [Min(1)]
        public float travelDistance = 8;
        [Min(.1f)]
        public float waveSeconds = 1.2f;
        [Header("Visual and Sound References")]
        public GameObject obstaclePrefab;
        public AudioClip warningSound, activationSound;
        public Color color = new Color(.2f, .85f, 1, .5f);
        public bool IsBlackout => kind == ArenaEventKind.LightsOut || kind == ArenaEventKind.Blackout;
        public bool IsWave => kind == ArenaEventKind.KnockbackWave || kind == ArenaEventKind.SoundBlast;
        public bool IsBlock => kind == ArenaEventKind.FallingEquipment || kind == ArenaEventKind.FallingProp || kind == ArenaEventKind.SeatBlock || kind == ArenaEventKind.CurtainDrop || kind == ArenaEventKind.BleachersMove;
        public bool IsInstant => kind == ArenaEventKind.PlayerSwap || kind == ArenaEventKind.PotatoShuffle || kind == ArenaEventKind.ExtraTime || kind == ArenaEventKind.TimeCut;
        public string DisabledReason => kind == ArenaEventKind.LockerRoomOpens || kind == ArenaEventKind.StageDoorOpens ? "Requires a functional shortcut door. This room has open passages, so this event is unavailable." : kind <= ArenaEventKind.Teleport ? "Legacy proximity pickup. Items are not part of the arena event pool." : "";

        public bool Supports(ArenaRoom room)
        {
            if (DisabledReason.Length > 0)
                return false;
            if (kind == ArenaEventKind.PlayerSwap)
                return true;
            if (room == ArenaRoom.Gymnasium)
                return kind >= ArenaEventKind.SlipperyFloor && kind <= ArenaEventKind.CourtShuffle;
            return kind == ArenaEventKind.PotatoShuffle || kind == ArenaEventKind.ExtraTime || kind == ArenaEventKind.TimeCut || kind >= ArenaEventKind.Spotlight;
        }
    }

    [CreateAssetMenu(menuName = "Escape 4 Now/Arena/Event Pool")]
    public sealed class ArenaEventPool : ScriptableObject
    {
        [Header("Match Selection — Events, Not Pickups")]
        public bool enableEvents = true;
        public Vector2Int count = new Vector2Int(3, 5);
        [Header("Real-Time Scheduling (paused with the match)")]
        public Vector2 initialDelay = new Vector2(5, 9);
        public Vector2 activationInterval = new Vector2(9, 15);
        [Min(1)]
        public int maximumSimultaneous = 2;
        [Min(.1f)]
        public float retryDelay = 2;
        [Min(.1f)]
        public float noticeSeconds = 3;
        [Header("Placement and Fairness")]
        [Min(.1f)]
        public float playerClearance = 1.25f;
        [Min(1)]
        public int safePlacementAttempts = 100;
        [Min(.1f)]
        public float minimumPotatoTimeAfterCut = 3;
        [Header("Enabled Events and Weights")]
        public ArenaEventEntry[] entries = Array.Empty<ArenaEventEntry>();
        [Header("Shared Presentation")]
        public ArenaEventVisualSettings visuals;
        [Range(0, 1)]
        public float volume = .12f;
    }
}
