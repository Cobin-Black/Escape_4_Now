//Handles selection and a loose cable; PuzzleSession still validates every match.
using Escape4Now.Puzzles;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class SurvivalMatchingUI : MonoBehaviour
    {
        [Header("Saved Unity UI")]
        [SerializeField] private PuzzleWireGraphic preview;
        [SerializeField] private Image pointerTip;
        [Header("Aim Feedback")]
        [SerializeField] private Color invalidColor = new Color(1,.3f,.25f,1);
        [SerializeField] private string idleHint = "Drag between ends, or click two ends.";
        [SerializeField] private string selectedHint = "Choose the matching end on the other side.";
        [SerializeField] private string invalidHint = "That end does not match. Move away to cancel.";
        [SerializeField] private string itemIdleHint = "Drag an item into its matching slot, or select the item and then the slot.";
        [SerializeField] private string itemSelectedHint = "Choose the matching slot. Select the item again to cancel.";
        [SerializeField] private string itemInvalidHint = "That slot does not match. Release in empty space to cancel.";
        private PuzzleTokenUI[] left, right;
        private PuzzleWireGraphic[] cables;
        private PuzzleSession puzzle;
        private int chosenSide = -1, chosenIndex = -1, hoverSide = -1, hoverIndex = -1;
        private bool dragging;
        private Vector2 pointerPosition;
        private Camera pointerCamera;
        private float thickness = 5;
        public bool IsWires => puzzle != null && puzzle.Definition.type == SurvivalPuzzleType.MatchWires;
        private bool Usable => puzzle != null && !puzzle.Solved && !puzzle.Failed
            && !Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused;
        private bool HasOppositeHover => chosenSide >= 0 && hoverSide >= 0 && hoverSide != chosenSide && Available(hoverSide,hoverIndex);
        public string Hint => HasOppositeHover && !SameKey(chosenSide,chosenIndex,hoverSide,hoverIndex)
            ? IsWires ? invalidHint : itemInvalidHint
            : chosenSide >= 0 ? IsWires ? selectedHint : itemSelectedHint : IsWires ? idleHint : itemIdleHint;
        public static bool Supports(PuzzleSession attempt) => attempt != null
            && (attempt.Definition.type == SurvivalPuzzleType.MatchWires || attempt.Definition.type == SurvivalPuzzleType.SortSupplies
                || attempt.Definition.type == SurvivalPuzzleType.Batteries);
        public void Configure(PuzzleTokenUI[] sources,PuzzleTokenUI[] targets,RectTransform[] connections)
        {
            left = sources; right = targets; cables = new PuzzleWireGraphic[connections.Length];
            for (int i = 0; i < connections.Length; i++) cables[i] = connections[i].GetComponentInChildren<PuzzleWireGraphic>(true);
        }
        public void Refresh(PuzzleSession attempt,float width)
        {
            if (puzzle != attempt) { ClearSelection(); puzzle = attempt; }
            thickness = width;
            for (int i = 0; i < cables.Length; i++)
            {
                bool connected = IsWires && i < puzzle.Matched.Length && puzzle.Matched[i];
                cables[i].gameObject.SetActive(connected);
                if (!connected) continue;
                int target = System.Array.IndexOf(puzzle.Right,puzzle.Left[i]);
                var space = (RectTransform)cables[i].transform;
                cables[i].SetCable(left[i].ConnectionPoint(space,0),right[target].ConnectionPoint(space,1),
                    puzzle.Definition.colors[puzzle.Left[i]],thickness);
            }
            for (int side = 0; side < 2; side++)
            {
                var tokens = side == 0 ? left : right;
                for (int i = 0; i < tokens.Length; i++)
                {
                    bool visible = Supports(puzzle) && i < puzzle.Left.Length;
                    bool hover = visible && chosenSide >= 0 && side != chosenSide && side == hoverSide && i == hoverIndex && Available(side,i);
                    tokens[i].SetMatchingState(visible && side == chosenSide && i == chosenIndex,hover,
                        visible && !Available(side,i),!hover || SameKey(chosenSide,chosenIndex,side,i),
                        visible ? puzzle.Definition.colors[Key(side,i)] : Color.white);
                }
            }
            DrawPreview();
        }
        public void ClearSelection()
        {
            chosenSide = chosenIndex = hoverSide = hoverIndex = -1; dragging = false;
            if (preview != null) preview.gameObject.SetActive(false);
            if (pointerTip != null) pointerTip.gameObject.SetActive(false);
        }
        //Either end can be chosen first; choosing it twice cancels the loose connection.
        public void Select(int side,int index)
        {
            if (!Usable || !Available(side,index)) return;
            if (chosenSide >= 0 && chosenSide != side) { Match(chosenSide,chosenIndex,side,index); return; }
            if (chosenSide == side && chosenIndex == index) { ClearSelection(); return; }
            chosenSide = side; chosenIndex = index;
        }
        public bool BeginDrag(int side,int index,Vector2 screen,Camera camera)
        {
            if (!Usable || !IsWires || !Available(side,index)) return false;
            chosenSide = side; chosenIndex = index; dragging = true; pointerPosition = screen; pointerCamera = camera; return true;
        }
        public void MoveDrag(Vector2 screen) { pointerPosition = screen; }
        public void EndDrag() { ClearSelection(); }
        public void Hover(int side,int index,bool entered)
        {
            if (entered) { hoverSide = side; hoverIndex = index; }
            else if (hoverSide == side && hoverIndex == index) { hoverSide = hoverIndex = -1; }
        }
        public void Match(int sourceSide,int source,int targetSide,int target)
        {
            if (!Usable || sourceSide == targetSide || !Available(sourceSide,source) || !Available(targetSide,target)) return;
            puzzle.Connect(sourceSide == 0 ? source : target,sourceSide == 0 ? target : source);
            ClearSelection();
        }
        private int Key(int side,int index) => side == 0 ? puzzle.Left[index] : puzzle.Right[index];
        private bool SameKey(int aSide,int a,int bSide,int b) => Key(aSide,a) == Key(bSide,b);
        private bool Available(int side,int index)
        {
            if (!Supports(puzzle) || side < 0 || side > 1 || index < 0 || index >= puzzle.Left.Length) return false;
            int source = side == 0 ? index : System.Array.IndexOf(puzzle.Left,puzzle.Right[index]);
            return source >= 0 && !puzzle.Matched[source];
        }
        private void DrawPreview()
        {
            bool visible = Usable && IsWires && chosenSide >= 0 && (dragging || HasOppositeHover);
            preview.gameObject.SetActive(visible); pointerTip.gameObject.SetActive(visible);
            if (!visible) return;
            var space = (RectTransform)preview.transform;
            var token = chosenSide == 0 ? left[chosenIndex] : right[chosenIndex];
            Vector2 from = token.ConnectionPoint(space,chosenSide);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(space,pointerPosition,pointerCamera,out var to);
            if (HasOppositeHover) to = (hoverSide == 0 ? left[hoverIndex] : right[hoverIndex]).ConnectionPoint(space,hoverSide);
            var tint = HasOppositeHover && !SameKey(chosenSide,chosenIndex,hoverSide,hoverIndex) ? invalidColor : puzzle.Definition.colors[Key(chosenSide,chosenIndex)];
            preview.SetCable(from,to,tint,thickness); pointerTip.color = tint; pointerTip.transform.position = space.TransformPoint(to);
        }
        private void OnDisable() { ClearSelection(); }
    }
}
