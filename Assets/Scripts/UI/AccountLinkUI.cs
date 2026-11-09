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
        private const float PopupWidth = 420f;
        private const float PopupHeight = 320f;
        private const float PopupPadding = 24f;
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color PopupColor = new Color(0.07f, 0.08f, 0.11f, 1f);
        private static readonly Color InputColor = new Color(0.2f, 0.22f, 0.28f, 1f);
        private static readonly Color ButtonColor = new Color(0.36f, 0.7f, 0.48f, 1f);
        private static readonly Color CancelColor = new Color(0.32f, 0.35f, 0.42f, 1f);
        private static readonly Color UnlinkedChainColor = new Color(0.45f, 0.48f, 0.55f, 1f);
        private static readonly Color LinkedChainColor = new Color(0.21f, 0.95f, 0.8f, 1f);
        private static readonly Color ErrorColor = new Color(1f, 0.45f, 0.5f, 1f);

        private InputField[] names;
        private Func<int, bool> slotCanLink;
        private Button[] chains;
        private string[] typedNames;
        private bool[] showingAccount;
        private Font font;
        private GameObject popup;
        private Text popupTitle;
        private Text popupStatus;
        private InputField usernameField;
        private InputField passwordField;
        private int linkingSlot;

        //Adds the chains to a lobby's name boxes. slotCanLink hides a slot's chain, such as for bots.
        public static AccountLinkUI Create(GameObject owner, InputField[] names, Func<int, bool> slotCanLink)
        {
            var links = owner.AddComponent<AccountLinkUI>();
            links.names = names;
            links.slotCanLink = slotCanLink;
            links.Build();
            return links;
        }

        private void Build()
        {
            font = names[0].textComponent.font;
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
            popupTitle.text = "Link Player " + (slot + 1);
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
            ShowStatus("Linking...", Color.white);
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

            var box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var boxRect = (RectTransform)box.transform;
            boxRect.SetParent(dim, false);
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(PopupWidth, PopupHeight);
            box.GetComponent<Image>().color = PopupColor;

            float width = PopupWidth - PopupPadding * 2f;
            popupTitle = AddText(boxRect, "Title", 22, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(PopupPadding, -18f), new Vector2(width, 34f));
            AddText(boxRect, "Username Label", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(PopupPadding, -60f), new Vector2(width, 20f)).text = "Username";
            usernameField = AddInput(boxRect, "Username", new Vector2(PopupPadding, -82f), new Vector2(width, 40f), false);
            AddText(boxRect, "Password Label", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(PopupPadding, -130f), new Vector2(width, 20f)).text = "Password";
            passwordField = AddInput(boxRect, "Password", new Vector2(PopupPadding, -152f), new Vector2(width, 40f), true);
            popupStatus = AddText(boxRect, "Status", 14, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(PopupPadding, -198f), new Vector2(width, 44f));

            float buttonWidth = (width - 12f) * 0.5f;
            AddButton(boxRect, "Cancel", new Vector2(PopupPadding, -252f), new Vector2(buttonWidth, 44f), CancelColor, () => { if (!AccountService.IsBusy) ClosePopup(); });
            AddButton(boxRect, "Link Account", new Vector2(PopupPadding + buttonWidth + 12f, -252f), new Vector2(buttonWidth, 44f), ButtonColor, Submit);
            passwordField.onSubmit.AddListener(_ => Submit());
            popup.SetActive(false);
        }

        private InputField AddInput(RectTransform parent, string name, Vector2 position, Vector2 size, bool hidden)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            Place(go.transform, parent, position, size);
            var image = go.GetComponent<Image>();
            image.color = InputColor;
            Text text = AddText((RectTransform)go.transform, "Text", 16, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(10f, 0f), new Vector2(size.x - 20f, size.y));
            text.supportRichText = false;
            var field = go.GetComponent<InputField>();
            field.targetGraphic = image;
            field.textComponent = text;
            field.characterLimit = hidden ? AccountService.PasswordLimit : AccountService.UsernameLimit;
            field.contentType = hidden ? InputField.ContentType.Password : InputField.ContentType.Standard;
            return field;
        }

        private void AddButton(RectTransform parent, string label, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            Place(go.transform, parent, position, size);
            var image = go.GetComponent<Image>();
            image.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            AddText((RectTransform)go.transform, "Label", 16, FontStyle.Normal, TextAnchor.MiddleCenter, Vector2.zero, size).text = label;
        }

        private Text AddText(RectTransform parent, string name, int size, FontStyle style, TextAnchor alignment, Vector2 position, Vector2 rectSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            Place(go.transform, parent, position, rectSize);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
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
