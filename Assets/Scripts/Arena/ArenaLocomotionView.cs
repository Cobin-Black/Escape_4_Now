using UnityEngine;

namespace Escape4Now.Arena
{
    // Animate a separate drawing, never the collision root, identity, skin choice or ground ring.
    [DefaultExecutionOrder(300), RequireComponent(typeof(ArenaParticipant), typeof(SpriteRenderer))]
    public sealed class ArenaLocomotionView : MonoBehaviour
    {
        [Header("Stride with Existing Skin Sprites")]
        [SerializeField, Min(0)]
        private float strideCyclesPerUnit = .9f;
        [SerializeField, Min(0)]
        private float stepHeight = .025f;
        [SerializeField, Range(0, 5)]
        private float swayDegrees = 1.5f;
        [SerializeField, Range(0, .1f)]
        private float stretchAmount = .018f;
        [SerializeField, Min(.1f)]
        private float settleSpeed = 16;
        [SerializeField, Min(.001f)]
        private float movementThreshold = .02f;
        private ArenaParticipant player;
        private SpriteRenderer source, drawing;
        private float phase, blend;
        private void Awake()
        {
            player = GetComponent<ArenaParticipant>();
            source = GetComponent<SpriteRenderer>();
            drawing = new GameObject("Animated arena body").AddComponent<SpriteRenderer>();
            drawing.transform.SetParent(transform, false);
        }

        private void LateUpdate()
        {
            if (player.Match == null)
                return;
            drawing.sprite = source.sprite;
            drawing.color = source.color;
            drawing.flipX = source.flipX;
            drawing.flipY = source.flipY;
            drawing.sharedMaterial = source.sharedMaterial;
            drawing.sortingLayerID = source.sortingLayerID;
            drawing.sortingOrder = source.sortingOrder;
            drawing.enabled = player.Alive && source.sprite != null;
            source.enabled = false;
            if (player.Match.Paused || !player.Match.Playing)
                return;
            float speed = player.ScreenVelocity.magnitude;
            bool moving = speed > movementThreshold;
            blend = Mathf.MoveTowards(blend, moving ? 1 : 0, settleSpeed * Time.deltaTime);
            if (moving)
                phase += speed * Time.deltaTime * strideCyclesPerUnit * Mathf.PI * 2;
            float step = Mathf.Abs(Mathf.Sin(phase)) * blend;
            drawing.transform.localPosition = Vector3.up * (step * stepHeight);
            drawing.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase) * swayDegrees * blend);
            drawing.transform.localScale = new Vector3(1 - step * stretchAmount, 1 + step * stretchAmount, 1);
        }

        private void OnDisable()
        {
            if (drawing != null)
                drawing.enabled = false;
            if (source != null)
                source.enabled = gameObject.activeInHierarchy && player != null && player.Alive;
        }

        private void OnEnable()
        {
            if (drawing != null)
                drawing.enabled = true;
        }
    }
}
