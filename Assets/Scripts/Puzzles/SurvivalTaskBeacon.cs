// Highlights a task only when the current survivor can interact with it.

using UnityEngine;

namespace Escape4Now.Puzzles
{
    public sealed class SurvivalTaskBeacon : MonoBehaviour
    {
        [Header("Nearby Task Highlight")]
        [SerializeField] private SpriteRenderer ring;
        [SerializeField] private Color readyColor = new Color(.25f, .9f, 1f, .9f);
        [SerializeField, Min(.1f)] private float pulseSeconds = 2f;
        [SerializeField, Range(0, 1)] private float minimumOpacity = .45f;
        private bool available;
        private float elapsed;
        public void ConfigureSorting(int order) { if (ring != null) ring.sortingOrder = order; }
        public void SetAvailable(bool value)
        {
            available = value;
            if (ring != null) ring.enabled = value;
        }
        private void LateUpdate()
        {
            if (!available || ring == null || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused) return;
            elapsed += Time.deltaTime;
            var color = readyColor;
            color.a *= Mathf.Lerp(minimumOpacity, 1f, (Mathf.Sin(elapsed * Mathf.PI * 2 / pulseSeconds) + 1) * .5f);
            ring.color = color;
        }
    }
}
