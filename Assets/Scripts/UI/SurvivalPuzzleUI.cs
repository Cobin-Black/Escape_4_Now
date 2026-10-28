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
        public PuzzleTokenUI DragSource { get; set; }
        private bool CanUsePuzzle => mode.Tasks.Puzzle != null && !mode.Tasks.Puzzle.Solved && !mode.Tasks.Puzzle.Failed && !LocalMultiplayerSession.GameplayPaused;
        public bool CanDrag => CanUsePuzzle && (mode.Tasks.Puzzle.Definition.type == SurvivalPuzzleType.MatchWires
            || mode.Tasks.Puzzle.Definition.type == SurvivalPuzzleType.SortSupplies
            || mode.Tasks.Puzzle.Definition.type == SurvivalPuzzleType.Batteries
            || mode.Tasks.Puzzle.Definition.type == SurvivalPuzzleType.ArrangeNumbers);

        public void ClearSelection() => selectedIndex = -1;

        // Connect the saved Unity buttons and puzzle pieces to the task controller.
        private void Start()
        {
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
            if (attempt.Solved)
            {
                mode.Tasks.FinishSolvedPuzzle();
                panel.SetActive(false);
                return;
            }

            if (attempt.Failed)
            {
                mode.Tasks.ClosePuzzle();
                panel.SetActive(false);
                return;
            }

            if (revision != mode.Tasks.Revision)
            {
                revision = mode.Tasks.Revision;
                selectedIndex = -1;
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
            for (int i = 0; i < connections.Length; i++)
            {
                bool connected = definition.type == SurvivalPuzzleType.MatchWires && i < attempt.Matched.Length && attempt.Matched[i];
                connections[i].gameObject.SetActive(connected);
                if (!connected)
                    continue;
                int target = System.Array.IndexOf(attempt.Right, attempt.Left[i]);
                var a = (RectTransform)left[i].transform;
                var b = (RectTransform)right[target].transform;
                var parent = connections[i].parent;
                Vector3 start = parent.InverseTransformPoint(a.TransformPoint(a.rect.center + Vector2.right * a.rect.width * .5f));
                Vector3 end = parent.InverseTransformPoint(b.TransformPoint(b.rect.center + Vector2.left * b.rect.width * .5f));
                Vector3 delta = end - start;
                connections[i].localPosition = start;
                connections[i].localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                connections[i].sizeDelta = new Vector2(delta.magnitude, wireThickness);
                connections[i].GetComponent<Image>().color = definition.colors[attempt.Left[i]];
            }

            if (pairing)
            {
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
                    left[i].Show(definition.symbols[a], selectedIndex == i ? selected : definition.colors[a], !attempt.Matched[i]);
                    right[i].Show(definition.symbols[b], definition.colors[b], !rightMatched);
                }
            }
            else if (!code)
            {
                bool circuit = definition.type == SurvivalPuzzleType.Circuit;
                int count = circuit ? attempt.Ports.Length : definition.Count;
                bool numbers = definition.type == SurvivalPuzzleType.ArrangeNumbers;
                int columns = Mathf.Clamp(tokenColumns, 1, count);
                //Avoid leaving one control alone on the last row when a balanced grid fits.
                if (columns > 1 && count > columns && count % columns == 1) columns--;
                cellGrid.constraintCount = circuit ? definition.circuitSize : numbers ? count : columns;
                cellGrid.cellSize = circuit ? circuitCellSize : numbers ? numberCellSize : tokenCellSize;
                if (numbers)
                {
                    var area = ((RectTransform)pieces.transform).rect;
                    cellGrid.cellSize = new Vector2(Mathf.Min(numberCellSize.x, (area.width - cellGrid.spacing.x * (count - 1)) / count), numberCellSize.y);
                    var order = new string[count];
                    for (int i = 0; i < count; i++) order[i] = (i + 1).ToString();
                    clue.text = string.Format(numberOrderFormat, string.Join("  ", order));
                    status.text = selectedIndex >= 0 ? string.Format(numberSelectedFormat, attempt.Values[selectedIndex] + 1) : numberPickText;
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
                    status.text = attempt.SequenceReady ? string.Format(repeatFormat, attempt.SequenceProgress, attempt.Sequence.Length) : watchText;
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i].gameObject.SetActive(i < count);
                    if (i >= count)
                        continue;
                    string label = (i + 1).ToString();
                    Color color = neutral;
                    int ports = 0;
                    bool enabled = true;
                    if (circuit)
                    {
                        ports = attempt.Ports[i];
                        label = i == attempt.StartCell ? circuitInputLabel : i == attempt.EndCell ? circuitOutputLabel : "";
                        color = i == attempt.StartCell ? selected : i == attempt.EndCell ? complete : neutral;
                    }
                    else if (definition.type == SurvivalPuzzleType.ArrangeNumbers)
                    {
                        label = (attempt.Values[i] + 1).ToString();
                        color = selectedIndex == i ? selected : neutral;
                    }
                    else if (definition.type == SurvivalPuzzleType.ButtonSequence)
                    {
                        color = attempt.FlashButton == i ? flash : definition.colors[i];
                        enabled = attempt.SequenceReady;
                    }
                    else
                    {
                        label += "\n" + (attempt.Switches[i] ? onText : offText);
                        color = attempt.Switches[i] ? complete : neutral;
                    }

                    cells[i].Show(label, color, enabled, ports);
                }
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
            if (side == 0)
            {
                selectedIndex = index;
                return;
            }

            if (side == 1)
            {
                if (selectedIndex >= 0)
                    puzzle.Connect(selectedIndex, index);
                selectedIndex = -1;
                return;
            }

            switch (puzzle.Definition.type)
            {
                case SurvivalPuzzleType.ButtonSequence:
                    puzzle.PressSequence(index);
                    break;
                case SurvivalPuzzleType.Circuit:
                    puzzle.RotateCircuit(index);
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
                        puzzle.SwapNumbers(selectedIndex, index);
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
            if (sourceSide == 0 && targetSide == 1)
                mode.Tasks.Puzzle.Connect(sourceIndex, targetIndex);
            else if (sourceSide == 2 && targetSide == 2 && mode.Tasks.Puzzle.Definition.type == SurvivalPuzzleType.ArrangeNumbers)
                mode.Tasks.Puzzle.SwapNumbers(sourceIndex, targetIndex);
            selectedIndex = -1;
        }

        // Pass the visible code field to the puzzle instead of checking the code inside the UI.
        public void SubmitCode()
        {
            if (CanUsePuzzle)
                mode.Tasks.Puzzle.SubmitCode(codeInput.text);
        }
    }
}
