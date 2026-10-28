// Positions a saved task, event or exit prefab and updates its label and state colors.

using UnityEngine;

namespace Escape4Now.Map
{
    public sealed class SurvivalWorldMarker : MonoBehaviour
    {
        [Header("Authored Prefab References")]
        [SerializeField]
        private SurvivalPropVisual geometry;
        [SerializeField]
        private TextMesh caption;
        [SerializeField]
        private SpriteRenderer indicator;
        [Header("Caption Readability")]
        [SerializeField] private SpriteRenderer captionBackground;
        [SerializeField] private Vector2 captionPadding = new Vector2(.16f, .1f);
        [SerializeField] private bool useCaptionColor;
        [SerializeField] private Color captionColor = Color.white;
        [SerializeField, Tooltip("Optional compact symbol instead of the event title. Leave empty for task names.")]
        private string captionOverride;
        [Header("State Colors")]
        [SerializeField]
        private Color availableColor = Color.cyan;
        [SerializeField]
        private Color completedColor = Color.green;
        [SerializeField]
        private Color lockedColor = Color.yellow;
        // Place the prefab from grid data and set its drawing order so it fits the isometric scene.
        public void Configure(SurvivalMap map, Vector2Int cell, string label, bool locked = false)
        {
            transform.position = map.GridToWorld(cell);
            if (TryGetComponent<PolygonCollider2D>(out var collider))
                collider.points = new[]
                {
                    new Vector2(0, map.TileSize.y * .5f),
                    new Vector2(map.TileSize.x * .5f, 0),
                    new Vector2(0, -map.TileSize.y * .5f),
                    new Vector2(-map.TileSize.x * .5f, 0)
                };
            int order = 500 - (cell.x + cell.y) * 10;
            if (geometry != null)
                geometry.Build(map.TileSize, Vector2.one, map.MapMaterial, order);
            if (caption != null)
            {
                caption.text = string.IsNullOrEmpty(captionOverride) ? label : captionOverride;
                caption.GetComponent<Renderer>().sortingOrder = order + 1;
                if (captionBackground != null) captionBackground.sortingOrder = order;
            }

            if (indicator != null)
                indicator.sortingOrder = order - 1;
            if (TryGetComponent<Escape4Now.Puzzles.SurvivalTaskBeacon>(out var beacon)) beacon.ConfigureSorting(order - 1);
            SetState(false, locked);
        }

        public void SetState(bool complete, bool locked = false)
        {
            Color color = complete ? completedColor : locked ? lockedColor : availableColor;
            if (indicator != null)
                indicator.color = color;
            if (caption != null)
                caption.color = useCaptionColor ? captionColor : color;
        }

        private void LateUpdate()
        {
            if (caption == null || captionBackground == null) return;
            Bounds bounds = caption.GetComponent<Renderer>().localBounds;
            captionBackground.size = new Vector2(bounds.size.x + captionPadding.x * 2f,
                bounds.size.y + captionPadding.y * 2f);
            captionBackground.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0f);
        }

        // Hiding a marker changes drawing only, so reveal toggles cannot remove its gameplay effect.
        public void SetVisible(bool visible)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = visible;
        }

        // Task-name toggles affect the label without hiding the actual station.
        public void SetCaptionVisible(bool visible)
        {
            if (caption != null) caption.GetComponent<Renderer>().enabled = visible;
            if (captionBackground != null) captionBackground.enabled = visible;
        }
    }
}
