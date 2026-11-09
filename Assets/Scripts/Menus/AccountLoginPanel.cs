using Escape4Now.Accounts;
using UnityEngine;
using static Escape4Now.Menus.ArcadeMenuKit;

namespace Escape4Now.Menus
{
    //The log in and create account panel drawn over the start screen.
    //Tab moves between the boxes and Enter submits. The start screen moves on once the player is signed in.
    public sealed class AccountLoginPanel
    {
        private const string UsernameControl = "LoginUsername";

        private readonly Color accent;
        private string username = "";
        private string password = "";
        private string confirmPassword = "";
        private string error = "";
        private bool creating;
        private bool focusUsername;
        private int openedFrame;
        private GUIStyle heading, label, small, tab, button, field;

        public AccountLoginPanel(Color accent)
        {
            this.accent = accent;
        }

        public void Open()
        {
            ClearForm();
        }

        //A focused box keeps showing its old text, so focus is dropped before moving back to the first box.
        private void ClearForm()
        {
            username = "";
            password = "";
            confirmPassword = "";
            error = "";
            GUIUtility.keyboardControl = 0;
            focusUsername = true;
            openedFrame = Time.frameCount;
        }

        private void CreateStyles()
        {
            if (heading != null) return;
            heading = Text(Bungee, 30, TextAnchor.MiddleCenter);
            label = Text(MonoFont, 12);
            small = Text(MonoFont, 13, TextAnchor.UpperLeft, true);
            tab = Text(Bungee, 16, TextAnchor.MiddleCenter);
            button = Text(Bungee, 22, TextAnchor.MiddleCenter);
            field = new GUIStyle(GUI.skin.textField)
            {
                font = MonoFont,
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 14, 0, 0),
                border = new RectOffset(0, 0, 0, 0)
            };
            foreach (GUIStyleState s in new[] { field.normal, field.hover, field.focused, field.active, field.onNormal, field.onFocused })
            {
                s.background = null;
                s.textColor = Color.white;
            }
        }

        public void Draw(float ox)
        {
            CreateStyles();
            Fill(new Rect(0f, 0f, Width, DesignHeight), WithAlpha(Ink, 0.7f));
            Rect panel = new Rect(ox + 360f, 96f, 560f, 528f);
            Block(panel, Panel, Color.white, 4f, accent, 10f);
            Rect inner = new Rect(panel.x + 32f, panel.y + 24f, panel.width - 64f, panel.height - 48f);

            if (AccountService.IsBusy) DrawConnecting(inner);
            else if (AccountService.IsOffline) DrawOffline(inner);
            else DrawForm(inner);
        }

        private void DrawConnecting(Rect inner)
        {
            Label(inner, "CONNECTING...", heading, Color.white);
        }

        private void DrawOffline(Rect inner)
        {
            Label(new Rect(inner.x, inner.y + 110f, inner.width, 44f), "NO INTERNET", heading, Color.white);
            Label(new Rect(inner.x, inner.y + 170f, inner.width, 60f), "You need internet to log in. You can still play as a guest, but your stats won't be saved.", small, Soft);
            if (Button(new Rect(inner.x, inner.yMax - 136f, inner.width, 60f), "TRY AGAIN", true)) _ = AccountService.ConnectAsync();
            if (Button(new Rect(inner.x, inner.yMax - 62f, inner.width, 60f), "PLAY AS GUEST", false)) AccountService.PlayAsGuest();
        }

        private void DrawForm(Rect inner)
        {
            float half = (inner.width - 12f) * 0.5f;
            if (Tab(new Rect(inner.x, inner.y, half, 44f), "LOG IN", !creating)) SetCreating(false);
            if (Tab(new Rect(inner.x + half + 12f, inner.y, half, 44f), "CREATE ACCOUNT", creating)) SetCreating(true);

            bool ready = Time.frameCount > openedFrame;
            bool submit = ready && Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            if (submit) Event.current.Use();

            username = TextBox(new Rect(inner.x, inner.y + 66f, inner.width, 50f), "USERNAME", UsernameControl, username, AccountService.UsernameLimit, false);
            password = TextBox(new Rect(inner.x, inner.y + 144f, inner.width, 50f), "PASSWORD", "LoginPassword", password, AccountService.PasswordLimit, true);
            if (creating)
                confirmPassword = TextBox(new Rect(inner.x, inner.y + 222f, inner.width, 50f), "CONFIRM PASSWORD", "LoginConfirm", confirmPassword, AccountService.PasswordLimit, true);

            if (focusUsername && ready)
            {
                GUI.FocusControl(UsernameControl);
                focusUsername = false;
            }

            //The note sits under the last box, which is higher up on the log in tab.
            Rect note = new Rect(inner.x, inner.y + (creating ? 300f : 206f), inner.width, 60f);
            if (error.Length > 0) Label(note, error, small, Red);
            else if (creating) Label(note, "Password: 8-30 characters with an uppercase letter, lowercase letter, number and symbol. It can't be recovered, so remember it.", small, Muted);

            if (!creating && Button(new Rect(inner.x, inner.y + 276f, inner.width, 60f), "PLAY AS GUEST", false)) AccountService.PlayAsGuest();
            if (Button(new Rect(inner.x, inner.yMax - 62f, inner.width, 60f), creating ? "CREATE ACCOUNT" : "LOG IN", true)) submit = true;
            if (submit) Submit();
        }

        private void SetCreating(bool value)
        {
            if (creating == value) return;
            creating = value;
            ClearForm();
        }

        private async void Submit()
        {
            string name = username.Trim();
            error = creating ? AccountService.CheckNewAccount(name, password, confirmPassword) ?? ""
                : name.Length == 0 || password.Length == 0 ? "Enter your username and password." : "";
            if (error.Length > 0) return;
            error = await (creating ? AccountService.CreateAccountAsync(name, password) : AccountService.LogInAsync(name, password)) ?? "";
        }

        private string TextBox(Rect box, string title, string control, string value, int limit, bool hidden)
        {
            Label(new Rect(box.x, box.y - 20f, box.width, 16f), title, label, Muted);
            Block(box, PanelDark, GUI.GetNameOfFocusedControl() == control ? accent : Hex("#44444f"), 3f, Color.clear, 0f);
            Rect text = new Rect(box.x + 3f, box.y + 3f, box.width - 6f, box.height - 6f);
            GUI.SetNextControlName(control);
            return hidden ? GUI.PasswordField(text, value, '*', limit, field) : GUI.TextField(text, value, limit, field);
        }

        private bool Tab(Rect r, string text, bool on)
        {
            Block(r, on ? accent : Ink, on ? accent : Hex("#44444f"), 3f, Color.clear, 0f);
            Label(r, text, tab, on ? Ink : Color.white);
            return Hit(r);
        }

        private bool Button(Rect r, string text, bool primary)
        {
            Rect drawn = r;
            if (Hover(r)) { drawn.x -= 3f; drawn.y -= 3f; }
            if (primary) Block(drawn, accent, Ink, 4f, Red, 6f);
            else Block(drawn, Ink, Color.white, 4f, Red, 6f);
            Label(drawn, text, button, primary ? Ink : Color.white);
            return Hit(r);
        }
    }
}
