
using System.Collections;
using System.Collections.Generic;
using Escape4Now.GameModes;
using Escape4Now.Map;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using Escape4Now.Puzzles;
using UnityEngine;

namespace Escape4Now.Bots
{
    public sealed class SurvivalBotController : MonoBehaviour
    {
        private PlayerCharacter player;
        private IPlayerGridMap map;
        private TurnSystemController turnSystem;
        private Coroutine turnRoutine;
        private SurvivalMap survivalMap;
        private SurvivalTaskController taskController;
        private MonsterCharacter monster;

        // Find the player's existing movement and turn systems.
        private void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            turnSystem = FindFirstObjectByType<TurnSystemController>();
            survivalMap = FindFirstObjectByType<SurvivalMap>();
            taskController = FindFirstObjectByType<SurvivalTaskController>();
            monster = FindFirstObjectByType<MonsterCharacter>();
        }

        // Start a bot turn only when Survival Mode is active.
        private void Update()
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.Active)
                return;

            if (player == null || !(player.Mode is SurvivalModeController))
                return;

            if (!player.IsBotControlled || !player.CanReadTurnInput)
                return;

            if (Escape4Now.Multiplayer.LocalMultiplayerSession.MatchHasEnded)
                return;

            if (turnRoutine != null || !player.IsCurrentTurn
                || player.HasReachedExit || player.Health <= 0)
                return;

            map = player.GridMap;

            if (map != null)
                turnRoutine = StartCoroutine(TakeTurn());
        }

        // Roll, move to a reachable tile, and finish the turn.
        private IEnumerator TakeTurn()
        {
            yield return null;

            // Solve a nearby puzzle before rolling the dice.
            if (taskController != null && taskController.CanInteract(player))
            {
                yield return StartCoroutine(SolvePuzzle());
                turnRoutine = null;
                yield break;
            }

            if (!player.HasRolled)
                player.RollDice();

            while (player.IsRollAnimating)
                yield return null;

            while (player.IsCurrentTurn && player.MovesRemaining > 0)
            {
                if (player.IsMoving)
                {
                    yield return null;
                    continue;
                }

                // Head toward an exit once enough puzzles are completed.
                SurvivalModeController mode = player.Mode as SurvivalModeController;

                List<Vector2Int> path = mode != null && mode.ExitsUnlocked
                    ? FindExit()
                    : FindDestination();

                // Check whether that route moves closer to the Monster.
                if (IsMonsterNearby() && path != null && path.Count > 0)
                {
                    int currentDistance = DistanceToMonster(player.GridPosition);
                    int nextDistance = DistanceToMonster(path[0]);

                    // Only escape if the puzzle route approaches the Monster.
                    if (nextDistance < currentDistance)
                    {
                        List<Vector2Int> escapePath = FindEscapePath();

                        if (escapePath != null)
                            path = escapePath;
                    }
                }
                else if (IsMonsterNearby())
                {
                    // No puzzle route available, so try escaping.
                    path = FindEscapePath();
                }

                if (path == null || path.Count == 0)
                    break;

                if (!player.MoveAlongBotPath(path))
                    break;

                while (player.IsMoving)
                    yield return null;

                // Stop moving when a puzzle station is reached.
                if (taskController != null && taskController.CanInteract(player))
                {
                    yield return StartCoroutine(SolvePuzzle());
                    turnRoutine = null;
                    yield break;
                }

                yield return null;
            }

            // Finish the turn if the movement system hasn't done so.
            if (player.IsCurrentTurn && !player.IsMoving
                && !player.IsInMenu() && !player.IsUsingItem())
            {
                if (turnSystem != null)
                    turnSystem.AdvanceTurn();
            }

            turnRoutine = null;
        }

        // Calculate distance from a tile to the Monster's 2x2 footprint.
        private int DistanceToMonster(Vector2Int cell)
        {
            if (monster == null)
                return int.MaxValue;

            Vector2Int position = monster.GridPosition;

            int nearestX = Mathf.Clamp(cell.x, position.x, position.x + 1);
            int nearestY = Mathf.Clamp(cell.y, position.y, position.y + 1);

            return Mathf.Abs(cell.x - nearestX)
                 + Mathf.Abs(cell.y - nearestY);
        }

        // Check whether the Monster is within three tiles of the bot.
        private bool IsMonsterNearby()
        {
            return DistanceToMonster(player.GridPosition) <= 3;
        }

        // Open and solve supported puzzles using their existing rules.
        private IEnumerator SolvePuzzle()
        {
            if (taskController == null || !taskController.TryInteract(player))
                yield break;

            PuzzleSession puzzle = taskController.Puzzle;

            // Give the bot a short delay before solving.
            yield return new WaitForSeconds(1.5f);

            if (puzzle == null || taskController.Puzzle != puzzle
                || taskController.Actor != player)
                yield break;

            switch (puzzle.Definition.type)
            {
                case SurvivalPuzzleType.EnterCode:
                    puzzle.SubmitCode(puzzle.Code);
                    break;

                case SurvivalPuzzleType.MatchWires:
                case SurvivalPuzzleType.Batteries:

                    // Match each left symbol with the same right symbol.
                    for (int left = 0; left < puzzle.Left.Length; left++)
                    {
                        for (int right = 0; right < puzzle.Right.Length; right++)
                        {
                            if (puzzle.Left[left] == puzzle.Right[right])
                            {
                                puzzle.Connect(left, right);
                                break;
                            }
                        }
                    }
                    break;

                case SurvivalPuzzleType.SortSupplies:
                case SurvivalPuzzleType.ArrangeNumbers:

                    // Put each number in its correct position.
                    for (int i = 0; i < puzzle.Values.Length; i++)
                    {
                        if (puzzle.Values[i] == i)
                            continue;

                        // Find the position containing the correct number.
                        for (int j = i + 1; j < puzzle.Values.Length; j++)
                        {
                            if (puzzle.Values[j] == i)
                            {
                                puzzle.SwapNumbers(i, j);
                                break;
                            }
                        }
                    }
                    break;

                case SurvivalPuzzleType.Breaker:

                    // Make every switch match its target state.
                    for (int i = 0; i < puzzle.Switches.Length; i++)
                    {
                        if (puzzle.Switches[i] != puzzle.Targets[i])
                            puzzle.FlipSwitch(i);
                    }
                    break;

                case SurvivalPuzzleType.PowerSwitches:

                    // Try different switch combinations until all lights turn on.
                    int count = puzzle.Switches.Length;
                    int combinations = 1 << count;

                    for (int combination = 0; combination < combinations; combination++)
                    {
                        bool valid = true;

                        // Check whether this combination turns every light on.
                        for (int i = 0; i < count; i++)
                        {
                            bool current = puzzle.Switches[i];

                            bool flipCurrent = (combination & (1 << i)) != 0;
                            bool flipPrevious =
                                (combination & (1 << ((i - 1 + count) % count))) != 0;

                            if (current ^ flipCurrent ^ flipPrevious ^ true)
                            {
                                valid = false;
                                break;
                            }
                        }

                        if (!valid)
                            continue;

                        // Apply the correct combination using the puzzle's rules.
                        for (int i = 0; i < count; i++)
                        {
                            if ((combination & (1 << i)) != 0)
                                puzzle.FlipSwitch(i);
                        }

                        break;
                    }
                    break;

                case SurvivalPuzzleType.ButtonSequence:

                    // Wait until the button sequence preview finishes.
                    while (!puzzle.SequenceReady)
                        yield return null;

                    // Press the buttons in the correct order.
                    foreach (int button in puzzle.Sequence)
                        puzzle.PressSequence(button);

                    break;

                case SurvivalPuzzleType.Circuit:

                    // Find a valid circuit arrangement.
                    SolveCircuit(puzzle);
                    break;
            }

            // Complete successful puzzles and close unsupported attempts.
            if (puzzle.Solved)
                taskController.FinishSolvedPuzzle();
            else
                taskController.ClosePuzzle();
        }


        // Solve the circuit by searching for a valid arrangement of rotations.
        private void SolveCircuit(PuzzleSession puzzle)
        {
            int size = puzzle.Definition.circuitSize;
            int[] rotations = new int[puzzle.Ports.Length];

            // Find a valid arrangement before changing the puzzle.
            if (!FindCircuitSolution(puzzle, rotations, 0, size))
                return;

            // Apply the solution using the existing puzzle method.
            for (int i = 0; i < rotations.Length && !puzzle.Solved; i++)
            {
                for (int turn = 0; turn < rotations[i] && !puzzle.Solved; turn++)
                    puzzle.RotateCircuit(i);
            }
        }

        // Try each rotation combination and check whether the circuit connects.
        private bool FindCircuitSolution(
            PuzzleSession puzzle, int[] rotations, int index, int size)
        {
            if (index == rotations.Length)
                return CircuitPathExists(puzzle.Ports, rotations,
                    puzzle.StartCell, puzzle.EndCell, size);

            for (int turn = 0; turn < 4; turn++)
            {
                rotations[index] = turn;

                if (FindCircuitSolution(puzzle, rotations, index + 1, size))
                    return true;
            }

            return false;
        }

        // Check whether a proposed circuit arrangement connects start to end.
        private bool CircuitPathExists(
            int[] ports, int[] rotations, int start, int end, int size)
        {
            int[] arranged = new int[ports.Length];

            for (int i = 0; i < ports.Length; i++)
            {
                arranged[i] = ports[i];

                for (int turn = 0; turn < rotations[i]; turn++)
                    arranged[i] = ((arranged[i] << 1) & 15) | (arranged[i] >> 3);
            }

            if ((arranged[start] & 8) == 0 || (arranged[end] & 2) == 0)
                return false;

            Queue<int> queue = new Queue<int>();
            HashSet<int> visited = new HashSet<int>();

            queue.Enqueue(start);
            visited.Add(start);

            int[] dx = { 0, 1, 0, -1 };
            int[] dy = { -1, 0, 1, 0 };
            int[] bits = { 1, 2, 4, 8 };

            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();

                if (cell == end)
                    return true;

                int x = cell % size;
                int y = cell / size;

                for (int direction = 0; direction < 4; direction++)
                {
                    int nextX = x + dx[direction];
                    int nextY = y + dy[direction];

                    if (nextX < 0 || nextY < 0 ||
                        nextX >= size || nextY >= size)
                        continue;

                    int next = nextY * size + nextX;
                    int opposite = (direction + 2) % 4;

                    if ((arranged[cell] & bits[direction]) == 0 ||
                        (arranged[next] & bits[opposite]) == 0)
                        continue;

                    if (visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            return false;
        }

        // Find a path that avoids the Monster's danger area.
        private bool TryFindSafePath(
            Vector2Int start,
            Vector2Int target,
            out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();

            if (DistanceToMonster(target) <= 3)
                return false;

            var costs = new Dictionary<Vector2Int, int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            var open = new List<Vector2Int> { start };
            var visited = new HashSet<Vector2Int>();

            costs[start] = 0;

            while (open.Count > 0)
            {
                Vector2Int current = open[0];

                foreach (Vector2Int cell in open)
                {
                    if (costs[cell] < costs[current])
                        current = cell;
                }

                open.Remove(current);

                if (!visited.Add(current))
                    continue;

                if (current == target)
                {
                    while (current != start)
                    {
                        path.Insert(0, current);
                        current = previous[current];
                    }

                    return true;
                }

                Vector2Int[] directions =
                {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

                foreach (Vector2Int direction in directions)
                {
                    Vector2Int next = current + direction;

                    if (!map.IsWalkable(next) ||
                        DistanceToMonster(next) == 0 ||
                        visited.Contains(next))
                        continue;

                    int newCost = costs[current] + player.Mode.MovementCost(next);

                    if (!costs.ContainsKey(next) || newCost < costs[next])
                    {
                        costs[next] = newCost;
                        previous[next] = current;

                        if (!open.Contains(next))
                            open.Add(next);
                    }
                }
            }

            return false;
        }

        // Find a safe direction away from the Monster.
        private List<Vector2Int> FindEscapePath()
        {
            if (monster == null || DistanceToMonster(player.GridPosition) > 3)
                return null;

            Vector2Int current = player.GridPosition;
            Vector2Int bestCell = current;
            int bestDistance = DistanceToMonster(current);

            Vector2Int[] directions =
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;

                if (!map.IsWalkable(next) || map.IsExit(next))
                    continue;

                int cost = player.Mode.MovementCost(next);

                if (cost > player.MovesRemaining)
                    continue;

                int distance = DistanceToMonster(next);

                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestCell = next;
                }
            }

            if (bestCell == current)
                return null;

            return new List<Vector2Int> { bestCell };
        }

        // Find the safest reachable exit once enough puzzles are completed.
        private List<Vector2Int> FindExit()
        {
            if (survivalMap == null || survivalMap.Objectives == null)
                return null;

            List<Vector2Int> bestPath = null;
            int shortestCost = int.MaxValue;

            foreach (Vector2Int exit in survivalMap.Objectives.Exits)
            {
                if (!map.IsWalkable(exit))
                    continue;

                if (!TryFindSafePath(player.GridPosition, exit,
                    out List<Vector2Int> path))
                    continue;

                int cost = 0;

                foreach (Vector2Int step in path)
                    cost += player.Mode.MovementCost(step);

                if (cost < shortestCost)
                {
                    shortestCost = cost;
                    bestPath = path;
                }
            }

            if (bestPath == null || bestPath.Count == 0)
                return null;

            int remaining = player.MovesRemaining;
            List<Vector2Int> affordablePath = new List<Vector2Int>();

            foreach (Vector2Int step in bestPath)
            {
                int cost = player.Mode.MovementCost(step);

                if (cost > remaining || DistanceToMonster(step) == 0)
                    break;

                remaining -= cost;
                affordablePath.Add(step);
            }

            return affordablePath.Count > 0 ? affordablePath : null;
        }

        // Finds the closest unfinished puzzle and moves toward its interaction tile.
        // Find a safe route to the closest unfinished puzzle.
        private List<Vector2Int> FindDestination()
        {
            if (survivalMap == null || survivalMap.Objectives == null)
                return null;

            foreach (var task in survivalMap.Objectives.Tasks)
            {
                if (task.Completed)
                    continue;

                foreach (Vector2Int cell in task.InteractionCells)
                {
                    if (player.GridPosition == cell)
                        return null;
                }
            }

            List<Vector2Int> bestPath = null;
            int shortestCost = int.MaxValue;

            foreach (var task in survivalMap.Objectives.Tasks)
            {
                if (task.Completed)
                    continue;

                foreach (Vector2Int cell in task.InteractionCells)
                {
                    if (!map.IsWalkable(cell))
                        continue;

                    if (!TryFindSafePath(player.GridPosition, cell,
                        out List<Vector2Int> path))
                        continue;

                    int cost = 0;

                    foreach (Vector2Int step in path)
                        cost += player.Mode.MovementCost(step);

                    if (cost < shortestCost)
                    {
                        shortestCost = cost;
                        bestPath = path;
                    }
                }
            }

            if (bestPath == null || bestPath.Count == 0)
                return null;

            // Keep only the steps the bot can afford this turn.
            int remaining = player.MovesRemaining;
            List<Vector2Int> affordablePath = new List<Vector2Int>();

            foreach (Vector2Int step in bestPath)
            {
                int cost = player.Mode.MovementCost(step);

                if (cost > remaining || DistanceToMonster(step) == 0)
                    break;

                remaining -= cost;
                affordablePath.Add(step);
            }

            return affordablePath.Count > 0 ? affordablePath : null;
        }

    }
}
