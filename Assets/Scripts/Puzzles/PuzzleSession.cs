// Stores one short puzzle attempt. Closing an unfinished attempt discards that attempt's partial progress.

using System;
using System.Collections.Generic;
using Escape4Now.Map;

namespace Escape4Now.Puzzles
{
    //Transient attempt state. Only the station's completed flag survives a closed attempt.
    public sealed class PuzzleSession
    {
        public SurvivalPuzzleDefinition Definition { get; }
        public int[] Left { get; }
        public int[] Right { get; }
        public bool[] Matched { get; }
        public int[] Values { get; }
        public int[] Ports { get; }

        private readonly int[] solution;
        public bool[] Switches { get; }
        public bool[] PowerSwitchPositions { get; }
        public bool[] Targets { get; }
        public int[] Sequence { get; }
        public string Code { get; }
        public int StartCell { get; }
        public int EndCell { get; }
        public int SequenceProgress { get; private set; }
        public float Elapsed { get; private set; }
        public bool Solved { get; private set; }
        public bool Failed { get; private set; }
        public bool TimedOut { get; private set; }
        public float SecondsRemaining => Math.Max(0, Definition.timeLimitSeconds - Elapsed);
        public bool SequenceReady => Elapsed >= Sequence.Length * (Definition.sequenceFlashSeconds + Definition.sequenceGapSeconds);

        public int FlashButton
        {
            get
            {
                if (SequenceReady)
                    return -1;
                float span = Definition.sequenceFlashSeconds + Definition.sequenceGapSeconds;
                int step = (int)(Elapsed / span);
                return Elapsed % span < Definition.sequenceFlashSeconds ? Sequence[step] : -1;
            }
        }

        // Use the supplied seed to make a new attempt. The completed station state lives outside this class.
        public PuzzleSession(SurvivalPuzzleDefinition definition, int seed)
        {
            Definition = definition;
            var random = new Random(seed);
            int count = definition.Count;
            Left = Range(count);
            Right = Range(count);
            Values = Range(count);
            var palette = Range(definition.colors.Length);
            SurvivalObjectiveLayout.Shuffle(palette, random);
            for (int i = 0; i < count; i++)
                Left[i] = Right[i] = palette[i];
            Matched = new bool[count];
            SurvivalObjectiveLayout.Shuffle(Left, random);
            SurvivalObjectiveLayout.Shuffle(Right, random);
            //Keep matching layouts shuffled even when both random orders happen to be identical.
            bool aligned = true;
            for (int i = 0; i < count; i++) if (Left[i] != Right[i]) aligned = false;
            if (aligned && count > 1) SwapRaw(Right,0,1);
            SurvivalObjectiveLayout.Shuffle(Values, random);
            if (Sorted(Values))
                SwapRaw(Values, 0, 1);
            Switches = new bool[count];
            PowerSwitchPositions = new bool[count];
            Targets = new bool[count];
            for (int i = 0; i < count; i++)
                Targets[i] = random.Next(2) == 1;
            Targets[random.Next(count)] = true;
            if (definition.type == SurvivalPuzzleType.PowerSwitches)
            {
                for (int i = 0; i < count; i++)
                    Switches[i] = true;
                for (int i = 0; i < count; i++)
                    if (random.Next(2) == 1)
                        FlipPower(i);
                if (AllOn())
                    FlipPower(0);
            }

            Sequence = new int[definition.sequenceLength];
            for (int i = 0; i < Sequence.Length; i++)
                Sequence[i] = random.Next(count);
            for (int i = 0; i < definition.codeLength; i++)
                Code += random.Next(10).ToString();
            int size = definition.circuitSize;
            Ports = new int[size * size];
            solution = new int[Ports.Length];
            int startRow = random.Next(size), endRow = random.Next(size), bend = random.Next(size);
            StartCell = startRow * size;
            EndCell = endRow * size + size - 1;
            // Build a real circuit solution first, then rotate its pieces so every generated circuit is solvable.
            var route = new List<int>
            {
                StartCell
            };
            for (int x = 1; x <= bend; x++)
                route.Add(startRow * size + x);
            for (int y = startRow; y != endRow;)
            {
                y += endRow > y ? 1 : -1;
                route.Add(y * size + bend);
            }

            for (int x = bend + 1; x < size; x++)
                route.Add(endRow * size + x);
            for (int i = 0; i < route.Count; i++)
            {
                int cell = route[i];
                int previous = i == 0 ? 8 : Direction(cell, route[i - 1], size);
                int next = i == route.Count - 1 ? 2 : Direction(cell, route[i + 1], size);
                solution[cell] = previous | next;
            }

            for (int i = 0; i < Ports.Length; i++)
            {
                if (solution[i] == 0)
                    solution[i] = random.Next(2) == 0 ? 5 : 3;
                Ports[i] = solution[i];
                for (int turn = random.Next(4); turn > 0; turn--)
                    Ports[i] = Rotate(Ports[i]);
            }

            // Do not open a circuit puzzle with its starting arrangement already solved.
            while (CircuitConnected())
                Ports[StartCell] = Rotate(Ports[StartCell]);
        }

        private static int[] Range(int count)
        {
            var values = new int[count];
            for (int i = 0; i < count; i++)
                values[i] = i;
            return values;
        }

        private static bool Sorted(int[] values)
        {
            for (int i = 0; i < values.Length; i++)
                if (values[i] != i)
                    return false;
            return true;
        }

        private static void SwapRaw(int[] values, int a, int b)
        {
            int saved = values[a];
            values[a] = values[b];
            values[b] = saved;
        }

        // Advance the sequence preview using time supplied by the task controller; paused frames supply no time.
        public void Tick(float seconds)
        {
            if (Solved || Failed) return;
            Elapsed += Math.Max(0f, seconds);
            if (SecondsRemaining <= 0) { TimedOut = true; Failed = true; }
        }

        // Match the selected pair by its saved key, not by its current screen position.
        public void Connect(int left, int right)
        {
            if (Solved || Failed || left < 0 || right < 0 || left >= Left.Length || right >= Right.Length || Matched[left])
                return;
            if (Left[left] != Right[right])
            {
                Failed = true;
                return;
            }

            Matched[left] = true;
            Solved = Array.TrueForAll(Matched, value => value);
        }

        // Accept sequence input only after the preview has finished, and fail a wrong choice.
        public void PressSequence(int index)
        {
            if (Solved || Failed || !SequenceReady)
                return;
            if (index != Sequence[SequenceProgress])
            {
                Failed = true;
                return;
            }

            Solved = ++SequenceProgress == Sequence.Length;
        }

        public void SubmitCode(string value)
        {
            if (!Solved && !Failed)
            {
                Solved = value == Code;
                Failed = !Solved;
            }
        }

        // Swap the chosen positions and check the full order after each move.
        public void SwapNumbers(int a, int b)
        {
            if (Solved || Failed || a < 0 || b < 0 || a >= Values.Length || b >= Values.Length)
                return;
            SwapRaw(Values, a, b);
            Solved = Sorted(Values);
        }

        private bool AllOn() => Array.TrueForAll(Switches, value => value);
        // This switch changes itself and its neighboring lights, which makes Power Switches different from Breaker.
        private void FlipPower(int index)
        {
            Switches[index] = !Switches[index];
            int next = (index + 1) % Switches.Length;
            Switches[next] = !Switches[next];
        }

        public void FlipSwitch(int index)
        {
            if (Solved || Failed || index < 0 || index >= Switches.Length)
                return;
            if (Definition.type == SurvivalPuzzleType.PowerSwitches)
            {
                PowerSwitchPositions[index] = !PowerSwitchPositions[index];
                FlipPower(index);
                Solved = AllOn();
            }
            else
            {
                Switches[index] = !Switches[index];
                Solved = true;
                for (int i = 0; i < Switches.Length; i++)
                    if (Switches[i] != Targets[i])
                        Solved = false;
            }
        }

        // Rotate the selected piece's connection bits, then test whether power reaches the output.
        public void RotateCircuit(int cell)
        {
            if (Solved || Failed || cell < 0 || cell >= Ports.Length)
                return;
            Ports[cell] = Rotate(Ports[cell]);
            Solved = CircuitConnected();
        }

        private static int Rotate(int ports) => ((ports << 1) & 15) | (ports >> 3);
        private static int Direction(int from, int to, int size) => to == from + size ? 4 : to == from + 1 ? 2 : to == from - size ? 1 : 8;
        // Follow only connections that meet on both neighboring pieces; merely touching pieces are not connected.
        private bool CircuitConnected()
        {
            return (Ports[EndCell] & 2) != 0 && PoweredCircuitDistances()[EndCell] >= 0;
        }

        //The same search supplies both the solved check and the UI's traveling power animation.
        public int[] PoweredCircuitDistances()
        {
            var distances = new int[Ports.Length];
            for (int i = 0; i < distances.Length; i++) distances[i] = -1;
            if ((Ports[StartCell] & 8) == 0) return distances;
            int size = Definition.circuitSize;
            var visited = new HashSet<int>
            {
                StartCell
            };
            var queue = new Queue<int>();
            queue.Enqueue(StartCell);
            distances[StartCell] = 0;
            int[] dx =
            {
                0,
                1,
                0,
                -1
            }, dy =
            {
                -1,
                0,
                1,
                0
            }, bit =
            {
                1,
                2,
                4,
                8
            };
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int x = cell % size + dx[d], y = cell / size + dy[d];
                    if (x < 0 || y < 0 || x >= size || y >= size)
                        continue;
                    int next = y * size + x;
                    if ((Ports[cell] & bit[d]) != 0 && (Ports[next] & bit[(d + 2) % 4]) != 0 && visited.Add(next))
                    {
                        distances[next] = distances[cell] + 1;
                        queue.Enqueue(next);
                    }
                }
            }

            return distances;
        }
    }
}
