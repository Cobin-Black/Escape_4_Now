using System.Collections.Generic;
using Escape4Now.Multiplayer;
using Escape4Now.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Escape4Now.Arena
{
    // Only setup presentation and UI choices live here. ArenaMatch still creates and validates the players.
    public sealed class ArenaLobbyView : MonoBehaviour
    {
        [SerializeField]
        private ArenaInterfaceStyle style;
        [SerializeField]
        private Text title, instructions, error;
        [SerializeField, Tooltip("Bottom help line. It hides while a setup error is showing.")]
        private Text hint;
        [SerializeField]
        private Button[] playerCounts;
        [SerializeField] private Button[] openSlots, previousSkins, nextSkins;
        [SerializeField] private Text[] skinLabels;
        [SerializeField] private Text playerSummary;
        [SerializeField] private Button back;
        [SerializeField, Tooltip("Saved controller panel built by the lobby editor script. Replaces the runtime panel when set.")]
        private LobbyControllerPanel controllerPanel;
        [SerializeField] private Color selectedCountColor = new Color(.212f, .945f, .804f);
        [SerializeField, Tooltip("PLAYER half of each card's PLAYER / BOT switch.")]
        private Button[] humanRoles;
        [SerializeField, Tooltip("BOT half of each card's PLAYER / BOT switch. Player 1 is always a person.")]
        private Button[] botRoles;
        [SerializeField, Tooltip("Badge at the top of each card. Reads PLAYER or BOT.")]
        private Text[] roleBadges;
        [SerializeField]
        private GameObject[] rows;
        [SerializeField]
        private InputField[] names;
        [SerializeField]
        private Dropdown[] controls, skins;
        [SerializeField]
        private Image[] portraits, identityStrips;
        [SerializeField]
        private Button start;
        [Header("Setup Defaults and Labels")]
        [SerializeField, Range(2, 4)]
        private int playerCount = 2;
        [SerializeField]
        private string defaultSkinLabel = "Default";
        [SerializeField]
        private string keyboardFormat = "Keyboard: {0}";
        [SerializeField]
        private string gamepadFormat = "Gamepad {0}";
        [SerializeField]
        private string[] movementHints =
        {
            "WASD",
            "Arrow keys",
            "IJKL",
            "Numpad 8456"
        };
        [SerializeField]
        private string[] tagHints =
        {
            "WASD + F",
            "Arrows + Right Ctrl",
            "IJKL + O",
            "Numpad 8456 + 0"
        };
        [Header("Controller Panel (built beside the setup panel at runtime)")]
        [SerializeField]
        private bool showControllerPanel = true;
        [SerializeField, Tooltip("Open controller options over the lobby so the arcade cards keep their normal size.")]
        private bool controllerPanelInOverlay = true;
        [SerializeField, Min(120)]
        private float controllerPanelWidth = 210;
        [SerializeField, Min(0)]
        private float controllerPanelGap = 16;
        private ArenaMatch match;
        // Which slots the computer plays. ArenaBotController drives these players during the match.
        private readonly bool[] botSlots = new bool[ControllerHandling.MaxSlots];
        private readonly GameObject[] padRows = new GameObject[ControllerHandling.MaxSlots];
        private readonly Text[] padNames = new Text[ControllerHandling.MaxSlots];
        private readonly Button[] padButtons = new Button[ControllerHandling.MaxSlots];
        private readonly Text[] padButtonLabels = new Text[ControllerHandling.MaxSlots];
        private readonly Button[] changeButtons = new Button[ControllerHandling.MaxSlots];
        private readonly Text[] changeLabels = new Text[ControllerHandling.MaxSlots];
        private Text padFooter;
        private RectTransform controllerOverlay;
        private Button controllerButton;
        private InputAction controllerCancel;
        public int PlayerCount => playerCount;
        public ArenaInterfaceStyle Style => style;

        // Connect saved Unity widgets to the existing local setup options.
        public void Initialize(ArenaMatch owner, System.Action goBack)
        {
            match = owner;
            title.text = match.Title;
            instructions.text = match.Instructions;
            for (int i = 0; i < playerCounts.Length; i++)
            {
                int count = i + 2;
                playerCounts[i].onClick.AddListener(() => SetCount(count));
            }

            for (int i = 0; i < rows.Length; i++)
            {
                int slot = i;
                rows[i].GetComponent<PlayerColorView>()?.SetColor(match.GetPlayerColor(i));
                var skinOptions = new List<string>
                {
                    defaultSkinLabel
                };
                if (match.Skins != null)
                    for (int skin = 0; skin < match.Skins.Count; skin++)
                        skinOptions.Add(match.Skins.GetSkin(skin).skinName);
                skins[i].ClearOptions();
                skins[i].AddOptions(skinOptions);
                skins[i].onValueChanged.AddListener(value => RefreshPortrait(slot));
                if (openSlots != null && i < openSlots.Length && openSlots[i] != null)
                    openSlots[i].onClick.AddListener(() => SetCount(slot + 1));
                if (previousSkins != null && i < previousSkins.Length)
                {
                    previousSkins[i].onClick.AddListener(() => StepSkin(slot, -1));
                    nextSkins[i].onClick.AddListener(() => StepSkin(slot, 1));
                }
                // Controls are no longer chosen here: ControllerHandling gives each slot its controller,
                // so the old dropdown just shows what this player will use.
                controls[i].interactable = false;
                if (humanRoles != null && i < humanRoles.Length && humanRoles[i] != null)
                {
                    humanRoles[i].onClick.AddListener(() => SetBot(slot, false));
                    botRoles[i].onClick.AddListener(() => SetBot(slot, true));
                }
                if (identityStrips[i] != null) identityStrips[i].color = match.GetPlayerColor(i);
                RefreshPortrait(i);
            }

            start.onClick.AddListener(StartMatch);
            if (back != null) back.onClick.AddListener(() => goBack());
            // Bots never link an account.
            AccountLinkUI.Create(gameObject, names, slot => !IsBot(slot), match.GetPlayerColor);
            ControllerHandling.AssignmentsChanged += Refresh;
            if (showControllerPanel)
                BuildControllerPanel();
            // The saved CONTROLLERS / VIBRATE panel beside WHO'S PLAYING, the same as Cobin's Race panel.
            if (controllerPanel != null)
                controllerPanel.BindLocal(slot => slot < playerCount, match.GetPlayerColor, IsBot);
            Refresh();
        }

        private void SetCount(int count)
        {
            playerCount = Mathf.Clamp(count, 2, rows.Length);
            Refresh();
        }

        private bool IsBot(int slot) => slot > 0 && slot < botSlots.Length && botSlots[slot];

        // Player 1 stays a person so there is always someone at the keyboard or controller.
        private void SetBot(int slot, bool bot)
        {
            if (slot <= 0 || slot >= botSlots.Length || botSlots[slot] == bot) return;
            botSlots[slot] = bot;
            names[slot].text = bot ? "BOT " + (slot + 1) : "Player " + (slot + 1);
            names[slot].interactable = !bot;
            Refresh();
        }

        private void StepSkin(int slot, int direction)
        {
            var selector = skins[slot];
            int count = selector.options.Count;
            if (count > 1) selector.value = (selector.value + direction + count) % count;
        }

        // ---------- Controller / Vibrate panel ----------

        // A side panel in the same style as the setup panel: each player's controller and a VIBRATE button.
        private void BuildControllerPanel()
        {
            var lobbyRect = (RectTransform)transform;
            Sprite sprite = GetComponent<Image>() != null ? GetComponent<Image>().sprite : null;
            float pad = style != null ? style.padding : 18;
            float gap = style != null ? style.gap : 10;

            if (controllerPanelInOverlay && back != null)
            {
                controllerOverlay = NewRect("Controller Overlay", lobbyRect);
                controllerOverlay.anchorMin = Vector2.zero;
                controllerOverlay.anchorMax = Vector2.one;
                controllerOverlay.offsetMin = controllerOverlay.offsetMax = Vector2.zero;
                AddImage(controllerOverlay, null, new Color(0, 0, 0, .7f));
                // Copy the project's Cancel action so keyboard and gamepad can both close the panel.
                controllerCancel = match.GamepadActions?.FindAction("UI/Cancel")?.Clone();
                controllerCancel?.Enable();
            }
            var panel = NewRect("Controller Panel", controllerOverlay != null ? controllerOverlay : lobbyRect);
            panel.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            panel.anchorMin = panel.anchorMax = new Vector2(1, 1);
            panel.pivot = new Vector2(0, 1);
            panel.anchoredPosition = new Vector2(controllerPanelGap, 0);
            panel.sizeDelta = new Vector2(controllerPanelWidth, 0);
            if (controllerOverlay != null)
            {
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
                panel.anchoredPosition = Vector2.zero;
            }
            // Same look as the Race and Survival controller popup: navy panel, white border, dark block shadow.
            sprite = null;
            AddImage(panel, sprite, Escape4Now.Menus.ArcadeLobbyScreen.PanelNavy);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(4f, -4f);
            var shadow = panel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Escape4Now.Menus.ArcadeLobbyScreen.CardNavy;
            shadow.effectDistance = new Vector2(10f, -10f);
            Color accent = Escape4Now.Menus.ArcadeLobbyScreen.EdgeBlue;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)pad, (int)pad, (int)pad, (int)pad);
            layout.spacing = gap;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddText(panel, "Title", "CONTROLLERS", style != null ? style.headingSize : 18, true, StyleColor(s => s.text, Color.white), 28);
            AddText(panel, "Subtitle", "Press VIBRATE to find which controller is yours.", style != null ? style.smallSize : 12, false, Escape4Now.Menus.ArcadeLobbyScreen.SoftBlue, 34);

            for (int i = 0; i < ControllerHandling.MaxSlots; i++)
            {
                int slot = i;
                var row = NewRect("Player " + (i + 1), panel);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;
                AddImage(row, sprite, Escape4Now.Menus.ArcadeLobbyScreen.CardNavy);

                var strip = NewRect("Identity", row);
                strip.anchorMin = new Vector2(0, 0);
                strip.anchorMax = new Vector2(0, 1);
                strip.pivot = new Vector2(0, .5f);
                strip.sizeDelta = new Vector2(6, 0);
                AddImage(strip, null, match.GetPlayerColor(i));

                var label = AddText(row, "Player", "P" + (i + 1), style != null ? style.bodySize : 14, true, match.GetPlayerColor(i), 0);
                Place(label.rectTransform, new Vector2(14, -6), new Vector2(40, 24));
                padNames[i] = AddText(row, "Device", "", style != null ? style.smallSize : 12, false, Escape4Now.Menus.ArcadeLobbyScreen.SoftBlue, 0);
                Place(padNames[i].rectTransform, new Vector2(52, -6), new Vector2(controllerPanelWidth - pad * 2 - 58, 24));

                // Left: VIBRATE (USE KEYS while waiting). Right: CHANGE (CANCEL while waiting).
                padButtons[i] = RowButton(row, sprite, accent, new Vector2(0, .55f), new Vector2(14, -4), out padButtonLabels[i], () =>
                {
                    if (ControllerHandling.ListeningSlot == slot) ControllerHandling.ClearSlot(slot);
                    else ControllerHandling.Vibrate(slot);
                });
                changeButtons[i] = RowButton(row, sprite, Escape4Now.Menus.ArcadeLobbyScreen.DeepNavy, new Vector2(.55f, 1), new Vector2(4, -8), out changeLabels[i], () =>
                {
                    if (ControllerHandling.ListeningSlot == slot) ControllerHandling.CancelAssign();
                    else ControllerHandling.BeginAssign(slot);
                });
                padRows[i] = row.gameObject;
            }

            padFooter = AddText(panel, "Connected", "", style != null ? style.smallSize : 12, false, Escape4Now.Menus.ArcadeLobbyScreen.Tape, 22);

            if (controllerOverlay != null)
            {
                var close = AddText(panel, "Close", "CLOSE", style != null ? style.bodySize : 14, true, Color.white, 32);
                close.alignment = TextAnchor.MiddleCenter;
                close.raycastTarget = true;
                close.gameObject.AddComponent<Button>().onClick.AddListener(() => ShowControllerPanel(false));
                // Reuse the existing navigation button so its font, border and label padding match.
                controllerButton = Instantiate(back, lobbyRect);
                controllerButton.name = "Controller Settings";
                controllerButton.onClick = new Button.ButtonClickedEvent();
                var opener = (RectTransform)controllerButton.transform;
                var backRect = (RectTransform)back.transform;
                opener.anchorMin = backRect.anchorMin;
                opener.anchorMax = backRect.anchorMax;
                opener.pivot = backRect.pivot;
                opener.sizeDelta = backRect.sizeDelta;
                opener.anchoredPosition = backRect.anchoredPosition - Vector2.right * (backRect.rect.width + controllerPanelGap);
                controllerButton.GetComponentInChildren<Text>().text = "CONTROLLERS";
                controllerButton.onClick.AddListener(() => ShowControllerPanel(true));
                ShowControllerPanel(false);
            }

            // Keep both panels on screen: the setup panel shrinks a little when the window is narrow.
            var responsive = GetComponent<ResponsivePanel>();
            if (responsive != null && controllerOverlay == null)
                responsive.ReservedRightWidth = (controllerPanelWidth + controllerPanelGap) * 2;
        }

        private void ShowControllerPanel(bool visible)
        {
            controllerOverlay.gameObject.SetActive(visible);
            if (visible) controllerOverlay.SetAsLastSibling();
            else ControllerHandling.CancelAssign();
            EventSystem.current?.SetSelectedGameObject(visible ? changeButtons[0].gameObject : controllerButton.gameObject);
        }

        private void Update()
        {
            if (padFooter == null)
                return;
            for (int i = 0; i < padButtons.Length; i++)
                if (padButtonLabels[i] != null && padButtons[i].interactable && ControllerHandling.ListeningSlot != i)
                    padButtonLabels[i].text = ControllerHandling.IsVibrating(i) ? "BZZZ!" : "VIBRATE";
            if (controllerCancel?.WasPressedThisFrame() == true)
            {
                if (ControllerHandling.ListeningSlot >= 0) ControllerHandling.CancelAssign();
                else if (controllerOverlay != null && controllerOverlay.gameObject.activeSelf) ShowControllerPanel(false);
            }
        }

        private void RefreshControllerPanel()
        {
            if (padFooter == null)
                return;
            int listening = ControllerHandling.ListeningSlot;
            int[] layouts = ControllerHandling.GetKeyboardLayouts(playerCount);
            for (int i = 0; i < padRows.Length; i++)
            {
                padRows[i].SetActive(i < playerCount);
                bool connected = ControllerHandling.GetGamepad(i) != null;
                bool waiting = listening == i;
                // Keyboard players see their keys here on the setup screen (they are not shown during the match).
                padNames[i].text = waiting ? "PRESS ANY BUTTON"
                    : !connected && i < layouts.Length && layouts[i] >= 0 ? KeyboardKeys(layouts[i]).ToUpperInvariant()
                    : ControllerHandling.DescribeSlot(i);
                padButtons[i].interactable = connected || waiting;
                padButtonLabels[i].text = waiting ? "USE KEYS" : connected ? "VIBRATE" : "NO PAD";
                changeLabels[i].text = waiting ? "CANCEL" : "CHANGE";
            }
            int count = ControllerHandling.ConnectedCount;
            padFooter.text = listening >= 0 ? "Press a button on the controller for P" + (listening + 1) + "."
                : count == 0 ? "No controllers connected."
                : count + (count == 1 ? " controller connected." : " controllers connected.") + " CHANGE moves one to that player.";
        }

        // A button laid out across part of a row's bottom (xRange is the 0-1 share of the row width).
        private Button RowButton(RectTransform row, Sprite sprite, Color color, Vector2 xRange, Vector2 inset, out Text label, UnityEngine.Events.UnityAction onClick)
        {
            var rect = NewRect("Button", row);
            rect.anchorMin = new Vector2(xRange.x, 0);
            rect.anchorMax = new Vector2(xRange.y, 0);
            rect.pivot = new Vector2(.5f, 0);
            rect.offsetMin = new Vector2(inset.x, 8);
            rect.offsetMax = new Vector2(inset.y, 40);
            var image = AddImage(rect, sprite, color);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            label = AddText(rect, "Label", "", style != null ? style.smallSize : 12, true, StyleColor(s => s.text, Color.white), 0);
            label.alignment = TextAnchor.MiddleCenter;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            return button;
        }

        private Color StyleColor(System.Func<ArenaInterfaceStyle, Color> pick, Color fallback) => style != null ? pick(style) : fallback;

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image AddImage(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text AddText(RectTransform parent, string name, string content, int size, bool bold, Color color, float height)
        {
            var rect = NewRect(name, parent);
            if (height > 0)
                rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var text = rect.gameObject.AddComponent<Text>();
            // Lobby fonts: Bungee for headings and buttons, Space Mono for everything else.
            text.font = bold ? Escape4Now.Menus.ArcadeMenuKit.Bungee : Escape4Now.Menus.ArcadeMenuKit.MonoFont;
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 topLeft, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = size;
        }

        private void OnDestroy()
        {
            ControllerHandling.AssignmentsChanged -= Refresh;
            controllerCancel?.Dispose();
        }

        // Show "Controller 1" or the keyboard layout this slot will get with the current controllers.
        private void RefreshControls()
        {
            int[] layouts = ControllerHandling.GetKeyboardLayouts(playerCount);
            for (int i = 0; i < controls.Length; i++)
            {
                string label;
                if (i >= layouts.Length)
                    label = "";
                else if (IsBot(i))
                    label = "Computer";
                else if (layouts[i] < 0)
                    label = string.Format(gamepadFormat, i + 1);
                else
                    label = string.Format(keyboardFormat, KeyboardKeys(layouts[i]));
                controls[i].ClearOptions();
                controls[i].AddOptions(new List<string> { label });
                controls[i].SetValueWithoutNotify(0);
                controls[i].RefreshShownValue();
            }
        }

        // The keys a keyboard player uses: Player 1's come from the Input System bindings, Players 2-4 use the backup
        // layouts. The layout's action key is Pass in Hot Potato and Attack in King of the Hill.
        private string KeyboardKeys(int layout)
        {
            if (layout == 0)
                return PrimaryKeyboardHint();
            string[] hints = tagHints.Length > 0 ? tagHints : movementHints;
            return hints.Length > 0 ? hints[Mathf.Clamp(layout, 0, hints.Length - 1)] : "";
        }

        // Player 1's keyboard keys, read from the Input System bindings (e.g. "WASD + ENTER").
        private string PrimaryKeyboardHint()
        {
            var playerMap = match.GamepadActions != null ? match.GamepadActions.FindActionMap("Player") : null;
            if (playerMap == null)
                return movementHints.Length > 0 ? movementHints[0] : "";
            var labels = new List<string>();
            InputBindingHints.GetPrimaryLabels(playerMap.FindAction("Move"), "Keyboard&Mouse", labels);
            string move = labels.TrueForAll(l => l.Length == 1) ? string.Concat(labels) : string.Join("/", labels);
            InputBindingHints.GetPrimaryLabels(playerMap.FindAction(match is KingOfTheHillController ? "Attack" : "Potato Pass"), "Keyboard&Mouse", labels);
            return labels.Count > 0 ? move + " + " + labels[0] : move;
        }

        // A two-player lobby takes only two rows. Layout groups move the footer up automatically.
        private void Refresh()
        {
            if (match == null)
                return;
            RefreshControls();
            RefreshControllerPanel();
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].SetActive(i < playerCount);
                if (openSlots != null && i < openSlots.Length && openSlots[i] != null)
                    openSlots[i].gameObject.SetActive(i >= playerCount);
            }
            int bots = 0;
            for (int i = 0; i < playerCount; i++)
                if (IsBot(i)) bots++;
            if (playerSummary != null)
                playerSummary.text = (playerCount - bots) + (playerCount - bots == 1 ? " PLAYER" : " PLAYERS")
                    + (bots > 0 ? " · " + bots + (bots == 1 ? " BOT" : " BOTS") : "");
            RefreshRoles();
            for (int i = 0; i < playerCounts.Length; i++)
                ArcadeChip.Paint(playerCounts[i].transform, i + 2 == playerCount, selectedCountColor);
        }

        // Light the chosen half of each PLAYER / BOT switch in the player's color, like the Race lobby.
        private void RefreshRoles()
        {
            if (humanRoles == null || botRoles == null)
                return;
            for (int i = 0; i < humanRoles.Length && i < botRoles.Length; i++)
            {
                if (humanRoles[i] == null || botRoles[i] == null) continue;
                bool bot = IsBot(i);
                Color color = match.GetPlayerColor(i);
                PaintRole(humanRoles[i], !bot, color, true);
                PaintRole(botRoles[i], bot, color, i > 0);
                botRoles[i].interactable = i > 0;
                if (roleBadges != null && i < roleBadges.Length && roleBadges[i] != null)
                    roleBadges[i].text = bot ? "BOT" : "PLAYER";
            }
        }

        private static void PaintRole(Button half, bool selected, Color color, bool enabled)
        {
            var face = half.targetGraphic as Image;
            if (face != null) face.color = selected ? color : Escape4Now.Menus.ArcadeLobbyScreen.DeepNavy;
            var label = half.GetComponentInChildren<Text>();
            if (label == null) return;
            Color off = Escape4Now.Menus.ArcadeLobbyScreen.OffText;
            label.color = selected ? Escape4Now.Menus.ArcadeMenuKit.Ink : enabled ? off : new Color(off.r, off.g, off.b, .35f);
        }

        // Preview the selected existing skin without changing the match skin assignments.
        private void RefreshPortrait(int slot)
        {
            int chosen = skins[slot].value - 1;
            var skin = match.Skins != null ? match.Skins.GetSkin(chosen >= 0 ? chosen : slot) : null;
            portraits[slot].sprite = skin != null ? skin.front : null;
            portraits[slot].enabled = portraits[slot].sprite != null;
            if (skinLabels != null && slot < skinLabels.Length)
                skinLabels[slot].text = chosen < 0 ? defaultSkinLabel.ToUpperInvariant()
                    : "SKIN " + (chosen + 1) + " / " + match.Skins.Count;
        }

        public void StartMatch()
        {
            var labels = new string[playerCount];
            var looks = new int[playerCount];
            var bots = new bool[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                bots[i] = IsBot(i);
                labels[i] = bots[i] ? "BOT " + (i + 1) : names[i].text;
                looks[i] = skins[i].value - 1;
            }

            match.StartMatch(playerCount, labels, looks, bots);
            bool failed = !string.IsNullOrEmpty(match.Error);
            error.text = match.Error;
            error.gameObject.SetActive(failed);
            if (hint != null) hint.gameObject.SetActive(!failed);
        }
    }
}
