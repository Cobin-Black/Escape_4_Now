using System;
using Escape4Now.Accounts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    //Adds a chain icon to each lobby name box for Players 2-4. Clicking it opens a login popup, and
    //clicking it again on a linked player unlinks them. Linked names replace the typed name and are locked.
    public sealed class AccountLinkUI : MonoBehaviour
    {
        private const float ChainSize = 26f;
        private const float ChainMargin = 4f;
        private const float PopupWidth = 480f;
        private const float PopupHeight = 400f;
        private const float PopupPadding = 28f;
        //Same palette as the Race and Survival lobby.
        private static readonly Color DimColor = new Color(0.067f, 0.067f, 0.067f, 0.7f);
        private static readonly Color PanelNavy = Menus.ArcadeLobbyScreen.PanelNavy;
        private static readonly Color CardNavy = Menus.ArcadeLobbyScreen.CardNavy;
        private static readonly Color DeepNavy = Menus.ArcadeLobbyScreen.DeepNavy;
        private static readonly Color DashBlue = Menus.ArcadeLobbyScreen.DashBlue;
        private static readonly Color Accent = Menus.ArcadeMenuKit.Teal;
        private static readonly Color Ink = Menus.ArcadeMenuKit.Ink;
        private static readonly Color UnlinkedChainColor = Menus.ArcadeLobbyScreen.OffText;
        private static readonly Color LinkedChainColor = Menus.ArcadeMenuKit.Teal;
        private static readonly Color ErrorColor = Menus.ArcadeMenuKit.Hex("#ff8a93");

        private InputField[] names;
        private Func<int, bool> slotCanLink;
        private Button[] chains;
        private string[] typedNames;
        private bool[] showingAccount;
        private Font font;
        private Font heading;
        private Font mono;
        private Text popupSlot;
        private Image popupBand;
        private Text popupTag;
        private Func<int, Color> slotColor;
        private GameObject popup;
        private Text popupTitle;
        private Text popupStatus;
        private InputField usernameField;
        private InputField passwordField;
        private int linkingSlot;

        //Adds the chains to a lobby's name boxes. slotCanLink hides a slot's chain, such as for bots.
        //slotColor tints the popup's top band to match that player's card; older callers use the normal menu accent.
        public static AccountLinkUI Create(GameObject owner, InputField[] names, Func<int, bool> slotCanLink, Func<int, Color> slotColor = null)
        {
            var links = owner.AddComponent<AccountLinkUI>();
            links.names = names;
            links.slotCanLink = slotCanLink;
            links.slotColor = slotColor;
            links.Build();
            return links;
        }

        private void Build()
        {
            font = names[0].textComponent.font;
            heading = LoadFont("Fonts/Bungee-Regular");
            mono = LoadFont("Fonts/SpaceMono-Regular");
            chains = new Button[names.Length];
            typedNames = new string[names.Length];
            showingAccount = new bool[names.Length];
            Sprite chainSprite = CreateChainSprite();
            for (int i = 1; i < names.Length; i++)
                chains[i] = BuildChain(i, chainSprite);
            BuildPopup((RectTransform)names[0].GetComponentInParent<Canvas>(true).rootCanvas.transform);
        }

        //Sits inside the name box so it fits both lobby layouts.
        private Button BuildChain(int slot, Sprite sprite)
        {
            var go = new GameObject("Account Chain", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(names[slot].transform, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-ChainMargin, 0f);
            rect.sizeDelta = new Vector2(ChainSize, ChainSize);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => ToggleLink(slot));
            return button;
        }

        private void ToggleLink(int slot)
        {
            if (AccountService.IsBusy) return;
            if (AccountService.IsLinked(slot)) AccountService.UnlinkPlayer(slot);
            else OpenPopup(slot);
        }

        private void LateUpdate()
        {
            //Player 1 is briefly signed out while another account links.
            if (AccountService.IsBusy) return;
            for (int i = 0; i < names.Length; i++)
            {
                ShowAccountName(i);
                if (chains[i] == null) continue;
                bool linked = AccountService.IsLinked(i);
                bool canLink = slotCanLink == null || slotCanLink(i);
                chains[i].gameObject.SetActive(AccountService.IsSignedIn && canLink);
                chains[i].image.color = linked ? LinkedChainColor : UnlinkedChainColor;
            }
        }

        //Swaps the box to the account name while linked, and puts the typed name back after.
        private void ShowAccountName(int slot)
        {
            InputField field = names[slot];
            if (AccountService.IsLinked(slot))
            {
                if (!showingAccount[slot])
                {
                    typedNames[slot] = field.text;
                    showingAccount[slot] = true;
                }
                string accountName = AccountService.LinkedName(slot);
                if (field.text != accountName) field.text = accountName;
                field.interactable = false;
            }
            else if (showingAccount[slot])
            {
                showingAccount[slot] = false;
                field.text = typedNames[slot];
                field.interactable = true;
            }
        }

        private void Update()
        {
            if (!popup.activeSelf || AccountService.IsBusy) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) ClosePopup();
            else if (keyboard.tabKey.wasPressedThisFrame)
                (usernameField.isFocused ? passwordField : usernameField).ActivateInputField();
        }

        private void OpenPopup(int slot)
        {
            linkingSlot = slot;
            popupTitle.text = "LINK PLAYER " + (slot + 1);
            popupSlot.text = "P" + (slot + 1);
            Color band = slotColor != null ? slotColor(slot) : Accent;
            band.a = 1f;
            popupBand.color = band;
            popupTag.color = band;
            popupStatus.text = "";
            usernameField.text = "";
            passwordField.text = "";
            popup.SetActive(true);
            popup.transform.SetAsLastSibling();
            usernameField.ActivateInputField();
        }

        private void ClosePopup()
        {
            popup.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private async void Submit()
        {
            if (AccountService.IsBusy) return;
            string username = usernameField.text.Trim();
            if (username.Length == 0 || passwordField.text.Length == 0)
            {
                ShowStatus("Enter your username and password.", ErrorColor);
                return;
            }
            ShowStatus("LINKING...", Menus.ArcadeLobbyScreen.Tape);
            string error = await AccountService.LinkPlayerAsync(linkingSlot, username, passwordField.text);
            if (this == null) return;
            if (error == null) ClosePopup();
            else
            {
                passwordField.text = "";
                ShowStatus(error, ErrorColor);
            }
        }

        private void ShowStatus(string message, Color color)
        {
            popupStatus.text = message;
            popupStatus.color = color;
        }

        //Uses the lobby font when it exists, otherwise the name box's own font.
        private Font LoadFont(string path)
        {
            Font loaded = Resources.Load<Font>(path);
            return loaded != null ? loaded : font;
        }

        private void BuildPopup(RectTransform canvas)
        {
            popup = new GameObject("Account Link Popup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var dim = (RectTransform)popup.transform;
            dim.SetParent(canvas, false);
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
            popup.GetComponent<Image>().color = DimColor;

            //Teal block shadow, white border and navy panel, like the Race link popup.
            Centered(dim, "Shadow", new Vector2(8f, -8f), Accent);
            var boxRect = Centered(dim, "Box", Vector2.zero, Color.white);
            Fill(boxRect, "Panel", 4f, PanelNavy);

            //Colored top band with the player number, vents and an ACCOUNT tag.
            popupBand = MakeImage(boxRect, "Band", Accent);
            Place(popupBand.transform, boxRect, new Vector2(4f, -4f), new Vector2(PopupWidth - 8f, 40f));
            popupSlot = AddText(boxRect, "Slot", heading, 20, Ink, TextAnchor.MiddleLeft, new Vector2(18f, -4f), new Vector2(50f, 40f));
            for (int v = 0; v < 3; v++)
                Place(MakeImage(boxRect, "Vent", new Color(Ink.r, Ink.g, Ink.b, 0.35f)).transform, boxRect, new Vector2(58f, -17f - v * 6f), new Vector2(20f, 3f));
            var tag = MakeImage(boxRect, "Tag", Ink);
            Place(tag.transform, boxRect, new Vector2(PopupWidth - 16f - 72f, -14f), new Vector2(72f, 20f));
            popupTag = AddText((RectTransform)tag.transform, "Tag Text", mono, 11, Accent, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(72f, 20f));
            popupTag.text = "ACCOUNT";

            float width = PopupWidth - PopupPadding * 2f;
            popupTitle = AddText(boxRect, "Title", heading, 26, Menus.ArcadeLobbyScreen.PaleBlue, TextAnchor.MiddleLeft, new Vector2(PopupPadding, -58f), new Vector2(width, 36f));
            AddText(boxRect, "Help", mono, 12, Menus.ArcadeMenuKit.Soft, TextAnchor.MiddleLeft, new Vector2(PopupPadding, -96f), new Vector2(width, 20f)).text = "Log in so this player's stats are saved.";
            AddText(boxRect, "Username Label", mono, 12, Menus.ArcadeLobbyScreen.SoftBlue, TextAnchor.MiddleLeft, new Vector2(PopupPadding, -124f), new Vector2(width, 16f)).text = "USERNAME";
            usernameField = AddInput(boxRect, "Username", new Vector2(PopupPadding, -144f), new Vector2(width, 42f), false);
            AddText(boxRect, "Password Label", mono, 12, Menus.ArcadeLobbyScreen.SoftBlue, TextAnchor.MiddleLeft, new Vector2(PopupPadding, -200f), new Vector2(width, 16f)).text = "PASSWORD";
            passwordField = AddInput(boxRect, "Password", new Vector2(PopupPadding, -220f), new Vector2(width, 42f), true);
            popupStatus = AddText(boxRect, "Status", mono, 12, ErrorColor, TextAnchor.MiddleCenter, new Vector2(PopupPadding, -270f), new Vector2(width, 44f));
            popupStatus.horizontalOverflow = HorizontalWrapMode.Wrap;

            float buttonWidth = (width - 14f) * 0.5f;
            float buttonY = -(PopupHeight - 24f - 52f);
            AddButton(boxRect, "CANCEL", new Vector2(PopupPadding, buttonY), new Vector2(buttonWidth, 52f), false, () => { if (!AccountService.IsBusy) ClosePopup(); });
            AddButton(boxRect, "LINK", new Vector2(PopupPadding + buttonWidth + 14f, buttonY), new Vector2(buttonWidth, 52f), true, Submit);
            passwordField.onSubmit.AddListener(_ => Submit());
            popup.SetActive(false);
        }

        //A popup-sized image in the middle of the screen, used for the box and its block shadow.
        private static RectTransform Centered(RectTransform parent, string name, Vector2 offset, Color color)
        {
            var rect = (RectTransform)MakeImage(parent, name, color).transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(PopupWidth, PopupHeight);
            return rect;
        }

        //An image stretched inside its parent with an even inset.
        private static RectTransform Fill(RectTransform parent, string name, float inset, Color color)
        {
            var rect = (RectTransform)MakeImage(parent, name, color).transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        //A plain colored image that never blocks clicks.
        private static Image MakeImage(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        //A text box with a blue edge and dark inside, like the lobby's name boxes.
        private InputField AddInput(RectTransform parent, string name, Vector2 position, Vector2 size, bool hidden)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            Place(go.transform, parent, position, size);
            var image = go.GetComponent<Image>();
            image.color = DashBlue;
            Fill((RectTransform)go.transform, "Field", 2f, DeepNavy);
            Text text = AddText((RectTransform)go.transform, "Text", mono, 16, Color.white, TextAnchor.MiddleLeft, new Vector2(12f, 0f), new Vector2(size.x - 24f, size.y));
            text.supportRichText = false;
            var field = go.GetComponent<InputField>();
            field.targetGraphic = image;
            field.textComponent = text;
            field.characterLimit = hidden ? AccountService.PasswordLimit : AccountService.UsernameLimit;
            field.contentType = hidden ? InputField.ContentType.Password : InputField.ContentType.Standard;
            return field;
        }

        //Teal button with a red block shadow for the main action, navy with a dark shadow for the other.
        private void AddButton(RectTransform parent, string label, Vector2 position, Vector2 size, bool primary, UnityEngine.Events.UnityAction onClick)
        {
            Place(MakeImage(parent, label + " Shadow", primary ? Menus.ArcadeMenuKit.Red : DeepNavy).transform, parent, position + new Vector2(6f, -6f), size);
            var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            Place(go.transform, parent, position, size);
            var image = go.GetComponent<Image>();
            image.color = primary ? Ink : Color.white;
            var face = Fill((RectTransform)go.transform, "Face", primary ? 4f : 3f, primary ? Accent : CardNavy);
            var button = go.GetComponent<Button>();
            button.targetGraphic = face.GetComponent<Image>();
            button.onClick.AddListener(onClick);
            AddText((RectTransform)go.transform, "Label", heading, 18, primary ? Ink : Color.white, TextAnchor.MiddleCenter, Vector2.zero, size).text = label;
        }

        //A label placed from the top-left of its parent.
        private Text AddText(RectTransform parent, string name, Font textFont, int size, Color color, TextAnchor alignment, Vector2 position, Vector2 rectSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            Place(go.transform, parent, position, rectSize);
            var text = go.GetComponent<Text>();
            text.font = textFont;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Place(Transform child, RectTransform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)child;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        //Two overlapping rings, tinted to show whether the slot is linked.
        private static Sprite CreateChainSprite()
        {
            const int size = 32;
            const float radius = 8f;
            const float thickness = 3f;
            var left = new Vector2(11.5f, 15.5f);
            var right = new Vector2(20.5f, 15.5f);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var pixel = new Vector2(x, y);
                    float nearest = Mathf.Min(Mathf.Abs(Vector2.Distance(pixel, left) - radius), Mathf.Abs(Vector2.Distance(pixel, right) - radius));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(thickness * 0.5f - nearest + 0.5f)));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
