using Escape4Now.UI;
using UnityEngine;
using UnityEngine.UI;

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
        }
    }
}
