//Separates the input switches from the output lights and previews which pair each switch affects.
using Escape4Now.Puzzles;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class SurvivalPowerSwitchBoard : MonoBehaviour
    {
        [Header("Saved Panel Parts")]
        [SerializeField] private SurvivalPuzzleUI view;
        [SerializeField] private Image[] lights;
        [SerializeField] private Outline[] focusRings;
        [SerializeField] private Text[] lightLabels, linkedLabels;
        [Header("Lamp Layout and Colors")]
        [SerializeField] private float lampHeight = 72;
        [SerializeField] private float linkHeight = -49;
        [SerializeField] private Color onColor = new Color(.25f,1,.55f,1);
        [SerializeField] private Color offColor = new Color(.8f,.25f,.2f,1);
        [SerializeField] private string lightFormat = "LIGHT {0}";
        [SerializeField] private string linkedFormat = "LIGHTS {0} + {1}";
        private PuzzleSession puzzle;private PuzzleTokenUI[] cells;private int focused=-1;
        public bool CanInteract => puzzle!=null && !puzzle.Solved && !puzzle.Failed
            && !Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused;
        public void Present(PuzzleSession attempt,PuzzleTokenUI[] controls)
        {if(puzzle!=attempt)focused=-1;puzzle=attempt;cells=controls;Draw();}
        public void Focus(int index){focused=index;}
        public void ClearFocus(int index){if(focused==index)focused=-1;}
        public void SetPosition(int index,bool on)
        {
            if(!CanInteract || puzzle.PowerSwitchPositions[index]==on)return;
            view.Select(2,index);view.Feedback.Press();
        }
        private void OnEnable(){Canvas.willRenderCanvases+=Draw;}
        private void OnDisable(){Canvas.willRenderCanvases-=Draw;}
        private void Draw()
        {
            if(puzzle==null || !isActiveAndEnabled)return;
            for(int i=0;i<lights.Length;i++)
            {
                bool visible=i<puzzle.Switches.Length;
                lights[i].gameObject.SetActive(visible);lightLabels[i].gameObject.SetActive(visible);linkedLabels[i].gameObject.SetActive(visible);
                if(!visible)continue;
                var center=cells[i].Motion.SlotCenter;
                float scale=((RectTransform)cells[i].transform).lossyScale.y;
                lights[i].transform.position=center+Vector3.up*lampHeight*scale;
                lightLabels[i].transform.position=lights[i].transform.position+Vector3.up*25*scale;
                linkedLabels[i].transform.position=center+Vector3.up*linkHeight*scale;
                lights[i].color=puzzle.Switches[i]?onColor:offColor;
                focusRings[i].enabled=focused>=0 && (focused==i || (focused+1)%puzzle.Switches.Length==i);
                lightLabels[i].text=string.Format(lightFormat,i+1);
                linkedLabels[i].text=string.Format(linkedFormat,i+1,(i+1)%puzzle.Switches.Length+1);
            }
        }
    }
}
