// Displays the current puzzle and sends UI choices to its puzzle session; completion is handled by the task controller.

using Escape4Now.GameModes;
using Escape4Now.Multiplayer;
using Escape4Now.Puzzles;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class SurvivalPuzzleUI : MonoBehaviour
    {
        [Header("Survival Scene References")]
        [SerializeField]
        private SurvivalModeController mode;
        [SerializeField]
        private MatchUIContext context;
        [Header("Authored Unity UI")]
        [SerializeField]
        private GameObject panel;
        [SerializeField]
        private Text title;
        [SerializeField]
        private Text instruction;
        [SerializeField]
        private Text clue;
        [SerializeField]
        private Text status;
        [SerializeField]
        private GameObject pairs;
        [SerializeField]
        private GameObject pieces;
        [SerializeField]
        private GameObject codePanel;
        [SerializeField]
        private PuzzleTokenUI[] left;
        [SerializeField]
        private PuzzleTokenUI[] right;
        [SerializeField]
        private PuzzleTokenUI[] cells;
        [SerializeField]
        private RectTransform[] connections = new RectTransform[0];
        [SerializeField]
        private GridLayoutGroup cellGrid;
        [SerializeField]
        private InputField codeInput;
        [SerializeField]
        private Button interact;
        [SerializeField]
        private Text interactLabel;
        [SerializeField]
        private Button close;
        [Header("Interaction Presentation")]
        [SerializeField] private SurvivalMatchingUI matching;
        [SerializeField] private SurvivalPuzzleFeedback feedback;
        [SerializeField] private SurvivalPuzzleBoardView board;
        [SerializeField] private SurvivalCircuitFlowView circuitFlow;
        [SerializeField] private SurvivalNumberRowView numberRow;
        [SerializeField] private SurvivalPowerSwitchBoard powerSwitchBoard;
        [SerializeField] private Vector2 powerSwitchCellSize = new Vector2(100,70);
        [Header("Inspector Presentation")]
        [SerializeField]
        private Color neutral = new Color(.18f, .23f, .27f);
        [SerializeField]
        private Color selected = new Color(.1f, .65f, .85f);
        [SerializeField]
        private Color complete = new Color(.22f, .65f, .4f);
        [SerializeField]
        private Color flash = Color.white;
        [SerializeField]
        private Vector2 circuitCellSize = new Vector2(64, 64);
        [SerializeField]
        private Vector2 tokenCellSize = new Vector2(110, 64);
        [SerializeField] private Vector2 numberCellSize = new Vector2(62, 72);
        [SerializeField, Min(1)] private int tokenColumns = 3;
        [Header("Puzzle Guidance")]
        [SerializeField] private string numberOrderFormat = "ORDER: {0}";
        [SerializeField] private string numberPickText = "Select two tiles to swap, or drag one onto another.";
        [SerializeField] private string numberSelectedFormat = "Selected {0}. Choose another tile to swap.";
        [SerializeField] private string numberProgressFormat = "In place {0}/{1}";
        [SerializeField] private string circuitProgressFormat = "Powered pieces {0}/{1}  |  Output: {2}";
        [SerializeField] private Color circuitWaitingColor = new Color(.35f,.25f,.16f,1);
        [SerializeField] private string matchingProgressFormat = "Matched {0} / {1}";
        [SerializeField] private string lightsProgressFormat = "Green lights {0} / {1}";
        [SerializeField]
        private string interactFormat = "{0}  [{1}]\nInteract ends your turn";
        [SerializeField]
        private string watchText = "Watch the sequence";
        [SerializeField]
        private string repeatFormat = "Repeat: {0}/{1}";
        [SerializeField]
        private string codeFormat = "ACCESS CODE: {0}";
        [SerializeField]
        private string targetFormat = "TARGET: {0}";
        [SerializeField]
        private string onText = "ON";
        [SerializeField]
        private string offText = "OFF";
        [SerializeField, Min(1f)]
        private float wireThickness = 3f;
        [SerializeField]
        private string circuitInputLabel = "IN";
        [SerializeField]
        private string circuitOutputLabel = "OUT";
        private int revision = -1;
        private bool focusPending;
        private int selectedIndex = -1;
        private int openedFrame;
        private int lastSequenceFlash = -1;
        public SurvivalPuzzleFeedback Feedback => feedback;
        public bool Playing => context.Playing && !context.Finished;
        public Escape4Now.Map.SurvivalEventController EventSource => mode.Events;
        public bool IsWireMatching => mode.Tasks.Puzzle != null && mode.Tasks.Puzzle.Definition.type == SurvivalPuzzleType.MatchWires;
        public PuzzleTokenUI DragSource { get; set; }
        private bool CanUsePuzzle => mode.Tasks.Puzzle != null && !mode.Tasks.Puzzle.Solved && !mode.Tasks.Puzzle.Failed && !LocalMultiplayerSession.GameplayPaused;
        public bool CanDrag => CanUsePuzzle && (mode.Tasks.Puzzle.Definition.type == SurvivalPuzzleType.MatchWires
            || mode.Tasks.Puzzle.Definition.type == SurvivalPuzzleType.SortSupplies
            || mode.Tasks.Puzzle.Definition.type == SurvivalPuzzleType.Batteries
            || mode.Tasks.Puzzle.Definition.type == SurvivalPuzzleType.ArrangeNumbers);

        public void ClearSelection() { selectedIndex = -1; matching.ClearSelection(); }
        public bool BeginWireDrag(int side,int index,UnityEngine.EventSystems.PointerEventData data)
            => matching.BeginDrag(side,index,data.position,data.pressEventCamera);
        public void MoveWireDrag(Vector2 screen) => matching.MoveDrag(screen);
        public void EndWireDrag() => matching.EndDrag();
        public void HoverMatch(int side,int index,bool entered) => matching?.Hover(side,index,entered);
        public bool IsMatchingComplete(int side,int index)
        {
            var puzzle = mode.Tasks.Puzzle;
            if (!SurvivalMatchingUI.Supports(puzzle) || side > 1) return false;
            int source = side == 0 ? index : System.Array.IndexOf(puzzle.Left,puzzle.Right[index]);
            return source >= 0 && puzzle.Matched[source];
        }

        // Connect the saved Unity buttons and puzzle pieces to the task controller.
        private void Start()
        {
            matching.Configure(left,right,connections);
            for (int i = 0; i < left.Length; i++)
                left[i].Bind(this, 0, i);
            for (int i = 0; i < right.Length; i++)
                right[i].Bind(this, 1, i);
            for (int i = 0; i < cells.Length; i++)
                cells[i].Bind(this, 2, i);
            interact.onClick.AddListener(() => mode.Tasks.TryInteract(context.ActivePlayer));
            close.onClick.AddListener(mode.Tasks.ClosePuzzle);
        }

        // Show the current attempt, refresh its progress, and keep normal gameplay UI out of the puzzle's way.
        private void LateUpdate()
        {
            bool playable = context.Playing && !context.Finished && !LocalMultiplayerSession.GameplayPaused;
            var attempt = mode.Tasks.Puzzle;
            var task = playable && attempt == null ? mode.Tasks.NearbyTask(context.ActivePlayer) : null;
            interact.gameObject.SetActive(task != null);
            if (task != null)
                interactLabel.text = string.Format(interactFormat, task.Definition.title,
                    context.ActivePlayer.InputBindings.Interact.GetBindingDisplayString());
            if (playable && attempt != null && !panel.activeSelf)
                focusPending = true;
            panel.SetActive(playable && attempt != null);
            if (!panel.activeSelf)
                return;
            feedback.Present(attempt);
            if (attempt.Solved) mode.Tasks.RecordCompletion();
            if (attempt.Solved && feedback.ReadyToClose)
            {
                mode.Tasks.FinishSolvedPuzzle();
                panel.SetActive(false);
                return;
            }

            if (attempt.Failed && feedback.ReadyToClose)
            {
                mode.Tasks.ClosePuzzle();
                panel.SetActive(false);
                return;
            }

            if (revision != mode.Tasks.Revision)
            {
                revision = mode.Tasks.Revision;
                selectedIndex = -1;
                matching.ClearSelection();
                foreach (var token in left) token.Motion.ResetPiece();
                foreach (var token in right) token.Motion.ResetPiece();
                foreach (var token in cells) token.Motion.ResetPiece();
                lastSequenceFlash = -1;
                openedFrame = Time.frameCount;
                codeInput.SetTextWithoutNotify("");
                codeInput.characterLimit = attempt.Definition.codeLength;
            }

            var definition = attempt.Definition;
            title.text = definition.title;
            instruction.text = definition.instruction;
            bool pairing = definition.type == SurvivalPuzzleType.MatchWires || definition.type == SurvivalPuzzleType.SortSupplies || definition.type == SurvivalPuzzleType.Batteries;
            bool code = definition.type == SurvivalPuzzleType.EnterCode;
            pairs.SetActive(pairing);
            pieces.SetActive(!pairing && !code);
            codePanel.SetActive(code);
            clue.text = code ? string.Format(codeFormat, attempt.Code) : "";
            status.text = "";
            if (pairing)
            {
                board.Present(definition.type,attempt.Left.Length,left,right);
                int matched = 0;
                foreach (bool value in attempt.Matched) if (value) matched++;
                status.text = string.Format(matchingProgressFormat, matched, attempt.Matched.Length);
                for (int i = 0; i < left.Length; i++)
                {
                    bool visible = i < attempt.Left.Length;
                    left[i].gameObject.SetActive(visible);
                    right[i].gameObject.SetActive(visible);
                    if (!visible)
                        continue;
                    int a = attempt.Left[i], b = attempt.Right[i];
                    bool rightMatched = false;
                    for (int j = 0; j < attempt.Left.Length; j++)
                        if (attempt.Left[j] == b && attempt.Matched[j])
                            rightMatched = true;
                    left[i].Show(definition.symbols[a], definition.colors[a], !attempt.Matched[i] && !attempt.Failed && !attempt.Solved);
                    right[i].Show(definition.symbols[b], definition.colors[b], !rightMatched && !attempt.Failed && !attempt.Solved);
                    left[i].Motion.SetTheme(definition.type); right[i].Motion.SetTheme(definition.type);
                    left[i].MatchingView.Present(definition,a,false,attempt.Matched[i]);
                    right[i].MatchingView.Present(definition,b,true,rightMatched);
                }
                matching.Refresh(attempt,wireThickness);
                status.text += "\n" + matching.Hint;
            }
            else if (!code)
            {
                bool circuit = definition.type == SurvivalPuzzleType.Circuit;
                int count = circuit ? attempt.Ports.Length : definition.Count;
                bool numbers = definition.type == SurvivalPuzzleType.ArrangeNumbers;
                bool powerSwitches = definition.type == SurvivalPuzzleType.PowerSwitches;
                if(powerSwitchBoard!=null)powerSwitchBoard.gameObject.SetActive(powerSwitches);
                if(circuitFlow != null) circuitFlow.gameObject.SetActive(circuit);
                if(numberRow != null) numberRow.gameObject.SetActive(numbers);
                int columns = Mathf.Clamp(tokenColumns, 1, count);
                //Avoid leaving one control alone on the last row when a balanced grid fits.
                if (columns > 1 && count > columns && count % columns == 1) columns--;
                cellGrid.constraintCount = circuit ? definition.circuitSize : numbers || powerSwitches ? count : columns;
                cellGrid.cellSize = circuit ? circuitCellSize : numbers ? numberCellSize : powerSwitches ? powerSwitchCellSize : tokenCellSize;
                if(powerSwitches)
                    cellGrid.cellSize=new Vector2(Mathf.Min(powerSwitchCellSize.x,(((RectTransform)pieces.transform).rect.width-cellGrid.spacing.x*(count-1))/count),powerSwitchCellSize.y);
                if (numbers)
                {
                    var area = ((RectTransform)pieces.transform).rect;
                    cellGrid.cellSize = new Vector2(Mathf.Min(numberCellSize.x, (area.width - cellGrid.spacing.x * (count - 1)) / count), numberCellSize.y);
                    var order = new string[count];
                    for (int i = 0; i < count; i++) order[i] = (i + 1).ToString();
                    clue.text = string.Format(numberOrderFormat, string.Join("  ", order));
                    int placed = 0;
                    for(int i=0;i<count;i++) if(attempt.Values[i]==i) placed++;
                    status.text = string.Format(numberProgressFormat,placed,count)+"\n"
                        +(selectedIndex >= 0 ? string.Format(numberSelectedFormat, attempt.Values[selectedIndex] + 1) : numberPickText);
                }
                if (definition.type == SurvivalPuzzleType.PowerSwitches)
                {
                    int green = 0;
                    foreach (bool value in attempt.Switches) if (value) green++;
                    status.text = string.Format(lightsProgressFormat, green, count);
                }
                if (circuit)
                {
                    var area = ((RectTransform)pieces.transform).rect;
                    int side = definition.circuitSize;
                    cellGrid.cellSize = new Vector2(Mathf.Min(circuitCellSize.x, (area.width - cellGrid.spacing.x * (side - 1)) / side), Mathf.Min(circuitCellSize.y, (area.height - cellGrid.spacing.y * (side - 1)) / side));
                }

                if (definition.type == SurvivalPuzzleType.Breaker)
                {
                    var target = new string[count];
                    for (int i = 0; i < count; i++)
                        target[i] = (i + 1) + ": " + (attempt.Targets[i] ? onText : offText);
                    clue.text = string.Format(targetFormat, string.Join("  ", target));
                }

                if (definition.type == SurvivalPuzzleType.ButtonSequence)
                {
                    status.text = attempt.SequenceReady ? string.Format(repeatFormat, attempt.SequenceProgress, attempt.Sequence.Length) : watchText;
                    if (attempt.FlashButton >= 0 && attempt.FlashButton != lastSequenceFlash) feedback.Press();
                    lastSequenceFlash = attempt.FlashButton;
                }
                var powered = circuit ? attempt.PoweredCircuitDistances() : null;
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i].gameObject.SetActive(i < count);
                    if (i >= count)
                        continue;
                    string label = (i + 1).ToString();
                    Color color = neutral;
                    int ports = 0;
                    bool enabled = !attempt.Solved && !attempt.Failed;
                    if (circuit)
                    {
                        ports = attempt.Ports[i];
                        label = i == attempt.StartCell ? circuitInputLabel : i == attempt.EndCell ? circuitOutputLabel : "";
                        bool outputPowered = (attempt.Ports[attempt.EndCell]&2)!=0 && feedback.PowerReached(powered[attempt.EndCell]);
                        color = i == attempt.StartCell ? selected : i == attempt.EndCell ? outputPowered ? complete : circuitWaitingColor : neutral;
                    }
                    else if (definition.type == SurvivalPuzzleType.ArrangeNumbers)
                    {
                        label = (attempt.Values[i] + 1).ToString();
                        color = selectedIndex == i ? selected : attempt.Values[i] == i ? complete : neutral;
                    }
                    else if (definition.type == SurvivalPuzzleType.ButtonSequence)
                    {
                        color = attempt.FlashButton == i ? flash : definition.colors[i];
                        enabled = attempt.SequenceReady && !attempt.Solved && !attempt.Failed;
                    }
                    else if(!powerSwitches)
                    {
                        label += "\n" + (attempt.Switches[i] ? onText : offText);
                        color = attempt.Switches[i] ? complete : neutral;
                    }

                    cells[i].Show(label, color, enabled, ports);
                    cells[i].Motion.SetTheme(definition.type);
                    cells[i].ArrangeMechanismCaption(definition.type == SurvivalPuzzleType.PowerSwitches || definition.type == SurvivalPuzzleType.Breaker);
                    if(cells[i].PowerSwitch!=null)cells[i].PowerSwitch.Present(powerSwitchBoard,i,powerSwitches,powerSwitches && attempt.PowerSwitchPositions[i]);
                    if (circuit) cells[i].SetPowered(feedback.PowerReached(powered[i]));
                    else if (definition.type == SurvivalPuzzleType.PowerSwitches || definition.type == SurvivalPuzzleType.Breaker)
                    { cells[i].Motion.SetLever(attempt.Switches[i]); cells[i].Motion.SetPowered(attempt.Switches[i]); }
                    else if (definition.type == SurvivalPuzzleType.ButtonSequence) cells[i].Motion.SetPowered(attempt.FlashButton == i);
                }
                if(circuit)
                {
                    int lit=0;foreach(int distance in powered) if(feedback.PowerReached(distance)) lit++;
                    bool output=(attempt.Ports[attempt.EndCell]&2)!=0 && feedback.PowerReached(powered[attempt.EndCell]);
                    status.text=string.Format(circuitProgressFormat,lit,count,output?onText:offText);
                    circuitFlow?.Present(attempt,cells,feedback);
                }
                if(numbers) numberRow?.Present(attempt,cells);
                if(powerSwitches)powerSwitchBoard?.Present(attempt,cells);
            }

            if (focusPending)
            {
                Selectable first = code ? (Selectable)codeInput : pairing ? left[0].Button : cells[0].Button;
                if (first.gameObject.activeInHierarchy && first.IsInteractable())
                {
                    first.Select();
                    focusPending = false;
                }
            }
        }

        // Translate a UI selection into the move needed by the current puzzle type.
        public void Select(int side, int index)
        {
            var puzzle = mode.Tasks.Puzzle;
            //The press that opened a puzzle must not also choose its first piece in the same frame.
            if (!CanUsePuzzle || Time.frameCount <= openedFrame)
                return;
            if (side < 2)
            {
                var before = (bool[])puzzle.Matched.Clone();
                matching.Select(side,index);
                AnimateNewMatches(before,puzzle);
                return;
            }

            switch (puzzle.Definition.type)
            {
                case SurvivalPuzzleType.ButtonSequence:
                    puzzle.PressSequence(index);
                    break;
                case SurvivalPuzzleType.Circuit:
                    puzzle.RotateCircuit(index);
                    cells[index].Motion.Rotate(); feedback.CircuitChanged();
                    break;
                case SurvivalPuzzleType.PowerSwitches:
                case SurvivalPuzzleType.Breaker:
                    puzzle.FlipSwitch(index);
                    break;
                case SurvivalPuzzleType.ArrangeNumbers:
                    if (selectedIndex < 0)
                        selectedIndex = index;
                    else
                    {
                        SwapNumbers(puzzle,selectedIndex,index);
                        selectedIndex = -1;
                    }

                    break;
            }
        }

        // Use the same puzzle rules for dragging as for clicking; dragging is only another way to choose pieces.
        public void Drop(int sourceSide, int sourceIndex, int targetSide, int targetIndex)
        {
            if (!CanDrag)
                return;
            if (sourceSide < 2 && targetSide < 2)
            {
                var puzzle = mode.Tasks.Puzzle;
                var before = (bool[])puzzle.Matched.Clone();
                matching.Match(sourceSide,sourceIndex,targetSide,targetIndex);
                AnimateNewMatches(before,puzzle);
            }
            else if (sourceSide == 2 && targetSide == 2 && mode.Tasks.Puzzle.Definition.type == SurvivalPuzzleType.ArrangeNumbers)
                SwapNumbers(mode.Tasks.Puzzle,sourceIndex,targetIndex);
            selectedIndex = -1;
        }

        private void AnimateNewMatches(bool[] before,Escape4Now.Puzzles.PuzzleSession puzzle)
        {
            for (int i = 0; i < before.Length; i++)
                if (!before[i] && puzzle.Matched[i])
                {
                    feedback.Connected();
                    if (puzzle.Definition.type != SurvivalPuzzleType.MatchWires)
                        left[i].Motion.Insert(right[System.Array.IndexOf(puzzle.Right,puzzle.Left[i])].Motion.SlotCenter);
                }
        }
        private void SwapNumbers(Escape4Now.Puzzles.PuzzleSession puzzle,int a,int b)
        {
            if (a == b) return;
            puzzle.SwapNumbers(a,b);
            cells[a].Motion.AnimateFrom(cells[b].Motion.SlotCenter);
            cells[b].Motion.AnimateFrom(cells[a].Motion.SlotCenter);
        }

        // Pass the visible code field to the puzzle instead of checking the code inside the UI.
        public void SubmitCode()
        {
            if (CanUsePuzzle)
                mode.Tasks.Puzzle.SubmitCode(codeInput.text);
        }
    }
}
