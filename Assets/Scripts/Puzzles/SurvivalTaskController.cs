// Opens shared task puzzles, saves completed puzzle types and ends the survivor's turn after the attempt.

using System.Collections.Generic;
using Escape4Now.GameModes;
using Escape4Now.Map;
using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.Puzzles
{
    public sealed class SurvivalTaskController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField]
        private SurvivalModeController mode;
        [SerializeField]
        private SurvivalMap map;
        [Header("Feedback")]
        [SerializeField]
        private string completedMessage = "Puzzle completed for the team.";
        [SerializeField]
        private string incompleteMessage = "Puzzle incomplete. It can be attempted on a later turn.";
        [SerializeField] private string failedMessage = "Puzzle failed. Try again on a later turn.";
        [Header("Task Labels")]
        [SerializeField, InspectorName("Show Task Debug Labels")] private bool showTaskNames = true;
        [SerializeField] private bool showNearbyTaskNames = true;
        [SerializeField, Min(0)] private int taskNameDistance = 2;
        [SerializeField, Tooltip("{0} is the puzzle's short Map Label, or its full title when no short label is assigned.")] private string taskNameFormat = "{0}";
        public bool TaskNamesVisible => showTaskNames;
        private readonly List<SurvivalWorldMarker> stationMarkers = new List<SurvivalWorldMarker>();
        private readonly List<SurvivalWorldMarker> exitMarkers = new List<SurvivalWorldMarker>();
        private System.Random random;
        public PuzzleSession Puzzle { get; private set; }
        public PlayerCharacter Actor { get; private set; }
        public SurvivalTaskPlacement CurrentTask { get; private set; }
        public int Revision { get; private set; }

        // Create station and gate visuals from the objective layout; this does not choose new positions.
        public void BeginMatch()
        {
            random = new System.Random(unchecked(map.LayoutSeed ^ 0x72D35));
            foreach (var task in map.Objectives.Tasks)
            {
                var marker = Instantiate(task.Definition.stationPrefab, transform);
                marker.Configure(map, task.Cell, string.Format(taskNameFormat, task.Definition.MapLabel));
                marker.SetCaptionVisible(showTaskNames);
                stationMarkers.Add(marker);
            }

            foreach (var exit in map.Objectives.Exits)
            {
                var marker = Instantiate(mode.Rules.exitPrefab, transform);
                var boundary = SurvivalObjectiveLayout.ExitBoundary(exit, map.Width, map.Height);
                marker.Configure(map, boundary, mode.Rules.exitLabel, true);
                map.GetComponent<Escape4Now.Obstacles.SurvivalObstacleSpawner>().ReplaceFenceWithExit(boundary);
                if (marker.TryGetComponent<SurvivalExitGate>(out var gate)) gate.Configure(map, exit);
                exitMarkers.Add(marker);
            }
        }

        // Advance the sequence preview while solving, and read the current survivor's configured Interact action.
        private void Update()
        {
            if (map.Objectives != null) RefreshStationMarkers();
            if (Puzzle != null && !Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused)
                Puzzle.Tick(Time.deltaTime);
            if (Puzzle != null || !Escape4Now.Multiplayer.LocalMultiplayerSession.MatchIsRunning || Escape4Now.Multiplayer.LocalMultiplayerSession.MatchHasEnded)
                return;
            foreach (var player in mode.ActiveSurvivors)
                if (player.CanReadTurnInput && player.InputBindings.Interact.WasPressedThisFrame())
                    TryInteract(player);
        }

        public void SetTaskNamesVisible(bool visible)
        {
            showTaskNames = visible;
            RefreshStationMarkers();
        }

        // Only the station's reserved interaction cells allow starting its puzzle.
        public SurvivalTaskPlacement NearbyTask(PlayerCharacter player)
        {
            if (!mode.IsActiveSurvivor(player) || map.Objectives == null || mode.ExitsUnlocked)
                return null;
            foreach (var task in map.Objectives.Tasks)
                if (!task.Completed)
                    foreach (var cell in task.InteractionCells)
                        if (player.GridPosition == cell)
                            return task;
            return null;
        }

        public bool CanInteract(PlayerCharacter player) => NearbyTask(player) != null;
        // Open one attempt and lock normal player controls while the puzzle UI is open.
        public bool TryInteract(PlayerCharacter player)
        {
            var task = NearbyTask(player);
            if (Puzzle != null || task == null || !player.CanReadTurnInput || player.IsMoving || player.IsRollAnimating || player.IsUsingItem() || player.IsInMenu() || mode.Timer.HasExpired)
                return false;
            Actor = player;
            CurrentTask = task;
            Puzzle = new PuzzleSession(task.Definition, random.Next());
            Revision++;
            player.SetInMenu(true);
            return true;
        }

        public void ClosePuzzle()
        {
            if (Puzzle == null || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused)
                return;
            FinishAttempt(Puzzle.Solved, true);
        }

        public void FinishSolvedPuzzle()
        {
            if (Puzzle != null && Puzzle.Solved)
                FinishAttempt(true, true);
        }

        // Save only a successful completion; closing or failing keeps the station available for another turn.
        private void FinishAttempt(bool solved, bool endTurn)
        {
            var player = Actor;
            if (solved)
                mode.CompleteTask(CurrentTask, player);
            if (player != null)
                mode.Events.ShowMessage(player, solved ? completedMessage : Puzzle != null && Puzzle.Failed ? failedMessage : incompleteMessage);
            Puzzle = null;
            Actor = null;
            CurrentTask = null;
            Revision++;
            if (player != null)
                player.SetInMenu(false);
            if (endTurn && player != null && mode.IsActiveSurvivor(player) && !mode.Timer.HasExpired)
                mode.Turns.EndTurnAfterBattleLoss(player);
        }

        // Close an eliminated player's attempt without ending somebody else's turn.
        public void CancelFor(PlayerCharacter player)
        {
            if (Actor == player)
                FinishAttempt(false, false);
        }

        public void RefreshMarkers()
        {
            RefreshStationMarkers();
            foreach (var exit in exitMarkers)
            {
                exit.SetState(mode.ExitsUnlocked, !mode.ExitsUnlocked);
                if (exit.TryGetComponent<SurvivalExitGate>(out var gate)) gate.SetUnlocked(mode.ExitsUnlocked);
            }
        }

        // Keep debug names optional, while allowing nearby unfinished tasks to identify themselves.
        public bool ShouldShowTaskName(int index)
        {
            if (map.Objectives == null || index < 0 || index >= map.Objectives.Tasks.Count) return false;
            var task = map.Objectives.Tasks[index];
            if (task.Completed) return false;
            if (showTaskNames || CurrentTask == task) return true;
            var player = mode.ActiveTurnParticipant;
            return showNearbyTaskNames && mode.IsActiveSurvivor(player)
                && SurvivalObjectiveLayout.Distance(player.GridPosition, task.Cell) <= taskNameDistance;
        }

        // Update completion colors and highlight only the task the active survivor can currently use.
        private void RefreshStationMarkers()
        {
            var activePlayer = mode.ActiveTurnParticipant;
            var nearby = activePlayer != null && activePlayer.CanReadTurnInput ? NearbyTask(activePlayer) : null;
            for (int i = 0; i < stationMarkers.Count; i++)
            {
                if (stationMarkers[i].TryGetComponent<SurvivalTaskBeacon>(out var beacon))
                    beacon.SetAvailable(Puzzle == null && nearby == map.Objectives.Tasks[i]);
                bool complete = map.Objectives.Tasks[i].Completed;
                stationMarkers[i].SetState(complete);
                stationMarkers[i].SetCaptionVisible(ShouldShowTaskName(i));
            }
        }
    }
}
