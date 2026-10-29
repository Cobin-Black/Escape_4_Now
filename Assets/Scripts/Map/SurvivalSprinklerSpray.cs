//Draws moving water streams over the existing safe wet patch. It adds no colliders or movement rules.
using UnityEngine;

namespace Escape4Now.Map
{
    public sealed class SurvivalSprinklerSpray : MonoBehaviour
    {
        [Header("Saved Water Streams")]
        [SerializeField] private LineRenderer[] streams;
        [Header("Water Motion")]
        [SerializeField, Min(.1f)] private float reach = .85f;
        [SerializeField, Min(0)] private float arcHeight = .35f;
        [SerializeField, Min(.1f)] private float flightSeconds = .75f;
        [SerializeField, Range(.1f,1)] private float streamLength = .42f;
        [SerializeField, Range(0,180)] private float fanAngle = 110;
        [SerializeField, Min(0)] private float sweepAngle = 50;
        [SerializeField, Min(.1f)] private float sweepSeconds = 3;
        [SerializeField] private float groundProjection = .5f;
        [SerializeField] private Vector3 nozzleOffset = new Vector3(0,.1f,-.05f);
        private float elapsed;
        private void Update()
        {
            if(Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused
                || Escape4Now.Multiplayer.LocalMultiplayerSession.MatchHasEnded) return;
            elapsed += Time.deltaTime;
            for(int jet=0;jet<streams.Length;jet++)
            {
                var line=streams[jet];
                float phase=jet/(float)streams.Length;
                float angle=(phase*fanAngle-fanAngle*.5f+Mathf.Sin(elapsed/sweepSeconds*Mathf.PI*2)*sweepAngle)*Mathf.Deg2Rad;
                var landing=new Vector3(Mathf.Cos(angle)*reach,Mathf.Sin(angle)*reach*groundProjection,0);
                float head=Mathf.Repeat(elapsed/flightSeconds+phase,1+streamLength);
                for(int point=0;point<line.positionCount;point++)
                {
                    float t=Mathf.Clamp01(head-streamLength+point*streamLength/(line.positionCount-1));
                    line.SetPosition(point,nozzleOffset+landing*t+Vector3.up*(arcHeight*4*t*(1-t)));
                }
            }
        }
    }
}
