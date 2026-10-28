// Handles clicks and drag-and-drop on puzzle pieces; the puzzle session checks whether a move is correct.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    //The same authored token supports pointer dragging and UI navigation/click matching.
    public sealed class PuzzleTokenUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
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
        [Header("Circuit Caption")]
        [SerializeField, Range(8, 16)]
        private int circuitCaptionFontSize = 10;
        [Header("Caption Contrast")]
        [SerializeField] private Color lightCaptionColor = Color.white;
        [SerializeField] private Color darkCaptionColor = new Color(.025f, .045f, .07f, 1f);
        [SerializeField, Range(0f, 1f)] private float lightFaceThreshold = .55f;
        private int normalCaptionFontSize;
        private void Awake()
        {
            normalCaptionFontSize = label.resizeTextMaxSize;
        }

        private SurvivalPuzzleUI owner;
        private int side, index;
        private Vector3 home;
        public Button Button => button;

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
            if (!button.interactable || side == 1 || !owner.CanDrag)
                return;
            owner.ClearSelection();
            home = transform.position;
            owner.DragSource = this;
            dragGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData data)
        {
            if (owner.DragSource == this)
                transform.position = data.position;
        }

        // Return the visual to its home position; the puzzle decides whether the drop changed the answer.
        public void OnEndDrag(PointerEventData data)
        {
            if (owner.DragSource != this)
                return;
            transform.position = home;
            dragGroup.blocksRaycasts = true;
            owner.DragSource = null;
        }

        public void OnDrop(PointerEventData data)
        {
            if (owner.DragSource != null)
                owner.Drop(owner.DragSource.side, owner.DragSource.index, side, index);
        }

        private void OnDisable()
        {
            if (owner == null || owner.DragSource != this)
                return;
            transform.position = home;
            dragGroup.blocksRaycasts = true;
            owner.DragSource = null;
        }
    }
}
