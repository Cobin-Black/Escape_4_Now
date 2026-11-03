using UnityEngine;

namespace Escape4Now.Arena
{
    // Saved UI defaults for the scene authoring tool. The actual UI objects remain editable in Unity too.
    [CreateAssetMenu(menuName = "Escape 4 Now/Arena/Interface Style")]
    public sealed class ArenaInterfaceStyle : ScriptableObject
    {
        [Header("Typography")]
        public Font font;
        public int setupTitleSize = 28, headingSize = 18, bodySize = 14, smallSize = 12;
        [Header("Palette")]
        public Color panel = new Color(.025f, .075f, .12f, .98f);
        public Color card = new Color(.055f, .14f, .20f, 1);
        public Color input = new Color(.09f, .20f, .28f, 1);
        public Color accent = new Color(.08f, .72f, .94f, 1);
        public Color start = new Color(.16f, .7f, .39f, 1);
        public Color text = new Color(.93f, .97f, 1, 1);
        public Color secondary = new Color(.6f, .77f, .85f, 1);
        public Color healthy = new Color(.98f, .27f, .32f, 1);
        public Color emptyHealth = new Color(.12f, .24f, .31f, 1);
        public Color holder = new Color(1, .76f, .2f, 1);
        [Header("Setup Layout")]
        public float setupWidth = 650, padding = 22, gap = 12, playerRowHeight = 64, countButtonHeight = 40, startButtonHeight = 48;
        [Header("HUD Layout")]
        public float hudWidth = 290, hudPadding = 12, healthRowHeight = 40;
        [Range(1, 10)]
        public int healthSegments = 5;
        [Range(.7f, 1.3f)]
        public float hudScale = 1;
        [Min(0)]
        public float hudGap = 3;
        public Vector2 hudOffset = new Vector2(12, -12);
        [Range(0, 1)]
        public float urgentTimerFraction = .25f;
    }
}
