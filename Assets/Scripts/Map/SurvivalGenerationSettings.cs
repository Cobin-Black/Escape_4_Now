using System;

namespace Escape4Now.Map
{
    public enum OutdoorAreaKind { None, TrackField, Pool, Courts, Courtyard, Garden, Maintenance, Parking }
    public enum OutdoorSurface { Grass, Path, Track, Field, Deck, Water, Court, Paving, Service, Asphalt, Fence, TennisCourt }
    public enum OutdoorPropKind { Tree, Bench, Planter, Car, Bus, Shed, Fence, Flowers,
        Bush, Bleachers, BasketballHoop, TennisNet, LifeguardChair, LightPole, Barrier, Crates, Dumpster, SportsEquipment }
    public enum OutdoorCourtSport { Any, Basketball, Tennis }
    public enum OutdoorPlacementPattern { Scattered, Edges, Clustered, Ends, Center, ParkingBays }
    public enum OutdoorBlockingMode { Automatic, Blocking, Decoration }

    [Serializable]
    public sealed class OutdoorAreaRule
    {
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField]
#endif
        private OutdoorAreaKind area;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField]
#endif
        private bool required;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(0f, 1f)]
#endif
        private float chance = 1f;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Min(5)]
#endif
        private int minimumWidth = 5, maximumWidth = 8, minimumHeight = 5, maximumHeight = 8;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField]
#endif
        private bool fenced;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(2, 4)]
#endif
        private int entranceCount = 2;

        public OutdoorAreaKind Area => area;
        public bool Required => required;
        public float Chance { get => chance; set => chance = value; }
        public int MinimumWidth => minimumWidth;
        public int MaximumWidth => maximumWidth;
        public int MinimumHeight => minimumHeight;
        public int MaximumHeight => maximumHeight;
        public bool Fenced => fenced;
        public int EntranceCount => entranceCount;

        public OutdoorAreaRule(OutdoorAreaKind area, bool required, float chance, int minWidth, int maxWidth,
            int minHeight, int maxHeight, bool fenced = false, int entrances = 2)
        {
            this.area = area; this.required = required; this.chance = chance;
            minimumWidth = minWidth; maximumWidth = maxWidth;
            minimumHeight = minHeight; maximumHeight = maxHeight;
            this.fenced = fenced; entranceCount = entrances;
        }

        internal bool IsValid => area != OutdoorAreaKind.None && Enum.IsDefined(typeof(OutdoorAreaKind), area)
            && chance >= 0f && chance <= 1f && minimumWidth >= 5 && maximumWidth >= minimumWidth
            && minimumHeight >= 5 && maximumHeight >= minimumHeight && entranceCount >= 2 && entranceCount <= 4;
    }

    [Serializable]
    public sealed class OutdoorPropRule
    {
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField]
#endif
        private OutdoorPropKind prop;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField]
#endif
        private OutdoorAreaKind[] allowedAreas;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Min(0)]
#endif
        private int minimumCount, maximumCount;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(0f, 1f)]
#endif
        private float chance = 1f;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Min(1)]
#endif
        private int footprintWidth = 1, footprintHeight = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Min(0)]
#endif
        private int spacing = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Placement Pattern"), UnityEngine.SerializeField]
#endif
        private OutdoorPlacementPattern pattern;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(1, 4)]
#endif
        private int edgeBandWidth = 2;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(1, 6)]
#endif
        private int clusterRadius = 3;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Tooltip("Any applies to both court styles and to non-court areas.")]
#endif
        private OutdoorCourtSport courtSport;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Tooltip("Automatic preserves existing behavior: flowers are decoration; other props block their footprint.")]
#endif
        private OutdoorBlockingMode blocking;

        public OutdoorPropKind Prop => prop;
        public OutdoorAreaKind[] AllowedAreas => allowedAreas;
        public int MinimumCount => minimumCount;
        public int MaximumCount => maximumCount;
        public float Chance => chance;
        public int FootprintWidth => footprintWidth;
        public int FootprintHeight => footprintHeight;
        public int Spacing => spacing;
        public OutdoorPlacementPattern Pattern => pattern;
        public int EdgeBandWidth => edgeBandWidth;
        public int ClusterRadius => clusterRadius;
        public OutdoorCourtSport CourtSport => courtSport;
        public bool BlocksMovement => blocking == OutdoorBlockingMode.Blocking
            || (blocking == OutdoorBlockingMode.Automatic && prop != OutdoorPropKind.Flowers);

        public OutdoorPropRule(OutdoorPropKind prop, OutdoorAreaKind[] areas, int minimum, int maximum,
            int width = 1, int height = 1, int spacing = 1, float chance = 1f,
            OutdoorPlacementPattern pattern = OutdoorPlacementPattern.Scattered,
            OutdoorCourtSport courtSport = OutdoorCourtSport.Any, OutdoorBlockingMode blocking = OutdoorBlockingMode.Automatic)
        {
            this.prop = prop; allowedAreas = areas; minimumCount = minimum; maximumCount = maximum;
            footprintWidth = width; footprintHeight = height; this.spacing = spacing; this.chance = chance;
            this.pattern = pattern; this.courtSport = courtSport; this.blocking = blocking;
        }

        internal bool IsValid => Enum.IsDefined(typeof(OutdoorPropKind), prop) && allowedAreas != null
            && allowedAreas.Length > 0 && Array.TrueForAll(allowedAreas, area => Enum.IsDefined(typeof(OutdoorAreaKind), area))
            && minimumCount >= 0 && maximumCount >= minimumCount
            && maximumCount <= 256 && footprintWidth >= 1 && footprintHeight >= 1
            && footprintWidth <= 8 && footprintHeight <= 8 && spacing >= 0 && spacing <= 8 && chance >= 0f && chance <= 1f
            && edgeBandWidth >= 1 && edgeBandWidth <= 4 && clusterRadius >= 1 && clusterRadius <= 6
            && Enum.IsDefined(typeof(OutdoorPlacementPattern), pattern) && Enum.IsDefined(typeof(OutdoorCourtSport), courtSport)
            && Enum.IsDefined(typeof(OutdoorBlockingMode), blocking);
    }

    [Serializable]
    public sealed class SurvivalObstacleSettings
    {
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(1, 256)]
#endif
        private int placementAttempts = 80;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Tooltip("Count ranges apply to each matching area. None means unassigned landscaped ground.")]
#endif
        private OutdoorPropRule[] rules =
        {
            new OutdoorPropRule(OutdoorPropKind.Tree, new[] { OutdoorAreaKind.None, OutdoorAreaKind.Garden }, 2, 4, 1, 1, 1, pattern: OutdoorPlacementPattern.Clustered),
            new OutdoorPropRule(OutdoorPropKind.Bench, new[] { OutdoorAreaKind.Courtyard, OutdoorAreaKind.Garden, OutdoorAreaKind.Pool }, 0, 2, 1, 1, 0, pattern: OutdoorPlacementPattern.Edges),
            new OutdoorPropRule(OutdoorPropKind.Planter, new[] { OutdoorAreaKind.Courtyard, OutdoorAreaKind.None }, 1, 2, 1, 1, 1, pattern: OutdoorPlacementPattern.Edges),
            new OutdoorPropRule(OutdoorPropKind.Car, new[] { OutdoorAreaKind.Parking }, 1, 2, 1, 2, 0, pattern: OutdoorPlacementPattern.ParkingBays),
            new OutdoorPropRule(OutdoorPropKind.Bus, new[] { OutdoorAreaKind.Parking }, 0, 1, 1, 3, 0, pattern: OutdoorPlacementPattern.ParkingBays),
            new OutdoorPropRule(OutdoorPropKind.Shed, new[] { OutdoorAreaKind.Maintenance }, 1, 1, 2, 2, 0, pattern: OutdoorPlacementPattern.Edges),
            new OutdoorPropRule(OutdoorPropKind.Flowers, new[] { OutdoorAreaKind.Garden, OutdoorAreaKind.None }, 1, 3, 1, 1, 0, pattern: OutdoorPlacementPattern.Clustered, blocking: OutdoorBlockingMode.Decoration),
            new OutdoorPropRule(OutdoorPropKind.Bush, new[] { OutdoorAreaKind.Garden, OutdoorAreaKind.None }, 2, 4, 1, 1, 0, pattern: OutdoorPlacementPattern.Clustered),
            new OutdoorPropRule(OutdoorPropKind.Bleachers, new[] { OutdoorAreaKind.TrackField }, 0, 2, 2, 1, 0, pattern: OutdoorPlacementPattern.Edges),
            new OutdoorPropRule(OutdoorPropKind.BasketballHoop, new[] { OutdoorAreaKind.Courts }, 2, 2, 1, 1, 0, pattern: OutdoorPlacementPattern.Ends, courtSport: OutdoorCourtSport.Basketball),
            new OutdoorPropRule(OutdoorPropKind.TennisNet, new[] { OutdoorAreaKind.Courts }, 1, 1, 1, 4, 0, pattern: OutdoorPlacementPattern.Center, courtSport: OutdoorCourtSport.Tennis),
            new OutdoorPropRule(OutdoorPropKind.LifeguardChair, new[] { OutdoorAreaKind.Pool }, 0, 1, 1, 1, 0, pattern: OutdoorPlacementPattern.Edges),
            new OutdoorPropRule(OutdoorPropKind.LightPole, new[] { OutdoorAreaKind.Parking, OutdoorAreaKind.Courtyard }, 0, 2, 1, 1, 1, pattern: OutdoorPlacementPattern.Edges),
            new OutdoorPropRule(OutdoorPropKind.Barrier, new[] { OutdoorAreaKind.Parking }, 0, 1, 2, 1, 0, pattern: OutdoorPlacementPattern.Edges),
            new OutdoorPropRule(OutdoorPropKind.Crates, new[] { OutdoorAreaKind.Maintenance }, 1, 2, 1, 1, 0, pattern: OutdoorPlacementPattern.Edges),
            new OutdoorPropRule(OutdoorPropKind.Dumpster, new[] { OutdoorAreaKind.Maintenance }, 1, 1, 1, 2, 0, pattern: OutdoorPlacementPattern.Edges),
            new OutdoorPropRule(OutdoorPropKind.SportsEquipment, new[] { OutdoorAreaKind.TrackField, OutdoorAreaKind.Maintenance }, 0, 1, 1, 1, 0, pattern: OutdoorPlacementPattern.Edges)
        };
        public int PlacementAttempts { get => placementAttempts; set => placementAttempts = value; }
        public OutdoorPropRule[] Rules { get => rules; set => rules = value; }

        public void Validate()
        {
            if (placementAttempts < 1 || placementAttempts > 256 || rules == null)
                throw new ArgumentException("Invalid Survival obstacle placement settings.");
            foreach (OutdoorPropRule rule in rules)
                if (rule == null || !rule.IsValid) throw new ArgumentException("Invalid outdoor obstacle rule.");
        }
    }

    [Serializable]
    public sealed class SurvivalGenerationSettings
    {
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Map Dimensions"), UnityEngine.SerializeField, UnityEngine.Range(24, 96)]
#endif
        private int width = 32;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(24, 96)]
#endif
        private int height = 32;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Paths and Entrances"), UnityEngine.SerializeField, UnityEngine.Range(1, 4)]
#endif
        private int pathWidth = 2;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(0, 3), UnityEngine.Tooltip("Variation in the widths of the major layout bands, in tiles.")]
#endif
        private int partitionJitter = 2;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(1, 2)]
#endif
        private int areaWalkwayWidth = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Min(1)]
#endif
        private int poolDeckWidth = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(1, 2), UnityEngine.Tooltip("Outer circulation strip. Internal main paths still use Path Width.")]
#endif
        private int perimeterPathWidth = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Pool Water"), UnityEngine.SerializeField, UnityEngine.Range(2, 8)]
#endif
        private int minimumPoolWaterWidth = 4, minimumPoolWaterHeight = 4;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(.5f, 1f), UnityEngine.Tooltip("Fraction of the available pool interior used for water; the rest gives furniture additional deck space.")]
#endif
        private float poolWaterFill = .85f;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Garden and Unused Plots"), UnityEngine.SerializeField, UnityEngine.Range(1, 2)]
#endif
        private int gardenPathWidth = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(0, 3)]
#endif
        private int gardenPathBend = 2;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Tooltip("Small secondary courtyards in unused macro plots, surrounded by landscaped ground. Set Chance to zero to disable.")]
#endif
        private OutdoorAreaRule courtyardPocket = new OutdoorAreaRule(OutdoorAreaKind.Courtyard, false, .9f, 5, 6, 5, 6);
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Court Style"), UnityEngine.SerializeField, UnityEngine.Range(0f, 1f)]
#endif
        private float tennisCourtChance = .45f;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Parking Layout"), UnityEngine.SerializeField, UnityEngine.Range(2, 4)]
#endif
        private int parkingBayDepth = 3;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(1, 3)]
#endif
        private int parkingBayWidth = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(0, 2)]
#endif
        private int parkingBaySpacing = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(1, 3)]
#endif
        private int parkingAisleWidth = 2;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(1, 3)]
#endif
        private int trackWidth = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Player Spawn Reservations"), UnityEngine.SerializeField, UnityEngine.Range(1, 8)]
#endif
        private int playerSpawnCount = 4;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Min(0)]
#endif
        private int minimumSpawnDistance = 8;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(0, 3)]
#endif
        private int spawnClearance = 1;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Validation and Retry Limits"), UnityEngine.SerializeField, UnityEngine.Range(1, 256)]
#endif
        private int generationAttempts = 64;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("propPlacementAttempts")]
#endif
        private int previousPlacementAttempts = 80;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.Range(0, 16), UnityEngine.Tooltip("Reject layouts with a longer unavoidable one-tile dead-end branch. Zero forbids all such branches.")]
#endif
        private int maximumDeadEndLength = 3;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.Header("Area Rules"), UnityEngine.SerializeField]
#endif
        private OutdoorAreaRule[] areas =
        {
            new OutdoorAreaRule(OutdoorAreaKind.TrackField, true, 1f, 13, 18, 7, 10),
            new OutdoorAreaRule(OutdoorAreaKind.Courtyard, true, 1f, 5, 8, 5, 8),
            new OutdoorAreaRule(OutdoorAreaKind.Pool, false, .85f, 8, 10, 8, 10, true),
            new OutdoorAreaRule(OutdoorAreaKind.Courts, false, .9f, 6, 9, 6, 9),
            new OutdoorAreaRule(OutdoorAreaKind.Garden, false, .9f, 5, 9, 5, 9),
            new OutdoorAreaRule(OutdoorAreaKind.Maintenance, false, .75f, 8, 10, 8, 10, true),
            new OutdoorAreaRule(OutdoorAreaKind.Parking, false, .8f, 6, 9, 6, 9)
        };
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeField, UnityEngine.HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("props")]
#endif
        private OutdoorPropRule[] previousProps;

        internal bool TryTakePreviousObstacles(out SurvivalObstacleSettings settings)
        {
            settings = null;
            if (previousProps == null) return false;
            settings = new SurvivalObstacleSettings { Rules = previousProps, PlacementAttempts = previousPlacementAttempts };
            previousProps = null;
            return true;
        }

        public int Width { get => width; set => width = value; }
        public int Height { get => height; set => height = value; }
        public int PathWidth { get => pathWidth; set => pathWidth = value; }
        public int PartitionJitter { get => partitionJitter; set => partitionJitter = value; }
        public int AreaWalkwayWidth => areaWalkwayWidth;
        public int PoolDeckWidth => poolDeckWidth;
        public int PerimeterPathWidth => perimeterPathWidth;
        public int MinimumPoolWaterWidth => minimumPoolWaterWidth;
        public int MinimumPoolWaterHeight => minimumPoolWaterHeight;
        public float PoolWaterFill => poolWaterFill;
        public int GardenPathWidth => gardenPathWidth;
        public int GardenPathBend => gardenPathBend;
        public OutdoorAreaRule CourtyardPocket => courtyardPocket;
        public float TennisCourtChance { get => tennisCourtChance; set => tennisCourtChance = value; }
        public int ParkingBayDepth => parkingBayDepth;
        public int ParkingBayWidth => parkingBayWidth;
        public int ParkingBaySpacing => parkingBaySpacing;
        public int ParkingAisleWidth => parkingAisleWidth;
        public int TrackWidth => trackWidth;
        public int PlayerSpawnCount { get => playerSpawnCount; set => playerSpawnCount = value; }
        public int MinimumSpawnDistance { get => minimumSpawnDistance; set => minimumSpawnDistance = value; }
        public int SpawnClearance => spawnClearance;
        public int GenerationAttempts { get => generationAttempts; set => generationAttempts = value; }
        public int MaximumDeadEndLength { get => maximumDeadEndLength; set => maximumDeadEndLength = value; }
        public OutdoorAreaRule[] Areas => areas;

        public void Validate()
        {
            if (width < 24 || height < 24 || width > 96 || height > 96 || pathWidth < 1 || pathWidth > 4
                || partitionJitter < 0 || partitionJitter > 3 || areaWalkwayWidth < 1 || areaWalkwayWidth > 2
                || poolDeckWidth < 1 || trackWidth < 1 || trackWidth > 3 || playerSpawnCount < 1 || playerSpawnCount > 8
                || minimumSpawnDistance < 0 || spawnClearance < 0 || spawnClearance > 3
                || generationAttempts < 1 || generationAttempts > 256
                || maximumDeadEndLength < 0 || maximumDeadEndLength > 16 || areas == null)
                throw new ArgumentException("Invalid Survival map settings. Check dimensions, paths, spawns, and retry limits.");
            var types = new System.Collections.Generic.HashSet<OutdoorAreaKind>();
            foreach (OutdoorAreaRule rule in areas)
                if (rule == null || !rule.IsValid || !types.Add(rule.Area))
                    throw new ArgumentException("Area rules must have unique types, valid size ranges, chances, and entrances.");
            if (!Array.Exists(areas, rule => rule.Area == OutdoorAreaKind.TrackField && rule.Required))
                throw new ArgumentException("Survival requires one mandatory Track / Field rule.");
            if (perimeterPathWidth < 1 || perimeterPathWidth > 2 || minimumPoolWaterWidth < 2 || minimumPoolWaterWidth > 8
                || minimumPoolWaterHeight < 2 || minimumPoolWaterHeight > 8 || !(poolWaterFill >= .5f && poolWaterFill <= 1f)
                || gardenPathWidth < 1 || gardenPathWidth > 2 || gardenPathBend < 0 || gardenPathBend > 3
                || courtyardPocket == null || !courtyardPocket.IsValid || courtyardPocket.Area != OutdoorAreaKind.Courtyard
                || !(tennisCourtChance >= 0f && tennisCourtChance <= 1f) || parkingBayDepth < 2 || parkingBayDepth > 4
                || parkingBayWidth < 1 || parkingBayWidth > 3 || parkingBaySpacing < 0 || parkingBaySpacing > 2 || parkingAisleWidth < 1 || parkingAisleWidth > 3)
                throw new ArgumentException("Invalid Survival pool, garden, pocket courtyard, court style, or parking settings.");
        }
    }
}
