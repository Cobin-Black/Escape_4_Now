using UnityEngine;
using UnityEngine.InputSystem;
using static Escape4Now.Menus.ArcadeMenuKit;

namespace Escape4Now.Menus
{
    //The title screen. Press Space (or Enter, or click the Space key) to go to the main menu scene.
    public sealed class StartScreen : MonoBehaviour
    {
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private Color background = new Color(0.561f, 0.816f, 1f);
        [SerializeField] private Color tapeStripe = new Color(0.831f, 0.941f, 1f);
        [SerializeField] private Color accent = new Color(1f, 0.835f, 0f);
        [SerializeField, Min(0.1f)] private float dieRollSeconds = 0.7f;

        private int dieFace = 4;
        private float nextRoll;
        private string message = "";
        private float messageUntil;
        private GUIStyle titleStyle, nowStyle, taglineStyle, tapeStyle, exitStyle, keyStyle, pressStyle, messageStyle;

        private void Update()
        {
            if (Time.unscaledTime >= nextRoll)
            {
                nextRoll = Time.unscaledTime + dieRollSeconds;
                dieFace = dieFace % 6 + 1;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) GoToMainMenu();
        }

        private void GoToMainMenu()
        {
            if (TryLoadScene(mainMenuScene)) return;
            message = "Add the " + mainMenuScene + " scene to Build Settings to continue.";
            messageUntil = Time.unscaledTime + 4f;
        }

        private void CreateStyles()
        {
            if (titleStyle != null) return;
            titleStyle = Text(BungeeShade, 150, TextAnchor.MiddleCenter);
            nowStyle = Text(Bungee, 96, TextAnchor.MiddleLeft);
            taglineStyle = Text(Bungee, 18, TextAnchor.MiddleCenter);
            tapeStyle = Text(Bungee, 18, TextAnchor.MiddleCenter);
            exitStyle = Text(Bungee, 22, TextAnchor.MiddleLeft);
            keyStyle = Text(Bungee, 30, TextAnchor.MiddleCenter);
            pressStyle = Text(Bungee, 30, TextAnchor.MiddleCenter);
            messageStyle = Text(MonoBoldFont, 15, TextAnchor.MiddleCenter);
        }

        private void OnGUI()
        {
            CreateStyles();
            Matrix4x4 baseMatrix = BeginScaled();
            float t = Time.unscaledTime;
            float w = Width;
            float ox = OffsetX;

            Fill(new Rect(0f, 0f, w, DesignHeight), background);
            DrawGrid(w);
            DrawFloatingDie(new Rect(ox + 110f, 160f, 56f, 56f), -18f, 0f, Navy, 1, t);
            DrawFloatingDie(new Rect(ox + 190f, 470f, 40f, 40f), 22f, 1.2f, Hex("#8e1b2b"), 0, t);
            DrawFloatingDie(new Rect(ox + 1066f, 200f, 64f, 64f), 10f, 0.6f, Hex("#0b5e52"), 2, t);
            DrawFloatingDie(new Rect(ox + 1006f, 470f, 44f, 44f), -30f, 2f, Hex("#5b1f6e"), 0, t);

            DrawTape(new Rect(-40f, 28f, w + 80f, 46f), -3f, t * 45f, "DO NOT CROSS · SCHOOL CLOSED");
            DrawExitSign(new Rect(ox + 1280f - 60f - 132f, 100f, 132f, 44f), t);

            //Title, gently bobbing.
            float bob = Mathf.Sin(t * Mathf.PI * 2f / 3f) * 3f - 3f;
            Label(new Rect(ox, 112f + bob, DesignWidth, 160f), "ESCAPE", titleStyle, Color.white, Navy, 3f, ShadowBlue, 8f);

            //Rolling die next to "NOW".
            float nowWidth = nowStyle.CalcSize(new GUIContent("NOW")).x;
            float rowWidth = 104f + 26f + nowWidth;
            float rowX = ox + (DesignWidth - rowWidth) * 0.5f;
            float rowY = 280f + bob;
            Rect dieRect = new Rect(rowX, rowY, 104f, 104f);
            Matrix4x4 m = BeginRotate(-12f + Mathf.Sin(t * 7f) * 5f, dieRect.center);
            Die(dieRect, dieFace, Color.white, Ink, Red, 7f);
            EndRotate(m);
            Label(new Rect(rowX + 130f, rowY - 4f, nowWidth + 10f, 110f), "NOW", nowStyle, Navy, default, 0f, Color.white, 6f);

            Label(new Rect(ox, 398f, DesignWidth, 30f), "ROLL  ·  RUN  ·  GET OUT BEFORE THE BELL", taglineStyle, Navy);

            //The Space key and the blinking prompt.
            Rect key = new Rect(ox + (DesignWidth - 420f) * 0.5f, 452f, 420f, 76f);
            bool hover = Hover(key);
            if (hover) key.y -= 4f;
            Fill(new Rect(key.x + 8f, key.y + 16f, key.width, key.height), accent);
            Fill(new Rect(key.x, key.y + 8f, key.width, key.height), Navy);
            Block(key, Ink, Color.white, 4f, Color.clear, 0f);
            Label(key, "S P A C E", keyStyle, Color.white);
            if ((t % 1.1f) < 0.55f)
                Label(new Rect(ox, 560f, DesignWidth, 40f), "PRESS SPACE TO ROLL IN", pressStyle, Color.white, default, 0f, Navy, 3f);
            if (Hit(key) || Hit(new Rect(ox + 300f, 560f, 680f, 40f))) GoToMainMenu();

            DrawTape(new Rect(-40f, 664f, w + 80f, 30f), 1.5f, -t * 45f, null);

            if (t < messageUntil && message.Length > 0)
            {
                Rect box = new Rect(ox + 340f, 612f, 600f, 40f);
                Block(box, Ink, Red, 3f, Color.clear, 0f);
                Label(box, message, messageStyle, Color.white);
            }

            GUI.matrix = baseMatrix;
        }

        //A faint diamond grid behind the title, like the game board.
        private void DrawGrid(float w)
        {
            Color line = WithAlpha(Navy, 0.16f);
            Vector2 center = new Vector2(w * 0.5f, 360f);
            Matrix4x4 m = BeginRotate(45f, center);
            for (float d = -900f; d <= 900f; d += 60f)
            {
                Fill(new Rect(center.x + d, center.y - 900f, 2f, 1800f), line);
                Fill(new Rect(center.x - 900f, center.y + d, 1800f, 2f), line);
            }
            EndRotate(m);
        }

        //An outlined die that floats up and down and wobbles.
        private void DrawFloatingDie(Rect r, float baseAngle, float delay, Color color, int pips, float t)
        {
            float phase = (t + delay) * Mathf.PI * 2f / 5f;
            r.y += Mathf.Sin(phase) * 9f - 9f;
            Matrix4x4 m = BeginRotate(baseAngle + Mathf.Sin(phase) * 6f, r.center);
            Outline(r, 3f, color);
            float size = r.width * 0.2f;
            if (pips == 1) Circle(new Rect(r.center.x - size * 0.5f, r.center.y - size * 0.5f, size, size), color);
            if (pips == 2)
            {
                Circle(new Rect(r.x + r.width * 0.22f, r.y + r.height * 0.22f, size, size), color);
                Circle(new Rect(r.xMax - r.width * 0.22f - size, r.yMax - r.height * 0.22f - size, size, size), color);
            }
            EndRotate(m);
        }

        private void DrawTape(Rect r, float angle, float scroll, string label)
        {
            Matrix4x4 m = BeginRotate(angle, r.center);
            Stripes(r, tapeStripe, Navy, scroll, 34f);
            Fill(new Rect(r.x, r.y, r.width, 4f), Navy);
            Fill(new Rect(r.x, r.yMax - 4f, r.width, 4f), Navy);
            if (!string.IsNullOrEmpty(label))
            {
                float lw = tapeStyle.CalcSize(new GUIContent(label)).x + 32f;
                Rect box = new Rect(r.center.x - lw * 0.5f, r.y + 10f, lw, r.height - 20f);
                Fill(box, Navy);
                Label(box, label, tapeStyle, tapeStripe);
            }
            EndRotate(m);
        }

        //A small glowing EXIT sign that flickers now and then.
        private void DrawExitSign(Rect r, float t)
        {
            float cycle = t % 4f;
            float alpha = cycle > 3.64f && cycle < 3.72f ? 0.35f : cycle > 3.8f && cycle < 3.84f ? 0.55f : 1f;
            Color green = WithAlpha(ExitGreen, alpha);
            Fill(new Rect(r.x - 8f, r.y - 8f, r.width + 16f, r.height + 16f), WithAlpha(ExitGreen, 0.18f * alpha));
            Block(r, Hex("#0b1a12"), green, 3f, Color.clear, 0f);
            Label(new Rect(r.x + 16f, r.y, 80f, r.height), "EXIT", exitStyle, green);
            Fill(new Rect(r.x + 88f, r.center.y - 2f, 16f, 4f), green);
            TriangleRight(new Rect(r.x + 100f, r.center.y - 9f, 16f, 18f), green);
        }
    }
}
