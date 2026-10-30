// Handles clicks and drag-and-drop on puzzle pieces; the puzzle session checks whether a move is correct.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    //The same authored token supports pointer dragging and UI navigation/click matching.
    public sealed class PuzzleTokenUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField]
        private Button button;
        [SerializeField]
        private Text label;
        [SerializeField]
        private Image face;
        [SerializeField]
        private Image[] circuitPorts;
        [SerializeField]
        private CanvasGroup dragGroup;
        [Header("Matching and Physical Feedback")]
        [SerializeField] private Image connector;
        [SerializeField] private Outline interactionOutline;
        [SerializeField] private PuzzlePieceMotion motion;
        [SerializeField] private PuzzleMatchingPieceView matchingView;
        [SerializeField] private SurvivalPowerSwitchHandle powerSwitch;
        [SerializeField] private Color selectedOutline = Color.white;
        [SerializeField] private Color correctOutline = new Color(.3f,1,.65f,1);
        [SerializeField] private Color wrongOutline = new Color(1,.3f,.25f,1);
        [SerializeField] private Color poweredPortColor = new Color(.3f,1,.8f,1);
        [SerializeField] private Color idlePortColor = new Color(.3f,.4f,.45f,1);
        [Header("Circuit Caption")]
        [SerializeField, Range(8, 16)]
        private int circuitCaptionFontSize = 10;
        [Header("Caption Contrast")]
        [SerializeField] private Color lightCaptionColor = Color.white;
        [SerializeField] private Color darkCaptionColor = new Color(.025f, .045f, .07f, 1f);
        [SerializeField, Range(0f, 1f)] private float lightFaceThreshold = .55f;
        private int normalCaptionFontSize;
        [SerializeField, Min(0)] private float switchCaptionInset = 42;
        public void ArrangeMechanismCaption(bool hasSwitch)
        {
            label.rectTransform.offsetMin = new Vector2(hasSwitch ? switchCaptionInset : 6,4);
            label.rectTransform.offsetMax = new Vector2(-6,-4);
        }
        private void Awake()
        {
            normalCaptionFontSize = label.resizeTextMaxSize;
        }

        private SurvivalPuzzleUI owner;
        private int side, index;
        public Button Button => button;
        public PuzzlePieceMotion Motion => motion;
        public PuzzleMatchingPieceView MatchingView => matchingView;
        public SurvivalPowerSwitchHandle PowerSwitch => powerSwitch;
        public Vector2 ConnectionPoint(RectTransform space,int column)
        {
            var rect = (RectTransform)transform;
            var world = connector != null ? connector.transform.position
                : rect.TransformPoint(rect.rect.center + Vector2.right*rect.rect.width*(column == 0 ? .5f : -.5f));
            return space.InverseTransformPoint(world);
        }
        public void SetMatchingState(bool selected,bool hovered,bool matched,bool valid,Color tint)
        {
            if (connector != null) connector.color = tint;
            motion?.SetPowered(matched);
            if (interactionOutline == null) return;
            interactionOutline.enabled = selected || hovered || matched;
            interactionOutline.effectColor = hovered && !valid ? wrongOutline : matched || hovered ? correctOutline : selectedOutline;
        }
        public void SetPowered(bool on)
        {
            foreach (var port in circuitPorts) port.color = on ? poweredPortColor : idlePortColor;
            motion?.SetPowered(on);
        }

        // Remember which puzzle side and slot this saved UI piece represents.
        public void Bind(SurvivalPuzzleUI view, int column, int slot)
        {
            owner = view;
            side = column;
            index = slot;
            button.onClick.AddListener(() => owner.Select(side, index));
        }

        // Update the piece's caption, color and circuit ports without rebuilding the UI.
        public void Show(string text, Color color, bool enabled, int ports = 0)
        {
            label.text = text;
            label.alignment = ports != 0 ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter;
            label.resizeTextMaxSize = ports != 0 ? circuitCaptionFontSize : normalCaptionFontSize;
            label.resizeTextMinSize = Mathf.Min(label.resizeTextMinSize, label.resizeTextMaxSize);
            face.color = color;
            label.color = color.r * .2126f + color.g * .7152f + color.b * .0722f > lightFaceThreshold
                ? darkCaptionColor : lightCaptionColor;
            button.interactable = enabled;
            for (int i = 0; i < circuitPorts.Length; i++)
            {
                circuitPorts[i].gameObject.SetActive((ports & (1 << i)) != 0);
                if (ports == 0)
                    continue;
                var rect = circuitPorts[i].rectTransform;
                var size = ((RectTransform)transform).rect.size;
                bool vertical = i % 2 == 0;
                float thickness = vertical ? rect.sizeDelta.x : rect.sizeDelta.y;
                rect.sizeDelta = vertical ? new Vector2(thickness, size.y * .5f) : new Vector2(size.x * .5f, thickness);
                rect.anchoredPosition = i == 0 ? new Vector2(0, size.y * .25f) : i == 1 ? new Vector2(size.x * .25f, 0) : i == 2 ? new Vector2(0, -size.y * .25f) : new Vector2(-size.x * .25f, 0);
            }
        }

        // Start a drag only for puzzle types that support moving pieces.
        public void OnBeginDrag(PointerEventData data)
        {
            if (!button.interactable || !owner.CanDrag || owner.DragSource != null)
                return;
            if (side == 1 && !owner.IsWireMatching) return;
            owner.ClearSelection();
            if (owner.IsWireMatching && !owner.BeginWireDrag(side,index,data)) return;
            owner.DragSource = this;
            dragGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData data)
        {
            if (owner.DragSource == this)
            {
                if (owner.IsWireMatching) owner.MoveWireDrag(data.position);
                else motion.Drag(data.position,data.pressEventCamera);
            }
        }

        // Return the visual to its home position; the puzzle decides whether the drop changed the answer.
        public void OnEndDrag(PointerEventData data)
        {
            if (owner.DragSource != this)
                return;
            if (owner.IsWireMatching) owner.EndWireDrag();
            else if (!owner.IsMatchingComplete(side,index)) motion.ReturnHome();
            dragGroup.blocksRaycasts = true;
            owner.DragSource = null;
        }

        public void OnDrop(PointerEventData data)
        {
            if (owner.DragSource != null)
                owner.Drop(owner.DragSource.side, owner.DragSource.index, side, index);
        }
        public void OnPointerEnter(PointerEventData data) { owner?.HoverMatch(side,index,true); }
        public void OnPointerExit(PointerEventData data) { owner?.HoverMatch(side,index,false); }

        private void OnDisable()
        {
            if (owner == null || owner.DragSource != this)
                return;
            motion?.ReturnHome();
            dragGroup.blocksRaycasts = true;
            owner.DragSource = null;
        }
    }
}
