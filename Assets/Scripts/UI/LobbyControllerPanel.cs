using System;
using Escape4Now.Menus;
using Escape4Now.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    //The CONTROLLERS / VIBRATE panel beside WHO'S PLAYING in the arena and online lobbies.
    //It works the same as the Race and Survival panel in ArcadeLobbyScreen: each row shows a player's controller,
    //VIBRATE buzzes it so you can find it, and CHANGE waits for the next button press to move a controller to that player.
    //The lobby editor scripts build the rows; this only fills them in from ControllerHandling.
    public sealed class LobbyControllerPanel : MonoBehaviour
    {
        [Serializable]
        public sealed class RowButton
        {
            public Button button;
            public Image edge, fill;
            public Text label;
            //Dashed outline shown instead of the button when there is nothing to press, such as NO PAD.
            public GameObject dashes;
        }

        [Serializable]
        public sealed class Row
        {
            public GameObject root;
            public Image chip;
            public Text chipLabel, device;
            public RowButton left, right;
        }

        [SerializeField] private Row[] rows;
        [SerializeField] private Text help;

        //Set by the lobby that owns the panel. Online lobbies show one row for this computer's controller.
        private Func<int, bool> isPlaying = slot => true;
        private Func<int, Color> colorOf = slot => ArcadeMenuKit.Teal;
        private Func<int, bool> isBot = slot => false;
        private Func<int, string> labelOf = slot => "P" + (slot + 1);
        private int shownRows = ControllerHandling.MaxSlots;

        //Local lobbies show one row per player slot.
        public void BindLocal(Func<int, bool> playing, Func<int, Color> color, Func<int, bool> bot = null)
        {
            isPlaying = playing ?? isPlaying;
            colorOf = color ?? colorOf;
            isBot = bot ?? (slot => false);
            shownRows = ControllerHandling.MaxSlots;
            Wire();
        }

        //Online, each player is on their own computer, so only this computer's controller (slot 1) is listed.
        public void BindOnline(Func<string> label, Func<Color> color)
        {
            isPlaying = slot => true;
            colorOf = slot => color != null ? color() : ArcadeMenuKit.Teal;
            labelOf = slot => label != null ? label() : "YOU";
            shownRows = 1;
            Wire();
        }

        private bool wired;

        private void Wire()
        {
            if (wired || rows == null) return;
            wired = true;
            for (int i = 0; i < rows.Length; i++)
            {
                int slot = i;
                rows[i].left.button.onClick.AddListener(() =>
                {
                    if (ControllerHandling.ListeningSlot == slot) ControllerHandling.ClearSlot(slot);
                    else ControllerHandling.Vibrate(slot);
                });
                rows[i].right.button.onClick.AddListener(() =>
                {
                    if (ControllerHandling.ListeningSlot == slot) ControllerHandling.CancelAssign();
                    else ControllerHandling.BeginAssign(slot);
                });
            }
        }

        private void Start() => Wire();

        private void Update()
        {
            if (rows == null) return;
            float t = Time.unscaledTime;
            Color accent = ArcadeMenuKit.Teal;
            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i];
                bool shown = i < shownRows && i < ControllerHandling.MaxSlots;
                row.root.SetActive(shown);
                if (!shown) continue;

                bool playing = isPlaying(i);
                bool bot = playing && isBot(i);
                Color c = playing ? colorOf(i) : ArcadeMenuKit.WithAlpha(ArcadeLobbyScreen.SoftBlue, 0.35f);
                bool connected = ControllerHandling.GetGamepad(i) != null;
                bool listening = ControllerHandling.ListeningSlot == i;

                row.chip.color = c;
                row.chipLabel.text = labelOf(i);
                row.device.text = listening ? "PRESS ANY BUTTON" : bot && !connected ? "BOT" : ControllerHandling.DescribeSlot(i);
                row.device.color = listening ? Color.Lerp(Color.white, accent, 0.5f + 0.5f * Mathf.Sin(t * 8f))
                    : connected ? Color.white : ArcadeLobbyScreen.OffText;

                //A small shake while the controller buzzes, like the Race panel.
                var leftRect = (RectTransform)row.left.button.transform;
                bool shaking = !listening && connected && ControllerHandling.IsVibrating(i);
                leftRect.localPosition = new Vector3(LeftX(leftRect) + (shaking ? Mathf.Sin(t * 70f) * 2f : 0f), leftRect.localPosition.y, 0f);

                //Left: VIBRATE (or USE KEYS while waiting). Right: CHANGE (or CANCEL while waiting).
                if (listening)
                {
                    Paint(row.left, "USE KEYS", ArcadeLobbyScreen.CardNavy, c, Color.white, true);
                    Paint(row.right, "CANCEL", accent, ArcadeMenuKit.Ink, ArcadeMenuKit.Ink, true);
                }
                else
                {
                    if (connected)
                    {
                        bool buzzing = ControllerHandling.IsVibrating(i);
                        Paint(row.left, buzzing ? "BZZZ!" : "VIBRATE", buzzing ? (playing ? c : accent) : ArcadeLobbyScreen.CardNavy,
                            playing ? c : ArcadeLobbyScreen.DashBlue, buzzing ? ArcadeMenuKit.Ink : Color.white, true);
                    }
                    else Paint(row.left, "NO PAD", ArcadeLobbyScreen.DeepNavy, ArcadeLobbyScreen.DashBlue, ArcadeMenuKit.WithAlpha(ArcadeLobbyScreen.OffText, 0.6f), false);
                    Paint(row.right, "CHANGE", ArcadeLobbyScreen.CardNavy, playing ? c : ArcadeLobbyScreen.DashBlue, Color.white, true);
                }
            }

            int count = ControllerHandling.ConnectedCount;
            int waiting = ControllerHandling.ListeningSlot;
            help.text = waiting >= 0 ? "Press a button on the controller for " + labelOf(waiting) + "."
                : count == 0 ? (shownRows == 1 ? "Plug in a controller to play with it." : "Plug in a controller to give each player their own.")
                : count + " connected. CHANGE moves a controller to " + (shownRows == 1 ? "you." : "that player.");
        }

        //The button's resting x position, saved the first time so the shake always returns to it.
        private readonly System.Collections.Generic.Dictionary<RectTransform, float> restX = new System.Collections.Generic.Dictionary<RectTransform, float>();

        private float LeftX(RectTransform rect)
        {
            if (!restX.TryGetValue(rect, out float x))
            {
                x = rect.localPosition.x;
                restX[rect] = x;
            }
            return x;
        }

        //Colors one row button. A button with nothing to do shows a faded fill and a dashed edge instead.
        private static void Paint(RowButton target, string text, Color fill, Color edge, Color textColor, bool active)
        {
            target.label.text = text;
            target.label.color = textColor;
            target.fill.color = active ? fill : ArcadeMenuKit.WithAlpha(fill, 0.6f);
            target.edge.enabled = active;
            target.edge.color = edge;
            if (target.dashes != null) target.dashes.SetActive(!active);
            target.button.interactable = active;
        }
    }
}
