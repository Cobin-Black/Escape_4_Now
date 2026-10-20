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

        public void Show(int value, bool rolling)
        {
            int number = rolling ? (int)(Time.unscaledTime * animationSpeed) % 6 + 1 : value;
            bool supported = number >= 1 && faces != null && number <= faces.Length;
            face.enabled = supported;
            if (supported) face.sprite = faces[number - 1];
            overflow.enabled = !supported;
            overflow.text = number.ToString();
        }
    }
}
