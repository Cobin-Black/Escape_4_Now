//A horizontal switch operated by the existing UI button or a pointer drag.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class SurvivalPowerSwitchHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Saved Switch Parts")]
        [SerializeField] private GameObject housing, oldHandle, oldLamp;
        [SerializeField] private RectTransform knob;
        [SerializeField] private Image track;
        [SerializeField] private Text caption;
        [Header("Slide Appearance")]
        [SerializeField] private float travel = 18;
        [SerializeField, Min(1)] private float response = 18;
        [SerializeField, Min(1)] private float dragDistance = 18;
        [SerializeField] private Color leftColor = new Color(.15f,.24f,.29f,1);
        [SerializeField] private Color rightColor = new Color(.1f,.5f,.65f,1);
        [SerializeField] private string captionFormat = "SWITCH {0}";
        private SurvivalPowerSwitchBoard board;
        private int index;
        private bool dragging, visible, state;
        private Vector2 start;
        public bool IsSliding => dragging;
        public void Present(SurvivalPowerSwitchBoard owner,int slot,bool enabled,bool on)
        {
            board=owner;index=slot;visible=enabled;state=on;housing.SetActive(enabled);
            if(!enabled)return;
            oldHandle.SetActive(false);oldLamp.SetActive(false);
            caption.text=string.Format(captionFormat,slot+1);
            caption.rectTransform.offsetMin=new Vector2(5,44);caption.rectTransform.offsetMax=new Vector2(-5,-3);
            caption.alignment=TextAnchor.MiddleCenter;caption.resizeTextMaxSize=12;
            if(!dragging)knob.anchoredPosition=Vector2.Lerp(knob.anchoredPosition,new Vector2(on?travel:-travel,0),Time.deltaTime*response);
            track.color=on?rightColor:leftColor;
        }
        public void OnBeginDrag(PointerEventData data)
        {if(visible && board.CanInteract){dragging=true;start=data.position;}}
        public void OnDrag(PointerEventData data)
        {
            if(!dragging)return;
            float delta=(data.position.x-start.x)/Mathf.Max(.01f,knob.lossyScale.x);
            knob.anchoredPosition=new Vector2(Mathf.Clamp((state?travel:-travel)+delta,-travel,travel),0);
        }
        public void OnEndDrag(PointerEventData data)
        {
            if(!dragging)return;dragging=false;
            float delta=(data.position.x-start.x)/Mathf.Max(.01f,knob.lossyScale.x);
            if(Mathf.Abs(delta)>=dragDistance)board.SetPosition(index,delta>0);
        }
        public void OnPointerEnter(PointerEventData data){if(visible)board.Focus(index);}
        public void OnPointerExit(PointerEventData data){if(visible)board.ClearFocus(index);}
        public void OnSelect(BaseEventData data){if(visible)board.Focus(index);}
        public void OnDeselect(BaseEventData data){if(visible)board.ClearFocus(index);}
        private void OnDisable(){dragging=false;}
    }
}
