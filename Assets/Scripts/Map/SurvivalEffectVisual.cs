// Animates local environmental highlights and sprinkler spray; the event controller decides when the effect ends.

using UnityEngine;

namespace Escape4Now.Map
{
    //Cosmetic animation only. The event controllers own placement, costs and expiry.
    public sealed class SurvivalEffectVisual : MonoBehaviour
    {
        [Header("Authored Visual References")]
        [SerializeField] private SpriteRenderer[] highlights = new SpriteRenderer[0];
        [SerializeField] private Transform rotatingSpray;
        [Header("Animation")]
        [SerializeField, Min(.1f)] private float pulseSeconds = 2f;
        [SerializeField, Range(0, 1)] private float minimumOpacity = .35f;
        [SerializeField, Range(0, 1)] private float maximumOpacity = .8f;
        [SerializeField] private float sprayDegreesPerSecond = 45f;
        private float elapsed;
        private void Update()
        {
            if (Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused
                || Escape4Now.Multiplayer.LocalMultiplayerSession.MatchHasEnded) return;
            elapsed += Time.deltaTime;
            for (int i = 0; i < highlights.Length; i++)
            {
                if (highlights[i] == null) continue;
                Color color = highlights[i].color;
                color.a = Mathf.Lerp(minimumOpacity, maximumOpacity,
                    (Mathf.Sin(elapsed * Mathf.PI * 2f / pulseSeconds + i * Mathf.PI) + 1f) * .5f);
                highlights[i].color = color;
            }
            if (rotatingSpray != null) rotatingSpray.Rotate(0, 0, sprayDegreesPerSecond * Time.deltaTime);
        }
    }
}
