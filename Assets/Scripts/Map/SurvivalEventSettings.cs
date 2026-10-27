// Inspector settings for enabled events, trigger types, spawn chances, durations, limits and feedback.

using System;
using UnityEngine;

namespace Escape4Now.Map
{
    public enum SurvivalEventTrigger { Tile, Environmental, Global, Pickup }
    public enum SurvivalEventType
    {
        LuckyRoll,
        AdrenalineRush,
        SecondChance,
        TrackBoost,
        EnergyDrink,
        Trip,
        Exhausted,
        BadRoll,
        Freeze,
        PoolsideSlip,
        Mud,
        Warp,
        AreaSwitch,
        RandomEvent,
        LocationSwitch,
        FallenBranch,
        OpenGate,
        Blackout,
        RainStorm,
        SchoolBell,
        Sprinklers,
        LocationShuffle,
        CarAlarm
    }

    [Serializable]
    public sealed class SurvivalEventRule
    {
        public SurvivalEventType type;
        public bool enabled = true;
        public string title;
        [TextArea, Tooltip("Optional message format: {0}=amount, {1}=duration, {2}=remaining moves.")]
        public string feedback;
        public MapEventCategory category;
        [Min(0f)]
        public float weight = 1f;
        [Tooltip("X/Y = minimum/maximum occurrences per match. Counts tile placements, environmental installations, or scheduled activations according to trigger type.")]
        public Vector2Int instancesPerMatch = new Vector2Int(1, 3);
        [Min(0f)]
        public float randomEffectWeight;
        [Min(0)]
        public int amount = 2;
        [Min(1)]
        public int durationTurns = 3;
        [Tooltip("Allowed generated areas for tile/object placement. Global events ignore area filters.")]
        public OutdoorAreaKind[] compatibleAreas;
        [Tooltip("Standard tile marker or environmental/object prefab. Globals use the separate weather/audio references.")]
        public SurvivalWorldMarker prefab;
        public bool Allows(OutdoorAreaKind area) => compatibleAreas == null || compatibleAreas.Length == 0 || Array.IndexOf(compatibleAreas, area) >= 0;
        public bool Supported => type != SurvivalEventType.OpenGate;
        public SurvivalEventTrigger Trigger => TriggerFor(type);
        [Range(0f, 1f), Tooltip("Chance per eligible environmental crossing, such as a Pool deck step.")]
        public float activationChance = 1f;
        // Classify how an event starts; outdoor and global effects do not become normal colored floor tiles.
        public static SurvivalEventTrigger TriggerFor(SurvivalEventType type)
        {
            switch (type)
            {
                case SurvivalEventType.Blackout:
                case SurvivalEventType.RainStorm:
                case SurvivalEventType.SchoolBell:
                case SurvivalEventType.LocationShuffle: return SurvivalEventTrigger.Global;
                case SurvivalEventType.EnergyDrink: return SurvivalEventTrigger.Pickup;
                case SurvivalEventType.Mud:
                case SurvivalEventType.Sprinklers:
                case SurvivalEventType.FallenBranch:
                case SurvivalEventType.PoolsideSlip:
                case SurvivalEventType.TrackBoost:
                case SurvivalEventType.CarAlarm:
                case SurvivalEventType.OpenGate: return SurvivalEventTrigger.Environmental;
                default: return SurvivalEventTrigger.Tile;
            }
        }
    }

    [CreateAssetMenu(menuName = "Escape 4 Now/Survival/Event Settings")]
    public sealed class SurvivalEventSettings : ScriptableObject
    {
        [Header("Event Placement")]
        [Min(0)]
        public int eventCount = 10;
        public bool showEventLocations;
        public SurvivalEventRule[] events = new SurvivalEventRule[0];
        [Header("Per-Match Variety and Density")]
        [Tooltip("Debug only: includes every compatible enabled type, using its upper occurrence limit.")]
        public bool useAllEnabledEventsForDebugging;
        public Vector2Int tileTypesPerMatch = new Vector2Int(4, 6);
        public Vector2Int environmentalTypesPerMatch = new Vector2Int(2, 4);
        public Vector2Int globalTypesPerMatch = new Vector2Int(1, 2);
        public Vector2Int pickupTypesPerMatch = new Vector2Int(0, 1);
        [Tooltip("Random tile count, capped by Event Count, density, occurrence limits and safe placement.")]
        public Vector2Int eventTilesPerMatch = new Vector2Int(6, 10);
        [Range(0f, .25f)] public float maximumEventTileDensity = .02f;
        [Min(0)] public int minimumEventTileDistance = 2;
        [Header("Turn-Based Event Scheduling")]
        [Tooltip("X = minimum and Y = maximum completed actor turns between global event checks.")]
        public Vector2Int globalIntervalTurns = new Vector2Int(3, 6);
        [Range(0f, 1f)] public float globalEventChance = .5f;
        [Tooltip("X = minimum and Y = maximum completed actor turns between environmental checks.")]
        public Vector2Int environmentalIntervalTurns = new Vector2Int(2, 4);
        [Range(0f, 1f)] public float environmentalEventChance = .65f;
        [Header("Environmental Placement")]
        [Min(1)] public int safePlacementAttempts = 64;
        [Min(1)] public int maximumActiveEnvironmentalEffects = 4;
        [Min(0)] public int initialMudPatches = 2;
        [Tooltip("X = minimum and Y = maximum connected tiles per muddy patch.")]
        public Vector2Int mudPatchTiles = new Vector2Int(3, 7);
        [Min(1)] public int maximumSprinklerTiles = 32;
        [Min(1)] public int trackBoostSectionTiles = 3;
        [Min(0)] public int maximumTrackBoostSections = 2;
        [Min(1)] public int minimumTrackSectionSpacing = 4;
        public bool preferTrackStraights = true;
        [Min(1)] public int trackPropClearance = 2;
        [Min(1)] public int poolWetWaterDistance = 2;
        public bool preferLandscapedMud = true;
        [Min(1)] public int mudLandscapeDistance = 2;
        [Min(1)] public int maximumSprinklerHeads = 3;
        [Min(1)] public int sprinklerHeadSpacing = 3;
        [Min(0)] public int maximumPoolWetZones = 2;
        public Vector2Int poolWetPatchTiles = new Vector2Int(3, 5);
        public bool poolSlipOncePerTurn = true;
        [Min(0)] public int energyDrinkPickups = 2;
        [Header("Weather, Bell and Car Alarm Prefabs")]
        public SurvivalAmbientEffect rainEffectPrefab;
        public SurvivalAmbientEffect schoolBellEffectPrefab;
        public SurvivalAmbientEffect carAlarmEffectPrefab;
        [Header("Movement and Temporary Areas")]
        [Min(1)]
        public int minimumRollMovement = 1;
        [Min(1)]
        public int maximumTileCost = 3;
        [Header("Blackout Duration by Active Survivor Count")]
        public int[] blackoutTurns =
        {
            4,
            4,
            3,
            2
        };
        [Header("Feedback")]
        [Min(.1f), Tooltip("Only immediate results use seconds. Ongoing effects stay visible until consumed or expired; Pause freezes both.")]
        public float immediateNotificationSeconds = 4f;
        [Min(1)] public int visibleNotifications = 2;
        public string moreNotificationsFormat = "+{0} other active effects";
        public string blockedEventFormat = "Second Chance blocked {0}.";
        public string skippedEventFormat = "{0}: no new effect; no safe change is available or the effect is already active.";
        // Report unusable settings before match startup rather than silently replacing the designer's choices.
        public void Validate()
        {
            if (immediateNotificationSeconds < .1f || float.IsNaN(immediateNotificationSeconds)
                || float.IsInfinity(immediateNotificationSeconds) || visibleNotifications < 1)
                throw new InvalidOperationException("Assign valid Survival notification duration and visible count.");
            if (eventCount < 0 || minimumRollMovement < 1 || maximumTileCost < 1 || blackoutTurns == null || blackoutTurns.Length < 4 || events == null)
                throw new InvalidOperationException("Assign valid Survival event settings and temporary-effect prefabs.");
            if (globalIntervalTurns.x < 1 || globalIntervalTurns.y < globalIntervalTurns.x || environmentalIntervalTurns.x < 1 || environmentalIntervalTurns.y < environmentalIntervalTurns.x || safePlacementAttempts < 1 || maximumActiveEnvironmentalEffects < 1 || mudPatchTiles.x < 1 || mudPatchTiles.y < mudPatchTiles.x || maximumSprinklerTiles < 1 || trackBoostSectionTiles < 1)
                throw new InvalidOperationException("Assign valid Survival event timing and environmental placement ranges.");
            foreach (var range in new[] { tileTypesPerMatch, environmentalTypesPerMatch, globalTypesPerMatch, pickupTypesPerMatch, eventTilesPerMatch })
                if (range.x < 0 || range.y < range.x || range.y == int.MaxValue) throw new InvalidOperationException("Invalid Survival match event range.");
            if (poolWetPatchTiles.x < 1 || poolWetPatchTiles.y < poolWetPatchTiles.x || maximumEventTileDensity < 0 || maximumEventTileDensity > 1 || minimumEventTileDistance < 0)
                throw new InvalidOperationException("Invalid Survival density or wet-deck placement settings.");
            var types = new System.Collections.Generic.HashSet<SurvivalEventType>();
            foreach (int duration in blackoutTurns)
                if (duration < 1)
                    throw new InvalidOperationException("Blackout durations must be positive.");
            foreach (var rule in events)
            {
                if (rule != null && (!types.Add(rule.type) || rule.instancesPerMatch.x < 0 || rule.instancesPerMatch.y < rule.instancesPerMatch.x || rule.instancesPerMatch.y == int.MaxValue))
                    throw new InvalidOperationException("Duplicate event type or invalid occurrence range: " + rule.type);
                if (rule != null && rule.enabled && rule.Supported && rule.instancesPerMatch.y > 0 && (rule.Trigger == SurvivalEventTrigger.Tile && rule.prefab == null || rule.amount < 0 || rule.durationTurns < 1 || float.IsNaN(rule.weight) || float.IsInfinity(rule.weight) || rule.weight < 0 || float.IsNaN(rule.randomEffectWeight) || float.IsInfinity(rule.randomEffectWeight) || rule.randomEffectWeight < 0))
                    throw new InvalidOperationException("Invalid Survival event rule: " + rule.type);
                if (rule == null || !rule.enabled || !rule.Supported || rule.weight <= 0 || rule.instancesPerMatch.y == 0) continue;
                if ((rule.type == SurvivalEventType.Mud || rule.type == SurvivalEventType.Sprinklers || rule.type == SurvivalEventType.FallenBranch || rule.type == SurvivalEventType.TrackBoost || rule.type == SurvivalEventType.PoolsideSlip || rule.type == SurvivalEventType.EnergyDrink) && rule.prefab == null)
                    throw new InvalidOperationException("Assign the environmental/object prefab for " + rule.type);
                if (rule.type == SurvivalEventType.EnergyDrink && rule.prefab.GetComponent<Escape4Now.Items.EnergyDrinkItem>() == null)
                    throw new InvalidOperationException("Energy Drink requires an inventory pickup prefab.");
                if (rule.type == SurvivalEventType.RainStorm && rainEffectPrefab == null || rule.type == SurvivalEventType.SchoolBell && schoolBellEffectPrefab == null || rule.type == SurvivalEventType.CarAlarm && carAlarmEffectPrefab == null)
                    throw new InvalidOperationException("Assign the weather/audio effect prefab for " + rule.type);
            }
        }
    }
}
