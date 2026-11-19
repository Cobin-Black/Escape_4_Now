// Keeps a saved UI panel inside the available screen space without changing gameplay.

using UnityEngine;

namespace Escape4Now.UI
{
    public sealed class ResponsivePanel : MonoBehaviour
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private Vector2 edgePadding = new Vector2(16f, 16f);
        [SerializeField] private float reservedRightWidth;
        // Extra width to keep free beside the content (e.g. a panel attached to its side).
        public float ReservedRightWidth { get => reservedRightWidth; set => reservedRightWidth = Mathf.Max(0f, value); }
        // Fit the content inside the current viewport without placing it under reserved UI space.
        private void LateUpdate()
        {
            if (viewport == null || content == null) return;
            float width = Mathf.Max(1f, viewport.rect.width - edgePadding.x * 2f - reservedRightWidth);
            float height = Mathf.Max(1f, viewport.rect.height - edgePadding.y * 2f);
            float scale = Mathf.Min(1f, width / content.rect.width, height / content.rect.height);
            content.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
