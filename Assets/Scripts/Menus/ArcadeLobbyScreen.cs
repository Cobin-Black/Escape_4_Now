using Escape4Now.Accounts;
using Escape4Now.GameModes;
using Escape4Now.Multiplayer;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static Escape4Now.Menus.ArcadeMenuKit;

namespace Escape4Now.Menus
{
    //Arcade-style player select for Race and Survival, shown before the match starts.
    //It adds itself to any scene with a LocalMultiplayerSession and hides the old lobby panel while it is open.
    //It only uses the session's public setup methods, so the match starts exactly as it did from the old lobby.
    public sealed class ArcadeLobbyScreen : MonoBehaviour
    {
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private Color background = new Color(0.663f, 0.863f, 1f);
        [SerializeField] private Color accent = new Color(0.212f, 0.945f, 0.804f);

        private static readonly Color PanelNavy = Hex("#1d3a66");
        private static readonly Color BoxNavy = Hex("#16305a");
        private static readonly Color CardNavy = Hex("#0f2447");
        private static readonly Color DeepNavy = Hex("#0b1c38");
        private static readonly Color EdgeBlue = Hex("#2f5590");
        private static readonly Color DashBlue = Hex("#4a6fa8");
        private static readonly Color SoftBlue = Hex("#a9c4e6");
        private static readonly Color PaleBlue = Hex("#8fd0ff");
        private static readonly Color OffText = Hex("#8fa6c8");
        private static readonly Color Tape = Hex("#d4f0ff");
        private static readonly Color MonsterCard = Hex("#3a1024");
        private static readonly Color[] SlotColors = { Hex("#ffd500"), Hex("#36f1cd"), Hex("#ff4fd8"), Hex("#7dff9a") };

        private LocalMultiplayerSession session;
        private bool hidOldLobby;
        private readonly string[] nameBuffers = new string[4];
        private GUIStyle modeName, smallWrap, tiny, tinyBold, chipBig, h2, bandLabel, field, monoField, startStyle, arrowStyle, logoShade, logoNow, hintStyle;

        //Account linking. Players 2-4 can link an account so their stats are saved; Player 1 is the logged in account.
        private const string LinkUserControl = "ArcadeLinkUser";
        private const string LinkPassControl = "ArcadeLinkPass";
        private static readonly Color ErrorText = Hex("#ff8a93");
        private readonly bool[] linked = new bool[4];
        private bool signedIn;
        private int linkingSlot = -1;
        private string linkUser = "";
        private string linkPass = "";
        private string linkStatus = "";
        private bool focusLinkUser;
        private int linkOpenedFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var found = FindFirstObjectByType<LocalMultiplayerSession>();
            if (found == null || FindFirstObjectByType<ArcadeLobbyScreen>() != null) return;
            var screen = new GameObject("Arcade Lobby").AddComponent<ArcadeLobbyScreen>();
            screen.session = found;
        }

        private bool IsSurvival => session != null && session.Mode is SurvivalModeController;
        private bool Showing => session != null && session.Mode != null && !session.MatchStarted && !LanMultiplayerSession.Active;
        private int MinPlayers => session.Mode.MinimumPlayers;
        private int MaxPlayers => Mathf.Min(4, session.Mode.MaximumPlayers);

        private void Start()
        {
            if (session == null) session = FindFirstObjectByType<LocalMultiplayerSession>();
            if (session == null) { enabled = false; return; }
            for (int i = 0; i < nameBuffers.Length; i++) nameBuffers[i] = session.GetPlayerName(i).ToUpperInvariant();
            if (session.Skins != null) session.Skins.FillMissing();
        }

        private void Update()
        {
            if (session == null) return;
            //Hide the old lobby only while this screen is up, and give the temporary UI back once the match starts.
            if (Showing)
            {
                if (session.ShowTemporaryUI)
                {
                    session.SetTemporaryUIVisible(false);
                    hidOldLobby = true;
                }
            }
            else if (hidOldLobby)
            {
                session.SetTemporaryUIVisible(true);
                hidOldLobby = false;
            }
            if (!Showing) return;
            SyncAccounts();

            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (linkingSlot >= 0)
            {
                //The link popup takes Escape, so it never sends the players back to the menu.
                if (k.escapeKey.wasPressedThisFrame && !AccountService.IsBusy) CloseLink();
                return;
            }
            if (k.escapeKey.wasPressedThisFrame)
            {
                if (GUIUtility.keyboardControl != 0) GUIUtility.keyboardControl = 0;
                else BackToMenu();
            }
            else if (GUIUtility.keyboardControl == 0 && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
                StartMatch();
        }

        private void OnDisable()
        {
            if (hidOldLobby && session != null) session.SetTemporaryUIVisible(true);
            hidOldLobby = false;
        }

        private void BackToMenu()
        {
            TryLoadScene(mainMenuScene);
        }

        private void StartMatch()
        {
            if (linkingSlot >= 0 || AccountService.IsBusy) return;
            GUIUtility.keyboardControl = 0;
            for (int i = 0; i < session.PlayerCount && i < nameBuffers.Length; i++) session.SetPlayerName(i, NameFor(i));
            session.StartLocalMatch();
        }

        //A linked player always plays under their account name.
        private string NameFor(int i)
        {
            return linked[i] && !session.IsBotPlayer(i) ? AccountService.LinkedName(i) : nameBuffers[i];
        }

        //Player 1 is briefly signed out while another account links, so the last state is kept until that finishes.
        //Linking swaps the slot to the account name, and unlinking puts the typed name back.
        private void SyncAccounts()
        {
            if (AccountService.IsBusy) return;
            signedIn = AccountService.IsSignedIn;
            for (int i = 0; i < linked.Length; i++)
            {
                bool now = AccountService.IsLinked(i);
                if (now && !session.IsBotPlayer(i))
                {
                    string accountName = AccountService.LinkedName(i);
                    if (session.GetPlayerName(i) != accountName) session.SetPlayerName(i, accountName);
                }
                else if (!now && linked[i]) session.SetPlayerName(i, nameBuffers[i]);
                linked[i] = now;
            }
            if (linkingSlot >= 0 && (!signedIn || session.IsBotPlayer(linkingSlot))) CloseLink();
        }

        private void CreateStyles()
        {
            if (h2 != null) return;
            logoShade = Text(BungeeShade, 40);
            logoNow = Text(Bungee, 32);
            modeName = Text(Bungee, 30);
            h2 = Text(Bungee, 32);
            smallWrap = Text(MonoFont, 13, TextAnchor.UpperLeft, true);
            tiny = Text(MonoFont, 11);
            tinyBold = Text(MonoBoldFont, 11, TextAnchor.MiddleCenter);
            chipBig = Text(Bungee, 24, TextAnchor.MiddleCenter);
            bandLabel = Text(Bungee, 20);
            startStyle = Text(Bungee, 30, TextAnchor.MiddleCenter);
            arrowStyle = Text(Bungee, 16, TextAnchor.MiddleCenter);
            hintStyle = Text(MonoBoldFont, 14, TextAnchor.MiddleLeft, true);
            field = new GUIStyle(GUI.skin.textField)
            {
                font = Bungee,
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 0, 0),
                border = new RectOffset(0, 0, 0, 0)
            };
            foreach (GUIStyleState s in new[] { field.normal, field.hover, field.focused, field.active, field.onNormal, field.onFocused })
            {
                s.background = null;
                s.textColor = Color.white;
            }
            //Usernames and passwords keep their real case, so the link popup uses the mono font.
            monoField = new GUIStyle(field) { font = MonoFont, fontSize = 16, padding = new RectOffset(12, 12, 0, 0) };
        }

        private void OnGUI()
        {
            if (!Showing) return;
            CreateStyles();
            //Draw over the game's own UI.
            GUI.depth = -100;
            Matrix4x4 baseMatrix = BeginScaled();
            float t = Time.unscaledTime;
            float ox = OffsetX;

            Fill(new Rect(0f, 0f, Width, DesignHeight), background);
            Color gridLine = WithAlpha(Navy, 0.12f);
            for (float x = 0f; x < Width; x += 40f) Fill(new Rect(x, 0f, 1f, DesignHeight), gridLine);
            for (float y = 0f; y < DesignHeight; y += 40f) Fill(new Rect(0f, y, Width, 1f), gridLine);
            Stripes(new Rect(0f, 0f, Width, 18f), Tape, Navy, t * 36f, 22f);

            //Everything behind the link popup stops taking clicks while it is open.
            bool linking = linkingSlot >= 0;
            GUI.enabled = !linking;
            DrawHeader(ox);
            DrawModePanel(new Rect(ox + 56f, 118f, 290f, 470f));
            DrawPlayers(new Rect(ox + 378f, 118f, 846f, 470f), t);
            DrawBottomBar(new Rect(ox + 56f, 612f, 1168f, 72f));
            GUI.enabled = true;
            if (linking) DrawLinkPopup(ox);

            GUI.matrix = baseMatrix;
        }

        private void DrawHeader(float ox)
        {
            float x = ox + 56f;
            float w = logoShade.CalcSize(new GUIContent("ESCAPE")).x;
            Label(new Rect(x, 36f, w + 8f, 56f), "ESCAPE", logoShade, Color.white, Navy, 2f, ShadowBlue, 4f);
            Die(new Rect(x + w + 14f, 45f, 38f, 38f), 4, Color.white, Ink, Red, 3f);
            Label(new Rect(x + w + 64f, 36f, 120f, 56f), "NOW", logoNow, Navy);

            Rect back = new Rect(ox + 1224f - 190f, 40f, 190f, 48f);
            Block(back, PanelNavy, Color.white, 2f, Color.clear, 0f);
            Label(back, "ESC · MAIN MENU", Text(MonoFont, 14, TextAnchor.MiddleCenter), Tape);
            if (Hit(back)) BackToMenu();
        }

        private void DrawModePanel(Rect r)
        {
            Block(r, PanelNavy, Color.white, 4f, CardNavy, 8f);
            float x = r.x + 22f;
            float w = r.width - 44f;
            float y = r.y + 22f;

            Rect icon = new Rect(x, y, 52f, 52f);
            Fill(icon, accent);
            if (IsSurvival) DrawStopwatch(new Rect(icon.x + 11f, icon.y + 11f, 30f, 30f), Ink);
            else DrawFlag(new Rect(icon.x + 11f, icon.y + 11f, 30f, 30f), Ink);
            Label(new Rect(x + 64f, y + 4f, w - 64f, 16f), "GAME MODE", tiny, SoftBlue);
            Label(new Rect(x + 64f, y + 18f, w - 64f, 34f), IsSurvival ? "SURVIVAL" : "RACE", modeName, Color.white);
            y += 66f;

            string rule = IsSurvival
                ? "Solve puzzles as a team to unlock the exits. Each survivor escapes on their own. Don't get caught."
                : "One key is hidden in a locker. Find it, battle for it, and carry it through the EXIT first.";
            Label(new Rect(x, y, w, 84f), rule, smallWrap, Soft);
            y += 90f;
            Fill(new Rect(x, y, w, 2f), EdgeBlue);
            y += 14f;
            Label(new Rect(x, y, w, 16f), "HOW MANY PLAYERS?", Text(MonoFont, 12), SoftBlue);
            y += 24f;

            float cw = (w - 10f) * 0.5f;
            for (int n = 1; n <= 4; n++)
            {
                int col = (n - 1) % 2;
                int row = (n - 1) / 2;
                Rect chip = new Rect(x + col * (cw + 10f), y + row * 74f, cw, 64f);
                bool allowed = n >= MinPlayers && n <= MaxPlayers;
                bool on = session.PlayerCount == n;
                if (on) Block(chip, accent, accent, 4f, Red, 4f);
                else Block(chip, allowed ? CardNavy : WithAlpha(CardNavy, 0.4f), allowed ? EdgeBlue : WithAlpha(EdgeBlue, 0.4f), 4f, Color.clear, 0f);
                Color text = on ? Ink : allowed ? Color.white : WithAlpha(Color.white, 0.3f);
                Label(new Rect(chip.x, chip.y + 8f, chip.width, 28f), n == 1 ? "SOLO" : n.ToString(), chipBig, text);
                string sub = n == 1 ? (IsSurvival ? "you vs the AI" : "just you") : "players";
                Label(new Rect(chip.x, chip.y + 38f, chip.width, 16f), sub, Text(MonoFont, 11, TextAnchor.MiddleCenter), text);
                if (allowed && Hit(chip)) session.SetPlayerCount(n);
            }

            Rect foot = new Rect(x, r.yMax - 22f - 66f, w, 66f);
            if (IsSurvival)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI);
                Fill(new Rect(foot.x - 4f, foot.y - 4f, foot.width + 8f, foot.height + 8f), WithAlpha(Red, 0.25f * pulse));
                Block(foot, Hex("#1c0b0f"), Red, 3f, Color.clear, 0f);
                Rect m = new Rect(foot.x + 12f, foot.y + 13f, 40f, 40f);
                Fill(m, Red);
                DrawMonsterIcon(new Rect(m.x + 7f, m.y + 7f, 26f, 26f), Ink);
                Label(new Rect(m.xMax + 12f, foot.y + 14f, foot.width - 70f, 16f), "THE MONSTER", tiny, Hex("#ff8a93"));
                int monster = MonsterIndex();
                Label(new Rect(m.xMax + 12f, foot.y + 30f, foot.width - 70f, 22f), monster >= 0 ? "PLAYED BY P" + (monster + 1) : "CONTROLLED BY THE AI", Text(Bungee, 12), Color.white);
            }
            else if (session.Mode.SupportsBots)
            {
                Label(new Rect(foot.x, foot.y + 10f, foot.width, 56f), "Not enough people? Switch any slot to BOT and the computer plays it.", Text(MonoFont, 12, TextAnchor.UpperLeft, true), Tape);
            }
        }

        private void DrawPlayers(Rect r, float t)
        {
            Block(r, BoxNavy, Color.white, 4f, accent, 8f);
            Label(new Rect(r.x + 22f, r.y + 18f, 400f, 40f), "WHO'S PLAYING?", h2, PaleBlue);
            Label(new Rect(r.xMax - 22f - 380f, r.y + 26f, 380f, 20f), Summary(), Text(MonoFont, 13, TextAnchor.MiddleRight), SoftBlue);

            float top = r.y + 74f;
            float h = r.yMax - 20f - top;
            float cw = (r.width - 44f - 42f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                Rect card = new Rect(r.x + 22f + i * (cw + 14f), top, cw, h);
                if (i >= MaxPlayers) continue;
                Matrix4x4 m = BeginRotate(i % 2 == 0 ? -0.8f : 0.8f, card.center);
                if (i < session.PlayerCount) DrawSlot(card, i, t);
                else DrawOpenSlot(card, i);
                EndRotate(m);
            }
        }

        private void DrawOpenSlot(Rect card, int i)
        {
            Fill(card, WithAlpha(CardNavy, 0.55f));
            DashedOutline(card, 4f, DashBlue);
            Rect plus = new Rect(card.center.x - 28f, card.center.y - 44f, 56f, 56f);
            DashedOutline(plus, 3f, Hex("#55556a"));
            Label(plus, "+", Text(Bungee, 28, TextAnchor.MiddleCenter), SoftBlue);
            Label(new Rect(card.x, plus.yMax + 10f, card.width, 22f), "ADD P" + (i + 1), Text(Bungee, 16, TextAnchor.MiddleCenter), SoftBlue);
            if (Hit(card)) session.SetPlayerCount(i + 1);
        }

        private void DrawSlot(Rect card, int i, float t)
        {
            bool survival = IsSurvival;
            bool isMonster = survival && session.IsMonsterPlayer(i);
            bool bot = session.IsBotPlayer(i);
            //A linked player must be unlinked before their slot can become a bot.
            bool canBot = session.Mode.SupportsBots && !(session.PlayerCount == 1 && i == 0) && !(i > 0 && linked[i] && !bot);
            Color c = isMonster ? Red : SlotColors[i];

            Fill(new Rect(card.x + 5f, card.y + 5f, card.width, card.height), DeepNavy);
            Block(card, isMonster ? MonsterCard : CardNavy, c, 4f, Color.clear, 0f);

            //Colored top strip with locker vents and the role tag.
            Rect band = new Rect(card.x + 4f, card.y + 4f, card.width - 8f, 36f);
            Fill(band, c);
            Label(new Rect(band.x + 10f, band.y, 40f, band.height), "P" + (i + 1), bandLabel, Ink);
            for (int v = 0; v < 3; v++) Fill(new Rect(band.x + 50f, band.y + 11f + v * 6f, 20f, 3f), WithAlpha(Ink, 0.35f));
            string role = survival ? (isMonster ? "MONSTER" : "SURVIVOR") : (bot ? "BOT" : "PLAYER");
            float tw = tinyBold.CalcSize(new GUIContent(role)).x + 12f;
            Rect tag = new Rect(band.xMax - 8f - tw, band.y + 8f, tw, 20f);
            Fill(tag, Ink);
            Label(tag, role, tinyBold, c);

            float x = card.x + 14f;
            float w = card.width - 28f;
            float y = band.yMax + 10f;

            //Skin preview with arrows.
            float frameH = survival ? 118f : 150f;
            Rect frame = new Rect(x, y, w, frameH);
            Fill(frame, DeepNavy);
            DashedOutline(frame, 2f, DashBlue);
            DrawSkinPreview(new Rect(frame.x + 36f, frame.y + 8f, frame.width - 72f, frameH - 34f), i, c, bot, t);
            var skins = session.Skins;
            string skinText = skins != null && skins.Count > 0 && skins.GetSkinIndex(i) >= 0
                ? "SKIN " + (skins.GetSkinIndex(i) + 1) + " / " + skins.Count
                : "NO SKIN";
            Label(new Rect(frame.x, frame.yMax - 22f, frame.width, 16f), skinText, Text(MonoFont, 11, TextAnchor.MiddleCenter), SoftBlue);
            if (skins != null && skins.Count > 1)
            {
                Rect prev = new Rect(frame.x + 4f, frame.center.y - 18f, 30f, 36f);
                Rect next = new Rect(frame.xMax - 34f, frame.center.y - 18f, 30f, 36f);
                Fill(prev, BoxNavy);
                Fill(next, BoxNavy);
                Label(prev, "<", arrowStyle, Color.white);
                Label(next, ">", arrowStyle, Color.white);
                if (Hit(prev)) CycleSkin(i, -1);
                if (Hit(next)) CycleSkin(i, 1);
            }
            y = frame.yMax + 6f;

            //One square per skin: yours is filled in your color, another player's is crossed out in theirs,
            //and an open square can be clicked to take that skin. No two players can hold the same skin.
            if (skins != null && skins.Count > 0)
            {
                int count = skins.Count;
                float size = Mathf.Min(16f, (w - (count - 1) * 4f) / count);
                float rowX = x + (w - (count * size + (count - 1) * 4f)) * 0.5f;
                for (int s = 0; s < count; s++)
                {
                    Rect sq = new Rect(rowX + s * (size + 4f), y, size, size);
                    int owner = ActiveOwner(s);
                    if (owner == i) Block(sq, c, Color.white, 2f, Color.clear, 0f);
                    else if (owner >= 0)
                    {
                        Fill(sq, WithAlpha(OwnerColor(owner), 0.4f));
                        Fill(new Rect(sq.x, sq.center.y - 1f, sq.width, 2f), DeepNavy);
                    }
                    else
                    {
                        if (Hover(sq)) Fill(sq, WithAlpha(Color.white, 0.25f));
                        Outline(sq, 2f, DashBlue);
                        if (Hit(sq)) AssignSkin(i, s);
                    }
                }
                y += size + 8f;
            }

            //Name box. Bots get a fixed name.
            float fieldH = survival ? 38f : 42f;
            Rect nameRect = new Rect(x, y, w, fieldH);
            if (bot)
            {
                Block(nameRect, Hex("#12284d"), EdgeBlue, 2f, Color.clear, 0f);
                Label(new Rect(nameRect.x + 10f, nameRect.y, nameRect.width - 20f, nameRect.height), "BOT " + (i + 1), Text(Bungee, 15), OffText);
            }
            else if (linked[i])
            {
                //Linked names come from the account and can't be typed over.
                Block(nameRect, Hex("#12284d"), c, 2f, Color.clear, 0f);
                Label(new Rect(nameRect.x + 10f, nameRect.y, nameRect.width - 44f, nameRect.height), AccountService.LinkedName(i), Text(Bungee, 15), Color.white);
                DrawChainIcon(new Rect(nameRect.xMax - 30f, nameRect.center.y - 6f, 20f, 12f), c);
            }
            else
            {
                string control = "ArcadeLobbyName" + i;
                bool focused = GUI.GetNameOfFocusedControl() == control;
                Block(nameRect, DeepNavy, focused ? c : DashBlue, 2f, Color.clear, 0f);
                GUI.SetNextControlName(control);
                string typed = GUI.TextField(new Rect(nameRect.x + 2f, nameRect.y + 2f, nameRect.width - 4f, nameRect.height - 4f), nameBuffers[i] ?? "", 12, field);
                if (typed != nameBuffers[i])
                {
                    nameBuffers[i] = typed.ToUpperInvariant();
                    session.SetPlayerName(i, nameBuffers[i]);
                }
            }
            y = nameRect.yMax + 8f;

            float segH = survival ? 34f : 38f;
            if (session.Mode.SupportsBots)
            {
                int pickBot = Segmented(new Rect(x, y, w, segH), "PLAYER", "BOT", bot ? 1 : 0, c, c, canBot);
                if (pickBot == 0 && bot) session.SetBotPlayer(i, false);
                if (pickBot == 1 && !bot && canBot) session.SetBotPlayer(i, true);
                y += segH + 8f;
            }
            if (survival)
            {
                int pickRole = Segmented(new Rect(x, y, w, segH), "SURVIVOR", "MONSTER", isMonster ? 1 : 0, SlotColors[i], Red, true);
                //SetMonsterPlayer switches the Monster on and off for that slot.
                if ((pickRole == 1 && !isMonster) || (pickRole == 0 && isMonster)) session.SetMonsterPlayer(i);
                y += segH + 8f;
            }
            //Account link sits at the bottom of the card. It only shows when Player 1 is logged in, and never for bots.
            if (signedIn && !bot) DrawLinkRow(new Rect(x, y, w, segH), i, c);
        }

        //Player 1 shows they are logged in. Players 2-4 click to link an account, or click again to unlink it.
        private void DrawLinkRow(Rect r, int i, Color c)
        {
            bool on = linked[i];
            bool playerOne = i == 0;
            bool hover = !playerOne && GUI.enabled && Hover(r);
            if (on) Block(r, hover ? Red : c, hover ? Red : c, 2f, Color.clear, 0f);
            else Block(r, DeepNavy, hover ? c : DashBlue, 2f, Color.clear, 0f);

            Color ink = on ? Ink : hover ? c : OffText;
            string text = playerOne ? "LOGGED IN" : on ? (hover ? "UNLINK" : "LINKED") : "LINK ACCOUNT";
            GUIStyle style = Text(Bungee, 12);
            float tw = style.CalcSize(new GUIContent(text)).x;
            float sx = r.center.x - (24f + 8f + tw) * 0.5f;
            DrawChainIcon(new Rect(sx, r.center.y - 7f, 24f, 14f), ink);
            Label(new Rect(sx + 32f, r.y, tw + 4f, r.height), text, style, ink);

            if (playerOne || !Hit(r) || AccountService.IsBusy) return;
            if (on) AccountService.UnlinkPlayer(i);
            else OpenLink(i);
        }

        //Two overlapping rings, the same chain shape the old lobby used.
        private static void DrawChainIcon(Rect r, Color c)
        {
            float d = r.height;
            RingShape(new Rect(r.x, r.y, d, d), c);
            RingShape(new Rect(r.xMax - d, r.y, d, d), c);
        }

        private void OpenLink(int slot)
        {
            linkingSlot = slot;
            linkUser = "";
            linkPass = "";
            linkStatus = "";
            //A focused box keeps showing its old text, so focus is dropped before moving to the username box.
            GUIUtility.keyboardControl = 0;
            focusLinkUser = true;
            linkOpenedFrame = Time.frameCount;
        }

        private void CloseLink()
        {
            linkingSlot = -1;
            linkPass = "";
            GUIUtility.keyboardControl = 0;
        }

        //Login popup for linking a player. Tab moves between the boxes, Enter links and Escape cancels.
        private void DrawLinkPopup(float ox)
        {
            int slot = linkingSlot;
            Color c = SlotColors[slot];
            bool busy = AccountService.IsBusy;

            Fill(new Rect(0f, 0f, Width, DesignHeight), WithAlpha(Ink, 0.7f));
            Rect box = new Rect(ox + 640f - 240f, 160f, 480f, 400f);
            Block(box, PanelNavy, Color.white, 4f, accent, 8f);

            Rect band = new Rect(box.x + 4f, box.y + 4f, box.width - 8f, 40f);
            Fill(band, c);
            Label(new Rect(band.x + 14f, band.y, 40f, band.height), "P" + (slot + 1), bandLabel, Ink);
            for (int v = 0; v < 3; v++) Fill(new Rect(band.x + 54f, band.y + 13f + v * 6f, 20f, 3f), WithAlpha(Ink, 0.35f));
            const string tagText = "ACCOUNT";
            float tagW = tinyBold.CalcSize(new GUIContent(tagText)).x + 12f;
            Rect tag = new Rect(band.xMax - 12f - tagW, band.y + 10f, tagW, 20f);
            Fill(tag, Ink);
            Label(tag, tagText, tinyBold, c);

            float x = box.x + 28f;
            float w = box.width - 56f;
            float y = band.yMax + 14f;
            Label(new Rect(x, y, w, 36f), "LINK PLAYER " + (slot + 1), Text(Bungee, 26), PaleBlue);
            y += 38f;
            Label(new Rect(x, y, w, 20f), "Log in so this player's stats are saved.", Text(MonoFont, 12), Soft);
            y += 28f;

            bool ready = Time.frameCount > linkOpenedFrame;
            Event e = Event.current;
            bool submit = ready && !busy && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            if (submit) e.Use();

            GUI.enabled = !busy;
            linkUser = LinkBox(new Rect(x, y + 20f, w, 42f), "USERNAME", LinkUserControl, linkUser, AccountService.UsernameLimit, false, c);
            y += 76f;
            linkPass = LinkBox(new Rect(x, y + 20f, w, 42f), "PASSWORD", LinkPassControl, linkPass, AccountService.PasswordLimit, true, c);
            y += 76f;
            if (focusLinkUser && ready)
            {
                GUI.FocusControl(LinkUserControl);
                focusLinkUser = false;
            }

            string status = busy ? "LINKING..." : linkStatus;
            Label(new Rect(x, y, w, 32f), status, Text(MonoFont, 12, TextAnchor.MiddleCenter, true), busy ? Tape : ErrorText);

            float bw = (w - 14f) * 0.5f;
            float by = box.yMax - 24f - 52f;
            if (LinkButton(new Rect(x, by, bw, 52f), "CANCEL", false)) CloseLink();
            if (LinkButton(new Rect(x + bw + 14f, by, bw, 52f), "LINK", true)) submit = true;
            GUI.enabled = true;
            if (submit) SubmitLink();
        }

        private string LinkBox(Rect box, string title, string control, string value, int limit, bool hidden, Color focusColor)
        {
            Label(new Rect(box.x, box.y - 20f, box.width, 16f), title, Text(MonoFont, 12), SoftBlue);
            Block(box, DeepNavy, GUI.GetNameOfFocusedControl() == control ? focusColor : DashBlue, 2f, Color.clear, 0f);
            Rect text = new Rect(box.x + 2f, box.y + 2f, box.width - 4f, box.height - 4f);
            GUI.SetNextControlName(control);
            return hidden ? GUI.PasswordField(text, value ?? "", '*', limit, monoField) : GUI.TextField(text, value ?? "", limit, monoField);
        }

        private bool LinkButton(Rect r, string text, bool primary)
        {
            Rect drawn = r;
            if (GUI.enabled && Hover(r)) { drawn.x -= 3f; drawn.y -= 3f; }
            if (primary) Block(drawn, accent, Ink, 4f, Red, 6f);
            else Block(drawn, CardNavy, Color.white, 3f, DeepNavy, 6f);
            Label(drawn, text, Text(Bungee, 18, TextAnchor.MiddleCenter), primary ? Ink : Color.white);
            return Hit(r);
        }

        private async void SubmitLink()
        {
            if (AccountService.IsBusy || linkingSlot < 0) return;
            int slot = linkingSlot;
            string username = linkUser.Trim();
            if (username.Length == 0 || linkPass.Length == 0)
            {
                linkStatus = "Enter your username and password.";
                return;
            }
            linkStatus = "";
            string error = await AccountService.LinkPlayerAsync(slot, username, linkPass);
            if (this == null) return;
            if (error == null)
            {
                if (linkingSlot == slot) CloseLink();
                return;
            }
            //Drop focus so the cleared password box shows empty.
            GUIUtility.keyboardControl = 0;
            linkPass = "";
            linkStatus = error;
        }

        //Two-option switch. Returns the clicked option, or -1.
        private static int Segmented(Rect r, string left, string right, int selected, Color leftOn, Color rightOn, bool rightEnabled)
        {
            Fill(r, DashBlue);
            Rect inner = new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f);
            Rect a = new Rect(inner.x, inner.y, inner.width * 0.5f, inner.height);
            Rect b = new Rect(a.xMax, inner.y, inner.width - a.width, inner.height);
            GUIStyle s = Text(Bungee, 12, TextAnchor.MiddleCenter);
            Fill(a, selected == 0 ? leftOn : DeepNavy);
            Fill(b, selected == 1 ? rightOn : DeepNavy);
            Label(a, left, s, selected == 0 ? Ink : OffText);
            Label(b, right, s, selected == 1 ? Ink : rightEnabled ? OffText : WithAlpha(OffText, 0.35f));
            if (Hit(a)) return 0;
            if (rightEnabled && Hit(b)) return 1;
            return -1;
        }

        //Shows the player's skin sprite, or a colored tile with their first letter when there is none.
        private void DrawSkinPreview(Rect box, int i, Color c, bool bot, float t)
        {
            float bob = Mathf.Sin((t + i * 0.3f) * Mathf.PI * 2f / 1.8f) * 2.5f - 2.5f;
            PlayerSkin skin = session.Skins != null ? session.Skins.GetPlayerSkin(i) : null;
            Sprite sprite = skin != null ? skin.front : null;
            if (sprite != null && sprite.texture != null)
            {
                Texture2D tex = sprite.texture;
                Rect tr = sprite.textureRect;
                float aspect = tr.width / Mathf.Max(1f, tr.height);
                float hgt = box.height;
                float wid = hgt * aspect;
                if (wid > box.width) { wid = box.width; hgt = wid / aspect; }
                Rect dest = new Rect(box.center.x - wid * 0.5f, box.center.y - hgt * 0.5f + bob, wid, hgt);
                GUI.DrawTextureWithTexCoords(dest, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
                return;
            }
            float size = Mathf.Min(64f, box.height - 6f);
            Rect tile = new Rect(box.center.x - size * 0.5f, box.center.y - size * 0.5f + bob, size, size);
            Block(tile, c, Color.white, 3f, Color.clear, 0f);
            string initial = bot ? "B" : (string.IsNullOrEmpty(nameBuffers[i]) ? "?" : nameBuffers[i].Substring(0, 1));
            Label(tile, initial, Text(Bungee, 30, TextAnchor.MiddleCenter), Ink);
        }

        //Moves to the next skin nobody else is using.
        private void CycleSkin(int player, int step)
        {
            var skins = session.Skins;
            if (skins == null || skins.Count == 0) return;
            int start = skins.GetSkinIndex(player);
            if (start < 0) start = step > 0 ? -1 : 0;
            for (int n = 1; n <= skins.Count; n++)
            {
                int candidate = ((start + step * n) % skins.Count + skins.Count) % skins.Count;
                int owner = ActiveOwner(candidate);
                if (owner >= 0 && owner != player) continue;
                if (AssignSkin(player, candidate)) return;
            }
        }

        //The player in the match who holds this skin, or -1. Slots not playing don't count.
        private int ActiveOwner(int skin)
        {
            int owner = session.Skins.GetOwner(skin);
            return owner >= 0 && owner < session.PlayerCount ? owner : -1;
        }

        private Color OwnerColor(int player)
        {
            return IsSurvival && session.IsMonsterPlayer(player) ? Red : SlotColors[player];
        }

        //Gives the player a skin only when no one in the match has it. A slot that is not playing
        //is moved to another free skin first, so it never blocks a real player.
        private bool AssignSkin(int player, int skin)
        {
            var skins = session.Skins;
            int owner = skins.GetOwner(skin);
            if (owner >= 0 && owner != player)
            {
                if (owner < session.PlayerCount) return false;
                bool moved = false;
                for (int s = 0; s < skins.Count && !moved; s++)
                    if (s != skin && skins.GetOwner(s) < 0) moved = skins.TryAssign(owner, s);
                if (!moved) return false;
            }
            return skins.TryAssign(player, skin);
        }

        private void DrawBottomBar(Rect r)
        {
            string label = "START " + (IsSurvival ? "SURVIVAL" : "RACE");
            float bw = startStyle.CalcSize(new GUIContent(label)).x + 80f;
            Rect start = new Rect(r.xMax - bw, r.y, bw, 72f);
            Rect drawn = start;
            if (Hover(start)) { drawn.x -= 3f; drawn.y -= 3f; }
            Block(drawn, accent, Ink, 4f, Red, 6f);
            Label(drawn, label, startStyle, Ink);
            if (Hit(start)) StartMatch();

            string hint = !string.IsNullOrWhiteSpace(session.Status) ? session.Status
                : IsSurvival ? "Only one player can be the Monster. Leave everyone as SURVIVOR and the computer plays it."
                : "Use < and > on each card to pick a skin. Press Enter to start.";
            Label(new Rect(r.x, r.y, start.x - r.x - 24f, r.height), hint, hintStyle, !string.IsNullOrWhiteSpace(session.Status) ? Red : Navy);
        }

        private string Summary()
        {
            int people = 0;
            int bots = 0;
            for (int i = 0; i < session.PlayerCount; i++)
            {
                if (session.IsBotPlayer(i)) bots++;
                else people++;
            }
            if (IsSurvival)
            {
                int monster = MonsterIndex();
                int survivors = session.PlayerCount - (monster >= 0 ? 1 : 0);
                return survivors + (survivors == 1 ? " SURVIVOR" : " SURVIVORS") + " · MONSTER: " + (monster >= 0 ? "P" + (monster + 1) : "AI");
            }
            return people + (people == 1 ? " PLAYER" : " PLAYERS") + (bots > 0 ? " · " + bots + (bots == 1 ? " BOT" : " BOTS") : "");
        }

        private int MonsterIndex()
        {
            for (int i = 0; i < session.PlayerCount; i++)
                if (session.IsMonsterPlayer(i)) return i;
            return -1;
        }

        private static void DashedOutline(Rect r, float t, Color c)
        {
            for (float d = 0f; d < r.width; d += 12f)
            {
                float len = Mathf.Min(7f, r.width - d);
                Fill(new Rect(r.x + d, r.y, len, t), c);
                Fill(new Rect(r.x + d, r.yMax - t, len, t), c);
            }
            for (float d = 0f; d < r.height; d += 12f)
            {
                float len = Mathf.Min(7f, r.height - d);
                Fill(new Rect(r.x, r.y + d, t, len), c);
                Fill(new Rect(r.xMax - t, r.y + d, t, len), c);
            }
        }

        private static void DrawFlag(Rect r, Color c)
        {
            float u = r.width / 30f;
            Fill(new Rect(r.x + 5f * u, r.y + 2f * u, 3f * u, 26f * u), c);
            TriangleRight(new Rect(r.x + 8f * u, r.y + 3f * u, 18f * u, 13f * u), c);
        }

        private static void DrawStopwatch(Rect r, Color c)
        {
            float u = r.width / 30f;
            RingShape(new Rect(r.x + 4f * u, r.y + 7f * u, 22f * u, 22f * u), c);
            Fill(new Rect(r.x + 11f * u, r.y + 1f * u, 8f * u, 3f * u), c);
            Fill(new Rect(r.x + 13.5f * u, r.y + 3f * u, 3f * u, 5f * u), c);
            Fill(new Rect(r.x + 14f * u, r.y + 12f * u, 2.4f * u, 7f * u), c);
        }

        private static void DrawMonsterIcon(Rect r, Color c)
        {
            float u = r.width / 26f;
            Circle(new Rect(r.x + 3f * u, r.y + 1f * u, 20f * u, 20f * u), c);
            Fill(new Rect(r.x + 3f * u, r.y + 11f * u, 20f * u, 10f * u), c);
            for (int i = 0; i < 4; i++) Fill(new Rect(r.x + (3f + i * 5f) * u, r.y + 21f * u, 3f * u, 4f * u), c);
            Fill(new Rect(r.x + 8f * u, r.y + 9f * u, 3f * u, 3f * u), Red);
            Fill(new Rect(r.x + 15f * u, r.y + 9f * u, 3f * u, 3f * u), Red);
        }
    }
}
