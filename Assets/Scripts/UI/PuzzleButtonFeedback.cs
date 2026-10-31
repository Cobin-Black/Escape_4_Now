//Adds the same hover, press and sound response to pointer and keyboard/controller button use.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class PuzzleButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private SurvivalPuzzleFeedback feedback;
        [SerializeField] private PuzzlePieceMotion motion;
        private Button button;
        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(() => { feedback.Press(); if (motion != null) motion.Pulse(); });
        }
        public void OnPointerEnter(PointerEventData data) { if (button.interactable) { feedback.Hover(); motion?.Hover(true); } }
        public void OnPointerExit(PointerEventData data) { motion?.Hover(false); }
        public void OnPointerDown(PointerEventData data) { if (button.interactable) motion?.Press(true); }
        public void OnPointerUp(PointerEventData data) { motion?.Press(false); }
        public void OnSelect(BaseEventData data) { if (button.interactable) motion?.Hover(true); }
        public void OnDeselect(BaseEventData data) { motion?.Hover(false); }
    }
}
