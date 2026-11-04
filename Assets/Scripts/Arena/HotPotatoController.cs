using Escape4Now.GameModes;
using UnityEngine;
using UnityEngine.Events;

namespace Escape4Now.Arena
{
    public sealed class HotPotatoController : ArenaMatch
    {
        [Header("Potato Rules")]
        [SerializeField, Min(1)]
        private float roundSeconds = 25;
        [SerializeField, Min(.1f)]
        private float passDistance = 1.3f;
        [SerializeField, Min(0)]
        private float passCooldown = .6f;
        [SerializeField, Min(0)]
        private float roundDelay = 2;
        [SerializeField]
        private bool transferOnContact;
        [SerializeField]
        private bool repositionBetweenRounds = true;
        [Header("Presentation")]
        [SerializeField, TextArea]
        private string objective = "Pass the potato before it explodes. Last player standing wins.";
        [SerializeField]
        private string roundFormat = "ROUND {0}";
        [SerializeField]
        private string passNotice = "{0} passed to {1}";
        [SerializeField]
        private string explosionNotice = "{0} was eliminated!";
        [SerializeField]
        private UnityEvent onPass, onExplosion;
        public override GameModeType Type => GameModeType.HotPotato;
        public override string Objective => objective;
        public override string MatchInfo => string.Format(roundFormat, Round);
        public ArenaParticipant Holder { get; private set; }
        public float RemainingSeconds { get; private set; }
        public float RoundSeconds => roundSeconds;
        public int Round { get; private set; }
        public bool BetweenRounds => delay > 0;

        // A presentation-only signal, sent before the eliminated holder is hidden.
        public event System.Action<Vector3> PotatoExploded;
        private float cooldown, delay;
        protected override void BeginRules()
        {
            Round = 0;
            StartRound();
        }

        // Keep the current rules: clear round effects, safely reposition living players and choose a holder.
        private void StartRound()
        {
            foreach (var p in Players)
                p.ClearRoundEffects();
            if (repositionBetweenRounds && Round > 0 && Map.Layout.TrySpawns(Remaining, Map.Settings, Map.Random, out var spawns))
            {
                int index = 0;
                foreach (var p in Players)
                    if (p.Alive)
                        p.Place(spawns[index++]);
            }

            Round++;
            RemainingSeconds = roundSeconds;
            cooldown = passCooldown;
            delay = 0;
            ShuffleHolder();
        }

        public void ShuffleHolder(bool excludeCurrent = false)
        {
            var alive = Players.FindAll(p => p.Alive && (!excludeCurrent || p != Holder));
            if (alive.Count > 0)
                Holder = alive[Map.Random.Next(alive.Count)];
            cooldown = passCooldown;
        }

        public void AdjustTime(float amount)
        {
            RemainingSeconds = Mathf.Max(0, RemainingSeconds + amount);
        }

        protected override void Tick(float dt)
        {
            if (delay > 0)
            {
                delay -= dt;
                if (delay <= 0)
                    StartRound();
                return;
            }

            if (Holder == null || !Holder.Alive)
            {
                ShuffleHolder();
                return;
            }

            RemainingSeconds = Mathf.Max(0, RemainingSeconds - dt);
            cooldown = Mathf.Max(0, cooldown - dt);
            // Resolve expiry before tag input so a late pass cannot escape an expired countdown.
            if (RemainingSeconds <= 0)
            {
                var lost = Holder;
                Holder = null;
                Notify(string.Format(explosionNotice, lost.name));
                PotatoExploded?.Invoke(lost.transform.position);
                onExplosion?.Invoke();
                lost.Eliminate();
                if (Playing)
                    delay = Mathf.Max(.01f, roundDelay);
                return;
            }

            if (cooldown <= 0 && (transferOnContact || Holder.Input.TagPressed))
                TryPass();
        }

        // Pass only on a valid tag attempt, within range and with no solid obstruction.
        public bool TryPass()
        {
            if (!Playing || Paused || BetweenRounds || cooldown > 0 || Holder == null || RemainingSeconds <= 0)
                return false;
            ArenaParticipant target = null;
            float nearest = passDistance;
            foreach (var other in Players)
            {
                if (!other.Alive || other == Holder || other.Shield)
                    continue;
                float d = Vector2.Distance(other.Position, Holder.Position);
                if (d <= nearest && ClearPass(Holder.Position, other.Position))
                {
                    nearest = d;
                    target = other;
                }
            }

            if (target == null)
                return false;
            string previous = Holder.name;
            Holder = target;
            cooldown = passCooldown;
            Notify(string.Format(passNotice, previous, target.name));
            onPass?.Invoke();
            return true;
        }

        // A short tag cannot reach through a wall, chair row or equipment.
        private bool ClearPass(Vector2 a, Vector2 b)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / Map.Settings.playerRadius));
            for (int i = 0; i <= steps; i++)
                if (!Map.Layout.Fits(Vector2.Lerp(a, b, i / (float)steps), .05f))
                    return false;
            return true;
        }

        protected override void ParticipantEliminated(ArenaParticipant player)
        {
            if (CheckLastPlayer())
            {
                Holder = null;
                return;
            }

            if (Holder == player)
            {
                Holder = null;
                delay = Mathf.Max(.01f, roundDelay);
            }
        }
    }
}
