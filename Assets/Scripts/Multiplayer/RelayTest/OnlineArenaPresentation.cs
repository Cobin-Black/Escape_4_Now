using System.Collections.Generic;
using Escape4Now.Arena;
using UnityEngine;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // Guests show their own movement immediately, then correct it using the host's updates.
    // Health, items, passing and match results are supplied by the host's normal snapshot.
    public sealed class OnlineArenaPresentation : MonoBehaviour
    {
        [Header("Guest Movement")]
        [SerializeField] private bool predictLocalMovement = true;
        [SerializeField, Range(.1f, 1f), Tooltip("Stop guessing after this much movement is waiting for a host reply.")]
        private float maximumPendingSeconds = .5f;
        [SerializeField, Range(16, 240)] private int maximumPendingFrames = 120;
        [Header("Corrections")]
        [SerializeField, Min(1f), Tooltip("How quickly small visual corrections settle into the host's position.")]
        private float correctionResponse = 20f;
        [SerializeField, Min(.1f), Tooltip("Correct larger differences immediately. Measured in map units.")]
        private float snapDistance = 1f;
        [Header("Other Players")]
        [SerializeField, Range(0, .25f), Tooltip("Longest time to continue another player's last movement while waiting for an update.")]
        private float maximumExtrapolationSeconds = .12f;

        private struct InputFrame
        {
            public int sequence;
            public Vector2 direction;
            public float seconds;
        }

        private sealed class PlayerView
        {
            public bool initialized;
            public int revision;
            public Vector2 velocity, previousPosition;
            public Vector3 previousRender, correction;
            public float projectedSeconds;
        }

        private readonly List<InputFrame> pending = new List<InputFrame>();
        private ArenaMatch arena;
        private PlayerView[] views;
        private int localSlot;
        private float pendingSeconds;
        // Direction of the newest input the host has confirmed. The host keeps moving the player this way
        // until the next input arrives.
        private Vector2 acknowledgedDirection;
        public int InputSequence { get; private set; }
        public int PendingFrames => pending.Count;

        public void Configure(ArenaMatch match, int slot)
        {
            arena = match;
            localSlot = slot;
            views = new PlayerView[arena.Players.Count];
            for (int i = 0; i < views.Length; i++) views[i] = new PlayerView();
            InputSequence = 0;
            ClearPending();
        }

        public void BeginSnapshot()
        {
            for (int i = 0; i < views.Length; i++)
            {
                views[i].previousRender = arena.Players[i].transform.position;
                views[i].previousPosition = arena.Players[i].Position;
            }
        }

        public void AcceptSnapshot(int slot, ArenaParticipant.MovementState movement, Vector2 velocity,
            int acknowledgedInput, float inputAge, int revision, bool reset, float transitSeconds)
        {
            var player = arena.Players[slot];
            var view = views[slot];
            bool snap = reset || !view.initialized || view.revision != revision || !player.Alive;
            player.RestoreMovement(movement);
            view.projectedSeconds = 0;
            view.velocity = velocity;

            if (slot == localSlot)
            {
                if (snap || !predictLocalMovement) ClearPending();
                else
                {
                    // Discard movement already seen by the host, then replay what is still in flight.
                    int confirmed = pending.FindIndex(frame => frame.sequence == acknowledgedInput);
                    if (confirmed >= 0) acknowledgedDirection = pending[confirmed].direction;
                    pending.RemoveAll(frame => frame.sequence <= acknowledgedInput);
                    pendingSeconds = 0;
                    // The host kept moving this player after the input arrived, so the first inputAge seconds
                    // are already in its position. Skip them in the replay without dropping the frames, since a
                    // later update with the same input will report a larger age.
                    // Only frames still going the confirmed direction are skipped: once the guest turned or stopped,
                    // the host hasn't seen that yet.
                    float skip = inputAge;
                    foreach (var frame in pending)
                    {
                        pendingSeconds += frame.seconds;
                        float seconds = frame.seconds;
                        if (skip > 0 && (frame.direction - acknowledgedDirection).sqrMagnitude > .0001f) skip = 0;
                        if (skip > 0)
                        {
                            float used = Mathf.Min(skip, seconds);
                            skip -= used;
                            seconds -= used;
                        }
                        if (seconds > 0) player.SimulateMovement(frame.direction, seconds);
                    }
                }
            }
            else if (!reset && player.Alive)
                ProjectRemote(slot, Mathf.Clamp(transitSeconds, 0, maximumExtrapolationSeconds));

            snap |= Vector2.Distance(view.previousPosition, player.Position) > snapDistance
                || !arena.Map.Layout.Fits(view.previousPosition, arena.Map.Settings.playerRadius);
            view.correction = snap ? Vector3.zero : view.previousRender - player.transform.position;
            view.initialized = true;
            view.revision = revision;
            player.SetPresentationOffset(view.correction);
        }

        public void Advance(Vector2 direction, float seconds)
        {
            if (arena == null || !arena.Replica || !arena.Playing || arena.Paused || seconds <= 0) return;
            for (int i = 0; i < views.Length; i++)
            {
                if (!views[i].initialized || !arena.Players[i].Alive) continue;
                if (i != localSlot) { ProjectRemote(i, seconds); continue; }
                if (!predictLocalMovement || pending.Count >= maximumPendingFrames || pendingSeconds >= maximumPendingSeconds) continue;
                float step = Mathf.Min(seconds, maximumPendingSeconds - pendingSeconds);
                var frame = new InputFrame { sequence = ++InputSequence, direction = Vector2.ClampMagnitude(direction, 1), seconds = step };
                pending.Add(frame);
                pendingSeconds += step;
                arena.Players[i].SimulateMovement(frame.direction, step);
            }
        }

        private void ProjectRemote(int slot, float seconds)
        {
            var view = views[slot];
            float step = Mathf.Min(seconds, Mathf.Max(0, maximumExtrapolationSeconds - view.projectedSeconds));
            if (step <= 0) return;
            arena.Players[slot].Move(arena.Map.GridDirection(view.velocity) * step, step);
            view.projectedSeconds += step;
        }

        private void LateUpdate()
        {
            if (arena == null || !arena.Replica || views == null) return;
            float settle = 1 - Mathf.Exp(-correctionResponse * Time.unscaledDeltaTime);
            for (int i = 0; i < views.Length; i++)
            {
                views[i].correction = Vector3.Lerp(views[i].correction, Vector3.zero, settle);
                arena.Players[i].SetPresentationOffset(views[i].correction);
            }
        }

        public void Stop()
        {
            ClearPending();
            if (views == null) return;
            foreach (var view in views) { view.correction = Vector3.zero; view.velocity = Vector2.zero; }
        }

        private void ClearPending()
        {
            pending.Clear();
            pendingSeconds = 0;
            acknowledgedDirection = Vector2.zero;
        }
    }
}
