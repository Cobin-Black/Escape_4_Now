using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.Arena
{
    // Reuses Race/Survival's blackout shader with a uniform shade, so no human player gets a private visibility advantage.
    public sealed class ArenaEventLightingUI : MonoBehaviour
    {
        [SerializeField]
        private ArenaEventDirector events;
        [SerializeField]
        private Graphic shade;
        private Material material;
        private void Awake()
        {
            // Supply the UI texture explicitly: the shared lighting shader has no sprite texture property.
            if (shade is RawImage raw)
                raw.texture = Texture2D.whiteTexture;
            if (events == null || events.Pool == null || events.Pool.visuals == null || events.Pool.visuals.blackoutMaterial == null)
                return;
            material = new Material(events.Pool.visuals.blackoutMaterial);
            material.SetFloat("_Radius", 0);
            shade.material = material;
        }

        private void LateUpdate()
        {
            if (events == null || events.Pool == null || events.Pool.visuals == null)
            {
                shade.color = Color.clear;
                return;
            }

            if (events.Match != null && events.Match.Paused)
                return;
            float alpha = events.Match != null && events.Match.Playing && events.BlackoutActive ? events.Pool.visuals.blackoutOpacity : 0;
            var color = Color.black;
            color.a = Mathf.MoveTowards(shade.color.a, alpha, Time.deltaTime * events.Pool.visuals.fadeSpeed);
            shade.color = color;
        }

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }
    }
}
