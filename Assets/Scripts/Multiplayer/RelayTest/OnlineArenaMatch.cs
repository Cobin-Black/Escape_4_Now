using System;
using System.Collections.Generic;
using System.Linq;
using Escape4Now.Arena;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // Shares the online movement, map and event code used by both arena modes.
    [DefaultExecutionOrder(-100)]
    public abstract class OnlineArenaMatch : OnlineModeMatch
    {
        [Header("Scene References")]
        [SerializeField, Tooltip("The local game mode controller used to run the online match.")]
        private ArenaMatch arena;
        [SerializeField] private ArenaUI arenaUI;
        [SerializeField] private OnlineRaceSession room;
        [SerializeField] private LanMultiplayerSession transport;
        [SerializeField] private OnlineArenaPresentation presentation;
        [Header("Online Input")]
        [SerializeField, Range(5, 25), Tooltip("How often to send held movement. Button presses are sent right away.")]
        private float inputUpdatesPerSecond = 20;
        [SerializeField, Tooltip("When enabled, use the first connected gamepad on this computer. Otherwise use keyboard.")]
        private bool preferGamepad = true;
        [SerializeField, Range(.001f, .25f), Tooltip("Send changed stick movement immediately when it differs by this amount.")]
        private float inputChangeThreshold = .03f;
        [SerializeField, Range(0, .3f), Tooltip("Longest wait added to the host's own input to match the guests' ping. Keeps a very slow guest from making the host's controls feel sluggish.")]
        private float maxHostInputDelay = .15f;
        // The host's own input packets, held until the guests' input would have arrived.
        private readonly Queue<(float time, InputPacket packet)> delayedHostInput = new Queue<(float time, InputPacket packet)>();

        private ArenaLocalInput input;
        private float nextInput;
        private Vector2 lastSentMovement;
        private readonly int[] appliedInput = new int[4];
        protected float EstimatedTransitSeconds => room != null && room.Relay != null
            ? Mathf.Clamp((float)room.Relay.LastRoundTripMilliseconds / 2000f, 0, .25f) : 0;
        public override bool Started => started;
        private bool started;
        public override bool Finished => started && !arena.Playing;
        public override string Error => arena.Error;
        public override string Title => arena.Title;
        public override string Objective => arena.Objective;
        public override Skin_Handler ProfileSkins => arena.Skins;
        private readonly string[] names = new string[4];
        // Slots the host fills with bots. Only the host runs them; guests just show the host's snapshot.
        private bool[] botSlots;
        public override bool SupportsBots => true;
        public override void SetBotSlots(bool[] bots) => botSlots = bots != null ? (bool[])bots.Clone() : null;
        public override string SlotName(int slot) => string.IsNullOrEmpty(names[slot]) ? base.SlotName(slot) : names[slot];
        public override void SetSlotName(int slot, string name)
        {
            name = name?.Replace("<", "").Replace(">", "").Trim();
            names[slot] = string.IsNullOrEmpty(name) ? null : name.Substring(0, Math.Min(16, name.Length));
        }
        public override Sprite SlotPortrait(int slot) => arena.Skins?.GetPlayerSkin(slot)?.front;
        public override Color SlotColor(int slot) => arena.GetPlayerColor(slot);
        protected ArenaMatch Arena => arena;

        [Serializable]
        private sealed class InputPacket
        {
            public Vector2 move;
            // These saved packet names carry Potato Pass and Attack, respectively.
            public bool tag, useItem;
            public int pause;
            public int sequence;
        }

        [Serializable]
        protected sealed class Actor
        {
            public Vector2 position, velocity;
            public int health;
            public Vector2 motorVelocity, eventVelocity;
            public int inputSequence, movementRevision;
            public float speedMultiplier = 1;
            // King of the Hill also sends score, respawn, and held item details.
            public int score, respawn, itemType = -1, itemUses, itemMaxUses, shieldBlocks;
            public int swing;
            public Vector3 swingPosition;
            public Vector2 swingDirection;
            public bool alive;
        }

        [Serializable]
        protected class Snapshot
        {
            public int seed;
            public bool playing, paused;
            public string notice, result;
            //Lobby slot of the winner, so each guest can save their own win or loss.
            public int winner = -1;
            public string[] names;
            public int[] skins;
            public Vector2Int[] equipmentCells;
            public int[] blockedCells;
            public Actor[] actors;
            public ArenaEventDirector.OnlineEvent[] events;
        }

        // Each mode supplies its own extra state using the same common snapshot.
        protected abstract Snapshot CaptureMode();
        protected abstract Snapshot ReadSnapshot(string payload, int count);
        protected abstract void ApplyMode(Snapshot snapshot);
        protected virtual void CapturePlayer(ArenaParticipant player, Actor actor) { }
        protected virtual void ApplyPlayer(ArenaParticipant player, Actor actor) { }
        protected virtual void ListenToMode() { }
        protected virtual void StopListeningToMode() { }
        protected virtual void AdvanceGuestView(float seconds) { }

        protected virtual void Awake()
        {
            arena.ConfigureOnline(false);
            arenaUI.OnlineLeave = room.Leave;
            arena.OnlinePauseRequested += RequestPause;
            ControllerHandling.AssignmentsChanged += ConfigureLocalInput;
            ListenToMode();
        }

        protected virtual void OnDestroy()
        {
            arena.OnlinePauseRequested -= RequestPause;
            ControllerHandling.AssignmentsChanged -= ConfigureLocalInput;
            StopListeningToMode();
        }

        // A guest builds the same map using the seed sent by the host.
        private bool StartBoard(int count, int? seed, int[] skinIndices = null)
        {
            arena.ConfigureOnline(seed.HasValue);
            var slots = Enumerable.Range(0, count).ToArray();
            var defaults = slots.Select(_ => -1).ToArray();
            skinIndices ??= slots.Select(slot => ProfileSkins != null ? ProfileSkins.GetSkinIndex(slot) : -1).ToArray();
            // The host names its bots so every guest sees "BOT 3" in the HUD and results.
            bool[] bots = seed.HasValue ? null : slots.Select(slot => botSlots != null && slot < botSlots.Length && botSlots[slot]).ToArray();
            if (bots != null)
                foreach (int slot in slots)
                    if (bots[slot]) names[slot] = "BOT " + (slot + 1);
            started = arena.StartMatch(count, slots.Select(SlotName).ToArray(), defaults, skinIndices, seed, bots);
            if (!started) return false;
            disconnected = false;
            Array.Clear(appliedInput, 0, appliedInput.Length);
            delayedHostInput.Clear();
            input = gameObject.AddComponent<ArenaLocalInput>();
            ConfigureLocalInput();
            if (arena.Replica)
            {
                if (presentation == null) presentation = GetComponent<OnlineArenaPresentation>();
                if (presentation == null) presentation = gameObject.AddComponent<OnlineArenaPresentation>();
                presentation.Configure(arena, transport.LocalPlayerIndex);
            }
            return true;
        }

        private void ConfigureLocalInput()
        {
            if (input == null) return;
            // Each computer reads the same Player actions used by the local mode.
            var pad = preferGamepad ? ControllerHandling.GetGamepad(0) : null;
            if (pad != null) input.ConfigureGamepad(arena.GamepadActions, pad);
            else input.ConfigurePrimaryKeyboard(arena.GamepadActions, null);
        }

        public override bool StartHost(int count) => StartBoard(count, null);

        public override string Capture()
        {
            var state = CaptureMode();
            state.seed = arena.Map.Seed;
            state.names = arena.Players.Select(p => SlotName(p.Slot)).ToArray();
            state.skins = arena.Players.Select(p => ProfileSkins != null ? ProfileSkins.GetSkinIndex(p.Slot) : -1).ToArray();
            state.equipmentCells = arena.Map.EquipmentViews.Select(prop => prop.Cell).ToArray();
            var layout = arena.Map.Layout;
            state.blockedCells = Enumerable.Range(0, layout.Width * layout.Height)
                .Where(cell => layout.blocked[cell % layout.Width, cell / layout.Width]).ToArray();
            state.playing = arena.Playing;
            state.paused = arena.Paused;
            state.result = arena.Result;
            state.winner = arena.WinnerSlot;
            state.notice = arena.Notice;
            state.actors = arena.Players.Select(CaptureActor).ToArray();
            state.events = arena.Events.CaptureOnlineEvents();
            return JsonUtility.ToJson(state);
        }

        private Actor CaptureActor(ArenaParticipant player)
        {
            var movement = player.CaptureMovement();
            var actor = new Actor
            {
                position = player.Position, velocity = player.ScreenVelocity, health = player.Character.Health, alive = player.Alive,
                motorVelocity = movement.motorVelocity, eventVelocity = movement.eventVelocity,
                inputSequence = appliedInput[player.Slot], movementRevision = player.MovementRevision,
                speedMultiplier = player.SpeedMultiplier
            };
            CapturePlayer(player, actor);
            return actor;
        }

        // Guests show the host's results. They do not run a second match simulation.
        public override bool Apply(int count, string payload)
        {
            var state = ReadSnapshot(payload, count);
            if (state?.actors == null || state.actors.Length != count
                || state.actors.Any(p => p == null || !Finite(p.position) || !Finite(p.velocity)
                    || !Finite(p.motorVelocity) || !Finite(p.eventVelocity) || p.inputSequence < 0
                    || float.IsNaN(p.speedMultiplier) || float.IsInfinity(p.speedMultiplier) || p.speedMultiplier < 0)) return false;
            if (!started)
            {
                if (state.names?.Length != count || state.skins?.Length != count) return false;
                for (int i = 0; i < count; i++) SetSlotName(i, state.names[i]);
                if (!StartBoard(count, state.seed, state.skins)) return false;
            }
            if (arena.Map.Seed != state.seed || count != arena.Players.Count) return false;
            var layout = arena.Map.Layout;
            if (state.blockedCells != null && (state.blockedCells.Length > layout.Width * layout.Height
                || state.blockedCells.Any(cell => cell < 0 || cell >= layout.Width * layout.Height))) return false;
            bool resetView = state.paused != arena.Paused || !state.playing;
            presentation?.BeginSnapshot();
            for (int i = 0; i < count; i++)
            {
                Actor actor = state.actors[i];
                var player = arena.Players[i];
                player.ApplyOnlineState(actor.position, actor.velocity, actor.health, actor.alive);
                player.SpeedMultiplier = actor.speedMultiplier;
                ApplyPlayer(player, actor);
            }
            ApplyMode(state);
            arena.Events.ApplyOnlineEvents(state.events);
            if (state.equipmentCells?.Length == arena.Map.EquipmentViews.Count)
                for (int i = 0; i < state.equipmentCells.Length; i++)
                {
                    var prop = arena.Map.EquipmentViews[i];
                    if (prop.Cell == state.equipmentCells[i]) continue;
                    prop.Cell = state.equipmentCells[i];
                    prop.Visual.position = arena.Map.World(prop.Cell);
                }
            // Prediction uses the host's current blockers, including fallen props and moved equipment.
            if (state.blockedCells != null)
            {
                Array.Clear(layout.blocked, 0, layout.blocked.Length);
                foreach (int cell in state.blockedCells) layout.blocked[cell % layout.Width, cell / layout.Width] = true;
            }
            arena.ApplyOnlineState(state.playing, state.paused, state.result, state.notice);
            arena.ApplyOnlineWinner(state.winner >= 0 && state.winner < count ? state.winner : -1);
            if (presentation != null)
                for (int i = 0; i < count; i++)
                {
                    var actor = state.actors[i];
                    presentation.AcceptSnapshot(i, new ArenaParticipant.MovementState
                    {
                        position = actor.position, motorVelocity = actor.motorVelocity, eventVelocity = actor.eventVelocity
                    }, actor.velocity, actor.inputSequence, actor.movementRevision,
                        resetView || state.paused, EstimatedTransitSeconds);
                }
            return true;
        }

        // The room has already matched the sender to this player slot.
        public override void ReceiveInput(int slot, string payload)
        {
            if (!arena.Playing || slot < 0 || slot >= arena.Players.Count || string.IsNullOrEmpty(payload)) return;
            InputPacket packet;
            try { packet = JsonUtility.FromJson<InputPacket>(payload); }
            catch (ArgumentException) { return; }
            if (packet == null || !Finite(packet.move) || packet.sequence < 0) return;
            if (packet.pause == 1 || packet.pause == -1)
            {
                delayedHostInput.Clear();
                arena.ApplyOnlineState(true, packet.pause == 1, arena.Result, arena.Notice);
                return;
            }
            // Slot 0 is the host. Its input arrives instantly, while a guest's has to cross the internet first,
            // so the host's moves wait the same amount before they count. Pause stays instant.
            float delay = Mathf.Min(HostInputDelay, maxHostInputDelay);
            if (slot == 0 && delay > 0)
            {
                delayedHostInput.Enqueue((Time.unscaledTime + delay, packet));
                return;
            }
            ApplyInput(slot, packet);
        }

        private void ApplyInput(int slot, InputPacket packet)
        {
            if (arena.Playing && !arena.Paused && slot < arena.Players.Count && arena.Players[slot].Alive)
            {
                arena.Players[slot].SetOnlineInput(packet.move, packet.tag, packet.useItem);
                appliedInput[slot] = packet.sequence;
            }
        }

        private void Update()
        {
            while (delayedHostInput.Count > 0 && delayedHostInput.Peek().time <= Time.unscaledTime)
                ApplyInput(0, delayedHostInput.Dequeue().packet);
            if (!arena.Playing) delayedHostInput.Clear();
            if (arena.Replica && started)
            {
                presentation?.Advance(input != null ? input.Move : Vector2.zero, Time.deltaTime);
                if (arena.Playing && !arena.Paused) AdvanceGuestView(Time.deltaTime);
            }
        }

        // Use the arena's Input Actions on this computer and send requests for the local slot.
        public override void SendLocalInput(LanMultiplayerSession session)
        {
            if (!arena.Playing || input == null) return;
            bool tag = input.PassPressed;
            bool useItem = input.AttackPressed;
            Vector2 movement = input.Move;
            bool changed = (movement - lastSentMovement).sqrMagnitude >= inputChangeThreshold * inputChangeThreshold;
            if (!tag && !useItem && !changed && Time.unscaledTime < nextInput) return;
            nextInput = Time.unscaledTime + 1f / inputUpdatesPerSecond;
            lastSentMovement = movement;
            session.SubmitModeInput(JsonUtility.ToJson(new InputPacket
            {
                move = movement, tag = tag, useItem = useItem, sequence = presentation != null ? presentation.InputSequence : 0
            }));
        }

        private void RequestPause(bool pause) =>
            transport.SubmitModeInput(JsonUtility.ToJson(new InputPacket { pause = pause ? 1 : -1 }));

        public override void ConnectionLost(string reason)
        {
            if (!started || Finished) return;
            disconnected = true;
            presentation?.Stop();
            arena.Events.Stop();
            arena.ApplyOnlineState(false, false, reason, "");
        }

        // A match that ended because the connection dropped has no winner to save.
        private bool disconnected;

        public override bool TryGetSlotResult(int slot, out Escape4Now.Accounts.MatchStat stat)
        {
            stat = default;
            if (!Finished || disconnected || slot < 0 || slot >= arena.Players.Count || string.IsNullOrEmpty(arena.Result)) return false;
            stat = new Escape4Now.Accounts.MatchStat(0, arena.StatMode, arena.WinnerSlot == slot, arena.Players.Count, 0f, true);
            return true;
        }

        private static bool Finite(Vector2 vector) =>
            !float.IsNaN(vector.x) && !float.IsNaN(vector.y) && !float.IsInfinity(vector.x) && !float.IsInfinity(vector.y);
    }
}
