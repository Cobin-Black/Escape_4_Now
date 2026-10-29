using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using static Escape4Now.Menus.ArcadeMenuKit;

namespace Escape4Now.Menus
{
    //The main menu: game mode, tutorial, settings, leaderboard, account and quit.
    //Use the mouse, or Up/Down to pick a section, Left/Right to pick a game mode, Enter to start and Esc to go back.
    public sealed class MainMenuScreen : MonoBehaviour
    {
        private enum Section { GameMode, Tutorial, Settings, Leaderboard, Account, Quit }

        [Header("Scenes")]
        [SerializeField] private string startScreenScene = "StartScreen";
        [SerializeField] private string tutorialScene = "Tutorial";
        [SerializeField] private string leaderboardScene = "Leaderboard";
        [Tooltip("Scene for each game mode, in order: Race, Survival. Leave one empty while that mode is not built yet.")]
        [SerializeField] private string[] modeScenes = { "Race_Gamemode", "SurvivalMode" };

        [Header("Colors")]
        [SerializeField] private Color background = new Color(0.663f, 0.863f, 1f);
        [SerializeField] private Color tapeStripe = new Color(0.831f, 0.941f, 1f);
        [SerializeField] private Color accent = new Color(0.212f, 0.945f, 0.804f);
        [SerializeField] private Color headingColor = new Color(0.561f, 0.816f, 1f);

        private static readonly string[] MenuTitles = { "GAME MODE", "START TUTORIAL", "SETTINGS", "LEADERBOARD", "ACCOUNT", "QUIT" };
        private static readonly string[] MenuSubtitles = { "Pick how you want to play", "New here? Start with this", "Sound, screen, controls", "Fastest escapes", "Sign up, name and picture", "Close the game" };
        //The gamemodes on main: Race (Race_Gamemode scene) and Survival (SurvivalMode scene).
        private static readonly string[] ModeNames = { "RACE", "SURVIVAL" };
        private static readonly string[] ModePlayers = { "2–4 PLAYERS", "2–4 PLAYERS" };
        private static readonly string[] ModeText =
        {
            "One key is hidden in a locker. Find it, battle other players for it on the same tile, and carry it out.",
            "Something is loose in the school. Stay alive together until the clock runs out."
        };
        private static readonly string[] ModeWin = { "Carry the key through the EXIT first", "Survive until time is up or complete the tasks to escape" };
        private static readonly Color[] AvatarColors =
        {
            new Color(1f, 0.835f, 0f), new Color(0.212f, 0.945f, 0.804f), new Color(1f, 0.31f, 0.847f),
            new Color(0.49f, 1f, 0.604f), new Color(1f, 0.478f, 0.102f), new Color(0.608f, 0.549f, 1f)
        };
        private static readonly int[] Barcode = { 3, 1, 2, 1, 4, 1, 1, 3, 2, 1, 1, 2, 4, 1, 2, 1, 3, 1, 1, 2, 1, 4, 2, 1, 1, 3, 1, 2, 1, 1, 4, 1, 2, 3, 1, 1, 2, 1, 3, 1 };

        private const string NameKey = "E4N_PlayerName";
        private const string AvatarKey = "E4N_Avatar";
        private const string UploadKey = "E4N_AvatarUploaded";
        private const string MusicKey = "E4N_Music";
        private const string SfxKey = "E4N_Sfx";

        private Section section = Section.GameMode;
        private int mode;
        private int music = 5;
        private int sfx = 6;
        private string playerName = "PLAYER ONE";
        private int avatar;
        private Texture2D uploadedPicture;
        private string toast = "";
        private float toastUntil;
        private GUIStyle h2, menuTitle, menuSub, body, small, smallBold, button, cardName, tagStyle, nameField, bigInitial, logoShade, logoNow, idStyle, passTitle, stamp, keyName;

        private string AvatarPath => Path.Combine(Application.persistentDataPath, "avatar.png");

        private void Start()
        {
            music = PlayerPrefs.GetInt(MusicKey, music);
            sfx = PlayerPrefs.GetInt(SfxKey, sfx);
            AudioListener.volume = sfx / 8f;
            if (PlayerPrefs.HasKey(NameKey)) LoadAccount(false);
        }

        private void OnDestroy()
        {
            if (uploadedPicture != null) Destroy(uploadedPicture);
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.escapeKey.wasPressedThisFrame)
            {
                if (GUIUtility.keyboardControl != 0) GUIUtility.keyboardControl = 0;
                else Back();
                return;
            }
            //While the name box is being typed in, letters belong to the name.
            if (GUIUtility.keyboardControl != 0) return;
            if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) section = (Section)(((int)section + 5) % 6);
            if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) section = (Section)(((int)section + 1) % 6);
            if (section == Section.GameMode)
            {
                if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) mode = (mode + ModeNames.Length - 1) % ModeNames.Length;
                if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) mode = (mode + 1) % ModeNames.Length;
            }
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) Activate();
        }

        //The big button of the open section.
        private void Activate()
        {
            switch (section)
            {
                case Section.GameMode: StartMode(); break;
                case Section.Tutorial: OpenScene(tutorialScene, "The tutorial scene"); break;
                case Section.Leaderboard: OpenScene(leaderboardScene, "The leaderboard screen"); break;
                case Section.Account: SignUp(); break;
                case Section.Quit: QuitGame(); break;
            }
        }

        private void Back()
        {
            if (!TryLoadScene(startScreenScene)) ShowToast("Add the " + startScreenScene + " scene to Build Settings.");
        }

        private void StartMode()
        {
            string scene = modeScenes != null && mode < modeScenes.Length ? modeScenes[mode] : "";
            if (string.IsNullOrEmpty(scene)) ShowToast(ModeNames[mode] + " is coming soon.");
            else OpenScene(scene, ModeNames[mode]);
        }

        private void OpenScene(string scene, string what)
        {
            if (!TryLoadScene(scene)) ShowToast(what + " isn't built yet (add \"" + scene + "\" to Build Settings).");
        }

        private void ShowToast(string text)
        {
            toast = text;
            toastUntil = Time.unscaledTime + 3.5f;
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void CreateStyles()
        {
            if (h2 != null) return;
            h2 = Text(Bungee, 36);
            menuTitle = Text(Bungee, 22);
            menuSub = Text(MonoFont, 12);
            body = Text(MonoFont, 17, TextAnchor.UpperLeft, true);
            small = Text(MonoFont, 13, TextAnchor.UpperLeft, true);
            smallBold = Text(MonoBoldFont, 13);
            button = Text(Bungee, 26, TextAnchor.MiddleCenter);
            cardName = Text(Bungee, 24, TextAnchor.UpperLeft, true);
            tagStyle = Text(MonoBoldFont, 11, TextAnchor.MiddleCenter);
            bigInitial = Text(Bungee, 72, TextAnchor.MiddleCenter);
            logoShade = Text(BungeeShade, 40);
            logoNow = Text(Bungee, 32);
            idStyle = Text(Bungee, 13, TextAnchor.MiddleCenter);
            passTitle = Text(Bungee, 20);
            stamp = Text(Bungee, 26, TextAnchor.MiddleCenter);
            keyName = Text(MonoBoldFont, 13, TextAnchor.MiddleCenter);
            nameField = new GUIStyle(GUI.skin.textField)
            {
                font = Bungee,
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 14, 0, 0),
                border = new RectOffset(0, 0, 0, 0)
            };
            foreach (GUIStyleState s in new[] { nameField.normal, nameField.hover, nameField.focused, nameField.active, nameField.onNormal, nameField.onFocused })
            {
                s.background = null;
                s.textColor = Color.white;
            }
        }

        private void OnGUI()
        {
            CreateStyles();
            Matrix4x4 baseMatrix = BeginScaled();
            float t = Time.unscaledTime;
            float ox = OffsetX;

            //Light blue backdrop with a faint grid and the striped tape along the top.
            Fill(new Rect(0f, 0f, Width, DesignHeight), background);
            Color gridLine = WithAlpha(Navy, 0.12f);
            for (float x = 0f; x < Width; x += 40f) Fill(new Rect(x, 0f, 1f, DesignHeight), gridLine);
            for (float y = 0f; y < DesignHeight; y += 40f) Fill(new Rect(0f, y, Width, 1f), gridLine);
            Stripes(new Rect(0f, 0f, Width, 18f), tapeStripe, Navy, t * 36f, 22f);

            DrawHeader(ox);
            DrawMenu(ox, t);

            Rect panel = new Rect(ox + 496f, 118f, 728f, 554f);
            Block(panel, Panel, Color.white, 4f, accent, 10f);
            Rect inner = new Rect(panel.x + 32f, panel.y + 26f, panel.width - 64f, panel.height - 52f);
            switch (section)
            {
                case Section.GameMode: DrawGameMode(inner); break;
                case Section.Tutorial: DrawTutorial(inner); break;
                case Section.Settings: DrawSettings(inner); break;
                case Section.Leaderboard: DrawLeaderboard(inner, t); break;
                case Section.Account: DrawAccount(inner); break;
                case Section.Quit: DrawQuit(inner); break;
            }

            if (t < toastUntil && toast.Length > 0)
            {
                Rect box = new Rect(panel.x + 64f, 680f, panel.width - 128f, 32f);
                Block(box, Ink, accent, 3f, Color.clear, 0f);
                Label(box, toast, Text(MonoBoldFont, 13, TextAnchor.MiddleCenter), Color.white);
            }
            GUI.matrix = baseMatrix;
        }

        private void DrawHeader(float ox)
        {
            float x = ox + 56f;
            float w = logoShade.CalcSize(new GUIContent("ESCAPE")).x;
            Label(new Rect(x, 36f, w + 8f, 56f), "ESCAPE", logoShade, Color.white, Navy, 2f, ShadowBlue, 4f);
            Die(new Rect(x + w + 14f, 45f, 38f, 38f), 4, Color.white, Ink, Red, 3f);
            Label(new Rect(x + w + 64f, 36f, 120f, 56f), "NOW", logoNow, Navy);

            //Back button and the signed-in box, which opens the account section.
            GUIStyle esc = Text(MonoFont, 14, TextAnchor.MiddleCenter);
            Rect back = new Rect(ox + 1224f - 196f, 40f, 196f, 48f);
            Block(back, Ink, Hex("#44444f"), 2f, Color.clear, 0f);
            Label(back, "ESC · START SCREEN", esc, Hex("#bdbdbd"));
            if (Hit(back)) Back();

            Rect me = new Rect(back.x - 14f - 236f, 40f, 236f, 48f);
            Block(me, Ink, Line, 2f, Color.clear, 0f);
            DrawAvatar(new Rect(me.x + 10f, me.y + 7f, 34f, 34f), Text(Bungee, 15, TextAnchor.MiddleCenter), 2f);
            Label(new Rect(me.x + 54f, me.y + 6f, 170f, 14f), "SIGNED IN AS", menuSub, Muted);
            Label(new Rect(me.x + 54f, me.y + 20f, 176f, 22f), DisplayName, Text(Bungee, 16), Color.white);
            if (Hit(me)) section = Section.Account;
        }

        private void DrawMenu(float ox, float t)
        {
            for (int i = 0; i < 6; i++)
            {
                bool on = (int)section == i;
                Rect r = new Rect(ox + 56f + (on ? 8f : 0f), 118f + i * 82f, 400f, 72f);
                if (on) Block(r, accent, Ink, 4f, Red, 6f);
                else Block(r, Ink, Hover(r) ? Hex("#55556a") : Hex("#2e2e38"), 4f, Color.clear, 0f);
                Color text = on ? Ink : Color.white;
                DrawIcon(i, new Rect(r.x + 20f, r.y + 21f, 30f, 30f), text);
                Label(new Rect(r.x + 64f, r.y + 12f, 300f, 28f), MenuTitles[i], menuTitle, text);
                Label(new Rect(r.x + 64f, r.y + 42f, 300f, 16f), MenuSubtitles[i], menuSub, WithAlpha(text, 0.75f));
                if (on) TriangleRight(new Rect(r.xMax - 40f + Mathf.Sin(t * 7f) * 3f, r.center.y - 9f, 16f, 18f), Ink);
                if (Hit(r))
                {
                    section = (Section)i;
                    GUIUtility.keyboardControl = 0;
                }
            }
        }

        private void DrawGameMode(Rect inner)
        {
            Label(new Rect(inner.x, inner.y, 480f, 44f), "CHOOSE GAME MODE", h2, headingColor);
            Label(new Rect(inner.xMax - 200f, inner.y + 10f, 200f, 24f), ModeNames[mode] + " SELECTED", Text(MonoFont, 13, TextAnchor.MiddleRight), Muted);

            int count = ModeNames.Length;
            float cw = (inner.width - 14f * (count - 1)) / count;
            for (int i = 0; i < count; i++)
            {
                bool on = mode == i;
                Rect c = new Rect(inner.x + i * (cw + 14f), inner.y + 62f, cw, 300f);
                if (on) Block(c, accent, Ink, 4f, Red, 6f);
                else Block(c, Ink, Hover(c) ? Hex("#55556a") : Hex("#2e2e38"), 4f, Color.clear, 0f);
                Color text = on ? Ink : Color.white;

                Rect iconBox = new Rect(c.x + 18f, c.y + 18f, 56f, 56f);
                if (on) Fill(iconBox, Ink);
                else Block(iconBox, Hex("#1e1e26"), Line, 2f, Color.clear, 0f);
                DrawModeIcon(i == 0 ? 0 : 1, new Rect(iconBox.x + 11f, iconBox.y + 11f, 34f, 34f), accent);

                Vector2 tagSize = tagStyle.CalcSize(new GUIContent(ModePlayers[i]));
                Rect tagRect = new Rect(c.xMax - 18f - tagSize.x - 12f, c.y + 18f, tagSize.x + 12f, 20f);
                if (on) Fill(tagRect, Ink);
                else Outline(tagRect, 2f, Hex("#44444f"));
                Label(tagRect, ModePlayers[i], tagStyle, on ? accent : Hex("#bdbdbd"));

                float nameH = cardName.CalcHeight(new GUIContent(ModeNames[i]), cw - 36f);
                Label(new Rect(c.x + 18f, c.y + 86f, cw - 36f, nameH), ModeNames[i], cardName, text);
                Label(new Rect(c.x + 18f, c.y + 94f + nameH, cw - 36f, 110f), ModeText[i], small, text);
                Fill(new Rect(c.x + 18f, c.yMax - 62f, cw - 36f, 2f), on ? WithAlpha(Ink, 0.35f) : Hex("#2e2e38"));
                Label(new Rect(c.x + 18f, c.yMax - 52f, cw - 36f, 40f), "WIN: " + ModeWin[i], Text(MonoFont, 12, TextAnchor.UpperLeft, true), text);
                if (Hit(c)) mode = i;
            }

            if (BigButton(new Rect(inner.x, inner.yMax - 64f, 0f, 64f), "START " + ModeNames[mode], true)) StartMode();
        }

        private void DrawTutorial(Rect inner)
        {
            Label(new Rect(inner.x, inner.y, 480f, 44f), "START TUTORIAL", h2, headingColor);
            Label(new Rect(inner.x, inner.y + 56f, 560f, 52f), "Every new student gets a trainee pass. Earn your way out on the practice floor first.", body, Soft);

            //The trainee pass, tilted a little like a card on a desk.
            Rect card = new Rect(inner.x + 6f, inner.y + 130f, inner.width - 12f, 226f);
            Matrix4x4 m = BeginRotate(-1.5f, card.center);
            Block(card, Hex("#e9f6ff"), Color.white, 4f, accent, 8f);
            Rect strip = new Rect(card.x + 4f, card.y + 4f, 170f, card.height - 8f);
            Fill(strip, Navy);
            Rect photo = new Rect(strip.center.x - 55f, strip.y + 20f, 110f, 130f);
            Block(photo, headingColor, Color.white, 3f, Color.clear, 0f);
            Color me = AvatarColors[avatar];
            Fill(new Rect(photo.center.x - 32f, photo.yMax - 3f - 68f, 64f, 68f), me);
            Circle(new Rect(photo.center.x - 32f, photo.yMax - 3f - 100f, 64f, 64f), me);
            Label(new Rect(strip.x, photo.yMax + 12f, strip.width, 20f), "ID 0000-001", idStyle, tapeStripe);

            float x = strip.xMax + 22f;
            float right = card.xMax - 22f;
            Color ink = Hex("#12233f");
            Label(new Rect(x, card.y + 16f, 260f, 28f), "TRAINEE PASS", passTitle, ink);
            Label(new Rect(right - 200f, card.y + 18f, 200f, 24f), "ESCAPE 4 NOW HIGH", Text(MonoBoldFont, 11, TextAnchor.MiddleRight), ink);
            Fill(new Rect(x, card.y + 50f, right - x, 3f), Navy);
            string[] labels = { "NAME", "STATUS", "ACCESS", "TIME NEEDED" };
            string[] values = { DisplayName, "NEW STUDENT", "PRACTICE FLOOR ONLY", "ABOUT 3 MINUTES" };
            for (int i = 0; i < labels.Length; i++)
            {
                Label(new Rect(x, card.y + 62f + i * 24f, 130f, 22f), labels[i], Text(MonoFont, 14), Hex("#4b6a94"));
                Label(new Rect(x + 130f, card.y + 62f + i * 24f, 260f, 22f), values[i], Text(MonoBoldFont, 14), ink);
            }
            float bx = x;
            for (int i = 0; i < Barcode.Length; i++)
            {
                float h = i % 7 == 0 ? 34f : 28f;
                Fill(new Rect(bx, card.yMax - 16f - h, Barcode[i], h), ink);
                bx += Barcode[i] + 2f;
            }
            Rect stampRect = new Rect(right - 250f, card.y + 64f, 250f, 46f);
            Matrix4x4 sm = BeginRotate(-14f, stampRect.center);
            Outline(stampRect, 4f, WithAlpha(Red, 0.9f));
            Label(stampRect, "NOT YET ESCAPED", stamp, WithAlpha(Red, 0.9f));
            EndRotate(sm);
            EndRotate(m);

            if (BigButton(new Rect(inner.x, inner.yMax - 64f, 0f, 64f), "START TUTORIAL", true)) OpenScene(tutorialScene, "The tutorial scene");
        }

        private void DrawSettings(Rect inner)
        {
            Label(new Rect(inner.x, inner.y, 480f, 44f), "SETTINGS", h2, headingColor);
            DrawSlider(new Rect(inner.x, inner.y + 70f, inner.width, 44f), "Music", ref music, MusicKey);
            DrawSlider(new Rect(inner.x, inner.y + 128f, inner.width, 44f), "Sound effects", ref sfx, SfxKey);
            AudioListener.volume = sfx / 8f;

            
        }

        private void DrawSlider(Rect row, string label, ref int value, string key)
        {
            Label(new Rect(row.x, row.y, 150f, row.height), label, Text(MonoFont, 16), Color.white);
            Rect minus = new Rect(row.x + 164f, row.y, 44f, 44f);
            Rect plus = new Rect(minus.xMax + 14f + 8f * 25f, row.y, 44f, 44f);
            Block(minus, Ink, Hex("#44444f"), 2f, Color.clear, 0f);
            Block(plus, Ink, Hex("#44444f"), 2f, Color.clear, 0f);
            Label(minus, "-", Text(Bungee, 22, TextAnchor.MiddleCenter), Color.white);
            Label(plus, "+", Text(Bungee, 22, TextAnchor.MiddleCenter), Color.white);
            for (int n = 1; n <= 8; n++)
            {
                float h = 10f + n * 3f;
                Fill(new Rect(minus.xMax + 14f + (n - 1) * 25f, row.yMax - 6f - h, 20f, h), n <= value ? accent : Hex("#2a2a33"));
            }
            Label(new Rect(plus.xMax + 14f, row.y, 80f, row.height), value + " / 8", Text(MonoFont, 14), Muted);
            int old = value;
            if (Hit(minus)) value = Mathf.Max(0, value - 1);
            if (Hit(plus)) value = Mathf.Min(8, value + 1);
            if (value != old)
            {
                PlayerPrefs.SetInt(key, value);
                PlayerPrefs.Save();
            }
        }

        private void DrawLeaderboard(Rect inner, float t)
        {
            Label(new Rect(inner.x, inner.y, 480f, 44f), "LEADERBOARD", h2, headingColor);
            Label(new Rect(inner.x, inner.y + 56f, 560f, 52f), "Who got out of the school the fastest? See every record on the full leaderboard.", body, Soft);

            float top = inner.y + 124f;
            float bottom = inner.yMax - 84f;
            Rect art = new Rect(inner.x, top, 250f, bottom - top);
            Block(art, PanelDark, Line, 2f, Color.clear, 0f);
            //Slowly turning light rays behind a gold trophy medal. Rays stay short enough to fit in the box.
            Vector2 c = art.center;
            float reach = Mathf.Min(art.width, art.height) * 0.5f - 6f;
            for (int i = 0; i < 12; i++)
            {
                Matrix4x4 rm = BeginRotate(i * 30f + t * 8f, c);
                Fill(new Rect(c.x - 6f, c.y - reach, 12f, reach), WithAlpha(Gold, 0.16f));
                EndRotate(rm);
            }
            float bob = Mathf.Sin(t * Mathf.PI * 2f / 1.6f) * 3f - 3f;
            Circle(new Rect(c.x - 87f, c.y - 87f + bob, 174f, 174f), WithAlpha(Gold, 0.25f));
            Circle(new Rect(c.x - 81f, c.y - 81f + bob, 162f, 162f), Gold);
            Circle(new Rect(c.x - 75f, c.y - 75f + bob, 150f, 150f), Navy);
            DrawIcon(3, new Rect(c.x - 44f, c.y - 44f + bob, 88f, 88f), Gold);

            string[] titles = { "FASTEST ESCAPES", "MOST WINS", "YOUR BEST" };
            string[] subs = { "Fewest turns to the EXIT", "Race and Survival champions", "Your own personal records" };
            Color[] colors = { Gold, Teal, Pink };
            float rx = art.xMax + 18f;
            float rh = (art.height - 20f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                Rect r = new Rect(rx, top + i * (rh + 10f), inner.xMax - rx, rh);
                Block(r, Ink, colors[i], 3f, Color.clear, 0f);
                DrawModeIcon(i == 0 ? 1 : i == 1 ? 0 : 3, new Rect(r.x + 16f, r.center.y - 15f, 30f, 30f), colors[i]);
                Label(new Rect(r.x + 62f, r.center.y - 22f, r.width - 70f, 24f), titles[i], Text(Bungee, 18), Color.white);
                Label(new Rect(r.x + 62f, r.center.y + 4f, r.width - 70f, 18f), subs[i], Text(MonoFont, 12), Hex("#bdbdbd"));
            }

            if (BigButton(new Rect(inner.x, inner.yMax - 64f, 0f, 64f), "OPEN LEADERBOARD  >", true))
                OpenScene(leaderboardScene, "The leaderboard screen");
        }

        private void DrawAccount(Rect inner)
        {
            Label(new Rect(inner.x, inner.y, 480f, 44f), "ACCOUNT", h2, headingColor);
            Label(new Rect(inner.xMax - 300f, inner.y + 10f, 300f, 24f), "SIGNED IN AS " + DisplayName, Text(MonoFont, 13, TextAnchor.MiddleRight), Muted);

            Rect big = new Rect(inner.x + 10f, inner.y + 76f, 150f, 150f);
            Fill(new Rect(big.x + 6f, big.y + 6f, big.width, big.height), accent);
            DrawAvatar(big, bigInitial, 4f);
            Label(new Rect(inner.x - 10f, big.yMax + 12f, 190f, 24f), DisplayName, Text(Bungee, 18, TextAnchor.MiddleCenter), Color.white);

            float x = inner.x + 196f;
            float w = inner.xMax - x;
            Label(new Rect(x, inner.y + 70f, w, 16f), "PLAYER NAME", menuSub, Muted);
            Rect field = new Rect(x, inner.y + 92f, w, 50f);
            Block(field, PanelDark, GUI.GetNameOfFocusedControl() == "PlayerName" ? accent : Hex("#44444f"), 3f, Color.clear, 0f);
            GUI.SetNextControlName("PlayerName");
            string typed = GUI.TextField(new Rect(field.x + 3f, field.y + 3f, field.width - 6f, field.height - 6f), playerName, 12, nameField);
            if (typed != playerName) playerName = typed.ToUpperInvariant();

            Label(new Rect(x, inner.y + 166f, w, 16f), "PICTURE", menuSub, Muted);
            for (int i = 0; i < AvatarColors.Length; i++)
            {
                Rect tile = new Rect(x + i * 56f, inner.y + 188f, 48f, 48f);
                bool on = uploadedPicture == null && avatar == i;
                if (on) Fill(new Rect(tile.x - 3f, tile.y - 3f, 54f, 54f), accent);
                Block(tile, AvatarColors[i], on ? Color.white : Ink, on ? 4f : 3f, Color.clear, 0f);
                if (Hit(tile))
                {
                    avatar = i;
                    ClearUpload();
                }
            }
            Rect upload = new Rect(x + 6f * 56f, inner.y + 188f, 120f, 48f);
            bool uploaded = uploadedPicture != null;
            if (uploaded) Block(upload, accent, accent, 3f, Color.clear, 0f);
            else
            {
                Fill(upload, Ink);
                for (float dx = 0f; dx < upload.width; dx += 10f)
                {
                    Fill(new Rect(upload.x + dx, upload.y, 6f, 3f), Muted);
                    Fill(new Rect(upload.x + dx, upload.yMax - 3f, 6f, 3f), Muted);
                }
                for (float dy = 0f; dy < upload.height; dy += 10f)
                {
                    Fill(new Rect(upload.x, upload.y + dy, 3f, 6f), Muted);
                    Fill(new Rect(upload.xMax - 3f, upload.y + dy, 3f, 6f), Muted);
                }
            }
            Label(upload, "UPLOAD", Text(Bungee, 14, TextAnchor.MiddleCenter), uploaded ? Ink : Color.white);
            if (Hit(upload)) PickPicture();
            Label(new Rect(x, inner.y + 248f, w, 40f), "Pick a color or upload your own image (PNG or JPG).", small, Muted);

            float bw = BigButton(new Rect(inner.x, inner.yMax - 64f, 0f, 64f), "SIGN UP", true, out bool signUp);
            if (signUp) SignUp();
            if (BigButton(new Rect(inner.x + bw + 16f, inner.yMax - 64f, 0f, 64f), "I HAVE AN ACCOUNT", false)) LoadAccount(true);
        }

        private void DrawQuit(Rect inner)
        {
            Label(new Rect(inner.x, inner.y, 480f, 44f), "QUIT?", h2, headingColor);
            Label(new Rect(inner.x, inner.y + 56f, 560f, 40f), "Leaving already? The doors are still locked.", body, Soft);
            float bw = BigButton(new Rect(inner.x, inner.yMax - 64f, 0f, 64f), "QUIT GAME", false, out bool quit);
            if (quit) QuitGame();
            if (BigButton(new Rect(inner.x + bw + 18f, inner.yMax - 64f, 0f, 64f), "KEEP PLAYING", true)) section = Section.GameMode;
        }

        //A big arcade button sized to its text. Width 0 means "fit the text".
        private bool BigButton(Rect r, string text, bool primary)
        {
            BigButton(r, text, primary, out bool clicked);
            return clicked;
        }

        private float BigButton(Rect r, string text, bool primary, out bool clicked)
        {
            GUIStyle style = primary ? button : Text(Bungee, 20, TextAnchor.MiddleCenter);
            if (r.width <= 0f) r.width = style.CalcSize(new GUIContent(text)).x + 72f;
            Rect drawn = r;
            if (Hover(r)) { drawn.x -= 3f; drawn.y -= 3f; }
            if (primary) Block(drawn, accent, Ink, 4f, Red, 6f);
            else Block(drawn, Ink, Color.white, 4f, Red, 6f);
            Label(drawn, text, style, primary ? Ink : Color.white);
            clicked = Hit(r);
            return r.width;
        }

        private bool Chip(Rect r, string text, bool on)
        {
            Block(r, on ? accent : Ink, on ? accent : Hex("#44444f"), 3f, Color.clear, 0f);
            Label(r, text, Text(Bungee, 15, TextAnchor.MiddleCenter), on ? Ink : Color.white);
            return Hit(r);
        }

        //The player's picture: an uploaded image, or a color with their first letter.
        private void DrawAvatar(Rect r, GUIStyle letter, float border)
        {
            Fill(r, Color.white);
            Rect inside = new Rect(r.x + border, r.y + border, r.width - border * 2f, r.height - border * 2f);
            if (uploadedPicture != null) GUI.DrawTexture(inside, uploadedPicture, ScaleMode.ScaleAndCrop);
            else
            {
                Fill(inside, AvatarColors[avatar]);
                string initial = DisplayName.Length > 0 ? DisplayName.Substring(0, 1) : "?";
                Label(inside, initial, letter, Ink);
            }
        }

        private string DisplayName
        {
            get
            {
                string n = (playerName ?? "").Trim();
                return n.Length > 0 ? n : "GUEST";
            }
        }

        private void PickPicture()
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Choose a picture", "", "png,jpg,jpeg");
            if (string.IsNullOrEmpty(path)) return;
#else
            string path = AvatarPath;
            if (!File.Exists(path))
            {
                ShowToast("Put a picture named avatar.png in " + Application.persistentDataPath);
                return;
            }
#endif
            var tex = new Texture2D(2, 2);
            if (tex.LoadImage(File.ReadAllBytes(path)))
            {
                ClearUpload();
                uploadedPicture = tex;
            }
            else
            {
                Destroy(tex);
                ShowToast("That file isn't a PNG or JPG picture.");
            }
        }

        private void ClearUpload()
        {
            if (uploadedPicture != null) Destroy(uploadedPicture);
            uploadedPicture = null;
        }

        //Saves the name and picture on this computer.
        private void SignUp()
        {
            PlayerPrefs.SetString(NameKey, DisplayName);
            PlayerPrefs.SetInt(AvatarKey, avatar);
            PlayerPrefs.SetInt(UploadKey, uploadedPicture != null ? 1 : 0);
            PlayerPrefs.Save();
            if (uploadedPicture != null)
            {
                try { File.WriteAllBytes(AvatarPath, uploadedPicture.EncodeToPNG()); }
                catch (System.Exception e) { Debug.LogWarning("Could not save the picture: " + e.Message); }
            }
            ShowToast("Saved! Signed in as " + DisplayName + ".");
        }

        private void LoadAccount(bool announce)
        {
            if (!PlayerPrefs.HasKey(NameKey))
            {
                if (announce) ShowToast("No saved account on this computer yet. Press SIGN UP first.");
                return;
            }
            playerName = PlayerPrefs.GetString(NameKey, playerName);
            avatar = Mathf.Clamp(PlayerPrefs.GetInt(AvatarKey, 0), 0, AvatarColors.Length - 1);
            ClearUpload();
            if (PlayerPrefs.GetInt(UploadKey, 0) == 1 && File.Exists(AvatarPath))
            {
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(File.ReadAllBytes(AvatarPath))) uploadedPicture = tex;
                else Destroy(tex);
            }
            if (announce) ShowToast("Welcome back, " + DisplayName + "!");
        }

        //Simple shape icons for the menu: 0 play, 1 tutorial, 2 settings, 3 trophy, 4 account, 5 quit.
        private static void DrawIcon(int kind, Rect r, Color c)
        {
            float u = r.width / 30f;
            switch (kind)
            {
                case 0:
                    TriangleRight(new Rect(r.x + 6f * u, r.y + 3f * u, 20f * u, 24f * u), c);
                    break;
                case 1:
                    Matrix4x4 m = BeginRotate(45f, new Vector2(r.center.x, r.y + 10f * u));
                    Fill(new Rect(r.center.x - 8f * u, r.y + 2f * u, 16f * u, 16f * u), c);
                    EndRotate(m);
                    Fill(new Rect(r.x + 8f * u, r.y + 14f * u, 14f * u, 7f * u), c);
                    Fill(new Rect(r.x + 25f * u, r.y + 9f * u, 2f * u, 10f * u), c);
                    break;
                case 2:
                    for (int i = 0; i < 3; i++)
                    {
                        float y = r.y + (6f + i * 9f) * u;
                        Fill(new Rect(r.x + 3f * u, y - 1.2f * u, 24f * u, 2.4f * u), c);
                        float kx = r.x + (i == 1 ? 9f : i == 0 ? 19f : 21f) * u;
                        Circle(new Rect(kx - 4f * u, y - 4f * u, 8f * u, 8f * u), c);
                    }
                    break;
                case 3:
                    Fill(new Rect(r.x + 8f * u, r.y + 3f * u, 14f * u, 9f * u), c);
                    Circle(new Rect(r.x + 8f * u, r.y + 5f * u, 14f * u, 14f * u), c);
                    Outline(new Rect(r.x + 3f * u, r.y + 5f * u, 6f * u, 8f * u), 2f * u, c);
                    Outline(new Rect(r.x + 21f * u, r.y + 5f * u, 6f * u, 8f * u), 2f * u, c);
                    Fill(new Rect(r.x + 13.5f * u, r.y + 18f * u, 3f * u, 6f * u), c);
                    Fill(new Rect(r.x + 8f * u, r.y + 24f * u, 14f * u, 3f * u), c);
                    break;
                case 4:
                    Circle(new Rect(r.x + 9f * u, r.y + 2f * u, 12f * u, 12f * u), c);
                    GUI.BeginGroup(new Rect(r.x, r.y + 16f * u, r.width, 12f * u));
                    Circle(new Rect(4f * u, 0f, 22f * u, 22f * u), c);
                    GUI.EndGroup();
                    break;
                case 5:
                    Outline(new Rect(r.x + 15f * u, r.y + 3f * u, 12f * u, 24f * u), 2.4f * u, c);
                    Fill(new Rect(r.x + 2f * u, r.y + 13.8f * u, 12f * u, 2.4f * u), c);
                    TriangleRight(new Rect(r.x + 11f * u, r.y + 10f * u, 7f * u, 10f * u), c);
                    break;
            }
        }

        //Game mode icons: 0 flag, 1 stopwatch, 2 padlock, 3 star.
        private static void DrawModeIcon(int kind, Rect r, Color c)
        {
            float u = r.width / 34f;
            switch (kind)
            {
                case 0:
                    Fill(new Rect(r.x + 6f * u, r.y + 3f * u, 3f * u, 28f * u), c);
                    TriangleRight(new Rect(r.x + 9f * u, r.y + 4f * u, 20f * u, 14f * u), c);
                    break;
                case 1:
                    RingShape(new Rect(r.x + 5f * u, r.y + 8f * u, 24f * u, 24f * u), c);
                    Fill(new Rect(r.x + 13f * u, r.y + 2f * u, 8f * u, 3f * u), c);
                    Fill(new Rect(r.x + 15.5f * u, r.y + 4f * u, 3f * u, 5f * u), c);
                    Matrix4x4 m = BeginRotate(45f, new Vector2(r.x + 17f * u, r.y + 20f * u));
                    Fill(new Rect(r.x + 15.8f * u, r.y + 13f * u, 2.4f * u, 7f * u), c);
                    EndRotate(m);
                    break;
                case 2:
                    GUI.BeginGroup(new Rect(r.x, r.y + 2f * u, r.width, 12f * u));
                    RingShape(new Rect(9f * u, 0f, 16f * u, 16f * u), c);
                    GUI.EndGroup();
                    Fill(new Rect(r.x + 9f * u, r.y + 8f * u, 3f * u, 6f * u), c);
                    Fill(new Rect(r.x + 22f * u, r.y + 8f * u, 3f * u, 6f * u), c);
                    Fill(new Rect(r.x + 6f * u, r.y + 14f * u, 22f * u, 17f * u), c);
                    break;
                case 3:
                    Rect s = new Rect(r.x + 8f * u, r.y + 8f * u, 18f * u, 18f * u);
                    Fill(s, c);
                    Matrix4x4 sm = BeginRotate(45f, s.center);
                    Fill(s, c);
                    EndRotate(sm);
                    break;
            }
        }
    }
}
