// Draws the player's visible body and turn marker while keeping the player's identity color.

using UnityEngine;
using Escape4Now.Multiplayer;

namespace Escape4Now.Player
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class PlayerAppearance : MonoBehaviour
    {
        [Header("Player Visual References")]
        [SerializeField] private PlayerCharacter player;
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer outline;
        [SerializeField] private SpriteRenderer turnRing;
        [SerializeField] private SpriteRenderer turnGlow;
        [Header("Active Turn Indicator")]
        [SerializeField] private bool matchPlayerColor = true;
        [SerializeField, Tooltip("Custom color when Match Player Color is off. Alpha controls indicator opacity in both modes.")]
        private Color ringColor = new Color(0.22f, 0.86f, 1f, 0.9f);
        [SerializeField] private Vector2 ringSize = new Vector2(0.86f, 0.42f);
        [SerializeField, Range(0f, 1f)] private float glowStrength = 0.22f;
        [SerializeField, Min(0f)] private float pulseSpeed = 1.5f;
        [SerializeField, Range(0f, 0.15f)] private float pulseAmount = 0.035f;
        [SerializeField] private float groundOffset = 0.015f;
        [Header("Skin")]
        [SerializeField, Min(0.1f), Tooltip("Uniform body scale used once a skin sprite replaces the placeholder body.")]
        private float skinScale = 1f;
        [Header("Editor Preview")]
        [SerializeField, Tooltip("Show the saved player visuals outside Play Mode. Off while editing maps by default.")]
        private bool previewInEditMode;
        private Sprite skinFront;
        private Sprite skinBack;
        private bool facingBack;
        private bool facingRight;

        //The placeholder outline only fits the plain body shape, so it is hidden once a skin is on.
        public bool HasSkin => skinFront != null && skinBack != null;

        private void OnEnable()
        {
            SetVisible(Application.IsPlaying(gameObject) ? player != null && player.isActiveAndEnabled : previewInEditMode);
        }

        private void OnValidate()
        {
            if (!Application.IsPlaying(gameObject)) SetVisible(previewInEditMode);
        }

        private void OnDisable() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            if (body != null) body.enabled = visible;
            if (outline != null) outline.enabled = visible && !HasSkin;
            if (turnRing != null) turnRing.enabled = visible;
            if (turnGlow != null) turnGlow.enabled = visible;
        }

        public void ApplyIdentity(Color color)
        {
            if (body != null && !HasSkin) body.color = color;
        }

        //Swaps the placeholder body for a skin. The player starts facing forward (down and to the left).
        public void ApplySkin(Sprite front, Sprite back)
        {
            if (body == null || front == null || back == null) return;
            skinFront = front;
            skinBack = back;
            body.color = Color.white;
            body.transform.localScale = new Vector3(skinScale, skinScale, 1f);
            if (outline != null) outline.enabled = false;
            facingBack = false;
            facingRight = false;
            RefreshFacing();
        }

        //Turns the skin toward the on-screen direction the player just moved.
        //Down-left: front. Down-right: front flipped. Up-right: back. Up-left: back flipped.
        public void FaceToward(Vector2 screenDirection)
        {
            if (!HasSkin) return;
            const float threshold = 0.0001f;
            if (Mathf.Abs(screenDirection.y) > threshold) facingBack = screenDirection.y > 0f;
            if (Mathf.Abs(screenDirection.x) > threshold) facingRight = screenDirection.x > 0f;
            RefreshFacing();
        }

        private void RefreshFacing()
        {
            body.sprite = facingBack ? skinBack : skinFront;
            body.flipX = facingBack ? !facingRight : facingRight;
        }

        private void LateUpdate()
        {
            if (!Application.IsPlaying(gameObject))
            {
                SetVisible(previewInEditMode);
                return;
            }
            if (player == null || body == null) return;
            //Editor preview/startup callbacks can hide the saved source player before gameplay is enabled.
            //Derive body visibility from the live player, just as the turn ring follows the live turn.
            bool visible = player.isActiveAndEnabled;
            body.enabled = visible;
            if (outline != null)
            {
                outline.enabled = visible && !HasSkin;
                outline.sortingLayerID = body.sortingLayerID;
                outline.sortingOrder = body.sortingOrder + 1;
            }
            bool active = visible && player.IsCurrentTurn
                && !LocalMultiplayerSession.MatchHasEnded
                && (LocalMultiplayerSession.MatchIsRunning || player.Mode == null);
            float pulse = Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f);
            Color indicator = matchPlayerColor ? player.IdentityColor : ringColor;
            indicator.a = ringColor.a;
            ApplyRing(turnRing, active, indicator, pulse);
            Color glow = indicator;
            glow.a *= glowStrength;
            ApplyRing(turnGlow, active, glow, pulse);
        }

        private void ApplyRing(SpriteRenderer renderer, bool active, Color color, float pulse)
        {
            if (renderer == null) return;
            renderer.enabled = active;
            if (!active || renderer.sprite == null) return;
            renderer.color = color;
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = body.sortingOrder - 1;
            Transform ring = renderer.transform;
            ring.position = transform.position + Vector3.up * groundOffset;
            Vector3 parentScale = ring.parent.lossyScale;
            Vector2 bounds = renderer.sprite.bounds.size;
            float scale = 1f + pulse * pulseAmount;
            ring.localScale = new Vector3(ringSize.x * scale / (bounds.x * parentScale.x),
                ringSize.y * scale / (bounds.y * parentScale.y), 1f);
        }
    }
}
