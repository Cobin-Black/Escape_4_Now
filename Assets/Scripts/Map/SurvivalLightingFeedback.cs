//Turns off the bright lamp-head colors during Blackout. Exit signals remain available as emergency lights.
using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Map
{
    public sealed class SurvivalLightingFeedback : MonoBehaviour
    {
        [SerializeField] private SurvivalEventController events;
        [SerializeField] private SurvivalMap map;
        [SerializeField, Range(0,1)] private float brightThreshold = .7f;
        [SerializeField] private Color unlitColor = new Color(.15f,.19f,.21f,1);
        private readonly Dictionary<Mesh,Color[]> original = new Dictionary<Mesh,Color[]>();
        private bool dark;
        private bool captured;
        private void Update()
        {
            if (events == null || map == null || events.MatchPlan == null) return;
            if (!captured)
            {
                captured = true;
                foreach (var prop in map.GetComponentsInChildren<SurvivalPropVisual>())
                    if (prop.Kind == OutdoorPropKind.LightPole)
                        foreach (var filter in prop.GetComponentsInChildren<MeshFilter>())
                            if (filter.sharedMesh != null) original[filter.sharedMesh] = filter.sharedMesh.colors;
            }
            bool next = events.BlackoutTurns > 0;
            if (next == dark) return;
            dark = next;
            foreach (var pair in original)
            {
                if (pair.Key == null) continue;
                var colors = (Color[])pair.Value.Clone();
                if (dark)
                    for (int i = 0; i < colors.Length; i++)
                        if (colors[i].grayscale >= brightThreshold) colors[i] = unlitColor;
                pair.Key.colors = colors;
            }
        }
        private void OnDisable() { foreach (var pair in original) if (pair.Key != null) pair.Key.colors = pair.Value; }
    }
}
