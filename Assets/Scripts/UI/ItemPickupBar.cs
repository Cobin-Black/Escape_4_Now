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
        public enum BarAnchor { BottomCenter, BottomLeft }

        //Matches PlayerInventory.IsFull, which lets a player carry one item.
        [SerializeField, Min(1)] private int slotCount = 1;
        [SerializeField, Min(24f)] private float slotSize = 64f;
        [SerializeField, Min(0f)] private float slotSpacing = 8f;
        [SerializeField, Min(0f)] private float bottomMargin = 16f;
        //Where the bar sits along the bottom of the screen.
        [SerializeField] private BarAnchor anchor = BarAnchor.BottomCenter;
        [SerializeField, Min(0f)] private float sideMargin = 16f;
        //Header text. Empty shows the player's name, such as "Player One's Items".
        [SerializeField] private string title = "";
        [SerializeField, Min(0.1f)] private float popupSeconds = 2.5f;
        [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.65f);
        [SerializeField] private Color slotColor = new Color(1f, 1f, 1f, 0.12f);
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
            if (Escape4Now.Multiplayer.LanMultiplayerSession.Active) return;
            PlayerCharacter player = GetShownPlayer();
            if (player == null || player.Inventory == null) return;
            CreateStyles();
            Matrix4x4 oldMatrix = HudScale.Apply();

            IReadOnlyList<Item> items = player.Inventory.Items;
            int count = Mathf.Max(slotCount, items.Count);
            float padding = 10f;
            float titleHeight = 22f;
            float hintHeight = 18f;
            float barWidth = Mathf.Max(180f, count * slotSize + (count - 1) * slotSpacing + padding * 2f);
            float barHeight = titleHeight + slotSize + hintHeight + padding * 2f;
            float panelX = anchor == BarAnchor.BottomLeft ? sideMargin : (HudScale.Width - barWidth) * 0.5f;
            Rect panel = new Rect(panelX, HudScale.Height - barHeight - bottomMargin, barWidth, barHeight);
            PanelRect = panel;

            DrawBox(panel, panelColor);
            GUI.Label(new Rect(panel.x + padding, panel.y + 4f, panel.width - padding * 2f, titleHeight),
                string.IsNullOrEmpty(title) ? player.name + "'s Inventory" : title, titleStyle);

            float elapsed = Time.unscaledTime - popupStartTime;
            float slotsWidth = count * slotSize + (count - 1) * slotSpacing;
            float firstSlotX = panel.x + (panel.width - slotsWidth) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Rect slot = new Rect(firstSlotX + i * (slotSize + slotSpacing), panel.y + padding + titleHeight, slotSize, slotSize);
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
                if (item != null) DrawItem(slot, item);
            }

            //Tells the player how to use what they are holding.
            string hint = items.Count == 0 ? "No item" : "Press " + GetUseKey() + " to use";
            GUI.Label(new Rect(panel.x, panel.yMax - hintHeight - 4f, panel.width, hintHeight), hint, hintStyle);

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
            if (useItemAction == null) return "Use Item";
            string key = useItemAction.GetBindingDisplayString();
            return string.IsNullOrEmpty(key) ? "Use Item" : key;
        }

        //Draws the item's sprite in its map color, or a plain square, with its name underneath.
        private void DrawItem(Rect slot, Item item)
        {
            Rect art = new Rect(slot.x + slot.width * 0.25f, slot.y + slot.height * 0.12f, slot.width * 0.5f, slot.height * 0.5f);
            SpriteRenderer look = item.GetComponent<SpriteRenderer>();
            Sprite sprite = look != null ? look.sprite : null;
            Color tint = look != null ? look.color : Color.white;
            if (sprite != null && sprite.texture != null)
            {
                Texture2D texture = sprite.texture;
                Rect source = sprite.textureRect;
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
            GUI.Label(new Rect(slot.x - 8f, slot.yMax - 20f, slot.width + 16f, 18f), item.ItemName, nameStyle);
        }

        private static void DrawBox(Rect rect, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private void CreateStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = Color.white;
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            nameStyle.normal.textColor = Color.white;
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
            popupStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            popupStyle.normal.textColor = highlightColor;
        }
    }
}
