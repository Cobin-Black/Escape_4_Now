// Draws merged mud, wet ground and track paint from safe cells. It does not block tiles or change movement costs.

using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Map
{
    public enum EnvironmentalSurfaceStyle { Mud, WetGround, TrackPaint }

    //Draws one connected ground region from the controller's safe cells. Never changes walkability.
    public sealed class SurvivalEnvironmentalSurface : MonoBehaviour
    {
        [Header("Surface and Existing Prefab Visuals")]
        [SerializeField] private EnvironmentalSurfaceStyle style;
        [SerializeField] private GameObject[] replacedVisuals = new GameObject[0];
        [SerializeField] private Material materialOverride;
        [Header("Ground Appearance")]
        [SerializeField] private Color groundColor = new Color(.25f,.17f,.09f,1);
        [SerializeField] private Color variationColor = new Color(.38f,.27f,.15f,1);
        [SerializeField] private Color puddleColor = new Color(.42f,.39f,.28f,.7f);
        [SerializeField, Range(.05f,.48f)] private float edgeRoundness = .32f;
        [SerializeField, Range(2,12)] private int cornerSegments = 5;
        [SerializeField, Min(.01f)] private float grainScale = 1.4f;
        [SerializeField, Range(0,1)] private float puddleChance = .45f;
        [SerializeField] private Vector2 puddleRadius = new Vector2(.22f,.14f);
        [Header("Track Paint")]
        [SerializeField] private Color paintColor = new Color(1f,.9f,.42f,1);
        [SerializeField, Range(.1f,.7f)] private float chevronSize = .6f;
        [SerializeField, Min(.005f)] private float paintWidth = .04f;
        [Header("Rendering")]
        [SerializeField] private int groundSortingOrder = -9440;
        [SerializeField] private int paintSortingOrder = -9430;
        private readonly List<Mesh> meshes = new List<Mesh>();

        // Draw only the safe cells supplied by placement; this class never adds a collider or changes the grid.
        public void Configure(SurvivalMap map, IEnumerable<Vector2Int> region)
        {
            foreach (var legacy in replacedVisuals) if (legacy != null) legacy.SetActive(false);
            var cells = new HashSet<Vector2Int>(region);
            if (style == EnvironmentalSurfaceStyle.TrackPaint) { PaintTrack(map, cells); return; }
            var points = new List<Vector2>(); var triangles = new List<int>(); var colors = new List<Color>();
            var puddles = new List<Vector2>(); var puddleTriangles = new List<int>(); var puddleColors = new List<Color>();
            foreach (var cell in cells)
            {
                var boundary = new List<Vector2>();
                //Only exposed corners round off; adjoining cells share their full edge without seams.
                for (int corner = 0; corner < 4; corner++)
                {
                    int sx = corner == 0 || corner == 3 ? 1 : -1;
                    int sy = corner < 2 ? 1 : -1;
                    bool exposed = !cells.Contains(cell + new Vector2Int(sx,0)) && !cells.Contains(cell + new Vector2Int(0,sy));
                    float radius = exposed ? edgeRoundness : 0;
                    var center = (Vector2)cell + new Vector2(sx * (.5f-radius), sy * (.5f-radius));
                    int segments = exposed ? cornerSegments : 1;
                    for (int step = 0; step <= segments; step++)
                    {
                        float angle = (corner + step / (float)segments) * Mathf.PI * .5f;
                        boundary.Add(center + new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)) * radius);
                    }
                }
                AddPolygon(cell, boundary, points, triangles, colors, false);
                float noise = Mathf.PerlinNoise(cell.x * grainScale + map.LayoutSeed % 997, cell.y * grainScale);
                if (noise < puddleChance)
                {
                    var outline = new List<Vector2>();
                    var center = (Vector2)cell + new Vector2((noise-.5f)*.3f,0);
                    for (int i=0;i<cornerSegments*4;i++)
                    {
                        float angle = i * Mathf.PI * 2 / (cornerSegments*4);
                        float ripple = .9f + .1f * Mathf.Sin(angle*3 + noise*6);
                        outline.Add(center + new Vector2(Mathf.Cos(angle)*puddleRadius.x, Mathf.Sin(angle)*puddleRadius.y)*ripple);
                    }
                    AddPolygon(center, outline, puddles, puddleTriangles, puddleColors, true);
                }
            }
            Draw(map, style == EnvironmentalSurfaceStyle.Mud ? "Muddy Ground" : "Continuous wet ground", points, triangles, colors, groundSortingOrder);
            Draw(map, "Shallow irregular puddles", puddles, puddleTriangles, puddleColors, groundSortingOrder+1);
        }

        // Fill a ground outline with triangles sharing a center point.
        private void AddPolygon(Vector2 center, List<Vector2> outline, List<Vector2> vertices, List<int> indices, List<Color> colors, bool puddle)
        {
            int start = vertices.Count; vertices.Add(center);
            colors.Add(puddle ? puddleColor : GroundColor(center));
            for (int i=0;i<outline.Count;i++)
            {
                vertices.Add(outline[i]); colors.Add(puddle ? puddleColor : GroundColor(outline[i]));
                indices.Add(start); indices.Add(start+i+1); indices.Add(start+(i+1)%outline.Count+1);
            }
        }
        private Color GroundColor(Vector2 point) => Color.Lerp(groundColor, variationColor,
            Mathf.PerlinNoise(point.x*grainScale,point.y*grainScale));

        // Turn each chevron along the oval and shrink it if needed to keep it within the running surface.
        private void PaintTrack(SurvivalMap map, HashSet<Vector2Int> cells)
        {
            OutdoorArea track = null;
            foreach (var area in map.Layout.Areas) if (area.Kind == OutdoorAreaKind.TrackField) { track = area; break; }
            if (track == null) return;
            var center = new Vector2(track.X+(track.Width-1)*.5f,track.Y+(track.Height-1)*.5f);
            var radius = new Vector2(track.Width,track.Height)*.5f;
            foreach (var cell in cells)
            {
                var offset = (Vector2)cell-center;
                var tangent = new Vector2(-offset.y/(radius.y*radius.y), offset.x/(radius.x*radius.x)).normalized;
                var across = new Vector2(-tangent.y,tangent.x);
                float size = chevronSize;
                Vector2[] points = null;
                for (int attempt=0;attempt<4;attempt++,size*=.7f)
                {
                    points = new[] { (Vector2)cell-tangent*size*.4f+across*size*.5f,
                        (Vector2)cell+tangent*size*.4f, (Vector2)cell-tangent*size*.4f-across*size*.5f };
                    bool fits = true;
                    foreach(var point in points)
                    {
                        var p = point-center; var inner = radius-Vector2.one*map.TrackWidth;
                        if (p.x*p.x/(radius.x*radius.x)+p.y*p.y/(radius.y*radius.y)>1
                            || p.x*p.x/(inner.x*inner.x)+p.y*p.y/(inner.y*inner.y)<1) fits=false;
                    }
                    if(fits) break;
                    points=null;
                }
                if(points==null)continue;
                var item = new GameObject("Lane-aligned boost paint"); item.transform.SetParent(transform,false);
                var line = item.AddComponent<LineRenderer>(); line.useWorldSpace=false;
                line.sharedMaterial=materialOverride!=null?materialOverride:map.MapMaterial;
                line.positionCount=points.Length; line.startWidth=line.endWidth=paintWidth;
                line.startColor=line.endColor=paintColor; line.sortingOrder=paintSortingOrder;
                for(int i=0;i<points.Length;i++)line.SetPosition(i,item.transform.InverseTransformPoint(map.GridToWorld(points[i])));
            }
        }

        // Convert grid points into local mesh positions so the surface follows the current map size.
        private void Draw(SurvivalMap map,string label,List<Vector2> points,List<int> triangles,List<Color> colors,int order)
        {
            if(points.Count==0)return;
            var item=new GameObject(label); item.transform.SetParent(transform,false);
            var vertices=new Vector3[points.Count];
            for(int i=0;i<points.Count;i++)vertices[i]=item.transform.InverseTransformPoint(map.GridToWorld(points[i]));
            var mesh=new Mesh { name=label }; mesh.vertices=vertices; mesh.triangles=triangles.ToArray(); mesh.colors=colors.ToArray();
            mesh.RecalculateBounds(); meshes.Add(mesh); item.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=item.AddComponent<MeshRenderer>();renderer.sharedMaterial=materialOverride!=null?materialOverride:map.MapMaterial;
            renderer.sortingOrder=order;
        }
        // Release only the meshes created for this instance when its event visual is removed.
        private void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}
