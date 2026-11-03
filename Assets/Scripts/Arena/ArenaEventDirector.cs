using System;
using System.Collections.Generic;
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
            if (Match == null || !Match.Playing || Match.Paused)
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
