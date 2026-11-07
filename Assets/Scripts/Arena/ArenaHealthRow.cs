using Escape4Now.UI;
using UnityEngine;
using UnityEngine.UI;
using Escape4Now.Items;

namespace Escape4Now.Arena
{
    // Displays the existing player health. It never owns or changes health values.
    public sealed class ArenaHealthRow : MonoBehaviour
    {
        [SerializeField]
        private ArenaInterfaceStyle style;
        [SerializeField]
        private Image portrait, identity;
        [SerializeField]
        private Text hearts;
        [SerializeField]
        private Text playerName, value;
        [SerializeField]
        private Outline holderOutline;
        [SerializeField]
        private string healthFormat = "{0}/{1}";
        [SerializeField]
        private string eliminatedLabel = "OUT";
        private ArenaParticipant bound;
        private int startingHealth;

        [Header("FreeForAll Inventory")]
        [SerializeField] private GameObject inventorySection;
        [SerializeField] private Image itemIcon;
        [SerializeField] private Text itemCharges;
        [SerializeField] private Sprite spitballIcon;
        [SerializeField] private Sprite rulerIcon;
        [SerializeField] private Sprite eraserIcon;
        // Bind to one participant and display their own health and potato-holder state.
        public void Show(ArenaParticipant player, bool hasPotato)
        {
            gameObject.SetActive(player != null);
            if (player == null)
                return;
            if (bound != player)
            {
                bound = player;
                startingHealth = Mathf.Max(1, player.InitialHealth);
            }

            playerName.text = player.name;
            identity.color = player.Character.IdentityColor;
            portrait.sprite = player.Character.GetComponent<SpriteRenderer>().sprite;
            portrait.color = player.Character.Appearance != null && player.Character.Appearance.HasSkin ? Color.white : player.Character.IdentityColor;
            holderOutline.enabled = hasPotato;
            holderOutline.effectColor = style.holder;
            int health = player.Character.Health;
            value.text = player.Alive ? string.Format(healthFormat, health, startingHealth) : eliminatedLabel;
            hearts.color = style.healthy;
            hearts.text = HealthHeartText.Format(player.Alive ? health : 0, startingHealth, "#" + ColorUtility.ToHtmlStringRGB(style.emptyHealth));
            ShowInventory(player);
        }

        // Display the player's current FreeForAll item and charges.
        private void ShowInventory(ArenaParticipant player)
        {
            bool isFFA = player.Match is FreeForAllController;

            if (!isFFA)
            {
                inventorySection.SetActive(false);
                return;
            }

            PlayerInventory inventory = player.GetComponent<PlayerInventory>();

            if (inventory == null || inventory.Items.Count == 0 ||
                inventory.Items[0] is not FreeForAllItem item)
            {
                inventorySection.SetActive(false);
                return;
            }

            inventorySection.SetActive(true);

            switch (item.ItemType)
            {
                case FreeForAllItemType.Spitball:
                    itemIcon.sprite = spitballIcon;
                    break;

                case FreeForAllItemType.Ruler:
                    itemIcon.sprite = rulerIcon;
                    break;

                case FreeForAllItemType.Eraser:
                    itemIcon.sprite = eraserIcon;
                    break;
            }

            itemCharges.text = item.UsesRemaining + "/" + item.MaxUses;
        }
    }
}
