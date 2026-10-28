// Displays one compact screen-space marker and lets the marker manager position it.

using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class WorldMarkerUI : MonoBehaviour
    {
        [SerializeField] private RectTransform rect;
        [SerializeField] private Image icon;
        [SerializeField] private Text label;
        [SerializeField] private Image border;
        private readonly Vector3[] corners = new Vector3[4];
        public Rect ScreenBounds
        {
            get
            {
                rect.GetWorldCorners(corners);
                return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            }
        }

        public void MoveBottomTo(float screenY)
        {
            rect.position += Vector3.up * (screenY - ScreenBounds.yMin);
        }

        public void Present(Vector2 screen, Sprite sprite, Color tint, string text, Color accent)
        {
            rect.position = screen;
            icon.gameObject.SetActive(sprite != null);
            label.gameObject.SetActive(sprite == null);
            icon.sprite = sprite;
            icon.color = tint;
            label.text = text;
            label.color = border.color = accent;
        }
    }
}
