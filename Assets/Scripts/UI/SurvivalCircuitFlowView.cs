//Shows only real matching connections and sends visible power pulses along them.
using Escape4Now.Puzzles;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class SurvivalCircuitFlowView : MonoBehaviour
    {
        [Header("Saved Connection Graphics")]
        [SerializeField] private Image[] bridges, pulses;
        [Header("Power Appearance")]
        [SerializeField, Min(1)] private float wireWidth = 4;
        [SerializeField, Min(1)] private float pulseSize = 6;
        [SerializeField, Min(.1f)] private float pulseSeconds = .65f;
        [SerializeField, Min(1)] private float terminalLength = 18;
        [SerializeField] private Color connectedColor = new Color(.2f,.4f,.46f,1);
        [SerializeField] private Color poweredColor = new Color(.2f,1,.75f,1);
        [SerializeField] private Color pulseColor = new Color(.85f,1,.95f,1);
        private PuzzleSession puzzle;
        private PuzzleTokenUI[] cells;
        private SurvivalPuzzleFeedback feedback;
        private float elapsed;
        public void Present(PuzzleSession attempt,PuzzleTokenUI[] pieces,SurvivalPuzzleFeedback response)
        { if(puzzle!=attempt) elapsed=0; puzzle=attempt; cells=pieces; feedback=response; Draw(); }
        private void OnEnable(){Canvas.willRenderCanvases+=Draw;}
        private void OnDisable(){Canvas.willRenderCanvases-=Draw;}
        private void Update(){if(!Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused) elapsed+=Time.deltaTime;}
        private void Draw()
        {
            if(puzzle==null || cells==null || !isActiveAndEnabled) return;
            int used=0, side=puzzle.Definition.circuitSize;
            var distances=puzzle.PoweredCircuitDistances();
            for(int i=0;i<puzzle.Ports.Length;i++)
            {
                if(i%side<side-1 && (puzzle.Ports[i]&2)!=0 && (puzzle.Ports[i+1]&8)!=0) Join(i,i+1,Vector2.right);
                if(i/side<side-1 && (puzzle.Ports[i]&4)!=0 && (puzzle.Ports[i+side]&1)!=0) Join(i,i+side,Vector2.down);
            }
            Terminal(puzzle.StartCell,Vector2.left,(puzzle.Ports[puzzle.StartCell]&8)!=0);
            Terminal(puzzle.EndCell,Vector2.right,(puzzle.Ports[puzzle.EndCell]&2)!=0 && feedback.PowerReached(distances[puzzle.EndCell]));
            for(int i=used;i<bridges.Length;i++){bridges[i].gameObject.SetActive(false);pulses[i].gameObject.SetActive(false);}

            void Join(int a,int b,Vector2 direction)
            {
                var rectA=(RectTransform)cells[a].transform; var rectB=(RectTransform)cells[b].transform;
                Vector2 centerA=transform.InverseTransformPoint(cells[a].Motion.SlotCenter), centerB=transform.InverseTransformPoint(cells[b].Motion.SlotCenter);
                var edgeA=(Vector2)transform.InverseTransformPoint(rectA.TransformPoint(rectA.rect.center+Vector2.Scale(direction,rectA.rect.size)*.5f));
                var edgeB=(Vector2)transform.InverseTransformPoint(rectB.TransformPoint(rectB.rect.center+Vector2.Scale(-direction,rectB.rect.size)*.5f));
                bool powered=feedback.PowerReached(distances[a]) && feedback.PowerReached(distances[b]);
                if(distances[b]>=0 && distances[b]<distances[a]) { var saved=centerA;centerA=centerB;centerB=saved; }
                Segment(edgeA,edgeB,centerA,centerB,powered);
            }
            void Terminal(int cell,Vector2 direction,bool powered)
            {
                var rect=(RectTransform)cells[cell].transform;
                Vector2 edge=transform.InverseTransformPoint(rect.TransformPoint(rect.rect.center+Vector2.Scale(direction,rect.rect.size)*.5f));
                Vector2 outside=edge+direction*terminalLength;
                Segment(edge,outside,direction==Vector2.left?outside:edge,direction==Vector2.left?edge:outside,powered);
            }
            void Segment(Vector2 from,Vector2 to,Vector2 pulseFrom,Vector2 pulseTo,bool powered)
            {
                if(used>=bridges.Length)return;
                var line=bridges[used];var pulse=pulses[used]; used++;
                line.gameObject.SetActive(true); line.color=powered?poweredColor:connectedColor;
                var delta=to-from; line.rectTransform.anchoredPosition=(from+to)*.5f;
                line.rectTransform.sizeDelta=new Vector2(delta.magnitude+1,wireWidth);
                line.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
                pulse.gameObject.SetActive(powered); pulse.color=pulseColor;
                pulse.rectTransform.sizeDelta=Vector2.one*pulseSize;
                pulse.rectTransform.anchoredPosition=Vector2.Lerp(pulseFrom,pulseTo,Mathf.Repeat(elapsed/pulseSeconds,1));
            }
        }
    }
}
