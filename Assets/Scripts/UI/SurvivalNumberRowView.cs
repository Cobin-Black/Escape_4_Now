//Labels the fixed destination slots; number artwork can animate without moving these targets.
using Escape4Now.Puzzles;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class SurvivalNumberRowView : MonoBehaviour
    {
        [SerializeField] private Text[] targets;
        [SerializeField] private float targetOffset = 13;
        [SerializeField] private string targetFormat = "SLOT {0}";
        [SerializeField] private Color waitingColor = new Color(.45f,.65f,.72f,1);
        [SerializeField] private Color correctColor = new Color(.3f,1,.65f,1);
        private PuzzleSession puzzle; private PuzzleTokenUI[] cells;
        public void Present(PuzzleSession attempt,PuzzleTokenUI[] pieces){puzzle=attempt;cells=pieces;Draw();}
        private void OnEnable(){Canvas.willRenderCanvases+=Draw;}
        private void OnDisable(){Canvas.willRenderCanvases-=Draw;}
        private void Draw()
        {
            if(puzzle==null || !isActiveAndEnabled)return;
            for(int i=0;i<targets.Length;i++)
            {
                bool visible=i<puzzle.Values.Length;targets[i].gameObject.SetActive(visible);if(!visible)continue;
                var rect=(RectTransform)cells[i].transform;
                targets[i].transform.position=rect.TransformPoint(rect.rect.center+new Vector2(0,-rect.rect.height*.5f-targetOffset));
                targets[i].text=string.Format(targetFormat,i+1);
                targets[i].color=puzzle.Values[i]==i?correctColor:waitingColor;
            }
        }
    }
}
