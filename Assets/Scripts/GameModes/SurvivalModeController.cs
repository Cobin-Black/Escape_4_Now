// Owns Survival match rules: shared puzzles unlock exits, while each survivor escapes or loses separately.

using System;
using System.Collections.Generic;
using Escape4Now.Map;
using Escape4Now.Player;
using Escape4Now.Puzzles;
using UnityEngine;

namespace Escape4Now.GameModes
{
    [DisallowMultipleComponent, AddComponentMenu("Escape 4 Now/Game Modes/Survival Mode")]
    public sealed class SurvivalModeController : GameModeController
    {
        [Header("Survival References")]
        [SerializeField]
        private SurvivalMap map;
        [SerializeField]
        private SurvivalRulesSettings rules;
        [SerializeField]
        private SurvivalTaskController tasks;
        [SerializeField]
        private SurvivalEventController events;
        [Header("Setup Roles - No Monster Behavior")]
        [SerializeField, Tooltip("Roles belong to setup slots, not turn positions. Leave all Survivor for testing without a monster. The monster team may assign any one slot through setup or SetSetupRole.")]
        private SurvivalRole[] setupRoles = new SurvivalRole[4];
        [Header("Optional External Monster Actor")]
        [SerializeField, Tooltip("Reserved integration for the monster team. Assign an existing actor before Start Match; this mode does not instantiate or control it. Allows four survivors plus one external actor.")]
        private PlayerCharacter externalMonster;
        private MonsterCharacter monsterActor;
        public MonsterCharacter Monster => monsterActor;
        public bool HasExternalMonsterConfiguration => externalMonster != null || Array.Exists(setupRoles, role => role == SurvivalRole.Monster);
        public void ConfigureMonster(MonsterCharacter actor) { if (!started) monsterActor = actor; }
        private readonly Dictionary<PlayerCharacter, SurvivalRole> roles = new Dictionary<PlayerCharacter, SurvivalRole>();
        private readonly Dictionary<PlayerCharacter, SurvivorOutcome> outcomes = new Dictionary<PlayerCharacter, SurvivorOutcome>();
        private bool started;
        public SurvivalRulesSettings Rules => rules;
        public SurvivalEventController Events => events;
        public SurvivalTaskController Tasks => tasks;
        public SurvivalObjectiveLayout Objectives => map.Objectives;
        public override IPlayerGridMap GridMap => map;
        public override GameModeType Type => GameModeType.Monster;
        public override int MinimumPlayers => 1;
        public override int MaximumTurnParticipants => 5;

        public PlayerCharacter ActiveTurnParticipant
        {
            get
            {
                foreach (var player in roles.Keys)
                    if (player.isActiveAndEnabled && Turns.IsPlayersTurn(player))
                        return player;
                return null;
            }
        }

        public bool ConfigureExternalMonster(PlayerCharacter actor)
        {
            if (started || actor == PlayerTemplate)
                return false;
            externalMonster = actor;
            return true;
        }

        public override PlayerCharacter[] BuildTurnParticipants(PlayerCharacter[] participants)
        {
            if (externalMonster == null)
                return participants;
            var all = new PlayerCharacter[participants.Length + 1];
            Array.Copy(participants, all, participants.Length);
            all[participants.Length] = externalMonster;
            return all;
        }

        public override bool TimerExpirationIsWin => false;
        public bool ExitsUnlocked => Objectives != null && CompletedPuzzles >= Objectives.Required;
        public int CompletedPuzzles { get; private set; }

        public SurvivalRole RoleOf(PlayerCharacter player) => player != null && roles.TryGetValue(player, out var role) ? role : SurvivalRole.Survivor;
        public SurvivorOutcome OutcomeOf(PlayerCharacter player) => player != null && outcomes.TryGetValue(player, out var outcome) ? outcome : SurvivorOutcome.Active;
        public override bool IsParticipantActive(PlayerCharacter player) => player != null && (!started || outcomes.TryGetValue(player, out var outcome) && outcome == SurvivorOutcome.Active);
        public bool IsActiveSurvivor(PlayerCharacter player) => IsParticipantActive(player) && RoleOf(player) == SurvivalRole.Survivor;
        public List<PlayerCharacter> ActiveSurvivors
        {
            get
            {
                var result = new List<PlayerCharacter>();
                foreach (var pair in roles)
                    if (IsActiveSurvivor(pair.Key))
                        result.Add(pair.Key);
                return result;
            }
        }

        // Setup slots store roles; a slot's place in the shuffled turn order does not change its role.
        public bool SetSetupRole(int slot, SurvivalRole role)
        {
            if (started || slot < 0 || slot >= setupRoles.Length || !Enum.IsDefined(typeof(SurvivalRole), role))
                return false;
            setupRoles[slot] = role;
            return true;
        }

        public override string GetObjective(PlayerCharacter player) => rules == null ? "" : ExitsUnlocked ? rules.unlockedObjective : string.Format(rules.objectiveFormat, CompletedPuzzles, Objectives != null ? Objectives.Required : 0);
        public override bool CanUseExit(PlayerCharacter player) => started && ExitsUnlocked && IsActiveSurvivor(player);
        public override bool FinishMovementTurn(PlayerCharacter player) => !tasks.CanInteract(player);
        public override bool AutoEndStrandedTurn => true;

        public override bool ValidateConfiguration(out string error)
        {
            if (!base.ValidateConfiguration(out error))
                return false;
            try
            {
                if (rules == null || tasks == null || events == null || setupRoles == null || setupRoles.Length != MaximumPlayers)
                    throw new InvalidOperationException("Assign Survival rules, task/event components, and four setup-role entries.");
                for (int survivors = MinimumPlayers; survivors <= MaximumPlayers; survivors++)
                    rules.Validate(survivors);
                events.ValidateConfiguration();
                var components = new HashSet<MonoBehaviour>(GameplayComponents);
                if (!components.Contains(this) || !components.Contains(tasks) || !components.Contains(events))
                    throw new InvalidOperationException("Include the Survival mode, tasks and events in Match Startup.");
                return true;
            }
            catch (InvalidOperationException failure)
            {
                error = failure.Message;
                return false;
            }
        }

        public override int MovementCost(Vector2Int cell) => events != null ? events.MovementCost(cell) : 1;
        public override int AdjustRollMovement(PlayerCharacter player, int movement) => events.AdjustRollMovement(player, movement);
        // Build a valid campus and reserve the objectives before creating the participants.
        public override bool PrepareMatch(int count, out string error)
        {
            error = "";
            try
            {
                if (rules == null || tasks == null || events == null)
                    throw new InvalidOperationException("Assign Survival rules, task controller and event controller in Unity.");
                if (setupRoles == null || setupRoles.Length < count || externalMonster == PlayerTemplate)
                    throw new InvalidOperationException("Assign a role for every setup slot and a distinct external actor, if used.");
                if (monsterActor != null && (rules.monsterFootprint.x < 2 || rules.monsterFootprint.y < 2))
                    throw new InvalidOperationException("The current MonsterCharacter requires a protected footprint of at least 2 by 2 tiles.");
                int survivors = 0, monsters = 0;
                for (int i = 0; i < count; i++)
                    if (setupRoles[i] == SurvivalRole.Monster)
                        monsters++;
                    else
                        survivors++;
                if (survivors < 1 || monsters + (externalMonster != null ? 1 : 0) > 1)
                    throw new InvalidOperationException("Choose at least one survivor and at most one external monster slot.");
                rules.Validate(survivors);
                events.ValidateConfiguration();
                int seed = map.LayoutSeed;
                for (int attempt = 0; attempt < rules.mapRegenerationAttempts; attempt++)
                {
                    try
                    {
                        map.SetObjectives(new SurvivalObjectiveLayout(map.Layout, rules, survivors));
                        return true;
                    }
                    catch (InvalidOperationException)when (attempt + 1 < rules.mapRegenerationAttempts)
                    {
                        if (!map.GenerateWithSeed(unchecked(seed + attempt + 1)))
                            throw new InvalidOperationException(map.LastGenerationError);
                    }
                }
            }
            catch (Exception failure)when (failure is InvalidOperationException || failure is ArgumentException)
            {
                error = failure.Message;
            }

            return false;
        }

        // Start fresh match progress, then create the task and event objects on the accepted layout.
        public override void BeginMatch(IReadOnlyList<PlayerCharacter> participants)
        {
            roles.Clear();
            outcomes.Clear();
            CompletedPuzzles = 0;
            for (int i = 0; i < participants.Count; i++)
            {
                roles.Add(participants[i], monsterActor != null ? SurvivalRole.Survivor : setupRoles[i]);
                outcomes.Add(participants[i], SurvivorOutcome.Active);
                if (monsterActor == null && setupRoles[i] == SurvivalRole.Monster)
                    participants[i].SetGridPosition(Objectives.MonsterSpawn);
            }

            if (externalMonster != null)
            {
                roles.Add(externalMonster, SurvivalRole.Monster);
                outcomes.Add(externalMonster, SurvivorOutcome.Active);
                externalMonster.ConfigureParticipant(map, Turns, Objectives.MonsterSpawn, participants.Count + 1, externalMonster.IdentityColor, externalMonster.name, this);
            }

            started = true;
            map.SetMonsterOccupancy(cell => monsterActor != null && monsterActor.isActiveAndEnabled
                && cell.x >= monsterActor.GridPosition.x && cell.x < monsterActor.GridPosition.x + rules.monsterFootprint.x
                && cell.y >= monsterActor.GridPosition.y && cell.y < monsterActor.GridPosition.y + rules.monsterFootprint.y);
            tasks.BeginMatch();
            events.BeginMatch();
        }

        // Count a puzzle type once for the whole team; duplicate stations of that type complete together.
        internal bool CompleteTask(SurvivalTaskPlacement task, PlayerCharacter player)
        {
            if (!started || Timer.HasExpired || ExitsUnlocked || !IsActiveSurvivor(player) || task == null || task.Completed)
                return false;
            if (!Objectives.TryCompletePuzzleType(task)) return false;
            CompletedPuzzles++;
            tasks.RefreshMarkers();
            return true;
        }

        // An unlocked exit awards this survivor an escape, without deciding the other survivors' results.
        public override bool OnPlayerEscaped(PlayerCharacter player)
        {
            if (!CanUseExit(player) || !map.IsExit(player.GridPosition) || Timer.HasExpired)
                return false;
            Retire(player, SurvivorOutcome.Escaped);
            return true;
        }

        //Integration point for the monster team. This mode does not deal damage or control a monster.
        public bool MarkSurvivorKilled(PlayerCharacter player)
        {
            if (!started || Timer.HasExpired || !IsActiveSurvivor(player) || !outcomes.ContainsKey(player))
                return false;
            Retire(player, SurvivorOutcome.Dead);
            return true;
        }

        // Remove this survivor from controls, occupied tiles and future turns, while keeping team puzzle progress.
        private void Retire(PlayerCharacter player, SurvivorOutcome outcome)
        {
            outcomes[player] = outcome;
            tasks.CancelFor(player);
            player.enabled = false;
            if (player.Inventory != null)
                player.Inventory.enabled = false;
            foreach (var renderer in player.GetComponentsInChildren<Renderer>())
                renderer.enabled = false;
            var occupied = new List<Vector2Int>();
            foreach (var pair in roles)
                if (IsParticipantActive(pair.Key))
                    occupied.Add(pair.Key.GridPosition);
            map.ReplacePlayerOccupants(occupied);
            Turns.RemoveFromTurnCycle(player);
            if (AllSurvivorsFinished())
                Time.timeScale = 0f;
        }

        private bool AllSurvivorsFinished() => started && ActiveSurvivors.Count == 0;
        // Handle deaths and timeout losses without treating survival until zero as a win.
        private void Update()
        {
            if (!started) return;
            foreach (var player in ActiveSurvivors)
                if (player.Health <= 0) MarkSurvivorKilled(player);
            if (Timer == null || !Timer.HasExpired)
                return;
            foreach (var player in ActiveSurvivors)
                Retire(player, SurvivorOutcome.TimedOut);
        }

        public override bool TryGetResult(
            IReadOnlyList<PlayerCharacter> participants,
            out MatchResult result)
        {
            result = default;
            if (!started || !AllSurvivorsFinished() && !Timer.HasExpired)
                return false;
            // List each survivor's result separately; an earlier escape stays a win if another survivor later loses.
            var escaped = new List<string>();
            var dead = new List<string>();
            var lost = new List<string>();
            foreach (var pair in roles)
            {
                if (pair.Value != SurvivalRole.Survivor)
                    continue;
                var outcome = OutcomeOf(pair.Key);
                if (outcome == SurvivorOutcome.Escaped)
                    escaped.Add(pair.Key.name);
                else if (outcome == SurvivorOutcome.Dead)
                    dead.Add(pair.Key.name);
                else
                    lost.Add(pair.Key.name);
            }

            string Names(List<string> list) => list.Count == 0 ? rules.noneLabel : string.Join("\n", list);
            result = new MatchResult(escaped.Count > 0, rules.resultTitle, rules.escapedLabel + "\n" + Names(escaped) + "\n\n" + rules.deadLabel + "\n" + Names(dead) + "\n\n" + rules.timeoutLabel + "\n" + Names(lost));
            return true;
        }
    }
}
