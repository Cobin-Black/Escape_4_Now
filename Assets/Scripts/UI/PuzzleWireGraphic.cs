//Draws a cable in the saved Unity UI; the matching view owns input and answer checks.
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PuzzleWireGraphic : MaskableGraphic
    {
        [Header("Cable Shape")]
        [SerializeField, Range(4,48)] private int segments = 20;
        [SerializeField] private float bend = -12f;
        [SerializeField, Min(0)] private float outlineWidth = 1.5f;
        [SerializeField] private Color outlineColor = new Color(0,0,0,.65f);
        private Vector2 start, end;
        private float thickness = 5;
        public void SetCable(Vector2 from,Vector2 to,Color tint,float width)
        {
            if (start == from && end == to && color == tint && Mathf.Approximately(thickness,width)) return;
            start = from; end = to; color = tint; thickness = width; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if ((end-start).sqrMagnitude < .01f) return;
            if (outlineWidth > 0) Strip(helper,thickness+outlineWidth*2,outlineColor);
            Strip(helper,thickness,color);
        }
        private void Strip(VertexHelper helper,float width,Color tint)
        {
            int offset = helper.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float t = i/(float)segments;
                var point = Vector2.Lerp(start,end,t)+Vector2.up*(bend*4*t*(1-t));
                var tangent = end-start+Vector2.up*(bend*(4-8*t));
                var normal = new Vector2(-tangent.y,tangent.x).normalized*width*.5f;
                helper.AddVert(point-normal,tint,Vector2.zero); helper.AddVert(point+normal,tint,Vector2.zero);
                if (i == 0) continue;
                int vertex = offset+(i-1)*2;
                helper.AddTriangle(vertex,vertex+1,vertex+2); helper.AddTriangle(vertex+1,vertex+3,vertex+2);
            }
        }
    }
}
