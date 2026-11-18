using System;
using Escape4Now.Arena;
using UnityEngine;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // Hot Potato adds the round, holder, clock, and explosion to the shared arena state.
    public sealed class OnlineHotPotatoMatch : OnlineArenaMatch
    {
        private HotPotatoController Potato => (HotPotatoController)Arena;
        private int explosion, shownExplosion;
        private Vector3 explosionPosition;

        [Serializable]
        private sealed class PotatoSnapshot : Snapshot
        {
            public int round, holder = -1, explosion;
            public Vector3 explosionPosition;
            public float seconds;
            public bool betweenRounds;
        }

        protected override void ListenToMode() => Potato.PotatoExploded += RecordExplosion;
        protected override void StopListeningToMode() => Potato.PotatoExploded -= RecordExplosion;

        protected override Snapshot CaptureMode() => new PotatoSnapshot
        {
            round = Potato.Round,
            holder = Potato.Holder != null ? Potato.Holder.Slot : -1,
            seconds = Potato.RemainingSeconds,
            betweenRounds = Potato.BetweenRounds,
            explosion = explosion,
            explosionPosition = explosionPosition
        };

        protected override Snapshot ReadSnapshot(string payload, int count)
        {
            var state = JsonUtility.FromJson<PotatoSnapshot>(payload);
            return state != null && state.holder >= -1 && state.holder < count ? state : null;
        }

        protected override void ApplyMode(Snapshot snapshot)
        {
            var state = (PotatoSnapshot)snapshot;
            float transit = !state.paused && !state.betweenRounds ? EstimatedTransitSeconds : 0;
            Potato.ApplyOnlineRound(state.round, Mathf.Max(0, state.seconds - transit), state.holder, state.betweenRounds);
            // The same explosion can appear in multiple packets. Play it once per guest.
            if (state.explosion > shownExplosion) Potato.ShowOnlineExplosion(state.explosionPosition);
            shownExplosion = state.explosion;
        }

        protected override void AdvanceGuestView(float seconds) => Potato.AdvanceOnlineClock(seconds);

        private void RecordExplosion(Vector3 position)
        {
            if (Arena.Replica) return;
            explosion++;
            explosionPosition = position;
        }
    }
}
