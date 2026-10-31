// Connects Survival event placement, player effects, outdoor effects, notifications and turn-based scheduling.

using System;
using System.Collections.Generic;
using Escape4Now.GameModes;
using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.Map
{
    public sealed class SurvivalEventController : MonoBehaviour, IMapEventSource
    {
        [Header("Scene and Inspector Settings")]
        [SerializeField]
        private SurvivalModeController mode;
        [SerializeField]
        private SurvivalMap map;
        [SerializeField]
        private SurvivalEventSettings settings;
        [Header("Survival Trigger Systems")]
        [SerializeField] private SurvivalEnvironmentController environment;
        [SerializeField] private SurvivalEventScheduler scheduler;
        [Header("Outdoor Visual and Audio Feedback")]
        [SerializeField] private SurvivalWorldFeedback worldFeedback;
        public SurvivalWorldFeedback WorldFeedback => worldFeedback;
        public bool RainActive => temporary.Exists(effect => effect.type == SurvivalEventType.RainStorm);
        private readonly Dictionary<Vector2Int, SurvivalEventRule> tiles = new Dictionary<Vector2Int, SurvivalEventRule>();
        private readonly List<SurvivalWorldMarker> markers = new List<SurvivalWorldMarker>();
        private readonly List<TemporaryEffect> temporary = new List<TemporaryEffect>();
        private System.Random random;
        private bool initialized;
        private SurvivalEventNotifications notifications = new SurvivalEventNotifications();
        public string CurrentMessage { get; private set; } = "";
        public int BlackoutTurns { get; private set; }
        public bool EventLocationsVisible { get; private set; }
        public IReadOnlyDictionary<Vector2Int, SurvivalEventRule> Tiles => tiles;
        public SurvivalEnvironmentController Environment => environment;
        public SurvivalMatchEventPlan MatchPlan { get; private set; }

        private sealed class TemporaryEffect
        {
            public SurvivalEventType type;
            public int turns, amount;
            public GameObject visual;
        }

        // Choose this match's event pool before creating tiles, outdoor effects and turn schedules.
        public void BeginMatch()
        {
            CurrentMessage = "";
            notifications = new SurvivalEventNotifications();
            random = new System.Random(unchecked(map.LayoutSeed ^ 0x47B3E2));
            EventLocationsVisible = settings.showEventLocations;
            MatchPlan = new SurvivalMatchEventPlan(map.Layout, map.Objectives, settings);
            var placement = new SurvivalEventLayout(map.Layout, map.Objectives, settings, MatchPlan);
            map.SetForcedMovementHazards(placement.ForcedMovementCells);
            foreach (var pair in placement.Tiles)
            {
                tiles.Add(pair.Key, pair.Value);
                var marker = Instantiate(pair.Value.prefab, transform);
                marker.Configure(map, pair.Key, pair.Value.title);
                marker.SetVisible(EventLocationsVisible);
                markers.Add(marker);
            }

            initialized = true;
            environment.Initialize(this, mode, map, settings, MatchPlan);
            scheduler.Initialize(this, settings, map.LayoutSeed);
            mode.Turns.PlayerTurnEnded += CountTurn;
        }

        private void OnDestroy()
        {
            if (mode != null && mode.Turns != null)
                mode.Turns.PlayerTurnEnded -= CountTurn;
        }

        public void SetEventLocationsVisible(bool visible)
        {
            EventLocationsVisible = visible;
            foreach (var marker in markers)
                if (marker != null)
                    marker.SetVisible(visible);
        }

        public void ApplyNetworkDisplay(string message, int blackout)
        {
            CurrentMessage = message;
            BlackoutTurns = blackout;
        }

        public void ShowMessage(PlayerCharacter player, string message)
        {
            Notify(player.name + ": " + message);
        }

        private void Update()
        {
            if (!initialized || Escape4Now.Multiplayer.LanMultiplayerSession.IsClient
                || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused || Time.timeScale <= 0) return;
            RefreshNotifications(Time.deltaTime);
        }

        // Remove finished messages using their effect state, then build the compact HUD text.
        private void RefreshNotifications(float elapsed = 0) => CurrentMessage = notifications.Tick(elapsed,
            settings.visibleNotifications, settings.moreNotificationsFormat);

        private void Notify(string message, Func<bool> active = null)
        {
            notifications.Add(message, settings.immediateNotificationSeconds, active);
            RefreshNotifications();
        }

        // Keep a message tied to its actual effect when one exists; instant outcomes use the short display time.
        public void ShowEffectMessage(PlayerCharacter player, SurvivalEventType type, string message)
        {
            Notify(player == null ? message : player.name + ": " + message,
                HasEffect(type, player) ? () => HasEffect(type, player) : (Func<bool>)null);
        }

        // Read the existing player and environment state rather than running a second effect countdown.
        private bool HasEffect(SurvivalEventType type, PlayerCharacter player)
        {
            if (type == SurvivalEventType.Blackout) return BlackoutTurns > 0;
            foreach (var effect in temporary) if (effect.type == type) return true;
            if (environment != null)
                foreach (var effect in environment.ActiveEffects) if (effect.Type == type) return true;
            if (type == SurvivalEventType.SchoolBell)
            {
                foreach (var survivor in mode.ActiveSurvivors) if (survivor.EventState.HasMovementBonus) return true;
                return false;
            }
            if (player == null || !mode.IsActiveSurvivor(player)) return false;
            var state = player.EventState;
            switch (type)
            {
                case SurvivalEventType.LuckyRoll: return state.HasLuckyEffect;
                case SurvivalEventType.BadRoll: return state.HasBadRollEffect;
                case SurvivalEventType.Freeze: return state.HasPendingFreeze;
                case SurvivalEventType.SecondChance: return state.HasSecondChance;
                case SurvivalEventType.EnergyDrink: return state.HasMovementBonus;
                case SurvivalEventType.Exhausted: return state.HasMovementPenalty;
                default: return false;
            }
        }

        // Resolve only a real movement step by an active survivor; scripted teleports must not chain into more events.
        public bool ResolveStep(PlayerCharacter player, Vector2Int direction)
        {
            if (!initialized || !player.IsResolvingEventStep || !mode.IsActiveSurvivor(player) || Time.timeScale <= 0f || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused)
                return false;
            environment.ResolveStep(player);
            if (tiles.TryGetValue(player.GridPosition, out var rule)) Apply(rule, player);
            return player.MovesRemaining == 0 && !mode.Tasks.CanInteract(player);
        }

        // Let the scheduler activate a compatible outdoor or global effect without requiring a floor tile.
        public bool TryActivateRule(SurvivalEventRule rule)
        {
            if (!initialized || !MatchPlan.CanUse(rule) || mode.Timer.HasExpired
                || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused || mode.ActiveSurvivors.Count == 0) return false;
            var actor = mode.ActiveSurvivors[0];
            if (rule.Trigger == SurvivalEventTrigger.Environmental)
            {
                if (!environment.TryActivate(rule)) return false;
                return true;
            }
            if (rule.Trigger != SurvivalEventTrigger.Global || rule.type == SurvivalEventType.Blackout && BlackoutTurns > 0
                || rule.type == SurvivalEventType.LocationShuffle && mode.ActiveSurvivors.Count < 2) return false;
            if (rule.type == SurvivalEventType.RainStorm && temporary.Count > 0) return false;
            Apply(rule, actor);
            MatchPlan.Consume(rule);
            return true;
        }

        private static string Feedback(SurvivalEventRule rule, PlayerCharacter actor) => string.IsNullOrWhiteSpace(rule.feedback)
            ? rule.title : string.Format(rule.feedback, rule.amount, rule.durationTurns, actor.MovesRemaining);

        public void ApplyEnvironmentalStep(SurvivalEventRule rule, PlayerCharacter player)
        {
            if (mode.IsActiveSurvivor(player)) Apply(rule, player);
        }

        // Apply the selected effect, respecting Second Chance before any negative player effect.
        private void Apply(SurvivalEventRule rule, PlayerCharacter player)
        {
            if (rule.category == MapEventCategory.Negative && player.EventState.BlockNegativeEvent())
            {
                ShowMessage(player, string.Format(settings.blockedEventFormat, rule.title));
                return;
            }

            switch (rule.type)
            {
                case SurvivalEventType.LuckyRoll:
                    SharedEventEffects.Apply(player, MapEventType.LuckyRoll);
                    break;
                case SurvivalEventType.BadRoll:
                    SharedEventEffects.Apply(player, MapEventType.BadRoll);
                    break;
                case SurvivalEventType.Freeze:
                    SharedEventEffects.Apply(player, MapEventType.Freeze);
                    break;
                case SurvivalEventType.SecondChance:
                    SharedEventEffects.Apply(player, MapEventType.SecondChance);
                    break;
                case SurvivalEventType.AdrenalineRush:
                case SurvivalEventType.TrackBoost:
                    player.ChangeEventMovement(rule.amount);
                    break;
                case SurvivalEventType.PoolsideSlip:
                    player.ChangeEventMovement(-rule.amount);
                    break;
                case SurvivalEventType.Trip:
                    player.ChangeEventMovement(-player.MovesRemaining);
                    break;
                case SurvivalEventType.EnergyDrink:
                    player.EventState.GiveMovementBonus(rule.amount);
                    break;
                case SurvivalEventType.Exhausted:
                    player.EventState.GiveMovementPenalty(rule.amount);
                    break;
                case SurvivalEventType.SchoolBell:
                    foreach (var survivor in mode.ActiveSurvivors)
                        survivor.EventState.GiveMovementBonus(rule.amount);
                    SpawnAmbient(settings.schoolBellEffectPrefab, player.transform.position);
                    break;
                case SurvivalEventType.Warp:
                    if (!Teleport(player, false))
                    {
                        ShowSkipped(player, rule);
                        return;
                    }

                    break;
                case SurvivalEventType.AreaSwitch:
                    if (!Teleport(player, true))
                    {
                        ShowSkipped(player, rule);
                        return;
                    }

                    break;
                case SurvivalEventType.LocationSwitch:
                case SurvivalEventType.LocationShuffle:
                    if (!MapEventController.SwitchSurvivorLocations(mode.ActiveSurvivors, random))
                    {
                        ShowSkipped(player, rule);
                        return;
                    }

                    break;
                case SurvivalEventType.Blackout:
                    if (BlackoutTurns == 0)
                    {
                        int count = Mathf.Clamp(mode.ActiveSurvivors.Count - 1, 0, settings.blackoutTurns.Length - 1);
                        BlackoutTurns = Mathf.Max(1, settings.blackoutTurns[count]);
                    }

                    break;
                case SurvivalEventType.RandomEvent:
                    var smaller = PickRule(player.GridPosition, true);
                    if (smaller != null)
                        Apply(smaller, player);
                    return;
                case SurvivalEventType.Mud:
                case SurvivalEventType.Sprinklers:
                case SurvivalEventType.RainStorm:
                case SurvivalEventType.FallenBranch:
                case SurvivalEventType.CarAlarm:
                    if (!AddTemporary(rule, player.GridPosition))
                    {
                        ShowSkipped(player, rule);
                        return;
                    }

                    break;
            }

            ShowEffectMessage(player, rule.type, Feedback(rule, player));
            worldFeedback?.Emit(rule.type,player);
        }

        private void ShowSkipped(PlayerCharacter player, SurvivalEventRule rule) => ShowMessage(player, string.Format(settings.skippedEventFormat, rule.title));
        private SurvivalEventRule PickRule(Vector2Int cell, bool smaller)
        {
            var pool = new List<SurvivalEventRule>();
            float total = 0f;
            foreach (var rule in settings.events)
            {
                if (rule == null || !rule.enabled || !rule.Supported || rule.prefab == null)
                    continue;
                if (!rule.Allows(map.Layout.GetArea(cell.x, cell.y)))
                    continue;
                if (smaller && (rule.Trigger != SurvivalEventTrigger.Tile || rule.type == SurvivalEventType.RandomEvent || rule.category != MapEventCategory.Positive && rule.category != MapEventCategory.Negative))
                    continue;
                float weight = smaller ? rule.randomEffectWeight : rule.weight;
                if (weight <= 0f || float.IsNaN(weight) || float.IsInfinity(weight))
                    continue;
                pool.Add(rule);
                total += weight;
            }

            float pick = (float)random.NextDouble() * total;
            foreach (var rule in pool)
            {
                pick -= smaller ? rule.randomEffectWeight : rule.weight;
                if (pick < 0f)
                    return rule;
            }

            return null;
        }

        // Exclude occupied tiles, objectives, exits and other event locations from teleport destinations.
        private List<Vector2Int> Destinations()
        {
            var cells = new List<Vector2Int>();
            for (int y = 1; y < map.Height - 1; y++)
                for (int x = 1; x < map.Width - 1; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (map.IsWalkable(cell) && !map.IsOccupied(cell) && !map.Objectives.IsReserved(cell) && !tiles.ContainsKey(cell) && !environment.IsEnvironmentalCell(cell))
                        cells.Add(cell);
                }

            return cells;
        }

        private bool Teleport(PlayerCharacter player, bool anotherArea)
        {
            var cells = Destinations();
            if (anotherArea)
            {
                var origin = AreaAt(player.GridPosition);
                cells.RemoveAll(cell => AreaAt(cell) == null || AreaAt(cell) == origin);
            }

            return cells.Count > 0 && player.SetGridPosition(cells[random.Next(cells.Count)]);
        }

        private OutdoorArea AreaAt(Vector2Int cell)
        {
            foreach (var area in map.Layout.Areas)
                if (area.Contains(cell.x, cell.y))
                    return area;
            return null;
        }

        public int MovementCost(Vector2Int cell)
        {
            return environment != null ? environment.MovementCost(cell) : 1;
        }

        // Rain changes the survivor's roll movement, but never below the configured minimum.
        public int AdjustRollMovement(PlayerCharacter player, int movement)
        {
            if (mode.IsActiveSurvivor(player))
                foreach (var effect in temporary)
                    if (effect.type == SurvivalEventType.RainStorm)
                        movement -= effect.amount;
            return Mathf.Max(settings.minimumRollMovement, movement);
        }

        private bool AddTemporary(SurvivalEventRule rule, Vector2Int center)
        {
            if (rule.type != SurvivalEventType.RainStorm) return environment.TryActivate(rule);
            foreach (var existing in temporary) if (existing.type == rule.type) return false;
            temporary.Add(new TemporaryEffect { type = rule.type, turns = rule.durationTurns, amount = rule.amount,
                visual = SpawnAmbient(settings.rainEffectPrefab, map.GridToWorld(center)) });
            return true;
        }

        // Create the assigned sound or particle prefab; its controller handles fitting and Pause.
        private GameObject SpawnAmbient(SurvivalAmbientEffect prefab, Vector3 position)
        {
            if (prefab == null) return null;
            var effect = Instantiate(prefab, position, Quaternion.identity, transform);
            effect.Configure(map);
            return effect.gameObject;
        }

        // Age temporary effects only after a completed player turn; then check whether a new event should start.
        private void CountTurn()
        {
            if (!initialized || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused || mode.Timer.HasExpired) return;
            if (BlackoutTurns > 0) BlackoutTurns--;

            for (int i = temporary.Count - 1; i >= 0; i--)
            {
                var effect = temporary[i];
                if (--effect.turns > 0)
                    continue;
                if (effect.visual != null) Destroy(effect.visual);
                temporary.RemoveAt(i);
            }
            environment.CountTurn();
            RefreshNotifications();
            scheduler.CountTurn();
        }

        public void ValidateConfiguration()
        {
            if (settings == null || environment == null || scheduler == null)
                throw new InvalidOperationException("Assign Survival event settings, environment controller and scheduler.");
            settings.Validate();
        }
    }
}
