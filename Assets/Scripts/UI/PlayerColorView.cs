using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    // Saved references identify the parts of a card that belong to its player color.
    // Text and panel backgrounds keep their own colors for readability.
    public sealed class PlayerColorView : MonoBehaviour
    {
        [SerializeField, Tooltip("Card borders, header strips and role labels that use the player's color.")]
        private Graphic[] accents;

        public void SetColor(Color color)
        {
            if (accents == null) return;
            foreach (var graphic in accents)
                if (graphic != null) graphic.color = color;
        }
    }
}
