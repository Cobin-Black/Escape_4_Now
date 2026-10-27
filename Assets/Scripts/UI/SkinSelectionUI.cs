using System;
using System.Collections.Generic;
using Escape4Now.Multiplayer;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    //Skin picking menu opened from the lobby's Skins button. Players pick in order (Player 1 first).
    //The picking player's arrow follows the card they hover over. A picked card is darkened
    //and outlined in the owner's color so it cannot be picked twice.
    public sealed class SkinSelectionUI : MonoBehaviour
    {
        private sealed class Card
        {
            public RectTransform rect;
            public Button button;
            public Image frame;
            public Image shade;
            public Text owner;
        }

        private const int Columns = 3;
        private const float CardWidth = 136f;
        private const float CardHeight = 164f;
        private const float CardGap = 12f;
        private const float RowGap = 36f;
        private const float GridTop = -112f;
        private const float FrameWidth = 4f;
        private static readonly Color BackgroundColor = new Color(0.07f, 0.08f, 0.11f, 0.98f);
        private static readonly Color CardColor = new Color(0.2f, 0.22f, 0.28f, 1f);
        private static readonly Color EmptyFrameColor = new Color(0.32f, 0.35f, 0.42f, 1f);
        private static readonly Color ButtonColor = new Color(0.36f, 0.7f, 0.48f, 1f);

        private LocalMultiplayerSession session;
        private Font font;
        private Action closed;
        private Text prompt;
        private Button resetButton;
        private Button doneButton;
        private readonly List<Card> cards = new List<Card>();
        private readonly Image[] arrows = new Image[Skin_Handler.MaxPlayers];
        private int picker;
        private int pickerCount;
        private int hovered = -1;

        private Skin_Handler Skins => session != null ? session.Skins : null;

        //Builds the menu over the lobby content. It starts hidden.
        public static SkinSelectionUI Create(RectTransform parent, Font font, LocalMultiplayerSession session, Action closed)
        {
            var root = new GameObject("Skin Menu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = BackgroundColor;
            var menu = root.AddComponent<SkinSelectionUI>();
            menu.session = session;
            menu.font = font;
            menu.closed = closed;
            menu.Build();
            root.SetActive(false);
            return menu;
        }

        //Clears everyone's skin so all of them can be picked, then starts with Player 1.
        public void Open()
        {
            if (Skins == null) return;
            Skins.ClearAll();
            picker = 0;
            pickerCount = Mathf.Clamp(session.PlayerCount, 1, Skin_Handler.MaxPlayers);
            hovered = -1;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh();
            SelectFirstFreeCard();
        }

        private void Close()
        {
            //Slots outside the current player count still get a free skin so the count can be raised later.
            Skins?.FillMissing();
            gameObject.SetActive(false);
            closed?.Invoke();
        }

        private void Build()
        {
            AddText(transform, "Title", "Skins", 22, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(456f, 40f));
            prompt = AddText(transform, "Prompt", "", 16, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(0f, -44f), new Vector2(456f, 30f));
            float left = (456f - (Columns * CardWidth + (Columns - 1) * CardGap)) * 0.5f;
            for (int i = 0; i < Skins.Count; i++)
            {
                int column = i % Columns;
                int row = i / Columns;
                var position = new Vector2(left + column * (CardWidth + CardGap), GridTop - row * (CardHeight + RowGap));
                cards.Add(BuildCard(i, position));
            }
            float buttonsTop = GridTop - Mathf.CeilToInt(Skins.Count / (float)Columns) * (CardHeight + RowGap) + RowGap - 20f;
            resetButton = AddButton("Reset", new Vector2(0f, buttonsTop), new Vector2(222f, 40f), new Color(0.3f, 0.45f, 0.75f, 1f), Open);
            doneButton = AddButton("Done", new Vector2(234f, buttonsTop), new Vector2(222f, 40f), ButtonColor, Close);
            Sprite arrowSprite = CreateArrowSprite();
            for (int i = 0; i < arrows.Length; i++)
            {
                var arrow = new GameObject("Player " + (i + 1) + " Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = Place(arrow.transform, transform, Vector2.zero, new Vector2(24f, 18f));
                rect.pivot = new Vector2(0.5f, 0f);
                var image = arrow.GetComponent<Image>();
                image.sprite = arrowSprite;
                image.raycastTarget = false;
                image.color = session.GetPlayerColor(i);
                arrows[i] = image;
                arrow.SetActive(false);
            }
            LinkNavigation();
        }

        private Card BuildCard(int index, Vector2 position)
        {
            PlayerSkin skin = Skins.GetSkin(index);
            var card = new Card();
            var root = new GameObject("Skin " + (index + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            card.rect = Place(root.transform, transform, position, new Vector2(CardWidth, CardHeight));
            card.frame = root.GetComponent<Image>();
            card.frame.color = EmptyFrameColor;

            var inner = AddImage(root.transform, "Background", CardColor, new Vector2(FrameWidth, -FrameWidth),
                new Vector2(CardWidth - FrameWidth * 2f, CardHeight - FrameWidth * 2f));
            var preview = AddImage(root.transform, "Preview", Color.white, new Vector2(18f, -12f), new Vector2(CardWidth - 36f, CardHeight - 48f));
            preview.sprite = skin != null ? skin.front : null;
            preview.preserveAspect = true;
            preview.raycastTarget = false;
            AddText(root.transform, "Name", skin != null ? skin.skinName : "", 14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(FrameWidth, -(CardHeight - 34f)), new Vector2(CardWidth - FrameWidth * 2f, 28f));
            card.shade = AddImage(root.transform, "Picked Shade", new Color(0f, 0f, 0f, 0.6f), new Vector2(FrameWidth, -FrameWidth),
                new Vector2(CardWidth - FrameWidth * 2f, CardHeight - FrameWidth * 2f));
            card.shade.raycastTarget = false;
            card.owner = AddText(card.shade.transform, "Owner", "", 26, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(CardWidth - FrameWidth * 2f, CardHeight - FrameWidth * 2f));

            card.button = root.GetComponent<Button>();
            card.button.targetGraphic = inner;
            ColorBlock colors = card.button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = Color.white;
            card.button.colors = colors;
            card.button.onClick.AddListener(() => Pick(index));

            //Mouse hover and keyboard/controller selection both move the picking player's arrow.
            var trigger = root.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => Hover(index, true));
            AddTrigger(trigger, EventTriggerType.Select, () => Hover(index, false));
            return card;
        }

        private void Hover(int index, bool fromPointer)
        {
            hovered = index;
            //Keeps keyboard navigation starting from the card the mouse is over.
            if (fromPointer && cards[index].button.interactable && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(cards[index].button.gameObject);
        }

        private void Pick(int index)
        {
            if (picker >= pickerCount || Skins == null || !Skins.TryAssign(picker, index)) return;
            picker++;
            Refresh();
            if (picker < pickerCount) SelectFirstFreeCard();
            else if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(doneButton.gameObject);
        }

        private void LateUpdate()
        {
            if (Skins == null) return;
            Refresh();
        }

        private void Refresh()
        {
            bool picking = picker < pickerCount;
            prompt.text = picking ? session.GetPlayerName(picker) + ", pick your skin" : "Everyone has a skin. Press Done.";
            prompt.color = picking ? session.GetPlayerColor(picker) : Color.white;
            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                int owner = Skins.GetOwner(i);
                bool taken = owner >= 0;
                card.button.interactable = !taken && picking;
                card.shade.gameObject.SetActive(taken);
                card.frame.color = taken ? session.GetPlayerColor(owner) : EmptyFrameColor;
                card.owner.text = taken ? "P" + (owner + 1) : "";
                card.owner.color = taken ? session.GetPlayerColor(owner) : Color.white;
            }
            doneButton.interactable = !picking;
            for (int i = 0; i < arrows.Length; i++)
            {
                bool show = picking && i == picker && hovered >= 0 && hovered < cards.Count;
                arrows[i].gameObject.SetActive(show);
                if (!show) continue;
                RectTransform card = cards[hovered].rect;
                ((RectTransform)arrows[i].transform).anchoredPosition =
                    card.anchoredPosition + new Vector2(CardWidth * 0.5f, 4f);
            }
        }

        private void SelectFirstFreeCard()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (Skins.IsTaken(i)) continue;
                hovered = i;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cards[i].button.gameObject);
                return;
            }
        }

        //Explicit links keep keyboard/controller focus inside the menu instead of the lobby behind it.
        private void LinkNavigation()
        {
            int rows = Mathf.CeilToInt(cards.Count / (float)Columns);
            for (int i = 0; i < cards.Count; i++)
            {
                int column = i % Columns;
                int row = i / Columns;
                var navigation = new Navigation { mode = Navigation.Mode.Explicit };
                navigation.selectOnLeft = column > 0 ? cards[i - 1].button : null;
                navigation.selectOnRight = column < Columns - 1 && i + 1 < cards.Count ? cards[i + 1].button : null;
                navigation.selectOnUp = row > 0 ? cards[i - Columns].button : null;
                navigation.selectOnDown = row < rows - 1 && i + Columns < cards.Count ? cards[i + Columns].button
                    : column == 0 ? resetButton : doneButton;
                cards[i].button.navigation = navigation;
            }
            Button lastRowFirst = cards.Count > 0 ? cards[(rows - 1) * Columns].button : null;
            resetButton.navigation = new Navigation
            { mode = Navigation.Mode.Explicit, selectOnRight = doneButton, selectOnUp = lastRowFirst };
            doneButton.navigation = new Navigation
            { mode = Navigation.Mode.Explicit, selectOnLeft = resetButton, selectOnUp = cards.Count > 0 ? cards[cards.Count - 1].button : null };
        }

        private Button AddButton(string label, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            Place(go.transform, transform, position, size);
            var image = go.GetComponent<Image>();
            image.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            AddText(go.transform, "Label", label, 16, FontStyle.Normal, TextAnchor.MiddleCenter, Vector2.zero, size);
            return button;
        }

        private Image AddImage(Transform parent, string name, Color color, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Place(go.transform, parent, position, size);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private Text AddText(Transform parent, string name, string value, int size, FontStyle style, TextAnchor alignment,
            Vector2 position, Vector2 rectSize)
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
            text.text = value;
            return text;
        }

        //Top-left anchored like the authored lobby layout.
        private static RectTransform Place(Transform child, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)child;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        //Small downward-pointing triangle tinted with each player's color.
        private static Sprite CreateArrowSprite()
        {
            const int width = 16;
            const int height = 12;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < height; y++)
            {
                float halfWidth = (y + 0.5f) / height * width * 0.5f;
                for (int x = 0; x < width; x++)
                {
                    bool inside = Mathf.Abs(x + 0.5f - width * 0.5f) <= halfWidth;
                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), 100f);
        }
    }
}
