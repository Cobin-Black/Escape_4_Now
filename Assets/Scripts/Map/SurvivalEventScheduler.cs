// Selects outdoor and global events after completed turns. It does not use a real-time countdown.

using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Map
{
    public sealed class SurvivalEventScheduler : MonoBehaviour
    {
        private SurvivalEventController owner;
        private SurvivalEventSettings settings;
        private System.Random random;
        private int globalTurns, environmentalTurns;
        // Keep scheduling randomness separate from map placement and dice rolls.
        public void Initialize(SurvivalEventController controller, SurvivalEventSettings configuration, int seed)
        {
            owner = controller; settings = configuration; random = new System.Random(unchecked(seed ^ 0x519A));
            globalTurns = Next(settings.globalIntervalTurns);
            environmentalTurns = Next(settings.environmentalIntervalTurns);
        }
        private int Next(Vector2Int range) => random.Next(range.x, range.y + 1);
        // Only completed turns reduce these counters. Pause and a finished match leave them unchanged.
        public void CountTurn()
        {
            if (owner == null || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused || Escape4Now.Multiplayer.LocalMultiplayerSession.MatchHasEnded) return;
            if (--globalTurns <= 0)
            {
                if (random.NextDouble() < settings.globalEventChance) Trigger(SurvivalEventTrigger.Global);
                globalTurns = Next(settings.globalIntervalTurns);
            }
            if (--environmentalTurns <= 0)
            {
                if (random.NextDouble() < settings.environmentalEventChance) Trigger(SurvivalEventTrigger.Environmental);
                environmentalTurns = Next(settings.environmentalIntervalTurns);
            }
        }
        // Choose by the Inspector weights, then try another compatible effect if the first has no safe placement.
        private void Trigger(SurvivalEventTrigger trigger)
        {
            var pool = new List<SurvivalEventRule>(); float total = 0;
            foreach (var rule in settings.events)
            {
                if (rule == null || !owner.MatchPlan.CanUse(rule) || rule.Trigger != trigger) continue;
                if (trigger == SurvivalEventTrigger.Environmental && rule.type != SurvivalEventType.Mud && rule.type != SurvivalEventType.Sprinklers && rule.type != SurvivalEventType.FallenBranch && rule.type != SurvivalEventType.CarAlarm) continue;
                pool.Add(rule); total += rule.weight;
            }
            //Try remaining compatible effects if the first choice has no safe placement.
            while (pool.Count > 0)
            {
                float pick = (float)random.NextDouble() * total;
                var chosen = pool[pool.Count - 1];
                foreach (var rule in pool) { pick -= rule.weight; if (pick < 0) { chosen = rule; break; } }
                if (owner.TryActivateRule(chosen)) return;
                total -= chosen.weight; pool.Remove(chosen);
            }
        }
    }
}
