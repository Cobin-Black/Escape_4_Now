using System;
using System.Linq;
using Escape4Now.GameModes;
using Escape4Now.Map;
using Escape4Now.Player;
using Escape4Now.Puzzles;
using Escape4Now.TurnSystem;
using Escape4Now.UI;
using UnityEngine;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // The host runs Survivor turns, the AI monster, events and puzzle validation.
    public sealed class OnlineSurvivalMatch : OnlineModeMatch
    {
        [Header("Scene References")]
        [SerializeField] private LocalMultiplayerSession local;
        [SerializeField] private SurvivalModeController mode;
        [SerializeField] private LanMultiplayerSession transport;
        [Header("Lobby Text")]
        [SerializeField] private string title = "Survival";
        [SerializeField, TextArea] private string objective = "Solve puzzles together, unlock the exits and escape the monster.";
        private string connectionError;
        // A slot of -1 leaves the monster under AI control.
        public int MonsterSlot { get; set; } = -1;
        // The room counts the monster slot, but the turn system only counts survivors.
        private int SurvivorIndex(int slot) => MonsterSlot < 0 || slot < MonsterSlot ? slot : slot == MonsterSlot ? -1 : slot - 1;
        private int RoomSlot(int survivor) => survivor >= 0 && MonsterSlot >= 0 && survivor >= MonsterSlot ? survivor + 1 : survivor;
        private SurvivalMap Map => (SurvivalMap)mode.GridMap;
        private SurvivalTaskController Tasks => mode.Tasks;
        public override bool UsesTurnSystem => true;
        public override bool Started => local.MatchStarted;
        public override bool Finished => Started && (connectionError != null || mode.TryGetResult(local.Participants, out _));
        public override string Error => local.Status;
        public override string Title => title;
        public override string Objective => objective;
        public override Skin_Handler ProfileSkins => local.Skins;
        public override void SetSlotName(int slot, string name) => local.SetPlayerName(slot, name);
        public override string SlotName(int slot) => local.GetPlayerName(slot);
        public override Color SlotColor(int slot) => local.GetPlayerColor(slot);
        public override Sprite SlotPortrait(int slot) => local.Skins?.GetPlayerSkin(slot)?.front;

        [Serializable]
        private sealed class Command
        {
            public string action, value;
            public int a, b, round, revision;
        }

        [Serializable]
        private sealed class Survivor
        {
            public LanPlayerState player;
            public int health;
            public SurvivorOutcome outcome;
        }

        [Serializable]
        private sealed class Snapshot
        {
            public int seed, round, slot, completed, blackout, actor = -1, revision, station, attemptSeed;
            public int monsterRoll, monsterMoves;
            public int monsterSlot = -1, chargeCooldown, roarCooldown, stompCooldown;
            public Vector2Int monsterCell;
            public Vector3 monsterPosition;
            public bool monsterRolled, monsterRolling, enemyTurn, paused;
            public float time;
            public string notice;
            public int[] order, completedStations;
            public Survivor[] survivors;
            public string[] names;
            public int[] skins;
            public bool rain;
            public SurvivalEnvironmentController.OnlineArea[] areas;
            public Vector2Int[] pickups;
            public PuzzleSession.Progress puzzle;
        }

        // The host picks the seed. Guests use that seed and show the host's updates.
        private bool StartBoard(int count, int? seed)
        {
            local.SetPlayerCount(count);
            if (MonsterSlot >= 0) local.SetMonsterPlayer(MonsterSlot);
            if (!local.StartOnlineSurvival(count, seed)) return false;
            if (mode.Monster != null) mode.Monster.OnlinePlayerControlled = MonsterSlot >= 0 && !mode.Monster.IsAIControlled;
            Tasks.OnlineHost = !seed.HasValue;
            Tasks.IsLocalPuzzlePlayer = player =>
                transport.LocalPlayerIndex != MonsterSlot && Array.IndexOf(local.Participants.ToArray(), player) == SurvivorIndex(transport.LocalPlayerIndex);
            Tasks.OnlineInteract = player =>
            {
                if (Tasks.OnlineHost && player.IsBotControlled) return Tasks.StartAttempt(player);
                if (!CanUseTurn(transport.LocalPlayerIndex) || player != mode.ActiveTurnParticipant) return false;
                Send("interact");
                return true;
            };
            Tasks.OnlineClose = () => Send("close");
            return true;
        }

        public override bool StartHost(int count) => StartBoard(count, null);

        // Translate the room slot into the survivor or monster turn before allowing input.
        public override bool CanUseTurn(int slot) =>
            slot >= 0 && Started && !Finished && !LocalMultiplayerSession.GameplayPaused
            && (slot == MonsterSlot ? mode.Turns.CurrentTurnOwner == TurnOwner.Enemy && mode.Monster != null
                : mode.Turns.CurrentTurnOwner == TurnOwner.Player && mode.Turns.CurrentPlayerIndex == SurvivorIndex(slot)
                && SurvivorIndex(slot) < local.Participants.Count && mode.IsActiveSurvivor(local.Participants[SurvivorIndex(slot)]));

        // Save the turn, monster, objectives, and active effects so everyone sees the same match.
        public override string Capture()
        {
            var monster = mode.Monster;
            return JsonUtility.ToJson(new Snapshot
            {
                seed = Map.LayoutSeed, round = mode.Turns.TurnNumber, slot = mode.Turns.CurrentPlayerIndex,
                order = mode.Turns.CopyTurnOrder(), enemyTurn = mode.Turns.CurrentTurnOwner == TurnOwner.Enemy,
                time = mode.Timer.TimeRemaining, paused = LocalMultiplayerSession.GameplayPaused,
                completed = mode.CompletedPuzzles, notice = mode.Events.CurrentMessage, blackout = mode.Events.BlackoutTurns,
                rain = mode.Events.RainActive, areas = mode.Events.Environment.CaptureOnlineAreas(),
                pickups = mode.Events.Environment.CaptureOnlinePickups(),
                completedStations = Enumerable.Range(0, mode.Objectives.Tasks.Count).Where(i => mode.Objectives.Tasks[i].Completed).ToArray(),
                actor = Array.IndexOf(local.Participants.ToArray(), Tasks.Actor), revision = Tasks.Revision,
                station = Array.IndexOf(mode.Objectives.Tasks.ToArray(), Tasks.CurrentTask),
                attemptSeed = Tasks.AttemptSeed, puzzle = Tasks.Puzzle?.CaptureProgress(),
                monsterSlot = MonsterSlot,
                names = Enumerable.Range(0, local.Participants.Count + (MonsterSlot >= 0 ? 1 : 0)).Select(SlotName).ToArray(),
                skins = Enumerable.Range(0, local.Participants.Count + (MonsterSlot >= 0 ? 1 : 0))
                    .Select(slot => ProfileSkins != null ? ProfileSkins.GetSkinIndex(slot) : -1).ToArray(),
                monsterCell = monster != null ? monster.GridPosition : default,
                monsterPosition = monster != null ? monster.transform.position : default,
                monsterRoll = monster != null ? monster.LastRoll : 0, monsterMoves = monster != null ? monster.MovesRemaining : 0,
                monsterRolled = monster != null && monster.HasRolled,
                monsterRolling = monster != null && monster.IsRolling,
                chargeCooldown = monster?.ChargeCooldownRemaining ?? 0, roarCooldown = monster?.RoarCooldownRemaining ?? 0,
                stompCooldown = monster?.StompCooldownRemaining ?? 0,
                survivors = local.Participants.Select(p => new Survivor
                {
                    outcome = mode.OutcomeOf(p), health = p.Health,
                    player = new LanPlayerState
                    {
                        x = p.GridPosition.x, y = p.GridPosition.y, roll = p.LastRoll, firstDie = p.FirstDie, secondDie = p.SecondDie,
                        moves = p.MovesRemaining, rolled = p.HasRolled, rolling = p.IsRollAnimating, moving = p.IsMoving,
                        worldX = p.transform.position.x, worldY = p.transform.position.y, won = p.HasReachedExit
                    }
                }).ToArray()
            });
        }

        // Validate the host update, build the matching campus if needed, and copy confirmed Survival progress.
        public override bool Apply(int count, string payload)
        {
            var state = JsonUtility.FromJson<Snapshot>(payload);
            if (state?.survivors == null || state.monsterSlot < -1 || state.monsterSlot >= count
                || state.survivors.Length != count - (state.monsterSlot >= 0 ? 1 : 0) || state.order == null
                || state.completedStations == null || state.actor < -1 || state.actor >= state.survivors.Length
                || state.survivors.Any(value => value?.player == null)) return false;
            if (!Started)
            {
                if (state.names?.Length != count || state.skins?.Length != count) return false;
                MonsterSlot = state.monsterSlot;
                ProfileSkins?.ClearAll();
                for (int i = 0; i < count; i++)
                {
                    SetSlotName(i, state.names[i]);
                    ProfileSkins?.TryAssign(i, state.skins[i]);
                }
                ProfileSkins?.FillMissing();
                if (!StartBoard(count, state.seed)) return false;
            }
            if (Map.LayoutSeed != state.seed || !mode.Turns.ApplyNetworkTurn(state.slot, state.round, state.order,
                state.enemyTurn ? TurnOwner.Enemy : TurnOwner.Player)) return false;
            for (int i = 0; i < state.survivors.Length; i++)
            {
                var player = local.Participants[i];
                var actor = state.survivors[i];
                player.ApplyNetworkState(actor.player, false);
                player.ApplyReplicatedHealth(actor.health);
                mode.ApplyOnlineOutcome(player, actor.outcome);
            }
            Map.ReplacePlayerOccupants(local.Participants.Where(mode.IsActiveSurvivor).Select(p => p.GridPosition).ToArray());
            mode.Monster?.ApplyOnlinePose(state.monsterCell, state.monsterPosition, state.monsterRoll, state.monsterMoves,
                state.monsterRolled, state.monsterRolling, state.chargeCooldown, state.roarCooldown, state.stompCooldown);
            mode.Timer.ApplyNetworkTime(state.time);
            mode.ApplyOnlineObjectives(state.completed, state.completedStations);
            mode.Events.ApplyNetworkDisplay(state.notice, state.blackout);
            mode.Events.ApplyOnlineRain(state.rain);
            mode.Events.Environment.ApplyOnlineAreas(state.areas, state.pickups);
            bool localPuzzle = state.actor >= 0 && RoomSlot(state.actor) == transport.LocalPlayerIndex;
            if (localPuzzle && (state.station < 0 || state.station >= mode.Objectives.Tasks.Count)) return false;
            Tasks.ApplyOnlineAttempt(localPuzzle ? local.Participants[state.actor] : null, state.revision,
                state.attemptSeed, state.station, state.puzzle, Send);
            local.ApplyNetworkPause(state.paused, Finished);
            return true;
        }

        // Check the sender's turn and puzzle attempt before passing their input to the local mode.
        public override void ReceiveInput(int slot, string payload)
        {
            if (Finished || string.IsNullOrEmpty(payload)) return;
            Command command;
            try { command = JsonUtility.FromJson<Command>(payload); }
            catch (ArgumentException) { return; }
            if (command == null) return;
            if (command.action == "pause" || command.action == "resume")
            {
                local.ApplyNetworkPause(command.action == "pause", false);
                return;
            }
            if (!CanUseTurn(slot) || command.round != mode.Turns.TurnNumber) return;
            if (slot == MonsterSlot)
            {
                if (command.a >= -1 && command.a <= 1 && command.b >= -1 && command.b <= 1)
                    mode.Monster.ReceiveOnlineInput(command.action, new Vector2Int(command.a, command.b));
                return;
            }
            var player = local.Participants[SurvivorIndex(slot)];
            var puzzle = Tasks.Puzzle;
            if (puzzle != null)
            {
                if (Tasks.Actor != player || command.revision != Tasks.Revision) return;
                var type = puzzle.Definition.type;
                switch (command.action)
                {
                    case "close": Tasks.CloseOnlineAttempt(); break;
                    case "connect" when type == SurvivalPuzzleType.MatchWires || type == SurvivalPuzzleType.SortSupplies || type == SurvivalPuzzleType.Batteries:
                        puzzle.Connect(command.a, command.b); break;
                    case "sequence" when type == SurvivalPuzzleType.ButtonSequence: puzzle.PressSequence(command.a); break;
                    case "code" when type == SurvivalPuzzleType.EnterCode: puzzle.SubmitCode(command.value); break;
                    case "swap" when type == SurvivalPuzzleType.ArrangeNumbers: puzzle.SwapNumbers(command.a, command.b); break;
                    case "switch" when type == SurvivalPuzzleType.PowerSwitches || type == SurvivalPuzzleType.Breaker:
                        puzzle.FlipSwitch(command.a); break;
                    case "rotate" when type == SurvivalPuzzleType.Circuit: puzzle.RotateCircuit(command.a); break;
                }
                return;
            }
            if (player.IsMoving || player.IsRollAnimating || player.IsInMenu()) return;
            switch (command.action)
            {
                case "roll": player.RollDice(); break;
                case "interact": Tasks.StartAttempt(player); break;
                case "end": if (mode.Turns.ShowEndTurnButton) mode.Turns.AdvanceTurn(); break;
                case "move":
                    if (command.a >= 0 && command.b >= 0 && command.a < Map.Width && command.b < Map.Height)
                        player.MoveToGridPosition(new Vector2Int(command.a, command.b));
                    break;
            }
        }

        // Read the same Input Actions that the local scene uses.
        public override void SendLocalInput(LanMultiplayerSession session)
        {
            if (!CanUseTurn(session.LocalPlayerIndex) || Tasks.Puzzle != null) return;
            if (session.LocalPlayerIndex == MonsterSlot)
            {
                mode.Monster.SendOnlineInput((action, direction) => Send(action, direction.x, direction.y));
                return;
            }
            var bindings = LocalMultiplayerSession.InputBindings;
            if (bindings == null) return;
            if (bindings.RollDice?.WasPressedThisFrame() == true) Send("roll");
            if (bindings.Interact?.WasPressedThisFrame() == true) Send("interact");
            if (bindings.Move?.WasPressedThisFrame() == true)
            {
                Vector2 direction = bindings.Move.ReadValue<Vector2>();
                Vector2Int step = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                    ? new Vector2Int(Math.Sign(direction.x), 0) : new Vector2Int(0, Math.Sign(direction.y));
                Vector2Int target = local.Participants[SurvivorIndex(session.LocalPlayerIndex)].GridPosition + step;
                if (step != Vector2Int.zero) Send("move", target.x, target.y);
            }
            if (bindings.Click?.WasPressedThisFrame() == true && bindings.Point != null && Camera.main != null)
            {
                Vector2 screen = bindings.Point.ReadValue<Vector2>();
                if (!UiPointer.IsOverUI(screen))
                {
                    var cell = Map.WorldToGrid(Camera.main.ScreenToWorldPoint(screen));
                    Send("move", cell.x, cell.y);
                }
            }
        }

        public override void SubmitTurnAction(LanMultiplayerSession session, string action) => Send(action);

        // Send a Survival command through the shared room with the current turn information.
        private void Send(string action, int a = 0, int b = 0, string value = null) =>
            transport.SubmitModeInput(JsonUtility.ToJson(new Command
            {
                action = action, a = a, b = b, value = value, round = mode.Turns.TurnNumber, revision = Tasks.Revision
            }));

        // Keep the connection failure separate from normal Survival win and loss rules.
        public override void ConnectionLost(string reason)
        {
            connectionError = reason;
            if (Started) local.ApplyNetworkPause(true, false);
        }

        // Survivors win by escaping. The monster player wins when nobody escaped, the same rule the local game saves.
        public override bool TryGetSlotResult(int slot, out Escape4Now.Accounts.MatchStat stat)
        {
            stat = default;
            var players = local.Participants;
            if (connectionError != null || !Started || !mode.TryGetResult(players, out _)) return false;
            int count = players.Count + (MonsterSlot >= 0 ? 1 : 0);
            bool anyoneEscaped = players.Any(p => mode.OutcomeOf(p) == SurvivorOutcome.Escaped);
            if (slot == MonsterSlot)
            {
                float time = !anyoneEscaped && mode.Timer != null ? mode.Timer.Elapsed : 0f;
                stat = new Escape4Now.Accounts.MatchStat(0, Escape4Now.Accounts.AccountStats.MonsterRole, !anyoneEscaped, count, time, true);
                return true;
            }
            int survivor = SurvivorIndex(slot);
            if (survivor < 0 || survivor >= players.Count) return false;
            bool escaped = mode.OutcomeOf(players[survivor]) == SurvivorOutcome.Escaped;
            float seconds = escaped ? mode.EscapeTimeOf(players[survivor]) : 0f;
            stat = new Escape4Now.Accounts.MatchStat(0, Escape4Now.Accounts.AccountStats.Survivor, escaped, count, seconds, true);
            return true;
        }
    }
}
