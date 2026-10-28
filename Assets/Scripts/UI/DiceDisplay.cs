using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class DiceDisplay : MonoBehaviour
    {
        [Header("Saved Face Images")]
        [SerializeField] private Image face;
        [SerializeField] private Sprite[] faces;
        [SerializeField] private Text overflow;
        [Header("Animation")]
        [SerializeField, Min(0f)] private float animationSpeed = 18f;
        [Header("Two Dice (Double Dice)")]
        [SerializeField, Range(0.3f, 1f), Tooltip("Size of each die when two are shown in the same space.")]
        private float pairScale = 0.68f;

        //A copy of this die, made the first time two dice are shown.
        private DiceDisplay partner;
        private bool hasLayout;
        private Vector2 singlePosition;
        private Vector3 singleScale;

        public void Show(int value, bool rolling) => Show(value, 0, rolling);

        //Shows one die, or two dice side by side when second is above 0 (each die keeps its own face).
        public void Show(int first, int second, bool rolling)
        {
            var rect = (RectTransform)transform;
            if (!hasLayout)
            {
                singlePosition = rect.anchoredPosition;
                singleScale = rect.localScale;
                hasLayout = true;
            }
            bool pair = second > 0;
            if (pair && partner == null) CreatePartner();

            ShowFace(rolling ? RollingNumber(0) : first);
            rect.localScale = pair ? singleScale * pairScale : singleScale;
            rect.anchoredPosition = singlePosition;
            if (partner == null) return;
            partner.gameObject.SetActive(pair);
            if (!pair) return;
            partner.ShowFace(rolling ? RollingNumber(3) : second);
            //Overlaps the first die toward the bottom-right, inside the single die's space.
            Vector2 size = rect.rect.size;
            var partnerRect = (RectTransform)partner.transform;
            partnerRect.localScale = rect.localScale;
            partnerRect.anchoredPosition = singlePosition + new Vector2(size.x, -size.y) * (1f - pairScale) * singleScale.x;
        }

        private int RollingNumber(int offset) => ((int)(Time.unscaledTime * animationSpeed) + offset) % 6 + 1;

        private void ShowFace(int number)
        {
            //Fall back to a number when no saved face sprite exists for this value.
            bool supported = number >= 1 && faces != null && number <= faces.Length;
            face.enabled = supported;
            if (supported) face.sprite = faces[number - 1];
            overflow.enabled = !supported;
            overflow.text = number.ToString();
        }

        private void CreatePartner()
        {
            partner = Instantiate(this, transform.parent);
            partner.name = name + " (Second Die)";
            partner.hasLayout = true;
            partner.transform.SetSiblingIndex(transform.GetSiblingIndex() + 1);
        }

        private void OnDestroy()
        {
            if (partner != null) Destroy(partner.gameObject);
        }
    }
}
