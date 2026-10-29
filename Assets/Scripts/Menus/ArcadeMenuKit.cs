using UnityEngine;

namespace Escape4Now.Menus
{
    //Shared drawing helpers for the start screen and main menu: colors, fonts, scaling, and simple shapes.
    //Everything is drawn with OnGUI on a virtual screen that is always 720 units tall.
    public static class ArcadeMenuKit
    {
        public const float DesignWidth = 1280f;
        public const float DesignHeight = 720f;

        public static readonly Color Ink = Hex("#111111");
        public static readonly Color Navy = Hex("#1d3a66");
        public static readonly Color ShadowBlue = Hex("#2f6db5");
        public static readonly Color Red = Hex("#e63946");
        public static readonly Color Panel = Hex("#15151a");
        public static readonly Color PanelDark = Hex("#0d0d0f");
        public static readonly Color Line = Hex("#33333d");
        public static readonly Color Muted = Hex("#8a8a96");
        public static readonly Color Soft = Hex("#d6d6d6");
        public static readonly Color ExitGreen = Hex("#2dff8c");
        public static readonly Color Gold = Hex("#ffd500");
        public static readonly Color Teal = Hex("#36f1cd");
        public static readonly Color Pink = Hex("#ff4fd8");

        private static Texture2D white;
        private static Texture2D circle;
        private static Texture2D ring;
        private static Texture2D triangle;
        private static Texture2D stripes;
        private static Font bungee;
        private static Font bungeeShade;
        private static Font mono;
        private static Font monoBold;
        private static readonly Vector2[] OutlineOffsets =
        {
            new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1), new Vector2(-1, 0),
            new Vector2(1, 0), new Vector2(-1, 1), new Vector2(0, 1), new Vector2(1, 1)
        };

        //Size of the virtual screen, and how far to shift the 1280-wide layout so it stays centered.
        public static float Scale => Mathf.Max(0.1f, Screen.height / DesignHeight);
        public static float Width => Screen.width / Scale;
        public static float OffsetX => (Width - DesignWidth) * 0.5f;

        public static Font Bungee => bungee != null ? bungee : bungee = LoadFont("Fonts/Bungee-Regular");
        public static Font BungeeShade => bungeeShade != null ? bungeeShade : bungeeShade = LoadFont("Fonts/BungeeShade-Regular");
        public static Font MonoFont => mono != null ? mono : mono = LoadFont("Fonts/SpaceMono-Regular");
        public static Font MonoBoldFont => monoBold != null ? monoBold : monoBold = LoadFont("Fonts/SpaceMono-Bold");

        //Call at the start of OnGUI; returns the old matrix to put back at the end.
        public static Matrix4x4 BeginScaled()
        {
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
            return previous;
        }

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
        }

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private static Font LoadFont(string path)
        {
            Font font = Resources.Load<Font>(path);
            return font != null ? font : GUI.skin.font;
        }

        //A text style in one of the menu fonts.
        public static GUIStyle Text(Font font, int size, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
        {
            return new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = size,
                alignment = anchor,
                wordWrap = wrap,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                richText = false
            };
        }

        public static void Fill(Rect r, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, WhiteTexture);
            GUI.color = old;
        }

        public static void Outline(Rect r, float t, Color color)
        {
            Fill(new Rect(r.x, r.y, r.width, t), color);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), color);
            Fill(new Rect(r.x, r.y, t, r.height), color);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), color);
        }

        //A filled box with a border and an offset block shadow, the arcade look used everywhere.
        public static void Block(Rect r, Color fill, Color border, float borderWidth, Color shadow, float shadowOffset)
        {
            if (shadowOffset > 0f) Fill(new Rect(r.x + shadowOffset, r.y + shadowOffset, r.width, r.height), shadow);
            Fill(r, border);
            Fill(new Rect(r.x + borderWidth, r.y + borderWidth, r.width - borderWidth * 2f, r.height - borderWidth * 2f), fill);
        }

        public static void Circle(Rect r, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, CircleTexture);
            GUI.color = old;
        }

        public static void RingShape(Rect r, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, RingTexture);
            GUI.color = old;
        }

        //A triangle pointing right, used for play arrows and flags.
        public static void TriangleRight(Rect r, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, TriangleTexture);
            GUI.color = old;
        }

        
public static void Label(Rect r, string text, GUIStyle style, Color color, Color outline = default, float outlineWidth = 0f, Color shadow = default, float shadowOffset = 0f)
{
    if (Event.current.type != EventType.Repaint) return;
    Color old = style.normal.textColor;
    GUIContent content = new GUIContent(text);
    if (shadowOffset > 0f)
    {
        style.normal.textColor = shadow;
        Rect s = new Rect(r.x + shadowOffset, r.y + shadowOffset, r.width, r.height);
        if (outlineWidth > 0f)
            foreach (Vector2 o in OutlineOffsets) style.Draw(new Rect(s.x + o.x * outlineWidth, s.y + o.y * outlineWidth, s.width, s.height), content, false, false, false, false);
        style.Draw(s, content, false, false, false, false);
    }
    if (outlineWidth > 0f)
    {
        style.normal.textColor = outline;
        foreach (Vector2 o in OutlineOffsets) style.Draw(new Rect(r.x + o.x * outlineWidth, r.y + o.y * outlineWidth, r.width, r.height), content, false, false, false, false);
    }
    style.normal.textColor = color;
    style.Draw(r, content, false, false, false, false);
    style.normal.textColor = old;
}

        //Diagonal two-color stripes that scroll sideways, for the hazard tape.
        public static void Stripes(Rect r, Color a, Color b, float scroll, float stripeSize)
        {
            Texture2D tex = StripeTexture;
            Color old = GUI.color;
            Fill(r, b);
            GUI.color = a;
            float tile = stripeSize * 2f;
            GUI.DrawTextureWithTexCoords(r, tex, new Rect(scroll / tile, 0f, r.width / tile, r.height / tile));
            GUI.color = old;
        }

        //Rotates everything drawn until EndRotate around a point on the virtual screen.
        public static Matrix4x4 BeginRotate(float degrees, Vector2 pivot)
        {
            Matrix4x4 previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(degrees, pivot * Scale);
            return previous;
        }

        public static void EndRotate(Matrix4x4 previous)
        {
            GUI.matrix = previous;
        }

        //A white die face showing 1 to 6 pips.
        public static void Die(Rect r, int face, Color body, Color pip, Color shadow, float shadowOffset)
        {
            if (shadowOffset > 0f) Fill(new Rect(r.x + shadowOffset, r.y + shadowOffset, r.width, r.height), shadow);
            Fill(r, body);
            float pad = r.width * 0.16f;
            float cell = (r.width - pad * 2f) / 3f;
            float size = cell * 0.62f;
            foreach (int i in Pips(face))
            {
                int col = i % 3;
                int row = i / 3;
                float cx = r.x + pad + cell * col + cell * 0.5f;
                float cy = r.y + pad + cell * row + cell * 0.5f;
                Circle(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), pip);
            }
        }

        private static int[] Pips(int face)
        {
            switch (Mathf.Clamp(face, 1, 6))
            {
                case 1: return new[] { 4 };
                case 2: return new[] { 0, 8 };
                case 3: return new[] { 0, 4, 8 };
                case 4: return new[] { 0, 2, 6, 8 };
                case 5: return new[] { 0, 2, 4, 6, 8 };
                default: return new[] { 0, 2, 3, 5, 6, 8 };
            }
        }

        //An invisible button over a rectangle. Returns true when clicked.
        public static bool Hit(Rect r)
        {
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        public static bool Hover(Rect r)
        {
            return Event.current != null && r.Contains(Event.current.mousePosition);
        }

        public static Texture2D WhiteTexture
        {
            get
            {
                if (white == null) white = MakeTexture(1, (x, y) => 1f);
                return white;
            }
        }

        private static Texture2D CircleTexture
        {
            get
            {
                if (circle == null) circle = MakeTexture(64, (x, y) => Mathf.Clamp01(31.5f - Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) + 0.5f));
                return circle;
            }
        }

        private static Texture2D RingTexture
        {
            get
            {
                if (ring == null)
                    ring = MakeTexture(64, (x, y) =>
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                        return Mathf.Clamp01(31.5f - d + 0.5f) * Mathf.Clamp01(d - 24f + 0.5f);
                    });
                return ring;
            }
        }

        private static Texture2D TriangleTexture
        {
            get
            {
                if (triangle == null)
                    triangle = MakeTexture(64, (x, y) =>
                    {
                        float half = 32f * (1f - x / 63f);
                        return Mathf.Clamp01(half - Mathf.Abs(y - 31.5f) + 0.5f);
                    });
                return triangle;
            }
        }

        private static Texture2D StripeTexture
        {
            get
            {
                if (stripes == null)
                {
                    stripes = MakeTexture(64, (x, y) => ((x + y) % 64) < 32 ? 1f : 0f);
                    stripes.wrapMode = TextureWrapMode.Repeat;
                    stripes.filterMode = FilterMode.Bilinear;
                }
                return stripes;
            }
        }

        //Builds a white texture whose alpha comes from the given function.
        private static Texture2D MakeTexture(int size, System.Func<int, int, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha(x, y));
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        //Loads another scene if it is in Build Settings. Returns false when it is not there yet.
        public static bool TryLoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName)) return false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            return true;
        }
    }
}
