using UnityEngine;

namespace Escape4Now.Player
{
    // One saved palette keeps player slots the same color in every lobby and match.
    [CreateAssetMenu(menuName = "Escape 4 Now/Player Colors")]
    public sealed class PlayerColorPalette : ScriptableObject
    {
        [SerializeField, Tooltip("Player 1 through Player 4. Used by local and online modes.")]
        private Color[] colors =
        {
            new Color(.15f, .7f, 1f), new Color(1f, .35f, .3f),
            new Color(.3f, .85f, .4f), new Color(1f, .8f, .2f)
        };

        public Color GetColor(int slot) => colors != null && slot >= 0 && slot < colors.Length
            ? colors[slot] : Color.white;

        private void OnValidate()
        {
            if (colors == null || colors.Length != 4) System.Array.Resize(ref colors, 4);
        }
    }
}
