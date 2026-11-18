using System;
using System.Collections.Generic;
using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Arena
{
    // Chooses a match subset, schedules warnings, and owns real-time lifetimes. This never spawns pickups.
    [DefaultExecutionOrder(100)]
    public sealed class ArenaEventDirector : MonoBehaviour
    {
        [Header("Mode Event Pool / Placement and Timing")]
        [SerializeField]
        private ArenaEventPool pool;
        [Header("Map Assistance / Indicators Only")]
        [SerializeField]
        private bool eventLocationsVisible;
        public bool EventLocationsVisible => eventLocationsVisible;

        public void SetEventLocationsVisible(bool visible) => eventLocationsVisible = visible;
        private readonly List<ArenaEventEntry> selected = new List<ArenaEventEntry>();
        private readonly List<ArenaEventState> active = new List<ArenaEventState>();
        private readonly Dictionary<ArenaEventEntry, int> uses = new Dictionary<ArenaEventEntry, int>();
        private readonly Dictionary<ArenaEventEntry, float> cooldowns = new Dictionary<ArenaEventEntry, float>();
        private ArenaEnvironmentalEvents effects;
        private System.Random random;
        private float nextEvent;
        private int round;
        private bool audioPaused;
        public ArenaMatch Match { get; private set; }
        public ArenaEventPool Pool => pool;
        public IReadOnlyList<ArenaEventEntry> Selected => selected;
        public IReadOnlyList<ArenaEventState> Active => active;
        public float NextEventSeconds => Mathf.Max(0, nextEvent);

        public bool HasScheduledEvents
        {
            get
            {
                if (pool == null || !pool.enableEvents)
                    return false;
                foreach (var entry in selected)
                    if (entry.enabled && uses.TryGetValue(entry, out var count) && count < entry.maximumPerMatch)
                        return true;
                return false;
            }
        }

        public bool BlackoutActive => active.Exists(e => !e.Warning && !e.Ended && e.Rule.IsBlackout);

        [Serializable]
        public sealed class OnlineEvent
        {
            public ArenaEventKind kind;
            public bool warning;
            public float remaining, elapsed;
            public Vector2 center, direction;
            public Vector2Int source;
            public Vector2Int block;
            public int equipment = -1, first = -1, second = -1;
            public Vector2Int[] cells;
            public Vector2[] balls;
            public float[] ballTravel;
        }

        public OnlineEvent[] CaptureOnlineEvents()
        {
            return active.ConvertAll(state => new OnlineEvent
            {
                kind = state.Rule.kind, warning = state.Warning, remaining = state.WarningRemaining,
                elapsed = state.Elapsed, center = state.Center, direction = state.Direction,
                source = state.sourceCell, block = state.blockCell, cells = state.Cells.ToArray(),
                equipment = Match.Map.EquipmentViews.IndexOf(state.equipment),
                first = state.first != null ? state.first.Slot : -1, second = state.second != null ? state.second.Slot : -1,
                balls = state.balls.ConvertAll(ball => ball.position).ToArray(),
                ballTravel = state.balls.ConvertAll(ball => ball.travel).ToArray()
            }).ToArray();
        }

        // Reuse the visual component while keeping event placement, damage and movement on the host.
        public void ApplyOnlineEvents(OnlineEvent[] snapshots)
        {
            if (pool == null || pool.visuals == null) return;
            snapshots ??= Array.Empty<OnlineEvent>();
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var state = active[i];
                if (!Array.Exists(snapshots, value => value != null && value.kind == state.Rule.kind)) End(state);
            }
            foreach (var snapshot in snapshots)
            {
                if (snapshot == null || snapshot.cells == null || snapshot.cells.Length > 1024) continue;
                var state = active.Find(value => value.Rule.kind == snapshot.kind);
                if (state == null)
                {
                    var rule = Array.Find(pool.entries, value => value != null && value.kind == snapshot.kind);
                    if (rule == null) continue;
                    state = new ArenaEventState { Rule = rule, sourceCell = snapshot.source };
                    state.Cells.AddRange(snapshot.cells);
                    state.View = ArenaEventView.Create(this, state);
                    active.Add(state);
                    if (snapshot.warning) Play(rule.warningSound);
                }
                state.first = snapshot.first >= 0 && snapshot.first < Match.Players.Count ? Match.Players[snapshot.first] : null;
                state.second = snapshot.second >= 0 && snapshot.second < Match.Players.Count ? Match.Players[snapshot.second] : null;
                state.Center = snapshot.center;
                state.Direction = snapshot.direction;
                state.Elapsed = snapshot.elapsed;
                state.WarningRemaining = snapshot.remaining;
                if (state.Warning && !snapshot.warning)
                {
                    Play(state.Rule.activationSound);
                    state.View.ActivateGround();
                    if (state.Rule.kind == ArenaEventKind.BleachersMove && snapshot.equipment >= 0
                        && snapshot.equipment < Match.Map.EquipmentViews.Count)
                    {
                        state.equipment = Match.Map.EquipmentViews[snapshot.equipment];
                        state.originalPosition = state.equipment.Visual.position;
                        state.originalScale = state.equipment.Visual.localScale;
                        state.blockCell = snapshot.block;
                        state.landed = true;
                        effects.ShowExtendedBleachers(state);
                    }
                    else if (state.Rule.IsBlock && state.Rule.obstaclePrefab != null)
                    {
                        var obstacle = state.View.SpawnObstacle(state.Rule.obstaclePrefab, state.Center);
                        if (obstacle.TryGetComponent<SurvivalBranchFall>(out var fall))
                            fall.Configure(() => true, null, () => Match.Paused || !Match.Playing);
                    }
                }
                state.Warning = snapshot.warning;
                if (pool.visuals.basketballPrefab == null || snapshot.balls == null) continue;
                int count = Mathf.Min(snapshot.balls.Length, 32);
                while (state.balls.Count > count)
                {
                    int last = state.balls.Count - 1;
                    Destroy(state.balls[last].visual);
                    state.balls.RemoveAt(last);
                }
                while (state.balls.Count < count)
                    state.balls.Add(new ArenaEventBall { visual = state.View.SpawnBall() });
                for (int i = 0; i < count; i++)
                {
                    float distance = snapshot.ballTravel != null && i < snapshot.ballTravel.Length ? snapshot.ballTravel[i] : 0;
                    float bounce = Mathf.Abs(Mathf.Sin(distance * pool.visuals.bounceRate)) * pool.visuals.bounceHeight + .15f;
                    state.balls[i].visual.transform.position = Match.Map.World(snapshot.balls[i]) + Vector3.up * bounce;
                }
            }
        }

        // Choose the match subset once using the generated map seed.
        public void Begin(ArenaMatch owner)
        {
            Stop();
            Match = owner;
            selected.Clear();
            uses.Clear();
            cooldowns.Clear();
            random = new System.Random(unchecked(owner.Map.Seed ^ 0x4A7D));
            effects = new ArenaEnvironmentalEvents(this, random);
            if (pool == null || !pool.enableEvents || pool.visuals == null)
                return;
            var available = new List<ArenaEventEntry>();
            var kinds = new HashSet<ArenaEventKind>();
            foreach (var entry in pool.entries)
                if (entry != null && entry.enabled && entry.Supports(owner.Map.Settings.room) && entry.weight > 0 && kinds.Add(entry.kind))
                    available.Add(entry);
            int count = random.Next(Mathf.Max(0, pool.count.x), Mathf.Max(pool.count.x, pool.count.y) + 1);
            while (selected.Count < count && available.Count > 0)
            {
                var entry = Pick(available);
                selected.Add(entry);
                uses[entry] = 0;
                cooldowns[entry] = 0;
                available.Remove(entry);
            }

            nextEvent = Range(pool.initialDelay);
            round = 0;
        }

        private ArenaEventEntry Pick(List<ArenaEventEntry> entries)
        {
            float total = 0;
            foreach (var entry in entries)
                total += entry.weight;
            double value = random.NextDouble() * total;
            foreach (var entry in entries)
            {
                value -= entry.weight;
                if (value <= 0)
                    return entry;
            }

            return entries[entries.Count - 1];
        }

        private float Range(Vector2 range) => Mathf.Max(.1f, Mathf.Lerp(range.x, Mathf.Max(range.x, range.y), (float)random.NextDouble()));
        private void Update()
        {
            bool pause = Match != null && Match.Paused;
            if (pause != audioPaused)
            {
                audioPaused = pause;
                foreach (var audio in GetComponentsInChildren<AudioSource>())
                    if (pause)
                        audio.Pause();
                    else
                        audio.UnPause();
            }

            Step(Time.deltaTime);
        }

        // Advance warnings and effects only while the match is playing and unpaused.
        public void Step(float seconds)
        {
            if (Match == null || !Match.Playing || Match.Paused || Match.Replica)
                return;
            if (pool == null || !pool.enableEvents || pool.visuals == null)
            {
                Stop();
                return;
            }

            var potato = Match as HotPotatoController;
            if (potato != null)
            {
                if (potato.BetweenRounds)
                {
                    ClearActive();
                    return;
                }

                if (round != potato.Round)
                {
                    if (round != 0)
                        ClearActive();
                    round = potato.Round;
                }
            }

            foreach (var entry in selected)
                cooldowns[entry] = Mathf.Max(0, cooldowns[entry] - seconds);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var state = active[i];
                if (!state.Rule.enabled)
                {
                    End(state);
                    continue;
                }

                if (state.Ended)
                {
                    End(state);
                    continue;
                }

                if (state.Warning)
                {
                    state.WarningRemaining -= seconds;
                    if (state.WarningRemaining > 0)
                        continue;
                    if (!effects.Activate(state))
                    {
                        End(state);
                        continue;
                    }

                    state.Warning = false;
                    state.Elapsed = 0;
                    Match.Notify(effects.ActivationMessage(state), pool.noticeSeconds);
                    Play(state.Rule.activationSound);
                }
                else
                {
                    state.Elapsed += seconds;
                    effects.Tick(state, seconds);
                    if (!Match.Playing)
                        break;
                    if (state.Elapsed >= state.Rule.duration || state.Ended)
                        End(state);
                }
            }

            if (Match is HotPotatoController finishedRound && finishedRound.BetweenRounds)
            {
                ClearActive();
                return;
            }

            nextEvent -= seconds;
            if (nextEvent > 0 || active.Count >= pool.maximumSimultaneous)
                return;
            var ready = selected.FindAll(e => uses[e] < e.maximumPerMatch && cooldowns[e] <= 0 && !active.Exists(s => s.Rule.kind == e.kind));
            bool started = false;
            while (ready.Count > 0 && !started)
            {
                var entry = Pick(ready);
                ready.Remove(entry);
                started = TryStart(entry);
            }

            nextEvent = started ? Range(pool.activationInterval) : pool.retryDelay;
        }

        // Used by the scheduler and by focused event tests. It obeys the same safety and concurrency rules.
        public bool TryStart(ArenaEventEntry entry)
        {
            if (Match == null || !Match.Playing || Match.Paused || effects == null || entry == null || !entry.enabled || !entry.Supports(Match.Map.Settings.room) || pool == null || !pool.enableEvents || active.Count >= pool.maximumSimultaneous || active.Exists(e => e.Rule.kind == entry.kind))
                return false;
            if (!selected.Contains(entry) || uses[entry] >= entry.maximumPerMatch || cooldowns[entry] > 0)
                return false;
            if (!effects.TryPrepare(entry, out var state))
                return false;
            state.Rule = entry;
            state.WarningRemaining = Mathf.Max(.1f, entry.warningSeconds);
            active.Add(state);
            state.View = ArenaEventView.Create(this, state);
            if (uses.ContainsKey(entry))
            {
                uses[entry]++;
                cooldowns[entry] = entry.cooldown;
            }

            Match.Notify(entry.label + " — warning (" + state.WarningRemaining.ToString("0.#") + "s)", pool.noticeSeconds);
            Play(entry.warningSound);
            return true;
        }

        internal bool Reserved(Vector2Int cell)
        {
            foreach (var state in active)
                if (state.Cells.Contains(cell) || state.equipment != null && state.sourceCell == cell)
                    return true;
            return false;
        }

        // Check hazards and new blockers together so their combined effect still leaves a safe route.
        internal bool CanChange(IReadOnlyList<Vector2Int> blocks, Vector2Int? open = null)
        {
            var unsafeCells = new HashSet<Vector2Int>(blocks);
            foreach (var state in active)
                if (!state.Warning && !state.Ended && state.Rule.kind == ArenaEventKind.HazardZone)
                    foreach (var cell in state.Cells)
                        unsafeCells.Add(cell);
            return ArenaEventSafety.CanChange(Match, new List<Vector2Int>(unsafeCells), open);
        }

        public Vector2 FilterMovement(ArenaParticipant player, Vector2 desired, Vector2 previous, float dt)
        {
            if (effects == null || Match == null || !Match.Playing)
                return desired;
            float response = 0, factor = 1;
            foreach (var state in active)
                if (!state.Warning && !state.Ended)
                {
                    if (state.Rule.kind == ArenaEventKind.SlipperyFloor && effects.Contains(state, player.Position))
                        response = Mathf.Max(response, state.Rule.steeringResponse);
                    if (state.Rule.kind == ArenaEventKind.MovingStageLights && effects.Contains(state, player.Position))
                        factor = Mathf.Min(factor, state.Rule.lightMovementFactor);
                }

            desired *= factor;
            return response > 0 ? Vector2.Lerp(previous, desired, 1 - Mathf.Exp(-response * dt)) : desired;
        }

        internal void Play(AudioClip clip)
        {
            if (clip == null || pool == null)
                return;
            var audio = GetComponent<AudioSource>();
            if (audio != null)
                audio.PlayOneShot(clip, pool.volume);
        }

        private void End(ArenaEventState state)
        {
            if (!active.Contains(state))
                return;
            effects?.End(state);
            state.Ended = true;
            if (state.View != null)
                Destroy(state.View.gameObject);
            active.Remove(state);
        }

        private void ClearActive()
        {
            while (active.Count > 0)
                End(active[active.Count - 1]);
        }

        // Release only event-owned changes and stop their sounds.
        public void Stop()
        {
            ClearActive();
            var audio = GetComponent<AudioSource>();
            if (audio != null)
                audio.Stop();
        }

        private void OnDisable() => Stop();
    }
}
