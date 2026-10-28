
using Escape4Now.GameModes;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    // Displays survivor health or Monster abilities inside the Survival Mode HUD.
    [DisallowMultipleComponent]
    public sealed class MonsterHudUI : MonoBehaviour
    {
        [Header("Unity UI References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField] private RectTransform hud;
        [SerializeField] private Text styleSource;

        [Header("Layout")]
        [SerializeField, Min(0f)] private float padding = 14f;
        [SerializeField, Min(1f)] private float healthHeight = 40f;
        [SerializeField, Min(1f)] private float abilitiesHeight = 92f;

        [Header("Copy")]
        [SerializeField, Min(1)] private int maxHearts = 5;
        [SerializeField] private string healthFormat = "HEALTH  {0}";
        [SerializeField] private string lostHeartColor = "#4A5058";
        [SerializeField] private string readyText = "READY";
        [SerializeField] private string cooldownFormat = "{0} TURNS";

        private RectTransform section;
        private Text health;
        private RectTransform abilitiesPanel;

        private Text chargeLabel;
        private Text chargeStatus;
        private Text roarLabel;
        private Text roarStatus;
        private Text stompLabel;
        private Text stompStatus;

        private RectTransform eventNotice;
        private Image divider;

        // Updates the Survival Mode HUD each frame.
        private void LateUpdate()
        {
            var session = context != null ? context.Session : null;

            bool survivalMode = session != null
                && session.Mode is SurvivalModeController;

            bool show = survivalMode
                && context.Playing
                && !context.Finished
                && hud != null
                && hud.gameObject.activeInHierarchy;

            if (!show)
            {
                if (section != null)
                    section.gameObject.SetActive(false);

                if (divider != null)
                    divider.gameObject.SetActive(false);

                return;
            }

            if (section == null)
                Build();

            MonsterCharacter monster = context.Monster;
            PlayerCharacter player = context.ActivePlayer;

            bool monsterTurn = monster != null
                && context.Turns != null
                && context.Turns.CurrentTurnOwner ==
                    Escape4Now.TurnSystem.TurnOwner.Enemy;

            if (!monsterTurn && player == null)
            {
                section.gameObject.SetActive(false);

                if (divider != null)
                    divider.gameObject.SetActive(false);

                return;
            }

            section.gameObject.SetActive(true);
            health.gameObject.SetActive(!monsterTurn);
            abilitiesPanel.gameObject.SetActive(monsterTurn);

            if (monsterTurn)
            {
                chargeLabel.text = Ability(
                    "CHARGE", monster.ChargeAction, monster);

                roarLabel.text = Ability(
                    "ROAR", monster.RoarAction, monster);

                stompLabel.text = Ability(
                    "STOMP", monster.StompAction, monster);

                UpdateStatus(chargeStatus,
                    monster.ChargeCooldownRemaining);

                UpdateStatus(roarStatus,
                    monster.RoarCooldownRemaining);

                UpdateStatus(stompStatus,
                    monster.StompCooldownRemaining);
            }
            else
            {
                health.text = string.Format(
                    healthFormat, Hearts(player.Health));
            }

            // Keep the section at the bottom of the main HUD.
            float sectionHeight = monsterTurn
                ? abilitiesHeight
                : healthHeight;

            section.anchorMin = new Vector2(0f, 0f);
            section.anchorMax = new Vector2(1f, 0f);
            section.pivot = new Vector2(0.5f, 0f);
            section.anchoredPosition = Vector2.zero;
            section.sizeDelta = new Vector2(0f, sectionHeight);

            // Move Event Notice above Health or Abilities.
            if (eventNotice != null)
            {
                eventNotice.anchoredPosition = new Vector2(
                    eventNotice.anchoredPosition.x,
                    sectionHeight + 8f);
            }

            // Position the divider above the section.
            if (divider != null)
            {
                RectTransform dividerRect =
                    (RectTransform)divider.transform;

                dividerRect.anchoredPosition = new Vector2(
                    0f, sectionHeight + 4f);

                dividerRect.sizeDelta = new Vector2(-28f, 1f);
                divider.gameObject.SetActive(true);
            }
        }

        // Returns an ability label with its keyboard binding.
        private static string Ability(
            string label, InputAction action, MonsterCharacter monster)
        {
            if (monster.IsAIControlled || action == null)
                return label;

            string binding = action.GetBindingDisplayString();

            return string.IsNullOrEmpty(binding)
                ? label
                : label + " (" + binding.ToUpperInvariant() + ")";
        }

        // Returns READY or the remaining cooldown.
        private string Cooldown(int turns)
        {
            return turns <= 0
                ? readyText
                : string.Format(cooldownFormat, turns);
        }

        // Updates the cooldown text and its color.
        private void UpdateStatus(Text status, int turns)
        {
            status.text = Cooldown(turns);

            status.color = turns <= 0
                ? new Color(0.45f, 0.85f, 0.64f)
                : new Color(1f, 0.75f, 0.35f);
        }

        // Creates filled and dimmed hearts based on health.
        private string Hearts(int remaining)
        {
            remaining = Mathf.Clamp(remaining, 0, maxHearts);

            string lost = new string('♥', maxHearts - remaining);

            return new string('♥', remaining)
                + (lost.Length > 0
                    ? "<color=" + lostHeartColor + ">"
                      + lost + "</color>"
                    : "");
        }

        // Creates the section, ability rows, and divider.
        private void Build()
        {
            var go = new GameObject(
                "Survival HUD Section",
                typeof(RectTransform));

            section = (RectTransform)go.transform;
            section.SetParent(hud, false);

            health = AddText("Health", section);

            var abilitiesObject = new GameObject(
                "Monster Abilities",
                typeof(RectTransform));

            abilitiesPanel =
                (RectTransform)abilitiesObject.transform;

            abilitiesPanel.SetParent(section, false);
            abilitiesPanel.anchorMin = Vector2.zero;
            abilitiesPanel.anchorMax = Vector2.one;
            abilitiesPanel.offsetMin = Vector2.zero;
            abilitiesPanel.offsetMax = Vector2.zero;

            // Create three evenly spaced ability rows.
            CreateAbilityRow("Charge", 0,
                out chargeLabel, out chargeStatus);

            CreateAbilityRow("Roar", 1,
                out roarLabel, out roarStatus);

            CreateAbilityRow("Stomp", 2,
                out stompLabel, out stompStatus);

            eventNotice = hud.Find("Event Notice") as RectTransform;

            var dividerObject = new GameObject(
                "Survival Divider",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            var dividerRect =
                (RectTransform)dividerObject.transform;

            dividerRect.SetParent(hud, false);

            dividerRect.anchorMin = new Vector2(0f, 0f);
            dividerRect.anchorMax = new Vector2(1f, 0f);
            dividerRect.pivot = new Vector2(0.5f, 0.5f);

            divider = dividerObject.GetComponent<Image>();
            divider.color = new Color(0.25f, 0.36f, 0.42f);
            divider.raycastTarget = false;
        }

        // Creates one row with an ability name and cooldown.
        private void CreateAbilityRow(
            string name, int index,
            out Text label, out Text status)
        {
            var rowObject = new GameObject(
                name + " Row",
                typeof(RectTransform));

            RectTransform row =
                (RectTransform)rowObject.transform;

            row.SetParent(abilitiesPanel, false);

            float top = -12f - index * 25f;

            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, top);
            row.sizeDelta = new Vector2(0f, 24f);

            label = AddText(name + " Label", row);
            status = AddText(name + " Status", row);

            // Use a smaller font for Monster abilities.
            label.fontSize = 14;
            status.fontSize = 14;

            RectTransform labelRect =
                (RectTransform)label.transform;

            RectTransform statusRect =
                (RectTransform)status.transform;

            // Ability name occupies the left side.
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0.65f, 1f);
            labelRect.offsetMin = new Vector2(padding, 0f);
            labelRect.offsetMax = Vector2.zero;

            // Cooldown status occupies the right side.
            statusRect.anchorMin = new Vector2(0.65f, 0f);
            statusRect.anchorMax = Vector2.one;
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = new Vector2(-padding, 0f);

            status.alignment = TextAnchor.MiddleRight;
        }

        // Creates text matching the main HUD style.
        private Text AddText(string name, RectTransform parent)
        {
            var go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));

            RectTransform rect =
                (RectTransform)go.transform;

            rect.SetParent(parent, false);

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, 0f);
            rect.offsetMax = new Vector2(-padding, 0f);

            Text text = go.GetComponent<Text>();

            text.font = styleSource != null
                ? styleSource.font
                : Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf");

            text.fontSize = styleSource != null
                ? styleSource.fontSize
                : 16;

            text.color = styleSource != null
                ? styleSource.color
                : Color.white;

            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.lineSpacing = 1.15f;

            return text;
        }
    }
}
