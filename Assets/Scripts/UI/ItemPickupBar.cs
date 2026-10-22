using System.Collections.Generic;
using Escape4Now.Items;
using Escape4Now.Player;
using Escape4Now.Multiplayer;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Escape4Now.UI
{
    public sealed class ItemPickupBar : MonoBehaviour
    {
        [Header("Unity UI References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Image icon;
        [SerializeField] private Text itemName;
        [SerializeField] private Text hint;
        [SerializeField] private Text popup;
        [Header("Display")]
        [SerializeField] private bool hideWhenEmpty = true;
        [SerializeField, Min(0.1f)] private float popupSeconds = 2.5f;
        [SerializeField] private string emptyText = "No item";
        [SerializeField] private string useFormat = "{0} TO USE";
        [SerializeField] private string pickupFormat = "{0} picked up {1}!";
        [SerializeField] private string usedFormat = "{0} used {1}.";
        [SerializeField, Tooltip("Input Actions control scheme used for button hints, rather than combining keyboard and gamepad bindings.")]
        private string bindingGroup = "Keyboard&Mouse";
        private readonly List<string> bindingLabels = new List<string>();
        private readonly Dictionary<PlayerCharacter, List<Item>> lastItems = new Dictionary<PlayerCharacter, List<Item>>();
        private float popupUntil;
        public Rect PanelRect => panel != null && panel.gameObject.activeInHierarchy
            ? MatchUIContext.ScreenRect(panel) : default;
        public Rect NoticeRect => popup != null && popup.gameObject.activeInHierarchy
            ? MatchUIContext.ScreenRect(popup.rectTransform) : default;

        private void LateUpdate()
        {
            var player = context.ActivePlayer;
            bool network = LanMultiplayerSession.Active;
            var items = player != null && player.Inventory != null ? player.Inventory.Items : null;
            bool show = !network && player != null && !context.Finished
                && (!hideWhenEmpty || items != null && items.Count > 0);
            panel.gameObject.SetActive(show);
            if (context.Session != null && context.Session.Participants != null)
                foreach (var participant in context.Session.Participants) TrackItems(participant);
            else TrackItems(player);
            popup.gameObject.SetActive(context.Playing && !context.Finished && Time.unscaledTime < popupUntil);
            if (!show) return;
            var item = items != null && items.Count > 0 ? items[0] : null;
            itemName.text = item != null ? item.ItemName : emptyText;
            var look = item != null ? item.GetComponentInChildren<SpriteRenderer>(true) : null;
            icon.enabled = item != null;
            icon.sprite = look != null ? look.sprite : null;
            icon.color = look != null ? look.color : Color.white;
            var use = player.InputBindings != null ? player.InputBindings.UseItem : null;
            InputBindingHints.GetPrimaryLabels(use, bindingGroup, bindingLabels);
            hint.text = item != null && bindingLabels.Count > 0 ? string.Format(useFormat, string.Join(" / ", bindingLabels)) : "";
        }

        private void TrackItems(PlayerCharacter player)
        {
            if (player == null || player.Inventory == null) return;
            if (!lastItems.TryGetValue(player, out var previous))
            {
                previous = new List<Item>();
                lastItems.Add(player, previous);
            }
            var items = player.Inventory.Items;
            foreach (var item in items)
                if (item != null && !previous.Contains(item)) ShowPopup(string.Format(pickupFormat, player.name, item.ItemName));
            foreach (var item in previous)
            {
                bool exists = false;
                foreach (var current in items) if (current == item) { exists = true; break; }
                if (item != null && !exists) ShowPopup(string.Format(usedFormat, player.name, item.ItemName));
            }
            previous.Clear();
            foreach (var item in items) previous.Add(item);
        }

        private void ShowPopup(string text)
        {
            popup.text = text;
            popupUntil = Time.unscaledTime + popupSeconds;
        }
    }
}
