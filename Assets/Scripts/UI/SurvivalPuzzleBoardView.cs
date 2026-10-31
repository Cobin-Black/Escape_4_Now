//Arranges the saved puzzle controls and headings. It does not change answers or input bindings.
using Escape4Now.Puzzles;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class SurvivalPuzzleBoardView : MonoBehaviour
    {
        [Header("Saved Board References")]
        [SerializeField] private RectTransform matchingArea;
        [SerializeField] private Text leftHeading, rightHeading;
        [Header("Matching Layout")]
        [SerializeField, Min(20)] private float rowHeight = 44;
        [SerializeField, Min(0)] private float rowGap = 8;
        [SerializeField, Min(0)] private float headingSpace = 28;
        [SerializeField, Range(.2f,.45f)] private float columnWidth = .36f;
        [SerializeField, Range(.1f,.3f)] private float columnCenter = .2f;
        [SerializeField] private string[] sourceHeadings = { "WIRE ENDS", "SUPPLIES", "BATTERIES" };
        [SerializeField] private string[] targetHeadings = { "SOCKETS", "CONTAINERS", "DEVICE SLOTS" };
        public void Present(SurvivalPuzzleType type,int count,PuzzleTokenUI[] left,PuzzleTokenUI[] right)
        {
            int theme = type == SurvivalPuzzleType.MatchWires ? 0 : type == SurvivalPuzzleType.SortSupplies ? 1 : 2;
            leftHeading.text = sourceHeadings[theme]; rightHeading.text = targetHeadings[theme];
            var size = matchingArea.rect.size;
            float height = Mathf.Min(rowHeight,(size.y-headingSpace-rowGap*(count-1))/count);
            float width = size.x*columnWidth;
            for(int i=0;i<count;i++)
            {
                Place((RectTransform)left[i].transform,columnCenter,i);
                Place((RectTransform)right[i].transform,1-columnCenter,i);
            }
            void Place(RectTransform rect,float x,int index)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(x,1);
                rect.pivot = new Vector2(.5f,.5f);
                rect.sizeDelta = new Vector2(width,height);
                rect.anchoredPosition = new Vector2(0,-headingSpace-height*.5f-index*(height+rowGap));
            }
        }
    }
}
