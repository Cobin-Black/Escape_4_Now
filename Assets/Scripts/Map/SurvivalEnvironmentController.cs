// Places outdoor effects in suitable areas, applies their movement costs and removes temporary effects when they end.

using System.Collections.Generic;
using Escape4Now.GameModes;
using Escape4Now.Items;
using Escape4Now.Obstacles;
using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.Map
{
    public sealed class SurvivalEnvironmentalArea
    {
        public SurvivalEventType Type { get; internal set; }
        public HashSet<Vector2Int> Cells { get; } = new HashSet<Vector2Int>();
        internal int turns, amount;
        internal readonly List<GameObject> visuals = new List<GameObject>();
    }

    public sealed class SurvivalEnvironmentController : MonoBehaviour
    {
        private SurvivalModeController mode;
        private SurvivalMap map;
        private SurvivalEventController events;
        private SurvivalEventSettings settings;
        private System.Random random;
        private SurvivalMatchEventPlan plan;
        private readonly List<SurvivalEnvironmentalArea> active = new List<SurvivalEnvironmentalArea>();
        private readonly HashSet<Vector2Int> boostCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> wetPoolCells = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, EnergyDrinkItem> pickups = new Dictionary<Vector2Int, EnergyDrinkItem>();
        private readonly HashSet<PlayerCharacter> boosted = new HashSet<PlayerCharacter>(), slipped = new HashSet<PlayerCharacter>();
        public IReadOnlyList<SurvivalEnvironmentalArea> ActiveEffects => active.AsReadOnly();
        public IEnumerable<Vector2Int> TrackBoostCells => boostCells;
        public IEnumerable<Vector2Int> PoolSlipCells => wetPoolCells;

        // Create the match's allowed outdoor hazards and pickups from generated area data.
        public void Initialize(SurvivalEventController owner, SurvivalModeController game, SurvivalMap campus, SurvivalEventSettings configuration, SurvivalMatchEventPlan matchPlan)
        {
            events = owner; mode = game; map = campus; settings = configuration;
            plan = matchPlan;
            random = new System.Random(unchecked(map.LayoutSeed ^ 0x3B92));
            var mud = Rule(SurvivalEventType.Mud);
            if (mud != null) for (int i = 0; i < settings.initialMudPatches; i++) TryActivate(mud);
            var boost = Rule(SurvivalEventType.TrackBoost);
            if (boost != null && boost.prefab != null)
            {
                for (int section = 0; section < settings.maximumTrackBoostSections && plan.CanUse(boost); section++)
                {
                    var candidates = Candidates(false);
                    candidates.RemoveAll(cell => map.Layout.GetSurface(cell.x, cell.y) != OutdoorSurface.Track || !boost.Allows(map.Layout.GetArea(cell.x, cell.y)));
                    candidates.RemoveAll(NearBoostSection);
                    candidates.RemoveAll(NearTrackProp);
                    if (settings.preferTrackStraights)
                    {
                        var straights = candidates.FindAll(OnTrackStraight);
                        straights.AddRange(candidates.FindAll(cell => !OnTrackStraight(cell)));
                        candidates = straights;
                    }
                    if (candidates.Count == 0) break;
                    var sectionCells = SurvivalEnvironmentalPlacement.Patch(candidates[0], candidates, settings.trackBoostSectionTiles);
                    foreach (var cell in sectionCells) boostCells.Add(cell);
                    SpawnRegion(boost.prefab, sectionCells);
                    plan.Consume(boost);
                }
            }
            var slip = Rule(SurvivalEventType.PoolsideSlip);
            if (slip != null && slip.prefab != null)
                for (int zone = 0; zone < settings.maximumPoolWetZones && plan.CanUse(slip); zone++)
                {
                    var candidates = Candidates(false);
                    candidates.RemoveAll(cell => map.Layout.GetArea(cell.x, cell.y) != OutdoorAreaKind.Pool
                        || map.Layout.GetSurface(cell.x, cell.y) != OutdoorSurface.Deck || !slip.Allows(OutdoorAreaKind.Pool)
                        || !NearWater(cell));
                    var patch = new List<Vector2Int>(); int attempts = 0;
                    int size = random.Next(settings.poolWetPatchTiles.x, settings.poolWetPatchTiles.y + 1);
                    foreach (var center in candidates)
                    {
                        if (++attempts > settings.safePlacementAttempts) break;
                        patch = SurvivalEnvironmentalPlacement.NaturalPatch(center, candidates, size, random);
                        if (patch.Count >= settings.poolWetPatchTiles.x) break;
                    }
                    if (patch.Count < settings.poolWetPatchTiles.x) break;
                    foreach (var cell in patch) wetPoolCells.Add(cell);
                    SpawnRegion(slip.prefab, patch);
                    plan.Consume(slip);
                }
            var drink = Rule(SurvivalEventType.EnergyDrink);
            if (drink != null && drink.prefab != null && drink.prefab.GetComponent<EnergyDrinkItem>() != null)
            {
                var candidates = Candidates(true);
                candidates.RemoveAll(cell => !drink.Allows(map.Layout.GetArea(cell.x, cell.y)));
                for (int i = 0; i < settings.energyDrinkPickups && i < candidates.Count && plan.CanUse(drink); i++)
                {
                    var marker = Spawn(drink.prefab, candidates[i]);
                    var item = marker.GetComponent<EnergyDrinkItem>();
                    item.Configure(mode, candidates[i], drink.amount);
                    pickups.Add(candidates[i], item);
                    plan.Consume(drink);
                }
            }
        }

        private SurvivalEventRule Rule(SurvivalEventType type)
        {
            foreach (var rule in settings.events)
                if (rule != null && rule.type == type && rule.enabled && rule.Supported && rule.weight > 0 && plan.IsSelected(type)) return rule;
            return null;
        }
        public bool IsEnvironmentalCell(Vector2Int cell)
        {
            if (boostCells.Contains(cell) || wetPoolCells.Contains(cell) || pickups.TryGetValue(cell, out var item) && item != null && item.IsAtPosition(cell)) return true;
            foreach (var effect in active) if (effect.Cells.Contains(cell)) return true;
            return false;
        }
        // Use one safety check for walkability, occupied tiles, objective reservations and existing effects.
        private bool Safe(Vector2Int cell, bool protectRoutes)
        {
            return map.IsWalkable(cell) && !map.IsOccupied(cell) && !map.Objectives.IsReserved(cell)
                && !events.Tiles.ContainsKey(cell) && !IsEnvironmentalCell(cell)
                && SurvivalEnvironmentalPlacement.IsEligible(map.Layout, map.Objectives, cell, protectRoutes);
        }
        // Shuffle safe cells with the match seed so placement varies but fixed-seed tests can repeat it.
        private List<Vector2Int> Candidates(bool protectRoutes)
        {
            var result = new List<Vector2Int>();
            for (int y = 1; y < map.Height - 1; y++) for (int x = 1; x < map.Width - 1; x++)
            {
                var cell = new Vector2Int(x, y);
                if (Safe(cell, protectRoutes)) result.Add(cell);
            }
            SurvivalObjectiveLayout.Shuffle(result, random);
            return result;
        }
        private SurvivalWorldMarker Spawn(SurvivalWorldMarker prefab, Vector2Int cell)
        {
            var marker = Instantiate(prefab, transform);
            marker.Configure(map, cell, "");
            return marker;
        }

        // Create one visual for the whole region instead of copying a floor marker onto every cell.
        private SurvivalWorldMarker SpawnRegion(SurvivalWorldMarker prefab, IEnumerable<Vector2Int> cells)
        {
            foreach (var cell in cells)
            {
                var marker = Spawn(prefab, cell);
                if (marker.TryGetComponent<SurvivalEnvironmentalSurface>(out var surface))
                    surface.Configure(map, cells);
                return marker;
            }
            return null;
        }
        private bool NearBoostSection(Vector2Int cell)
        {
            foreach (var existing in boostCells)
                if (SurvivalObjectiveLayout.Distance(cell, existing) < settings.minimumTrackSectionSpacing) return true;
            return false;
        }
        // Avoid large props that could cover the painted boost section in the isometric view.
        private bool NearTrackProp(Vector2Int cell)
        {
            foreach (var prop in map.Layout.Props)
            {
                if (prop.Kind != OutdoorPropKind.Bleachers && prop.Kind != OutdoorPropKind.Tree && prop.Kind != OutdoorPropKind.Shed) continue;
                int dx = Mathf.Max(prop.X-cell.x, cell.x-(prop.X+prop.Width-1), 0);
                int dy = Mathf.Max(prop.Y-cell.y, cell.y-(prop.Y+prop.Height-1), 0);
                if (dx+dy < settings.trackPropClearance) return true;
            }
            return false;
        }
        // Prefer the track's long sides before using a bend for a boost section.
        private bool OnTrackStraight(Vector2Int cell)
        {
            foreach (var area in map.Layout.Areas)
            {
                if (area.Kind != OutdoorAreaKind.TrackField) continue;
                float x = (cell.x-area.X-(area.Width-1)*.5f) / area.Width;
                float y = (cell.y-area.Y-(area.Height-1)*.5f) / area.Height;
                return Mathf.Abs(y) >= Mathf.Abs(x);
            }
            return false;
        }
        // Wet pool ground belongs beside actual generated water, not a random campus location.
        private bool NearWater(Vector2Int cell)
        {
            for (int y = -settings.poolWetWaterDistance; y <= settings.poolWetWaterDistance; y++)
                for (int x = -settings.poolWetWaterDistance; x <= settings.poolWetWaterDistance; x++)
                    if (Mathf.Abs(x) + Mathf.Abs(y) <= settings.poolWetWaterDistance
                        && map.Layout.GetSurface(cell.x + x, cell.y + y) == OutdoorSurface.Water) return true;
            return false;
        }
        // Prefer garden ground or grass near planted props when choosing a mud patch.
        private bool Landscaped(Vector2Int cell)
        {
            if (map.Layout.GetArea(cell.x, cell.y) == OutdoorAreaKind.Garden) return true;
            foreach (var prop in map.Layout.Props)
                if ((prop.Kind == OutdoorPropKind.Tree || prop.Kind == OutdoorPropKind.Planter)
                    && SurvivalObjectiveLayout.Distance(cell, new Vector2Int(prop.X, prop.Y)) <= settings.mudLandscapeDistance) return true;
            return false;
        }

        // Try a safe placement before consuming the event's allowed use; skip the event if none fits.
        public bool TryActivate(SurvivalEventRule rule)
        {
            if (!plan.CanUse(rule) || active.Count >= settings.maximumActiveEnvironmentalEffects) return false;
            var effect = new SurvivalEnvironmentalArea { Type = rule.type, turns = rule.durationTurns, amount = rule.amount };
            if (rule.type == SurvivalEventType.CarAlarm)
            {
                if (!rule.Allows(OutdoorAreaKind.Parking)) return false;
                foreach (var current in active) if (current.Type == SurvivalEventType.CarAlarm) return false;
                if (settings.carAlarmEffectPrefab == null) return false;
                var cars = new List<SurvivalPropVisual>();
                var spawner = map.GetComponent<SurvivalObstacleSpawner>();
                foreach (var prop in map.Layout.Props)
                    if (prop.Kind == OutdoorPropKind.Car && map.Layout.GetArea(prop.X, prop.Y) == OutdoorAreaKind.Parking
                        && spawner.TryGetSpawnedProp(new Vector2Int(prop.X, prop.Y), out var visual)) cars.Add(visual);
                if (cars.Count == 0) return false;
                var car = cars[random.Next(cars.Count)];
                var alarm = Instantiate(settings.carAlarmEffectPrefab, car.transform.position, Quaternion.identity, car.transform);
                effect.visuals.Add(alarm.gameObject);
            }
            else
            {
                if (rule.prefab == null) return false;
                var candidates = Candidates(rule.type == SurvivalEventType.Mud);
                candidates.RemoveAll(cell => !rule.Allows(map.Layout.GetArea(cell.x, cell.y)));
                if (rule.type == SurvivalEventType.Mud)
                {
                    candidates.RemoveAll(cell => map.Layout.GetSurface(cell.x, cell.y) != OutdoorSurface.Grass);
                    if(settings.preferLandscapedMud)
                    {
                        var preferred=candidates.FindAll(Landscaped);
                        preferred.AddRange(candidates.FindAll(cell=>!Landscaped(cell)));
                        candidates=preferred;
                    }
                }
                if (rule.type == SurvivalEventType.FallenBranch)
                {
                    bool placed = false; int attempts = 0;
                    foreach (var cell in candidates)
                    {
                        if (++attempts > settings.safePlacementAttempts) break;
                        // The map checks remaining routes before accepting this temporary branch block.
                        if (!map.Layout.IsMainPath(cell.x, cell.y) || !map.TryBlockTemporarily(cell)) continue;
                        effect.Cells.Add(cell); placed = true; break;
                    }
                    if (!placed) return false;
                }
                else if (rule.type == SurvivalEventType.Mud)
                {
                    if (candidates.Count == 0) return false;
                    int size = random.Next(settings.mudPatchTiles.x, settings.mudPatchTiles.y + 1);
                    int attempts = 0;
                    foreach (var center in candidates)
                    {
                        if (++attempts > settings.safePlacementAttempts) break;
                        var patch = SurvivalEnvironmentalPlacement.NaturalPatch(center, candidates, size, random);
                        if (patch.Count < settings.mudPatchTiles.x) continue;
                        foreach (var cell in patch) effect.Cells.Add(cell);
                        break;
                    }
                    if (effect.Cells.Count == 0) return false;
                }
                else if (rule.type == SurvivalEventType.Sprinklers)
                {
                    var areas = new List<OutdoorArea>(map.Layout.Areas);
                    SurvivalObjectiveLayout.Shuffle(areas, random);
                    foreach (var area in areas)
                    {
                        if (!rule.Allows(area.Kind)) continue;
                        // Keep the sprinkler's wet region connected and within one generated area.
                        var local=candidates.FindAll(cell=>area.Contains(cell.x,cell.y));
                        if(local.Count==0)continue;
                        foreach(var cell in SurvivalEnvironmentalPlacement.NaturalPatch(local[0],local,settings.maximumSprinklerTiles,random))effect.Cells.Add(cell);
                        if (effect.Cells.Count > 0) break;
                    }
                    if (effect.Cells.Count == 0) return false;
                }
                else return false;
                if(rule.type==SurvivalEventType.Mud||rule.type==SurvivalEventType.Sprinklers)
                {
                    effect.visuals.Add(SpawnRegion(rule.prefab,effect.Cells).gameObject);
                    if(rule.type==SurvivalEventType.Sprinklers)
                    {
                        var heads=new List<Vector2Int>();
                        foreach(var cell in effect.Cells)
                        {
                            if(heads.Count==0){heads.Add(cell);continue;}
                            if(heads.Count>=settings.maximumSprinklerHeads)break;
                            if(heads.Exists(head=>SurvivalObjectiveLayout.Distance(head,cell)<settings.sprinklerHeadSpacing))continue;
                            effect.visuals.Add(Spawn(rule.prefab,cell).gameObject);heads.Add(cell);
                        }
                    }
                }
                else foreach (var cell in effect.Cells) effect.visuals.Add(Spawn(rule.prefab, cell).gameObject);
            }
            active.Add(effect);
            plan.Consume(rule);
            events.ShowEffectMessage(null, rule.type, string.IsNullOrWhiteSpace(rule.feedback) ? rule.title
                : string.Format(rule.feedback, rule.amount, rule.durationTurns, 0));
            return true;
        }

        // Track boosts and pool slips react to crossing their real outdoor surfaces.
        public void ResolveStep(PlayerCharacter player)
        {
            var cell = player.GridPosition;
            var boost = Rule(SurvivalEventType.TrackBoost);
            if (boost != null && boostCells.Contains(cell) && boosted.Add(player)) events.ApplyEnvironmentalStep(boost, player);
            var slip = Rule(SurvivalEventType.PoolsideSlip);
            if (slip != null && wetPoolCells.Contains(cell) && !map.Objectives.IsReserved(cell) && map.Layout.GetArea(cell.x, cell.y) == OutdoorAreaKind.Pool
                && slip.Allows(OutdoorAreaKind.Pool)
                && map.Layout.GetSurface(cell.x, cell.y) == OutdoorSurface.Deck
                && (!settings.poolSlipOncePerTurn || !slipped.Contains(player)) && random.NextDouble() < slip.activationChance)
            { slipped.Add(player); events.ApplyEnvironmentalStep(slip, player); }
        }
        // Return the extra cost of crossing active ground effects; these regions stay walkable.
        public int MovementCost(Vector2Int cell)
        {
            if (settings == null) return 1;
            int cost = 1;
            foreach (var effect in active)
                if ((effect.Type == SurvivalEventType.Mud || effect.Type == SurvivalEventType.Sprinklers) && effect.Cells.Contains(cell)) cost += effect.amount;
            return Mathf.Clamp(cost, 1, settings.maximumTileCost);
        }
        // Expire effects in player turns, release branch blocks and remove the matching visuals.
        public void CountTurn()
        {
            if (Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused) return;
            boosted.Clear(); slipped.Clear();
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var effect = active[i];
                if (--effect.turns > 0) continue;
                if (effect.Type == SurvivalEventType.FallenBranch) foreach (var cell in effect.Cells) map.RemoveTemporaryBlock(cell);
                foreach (var visual in effect.visuals) if (visual != null) Destroy(visual);
                active.RemoveAt(i);
            }
        }
    }
}
