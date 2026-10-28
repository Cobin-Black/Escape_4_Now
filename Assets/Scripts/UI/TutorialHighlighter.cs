// Highlights the current lesson's target using the tutorial's existing screen-space UI.

using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class TutorialHighlighter : MonoBehaviour
    {
        [Header("Unity UI References")]
        [SerializeField] private RectTransform canvas;
        [SerializeField] private RectTransform outline;
        [SerializeField] private Image[] shades;
        [Header("Highlight Animation")]
        [SerializeField, Min(0f)] private float padding = 6f;
        [SerializeField, Min(0f)] private float dimSeconds = 1.6f;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.8f;
        [SerializeField, Min(0f)] private float pulseSpeed = 5f;
        [SerializeField] private Color shadeColor = new Color(0.01f, 0.02f, 0.05f, 0.65f);
        [SerializeField] private Color highlightColor = new Color(0.5f, 0.77f, 1f, 1f);

        public void Show(Rect? target, float elapsed, bool finished)
        {
            outline.gameObject.SetActive(target.HasValue);
            float dim = finished ? 1f : target.HasValue && dimSeconds > 0f
                ? Mathf.Clamp01(1f - (elapsed - dimSeconds) / fadeSeconds) : 0f;
            Rect hole = target ?? default;
            if (target.HasValue)
            {
                hole.xMin -= padding; hole.yMin -= padding;
                hole.xMax += padding; hole.yMax += padding;
                Place(outline, hole);
                Color color = highlightColor;
                color.a *= 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
                outline.GetComponent<Image>().color = color;
            }
            var rectangles = new[]
            {
                new Rect(0f, 0f, Screen.width, Mathf.Clamp(hole.yMin, 0f, Screen.height)),
                new Rect(0f, Mathf.Clamp(hole.yMax, 0f, Screen.height), Screen.width, Mathf.Max(0f, Screen.height - hole.yMax)),
                new Rect(0f, Mathf.Max(0f, hole.yMin), Mathf.Max(0f, hole.xMin), Mathf.Max(0f, hole.height)),
                new Rect(Mathf.Max(0f, hole.xMax), Mathf.Max(0f, hole.yMin), Mathf.Max(0f, Screen.width - hole.xMax), Mathf.Max(0f, hole.height))
            };
            if (finished) { rectangles[0] = new Rect(0f, 0f, Screen.width, Screen.height); }
            for (int i = 0; i < shades.Length; i++)
            {
                bool visible = dim > 0f && (!finished || i == 0);
                shades[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var color = shadeColor; color.a *= dim;
                shades[i].color = color;
                Place((RectTransform)shades[i].transform, rectangles[i]);
            }
        }

        private void Place(RectTransform rect, Rect screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, new Vector2(screen.xMin, Screen.height - screen.yMin), null, out var topLeft);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, new Vector2(screen.xMax, Screen.height - screen.yMax), null, out var bottomRight);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = new Vector2(bottomRight.x - topLeft.x, topLeft.y - bottomRight.y);
        }
    }
}
