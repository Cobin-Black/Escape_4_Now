// Shows a compact name above the current player and keeps the Race key badge clear of that name.

using Escape4Now.Multiplayer;
using Escape4Now.GameModes;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    [DefaultExecutionOrder(-10)]
    public sealed class ActivePlayerNameUI : MonoBehaviour
    {
        [Header("Unity UI References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text label;
        [SerializeField] private Image identityAccent;
        [SerializeField] private Image keyIcon;

        [Header("Placement and Size")]
        [SerializeField, Tooltip("Canvas-space offset above the top of the player's body.")]
        private Vector2 screenOffset = new Vector2(0f, 5f);
        [SerializeField] private Vector2 widthRange = new Vector2(48f, 180f);
        [SerializeField] private Vector2 textPadding = new Vector2(7f, 5f);
        [SerializeField, Min(0f)] private float keyIconSpacing = 4f;
        [SerializeField, Min(0f)] private float screenMargin = 8f;
        [SerializeField] private bool hideWhilePaused = true;

        private PlayerCharacter target;
        private SpriteRenderer body;
        private readonly Vector3[] corners = new Vector3[4];

        public Rect ScreenBounds
        {
            get
            {
                if (!panel.gameObject.activeInHierarchy) return default;
                panel.GetWorldCorners(corners);
                return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            }
        }

        public bool ShowsKeyFor(PlayerCharacter player) => target == player && panel.gameObject.activeInHierarchy
            && keyIcon.gameObject.activeSelf;

        private void LateUpdate()
        {
            var active = context.ActivePlayer;
            var camera = context.WorldCamera;
            bool visible = context.Playing && !context.Finished && active != null && active.isActiveAndEnabled
                && camera != null && (!hideWhilePaused || !LocalMultiplayerSession.GameplayPaused);
            if (active != target)
            {
                target = active;
                body = target != null ? target.GetComponent<SpriteRenderer>() : null;
            }
            visible &= body != null;
            Vector3 screen = Vector3.zero;
            if (visible)
            {
                Bounds bounds = body.bounds;
                screen = camera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
                visible = screen.z > 0f && camera.pixelRect.Contains(screen);
            }
            panel.gameObject.SetActive(visible);
            if (!visible) return;

            label.text = target.name;
            identityAccent.color = target.IdentityColor;
            var race = context.Session != null ? context.Session.Mode as Race_Gamemode : null;
            var keyLook = race != null && race.KeyPrefab != null ? race.KeyPrefab.GetComponentInChildren<SpriteRenderer>(true) : null;
            bool hasKey = target.HasKey && keyLook != null;
            keyIcon.gameObject.SetActive(hasKey);
            if (hasKey)
            {
                keyIcon.sprite = keyLook.sprite;
                keyIcon.color = keyLook.color;
            }
            // Reserve extra label space for the key icon so it cannot overlap the player's name.
            float keySpace = hasKey ? keyIcon.rectTransform.rect.width + keyIconSpacing : 0f;
            var textRect = label.rectTransform;
            textRect.offsetMin = new Vector2(textPadding.x + keySpace, textRect.offsetMin.y);
            textRect.offsetMax = new Vector2(-textPadding.y, textRect.offsetMax.y);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Clamp(label.preferredWidth + textPadding.x + textPadding.y + keySpace, widthRange.x, widthRange.y));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screen, null, out Vector2 position);
            position += screenOffset;
            Rect area = viewport.rect;
            Vector2 size = panel.rect.size;
            position.x = Mathf.Clamp(position.x, area.xMin + screenMargin + size.x * .5f,
                area.xMax - screenMargin - size.x * .5f);
            position.y = Mathf.Clamp(position.y, area.yMin + screenMargin, area.yMax - screenMargin - size.y);
            panel.anchoredPosition = position;
        }

        private void OnDisable()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }
    }
}
