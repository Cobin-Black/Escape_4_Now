using Escape4Now.Accounts;
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
        [Tooltip("Scenes in order: Race, Survival, Free For All, Hot Potato.")]
        [SerializeField] private string[] modeScenes = { "Race_Gamemode", "SurvivalMode", "FreeForAll", "HotPotato" };

        [Header("Colors")]
        [SerializeField] private Color background = new Color(0.663f, 0.863f, 1f);
        [SerializeField] private Color tapeStripe = new Color(0.831f, 0.941f, 1f);
        [SerializeField] private Color accent = new Color(0.212f, 0.945f, 0.804f);
        [SerializeField] private Color headingColor = new Color(0.561f, 0.816f, 1f);

        private static readonly string[] MenuTitles = { "GAME MODE", "START TUTORIAL", "SETTINGS", "LEADERBOARD", "ACCOUNT", "QUIT" };
        private static readonly string[] MenuSubtitles = { "Pick how you want to play", "New here? Start with this", "Sound, screen, controls", "Wins and fastest times", "Match history and log out", "Close the game" };
        [Header("Game Mode Cards (same order as Scenes)")]
        [SerializeField] private string[] ModeNames = { "RACE", "SURVIVAL", "FREE FOR ALL", "HOT POTATO" };
        [SerializeField] private string[] ModePlayers = { "2–4 PLAYERS", "2–4 PLAYERS", "2–4 PLAYERS", "2–4 PLAYERS" };
        [SerializeField,TextArea] private string[] ModeText =
        {
            "One key is hidden in a locker. Find it, battle other players for it on the same tile, and carry it out.",
            "Something is loose in the school. Stay alive together until the clock runs out.",
            "Free movement in the gym. Combat rules are still being developed.",
            "Chase, tag and pass the potato before it explodes."
        };
        [SerializeField] private string[] ModeWin = { "Carry the key through the EXIT first", "Survive until time is up or complete the tasks to escape", "Be the last player standing", "Be the last player standing" };
        private static readonly Color AvatarColor = new Color(1f, 0.835f, 0f);
        private static readonly int[] Barcode = { 3, 1, 2, 1, 4, 1, 1, 3, 2, 1, 1, 2, 4, 1, 2, 1, 3, 1, 1, 2, 1, 4, 2, 1, 1, 3, 1, 2, 1, 1, 4, 1, 2, 3, 1, 1, 2, 1, 3, 1 };

        private const string MusicKey = "E4N_Music";
        private const string SfxKey = "E4N_Sfx";

        private readonly LeaderboardPanel leaderboard = new LeaderboardPanel();
        private readonly MatchHistoryPanel history = new MatchHistoryPanel();
        private Section section = Section.GameMode;
        private int mode;

        private int infoMode = -1;

        private int music = 5;
        private int sfx = 6;
        private string toast = "";
        private float toastUntil;
        private GUIStyle h2, menuTitle, menuSub, body, small, smallBold, button, cardName, tagStyle, logoShade, logoNow, idStyle, passTitle, stamp, keyName;

        private void Start()
        {
            music = PlayerPrefs.GetInt(MusicKey, music);
            sfx = PlayerPrefs.GetInt(SfxKey, sfx);
            AudioListener.volume = sfx / 8f;
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.escapeKey.wasPressedThisFrame)
            {
                Back();
                return;
            }
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
            logoShade = Text(BungeeShade, 40);
            logoNow = Text(Bungee, 32);
            idStyle = Text(Bungee, 13, TextAnchor.MiddleCenter);
            passTitle = Text(Bungee, 20);
            stamp = Text(Bungee, 26, TextAnchor.MiddleCenter);
            keyName = Text(MonoBoldFont, 13, TextAnchor.MiddleCenter);
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
                case Section.Leaderboard: leaderboard.Draw(inner, h2, headingColor, accent); break;
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
            Label(new Rect(me.x + 54f, me.y + 6f, 170f, 14f), AccountService.IsSignedIn ? "SIGNED IN AS" : "PLAYING AS", menuSub, Muted);
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
                if (Hit(r)) section = (Section)i;
            }
        }

        private void DrawGameMode(Rect inner)
        {
   if (infoMode >= 0)
   {
       DrawModeInfo(inner);
       return;
   }

            Label(new Rect(inner.x, inner.y, 480f, 44f), "CHOOSE GAME MODE", h2, headingColor);
            Label(new Rect(inner.xMax - 200f, inner.y + 10f, 200f, 24f), ModeNames[mode] + " SELECTED", Text(MonoFont, 13, TextAnchor.MiddleRight), Muted);

            int count = ModeNames.Length;
            float cw = (inner.width - 14f) / 2;
            for (int i = 0; i < count; i++)
            {
                bool on = mode == i;
                Rect c = new Rect(inner.x + (i % 2) * (cw + 14f), inner.y + 62f + (i / 2) * 172f, cw, 158f);
                if (on) Block(c, accent, Ink, 4f, Red, 6f);
                else Block(c, Ink, Hover(c) ? Hex("#55556a") : Hex("#2e2e38"), 4f, Color.clear, 0f);
                Color text = on ? Ink : Color.white;

                Rect iconBox = new Rect(c.x + 14f, c.y + 12f, 32f, 32f);
                if (on) Fill(iconBox, Ink);
                else Block(iconBox, Hex("#1e1e26"), Line, 2f, Color.clear, 0f);
                DrawModeIcon(i == 0 ? 0 : 1, new Rect(iconBox.x + 4f, iconBox.y + 4f, 24f, 24f), accent);

                Vector2 tagSize = tagStyle.CalcSize(new GUIContent(ModePlayers[i]));
                Rect tagRect = new Rect(c.xMax - 18f - tagSize.x - 12f, c.y + 18f, tagSize.x + 12f, 20f);
                if (on) Fill(tagRect, Ink);
                else Outline(tagRect, 2f, Hex("#44444f"));
                Label(tagRect, ModePlayers[i], tagStyle, on ? accent : Hex("#bdbdbd"));

                Label(new Rect(c.x + 55f, c.y + 14f, cw - 175f, 30f), ModeNames[i], Text(MonoFont, 18, TextAnchor.MiddleLeft, true), text);
                Label(new Rect(c.x + 18f, c.y + 54f, cw - 36f, 55f), ModeText[i], small, text);
                Fill(new Rect(c.x + 18f, c.yMax - 42f, cw - 36f, 2f), on ? WithAlpha(Ink, 0.35f) : Hex("#2e2e38"));
                Label(new Rect(c.x + 18f, c.yMax - 34f, cw - 36f, 30f), "WIN: " + ModeWin[i], Text(MonoFont, 11, TextAnchor.UpperLeft, true), text);
        
                if (Hit(c)) mode = i;
            }

        
   float bw = BigButton(new Rect(inner.x, inner.yMax - 64f, 0f, 64f), "START " + ModeNames[mode], true, out bool startClicked);
   if (startClicked) StartMode();
   Rect info = new Rect(inner.x + bw + 18f, inner.yMax - 64f, 64f, 64f);
   Rect infoDrawn = info;
   if (Hover(info)) { infoDrawn.x -= 3f; infoDrawn.y -= 3f; }
   Block(infoDrawn, Ink, Color.white, 4f, Red, 6f);
   Label(infoDrawn, "i", Text(Bungee, 30, TextAnchor.MiddleCenter), Color.white);
   if (Hit(info)) infoMode = mode;
        }
        
          
           private static readonly string[] ModeDetails =
   {
       "GOAL\nBe the first player to carry the key through the EXIT.\n\nHOW IT WORKS\nOne key is hidden in a locker somewhere in the school. Find it, then carry it to the EXIT. If you are on the same tile as another player, you can battle them for the key.\n\nPLAYERS\nUp to 4 players.",
       "GOAL\nSurvive until time runs out, or complete the task to escape.\n\nHOW IT WORKS\nSomething is loose in the school. Work together as a team and solve puzzles to unlock the exits. Each survivor escapes on their own, so don't get caught.\n\nTHE MONSTER\nOne player can be the Monster. If nobody takes that role, the computer plays it.\n\nPLAYERS\nUp to 4 players.",
       "GOAL\nBe the last player standing.\n\nHOW IT WORKS\nEveryone moves freely around the gym and fights to be the last one left. Combat rules are still being developed, so details may change.\n\nPLAYERS\nUp to 4 players.",
       "GOAL\nBe the last player standing.\n\nHOW IT WORKS\nOne player starts with the potato. Chase, tag and pass it to someone else before it explodes. Whoever is holding it when it explodes is out.\n\nPLAYERS\nUp to 4 players."
   };

   private void DrawModeInfo(Rect inner)
   {
       Label(new Rect(inner.x, inner.y, 480f, 44f), ModeNames[infoMode], h2, headingColor);
       Label(new Rect(inner.xMax - 200f, inner.y + 10f, 200f, 24f), "HOW TO PLAY", Text(MonoFont, 13, TextAnchor.MiddleRight), Muted);
       Label(new Rect(inner.x, inner.y + 64f, inner.width, 330f), ModeDetails[infoMode], body, Soft);
       if (BigButton(new Rect(inner.x, inner.yMax - 64f, 0f, 64f), "BACK", true)) infoMode = -1;
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
            Color me = AvatarColor;
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

        private void DrawAccount(Rect inner)
        {
            Label(new Rect(inner.x, inner.y, 480f, 44f), "ACCOUNT", h2, headingColor);
            history.Draw(new Rect(inner.x, inner.y + 50f, inner.width, inner.height - 50f), accent);

            string text = AccountService.IsSignedIn ? "LOG OUT" : "LOG IN";
            float width = Text(Bungee, 20, TextAnchor.MiddleCenter).CalcSize(new GUIContent(text)).x + 72f;
            if (BigButton(new Rect(inner.xMax - width, inner.yMax - 64f, width, 64f), text, false)) LogOut();
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

        //The player's badge in the header: a color with their first letter.
        private void DrawAvatar(Rect r, GUIStyle letter, float border)
        {
            Fill(r, Color.white);
            Rect inside = new Rect(r.x + border, r.y + border, r.width - border * 2f, r.height - border * 2f);
            Fill(inside, AvatarColor);
            string initial = DisplayName.Length > 0 ? DisplayName.Substring(0, 1) : "?";
            Label(inside, initial, letter, Ink);
        }

        private static string DisplayName => AccountService.DisplayName;

        //Returns to the start screen, which opens the login panel.
        private void LogOut()
        {
            AccountService.LogOut();
            Back();
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
