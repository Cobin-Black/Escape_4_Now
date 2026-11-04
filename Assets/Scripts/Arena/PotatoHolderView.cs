using UnityEngine;

namespace Escape4Now.Arena
{
    public sealed class PotatoHolderView : MonoBehaviour
    {
        [Header("Saved References")]
        [SerializeField]
        private HotPotatoController mode;
        [SerializeField]
        private GameObject marker;
        [SerializeField]
        private SpriteRenderer potato, glow;
        [SerializeField]
        private TextMesh countdown;
        [SerializeField]
        private AudioSource audioSource;
        [SerializeField]
        private AudioClip tick;
        [Header("Holder Feedback")]
        [SerializeField]
        private Vector3 offset = new Vector3(.35f, 1.2f, 0);
        [SerializeField]
        private Color normal = new Color(1, .65f, .15f), urgent = new Color(1, .15f, .12f);
        [SerializeField, Range(.05f, .9f)]
        private float urgentFraction = .3f;
        [SerializeField]
        private Vector2 tickInterval = new Vector2(.14f, .8f);
        [SerializeField, Range(0, 1)]
        private float tickVolume = .12f;
        [SerializeField, Range(0, .5f)]
        private float pulseSize = .13f;
        [SerializeField]
        private int markerSortingOrder = 2500;
        private float clock, nextTick;
        // Follow the real holder and show the existing countdown; this component never changes the timer.
        private void LateUpdate()
        {
            bool visible = mode != null && mode.Playing && !mode.BetweenRounds && mode.Holder != null;
            marker.SetActive(visible);
            if (!visible)
                return;
            marker.transform.position = mode.Holder.transform.position + offset;
            float fraction = mode.RemainingSeconds / mode.RoundSeconds;
            if (!mode.Paused)
            {
                clock += Time.deltaTime;
                nextTick -= Time.deltaTime;
            }

            float interval = Mathf.Lerp(tickInterval.x, tickInterval.y, fraction);
            float pulse = (Mathf.Sin(clock * Mathf.PI * 2 / Mathf.Max(.05f, interval)) + 1) * .5f;
            marker.transform.localScale = Vector3.one * (1 + pulse * pulseSize);
            potato.color = fraction < urgentFraction ? Color.Lerp(normal, urgent, pulse) : normal;
            glow.color = new Color(potato.color.r, potato.color.g, potato.color.b, .15f + .25f * pulse);
            potato.sortingOrder = markerSortingOrder;
            glow.sortingOrder = potato.sortingOrder - 1;
            countdown.text = Mathf.CeilToInt(mode.RemainingSeconds).ToString();
            countdown.GetComponent<Renderer>().sortingOrder = potato.sortingOrder + 1;
            if (!mode.Paused && nextTick <= 0)
            {
                nextTick = interval;
                if (audioSource != null && tick != null)
                    audioSource.PlayOneShot(tick, tickVolume);
            }
        }
    }
}
