using System.Collections.Generic;
using Escape4Now.Items;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.UI
{
    //Draws the current player's item slots at the bottom of the screen and announces pickups and item use.
    public sealed class ItemPickupBar : MonoBehaviour
    {
        public enum BarAnchor { BottomCenter, BottomLeft, BottomRight }

        //Matches PlayerInventory.IsFull, which lets a player carry one item.
        [SerializeField, Min(1)] private int slotCount = 1;
        [SerializeField, Min(24f)] private float slotSize = 48f;
        [SerializeField, Min(0f)] private float slotSpacing = 4f;
        [SerializeField, Min(0f)] private float bottomMargin = 12f;
        //Where the bar sits along the bottom of the screen.
        [SerializeField] private BarAnchor anchor = BarAnchor.BottomRight;
        [SerializeField, Min(0f)] private float sideMargin = 12f;
        //Header text. Empty shows the player's name, such as "Player One's Items".
        [SerializeField] private string title = "";
        [SerializeField] private bool showPlayerName = false;
        [SerializeField] private bool hideWhenEmpty = true;
        [SerializeField, Min(0f)] private float panelPadding = 6f;
        [SerializeField, Min(40f)] private float minimumPanelWidth = 180f;
        [SerializeField, Min(0f)] private float titleHeight = 18f;
        [SerializeField, Min(0f)] private float hintHeight = 14f;
        [Header("Panel Appearance")]
        [SerializeField] private Color panelColor = new Color(0.015f, 0.045f, 0.09f, 0.96f);
        [SerializeField] private Color panelOutlineColor = new Color(0.18f, 0.72f, 0.95f, 0.95f);
        [SerializeField] private Color panelAccentColor = new Color(1f, 0.76f, 0.16f, 1f);
        [SerializeField, Min(0f)] private float panelBorderThickness = 1.5f;
        [SerializeField, Min(0f)] private float accentWidth = 3f;
        [SerializeField] private Color slotColor = new Color(0.12f, 0.25f, 0.38f, 0.9f);
        [Header("Text Style")]
        [SerializeField, Range(8, 24)] private int titleFontSize = 12;
        [SerializeField, Range(8, 20)] private int itemNameFontSize = 12;
        [SerializeField, Range(8, 20)] private int hintFontSize = 10;
        [SerializeField, Range(10, 32)] private int popupFontSize = 18;
        [SerializeField] private Color titleColor = Color.white;
        [SerializeField] private Color itemNameColor = Color.white;
        [SerializeField] private Color hintColor = new Color(0.69f, 0.81f, 0.92f, 1f);
        [SerializeField, Min(0.1f)] private float popupSeconds = 2.5f;
        [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.2f, 1f);

        private PlayerCharacter[] players;
        //Last items seen in each player's inventory, used to spot pickups and used items.
        private readonly Dictionary<PlayerCharacter, List<Item>> lastItems = new Dictionary<PlayerCharacter, List<Item>>();
        private InputAction useItemAction;
        private string popupText = "";
        private float popupStartTime = -100f;
        private PlayerCharacter highlightPlayer;
        private Item highlightItem;
        private GUIStyle titleStyle;

        //Where the bar was last drawn on screen, so the tutorial can point at it.
        public Rect PanelRect { get; private set; }
        private GUIStyle nameStyle;
        private GUIStyle hintStyle;
        private GUIStyle popupStyle;

        private void OnValidate()
        {
            slotCount = Mathf.Max(1, slotCount);
            slotSize = Mathf.Clamp(slotSize, 24f, 160f);
            slotSpacing = Mathf.Clamp(slotSpacing, 0f, 80f);
            bottomMargin = Mathf.Clamp(bottomMargin, 0f, 200f);
            sideMargin = Mathf.Clamp(sideMargin, 0f, 200f);
            panelPadding = Mathf.Clamp(panelPadding, 0f, 40f);
            minimumPanelWidth = Mathf.Clamp(minimumPanelWidth, 40f, 600f);
            titleHeight = Mathf.Clamp(titleHeight, 0f, 80f);
            hintHeight = Mathf.Clamp(hintHeight, 0f, 80f);
            panelBorderThickness = Mathf.Clamp(panelBorderThickness, 0f, 8f);
            accentWidth = Mathf.Clamp(accentWidth, 0f, 16f);
            titleStyle = null;
            nameStyle = null;
            hintStyle = null;
            popupStyle = null;
        }

        //Finds the players and the Use Item action once the scene is set up.
        private void Start()
        {
            players = FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);
            System.Array.Sort(players, (a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (PlayerCharacter player in players)
            {
                lastItems[player] = new List<Item>();
            }
            useItemAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Use Item") : null;
        }

        //Compares each inventory with the last frame to announce new and used items.
        private void Update()
        {
            if (players == null) return;
            foreach (PlayerCharacter player in players)
            {
                if (player == null || player.Inventory == null) continue;
                IReadOnlyList<Item> items = player.Inventory.Items;
                List<Item> previous = lastItems[player];

                foreach (Item item in items)
                {
                    if (item != null && !previous.Contains(item))
                    {
                        highlightPlayer = player;
                        highlightItem = item;
                        ShowPopup(player.name + " picked up " + item.ItemName + "!");
                    }
                }
                foreach (Item item in previous)
                {
                    if (item != null && !ContainsItem(items, item))
                    {
                        ShowPopup(player.name + " used " + item.ItemName + ".");
                    }
                }

                previous.Clear();
                foreach (Item item in items) previous.Add(item);
            }
        }

        private static bool ContainsItem(IReadOnlyList<Item> items, Item item)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == item) return true;
            }
            return false;
        }

        private void ShowPopup(string text)
        {
            popupText = text;
            popupStartTime = Time.unscaledTime;
        }

        //Shows whoever's turn it is, or the first player when no one has the turn.
        private PlayerCharacter GetShownPlayer()
        {
            if (players == null || players.Length == 0) return null;
            foreach (PlayerCharacter player in players)
            {
                if (player != null && player.isActiveAndEnabled && player.IsCurrentTurn) return player;
            }
            return players[0];
        }

        private void OnGUI()
        {
            if (Escape4Now.Multiplayer.LocalMultiplayerSession.MatchHasEnded) return;
            if (Escape4Now.Multiplayer.LanMultiplayerSession.Active) return;
            PlayerCharacter player = GetShownPlayer();
            if (player == null || player.Inventory == null) return;
            IReadOnlyList<Item> items = player.Inventory.Items;
            if (hideWhenEmpty && items.Count == 0) return;
            CreateStyles();
            Matrix4x4 oldMatrix = HudScale.Apply();

            int count = Mathf.Max(slotCount, items.Count);
            float currentTitleHeight = showPlayerName || !string.IsNullOrEmpty(title) ? titleHeight : 0f;
            float slotsWidth = count * slotSize + (count - 1) * slotSpacing;
            float barWidth = Mathf.Min(Mathf.Max(minimumPanelWidth, slotsWidth + panelPadding * 3f + 112f),
                Mathf.Max(1f, HudScale.Width - sideMargin * 2f));
            float barHeight = currentTitleHeight + Mathf.Max(slotSize, slotSize * 0.48f + hintHeight) + panelPadding * 2f;
            float panelX = anchor == BarAnchor.BottomLeft ? sideMargin
                : anchor == BarAnchor.BottomRight ? HudScale.Width - barWidth - sideMargin
                : (HudScale.Width - barWidth) * 0.5f;
            panelX = Mathf.Clamp(panelX, 0f, Mathf.Max(0f, HudScale.Width - barWidth));
            float panelY = Mathf.Clamp(HudScale.Height - barHeight - bottomMargin, 0f,
                Mathf.Max(0f, HudScale.Height - barHeight));
            Rect panel = new Rect(panelX, panelY, barWidth, barHeight);
            PanelRect = panel;

            DrawBox(panel, panelColor);
            DrawOutline(panel, panelOutlineColor, panelBorderThickness);
            DrawBox(new Rect(panel.x, panel.y, accentWidth, panel.height), panelAccentColor);
            if (currentTitleHeight > 0f)
            {
                string header = string.IsNullOrEmpty(title) ? player.name + "'s Items" : title;
                GUI.Label(new Rect(panel.x + panelPadding, panel.y + 2f,
                    panel.width - panelPadding * 2f, currentTitleHeight), header, titleStyle);
            }

            float elapsed = Time.unscaledTime - popupStartTime;
            float firstSlotX = panel.x + panelPadding;
            for (int i = 0; i < count; i++)
            {
                Rect slot = new Rect(firstSlotX + i * (slotSize + slotSpacing),
                    panel.y + panelPadding + currentTitleHeight, slotSize, slotSize);
                Item item = i < items.Count ? items[i] : null;

                //The newest item's slot pulses for a moment after the pickup.
                if (item != null && item == highlightItem && player == highlightPlayer && elapsed < popupSeconds)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 10f);
                    Color glow = highlightColor;
                    glow.a = Mathf.Lerp(0.4f, 1f, pulse) * (1f - elapsed / popupSeconds);
                    DrawBox(new Rect(slot.x - 3f, slot.y - 3f, slot.width + 6f, slot.height + 6f), glow);
                }
                DrawBox(slot, slotColor);
                DrawOutline(slot, panelOutlineColor, Mathf.Min(1f, panelBorderThickness));
                if (item != null) DrawItem(slot, item);
            }

            float detailX = firstSlotX + slotsWidth + panelPadding;
            float detailWidth = Mathf.Max(0f, panel.xMax - panelPadding - detailX);
            Item shownItem = items.Count > 0 ? items[0] : null;
            GUI.Label(new Rect(detailX, panel.y + panelPadding + currentTitleHeight + 2f,
                detailWidth, Mathf.Max(16f, slotSize * 0.48f)),
                shownItem != null ? shownItem.ItemName : "No item", nameStyle);
            if (shownItem != null)
                GUI.Label(new Rect(detailX, panel.y + panelPadding + currentTitleHeight + slotSize * 0.48f,
                    detailWidth, hintHeight), GetUseKey() + "  TO USE", hintStyle);

            //Pickup message above the bar that fades out.
            if (elapsed < popupSeconds && !string.IsNullOrEmpty(popupText))
            {
                Color oldColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((popupSeconds - elapsed) / 0.5f));
                popupStyle.alignment = anchor == BarAnchor.BottomLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                Rect popupRect = anchor == BarAnchor.BottomLeft
                    ? new Rect(panel.x, panel.y - 34f, 480f, 30f)
                    : new Rect(0f, panel.y - 34f, HudScale.Width, 30f);
                GUI.Label(popupRect, popupText, popupStyle);
                GUI.color = oldColor;
            }
            GUI.matrix = oldMatrix;
        }

        //Uses the key bound to Use Item so the hint stays right if the controls change.
        private string GetUseKey()
        {
            if (useItemAction == null) return "USE ITEM";
            string keyboardKey = useItemAction.GetBindingDisplayString(0);
            string controllerKey = useItemAction.bindings.Count > 1
                ? useItemAction.GetBindingDisplayString(1) : string.Empty;
            if (!string.IsNullOrEmpty(keyboardKey) && !string.IsNullOrEmpty(controllerKey))
                return keyboardKey + " / " + controllerKey;
            string key = string.IsNullOrEmpty(keyboardKey) ? controllerKey : keyboardKey;
            return string.IsNullOrEmpty(key) ? "USE ITEM" : key;
        }

        //Draws the item's sprite in its map color, or a plain square.
        private void DrawItem(Rect slot, Item item)
        {
            Rect art = new Rect(slot.x + slot.width * 0.12f, slot.y + slot.height * 0.12f,
                slot.width * 0.76f, slot.height * 0.76f);
            SpriteRenderer look = item.GetComponent<SpriteRenderer>();
            Sprite sprite = look != null ? look.sprite : null;
            Color tint = look != null ? look.color : Color.white;
            if (sprite != null && sprite.texture != null)
            {
                Texture2D texture = sprite.texture;
                Rect source = sprite.textureRect;
                float sourceAspect = source.width / Mathf.Max(1f, source.height);
                if (sourceAspect > 1f)
                {
                    float fittedHeight = art.width / sourceAspect;
                    art.y += (art.height - fittedHeight) * 0.5f;
                    art.height = fittedHeight;
                }
                else
                {
                    float fittedWidth = art.height * sourceAspect;
                    art.x += (art.width - fittedWidth) * 0.5f;
                    art.width = fittedWidth;
                }
                Rect uv = new Rect(source.x / texture.width, source.y / texture.height,
                    source.width / texture.width, source.height / texture.height);
                Color oldColor = GUI.color;
                GUI.color = tint;
                GUI.DrawTextureWithTexCoords(art, texture, uv);
                GUI.color = oldColor;
            }
            else
            {
                DrawBox(art, tint);
            }
        }

        private static void DrawBox(Rect rect, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private static void DrawOutline(Rect rect, Color color, float thickness)
        {
            if (thickness <= 0f) return;
            DrawBox(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawBox(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawBox(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawBox(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void CreateStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = titleFontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = titleColor;
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = itemNameFontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            nameStyle.normal.textColor = itemNameColor;
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = hintFontSize,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = false
            };
            hintStyle.normal.textColor = hintColor;
            popupStyle = new GUIStyle(GUI.skin.label) { fontSize = popupFontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            popupStyle.normal.textColor = highlightColor;
        }
    }
}
