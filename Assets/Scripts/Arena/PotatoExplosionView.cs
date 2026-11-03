using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Arena
{
    // Reuses the shared burst prefab and keeps the effect independent of the eliminated player.
    public sealed class PotatoExplosionView : MonoBehaviour
    {
        [Header("Scene and Prefab References")]
        [SerializeField]
        private HotPotatoController mode;
        [SerializeField]
        private SurvivalWorldBurst explosionPrefab;
        [Header("Explosion Presentation")]
        [SerializeField]
        private Vector3 offset = new Vector3(0, .35f, 0);
        [SerializeField, Min(.1f)]
        private float scale = 1;
        private void OnEnable()
        {
            if (mode != null)
                mode.PotatoExploded += Explode;
        }

        private void OnDisable()
        {
            if (mode != null)
                mode.PotatoExploded -= Explode;
        }

        private void Explode(Vector3 position)
        {
            if (explosionPrefab == null)
                return;
            var burst = Instantiate(explosionPrefab, position + offset, Quaternion.identity, transform);
            burst.transform.localScale = Vector3.one * scale;
            var animation = burst.GetComponent<ArenaExplosionAnimation>();
            if (animation != null)
                animation.ConfigurePause(() => mode != null && mode.Paused);
            var layout = mode.Map.Layout;
            // Finish the final explosion even after the result appears. Clear it on setup/restart.
            burst.ConfigureContext(() => mode != null && mode.Paused, () => mode == null || mode.Map.Layout != layout || (!mode.Playing && string.IsNullOrEmpty(mode.Result)));
        }
    }
}
