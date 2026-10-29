//Keeps the item picture, caption and receiving slot separate so they never cover each other.
using Escape4Now.Puzzles;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class PuzzleMatchingPieceView : MonoBehaviour
    {
        [Header("Saved Piece Parts")]
        [SerializeField] private RectTransform caption;
        [SerializeField] private Text label;
        [SerializeField] private Image face, socket;
        [SerializeField] private GameObject slotFrame;
        [SerializeField] private RectTransform iconMount;
        [SerializeField] private CanvasGroup iconOpacity;
        [Header("Item Layout")]
        [SerializeField, Min(0)] private float iconWidth = 30;
        [SerializeField, Min(0)] private float leftPadding = 10;
        [SerializeField, Min(0)] private float captionGap = 10;
        [SerializeField, Min(0)] private float rightPadding = 20;
        [SerializeField, Range(0,1)] private float emptySlotIconOpacity = .35f;
        [SerializeField, Range(0,1)] private float slotBorderOpacity = .7f;
        [SerializeField] private Color slotColor = new Color(.08f,.14f,.18f,1);
        [SerializeField] private Color filledColor = new Color(.12f,.35f,.26f,1);
        [SerializeField] private string filledFormat = "{0}  OK";
        private GameObject shownPrefab, instance;
        private Image[] borders;
        private void Awake()
        {
            borders = slotFrame.GetComponentsInChildren<Image>(true);
            slotFrame.SetActive(false); iconMount.gameObject.SetActive(false);
        }

        public void Present(SurvivalPuzzleDefinition definition,int key,bool target,bool matched)
        {
            bool wires = definition.type == SurvivalPuzzleType.MatchWires;
            socket.gameObject.SetActive(wires);
            slotFrame.SetActive(!wires && target);
            iconMount.gameObject.SetActive(!wires);
            caption.anchorMin = Vector2.zero; caption.anchorMax = Vector2.one;
            caption.offsetMin = new Vector2(wires ? leftPadding : leftPadding+iconWidth+captionGap,0);
            caption.offsetMax = new Vector2(-rightPadding,0);
            label.alignment = wires ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            if (wires) return;
            iconMount.anchorMin = iconMount.anchorMax = new Vector2(0,.5f);
            iconMount.pivot = new Vector2(0,.5f);
            iconMount.anchoredPosition = new Vector2(leftPadding,0);
            iconMount.sizeDelta = new Vector2(iconWidth,iconWidth);
            iconOpacity.alpha = target && !matched ? emptySlotIconOpacity : 1;
            if (target) { face.color = matched ? filledColor : slotColor; label.color = Color.white; }
            var borderColor = definition.colors[key]; borderColor.a = slotBorderOpacity;
            foreach (var border in borders) border.color = borderColor;
            if (target && matched) label.text = string.Format(filledFormat,definition.symbols[key]);
            var prefab = definition.matchingIconPrefabs != null && key < definition.matchingIconPrefabs.Length
                ? definition.matchingIconPrefabs[key] : null;
            if (shownPrefab == prefab) return;
            shownPrefab = prefab;
            if (instance != null) Destroy(instance);
            if (prefab == null) return;
            instance = Instantiate(prefab,iconMount,false);
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        }
    }
}
