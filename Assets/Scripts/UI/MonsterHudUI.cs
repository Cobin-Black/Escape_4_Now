using Escape4Now.GameModes;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    //Monster gamemode only: a small panel under the match HUD showing the active survivor's health,
    //or the Monster's ability cooldowns on the Monster's turn. Stays hidden in every other gamemode.
    [DisallowMultipleComponent]
    public sealed class MonsterHudUI : MonoBehaviour
    {
        [Header("Unity UI References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField, Tooltip("The match HUD panel this panel sits under.")]
        private RectTransform hud;
        [SerializeField, Tooltip("Text whose font and style the panel copies.")]
        private Text styleSource;
        [Header("Layout")]
        [SerializeField, Min(0f)] private float gap = 8f;
        [SerializeField, Min(0f)] private float padding = 14f;
        [SerializeField, Min(1f)] private float healthHeight = 40f;
        [SerializeField, Min(1f)] private float abilitiesHeight = 92f;
        [Header("Copy")]
        [SerializeField, Min(1)] private int maxHearts = 5;
        [SerializeField] private string healthFormat = "HEALTH  {0}";
        [SerializeField] private string lostHeartColor = "#4A5058";
        [SerializeField] private string readyText = "READY";
        [SerializeField] private string cooldownFormat = "{0} TURNS";

        private RectTransform panel;
        private Text health;
        private Text abilities;

        private void LateUpdate()
        {
            var session = context != null ? context.Session : null;
            bool monsterMode = session != null && session.Mode is SurvivalModeController;
            bool show = monsterMode && context.Playing && !context.Finished
                && hud != null && hud.gameObject.activeInHierarchy;
            if (!show)
            {
                if (panel != null) panel.gameObject.SetActive(false);
                return;
            }
            if (panel == null) Build();

            MonsterCharacter monster = context.Monster;
            PlayerCharacter player = context.ActivePlayer;
            bool monsterTurn = monster != null && context.Turns != null
                && context.Turns.CurrentTurnOwner == Escape4Now.TurnSystem.TurnOwner.Enemy;
            if (!monsterTurn && player == null)
            {
                panel.gameObject.SetActive(false);
                return;
            }
            panel.gameObject.SetActive(true);
            health.gameObject.SetActive(!monsterTurn);
            abilities.gameObject.SetActive(monsterTurn);
            if (monsterTurn)
                abilities.text = "CHARGE (Q)   " + Cooldown(monster.ChargeCooldownRemaining)
                    + "\nROAR (E)   " + Cooldown(monster.RoarCooldownRemaining)
                    + "\nSTOMP (R)   " + Cooldown(monster.StompCooldownRemaining);
            else
                health.text = string.Format(healthFormat, Hearts(player.Health));

            panel.anchoredPosition = hud.anchoredPosition - new Vector2(0f, hud.rect.height + gap);
            panel.sizeDelta = new Vector2(hud.rect.width, monsterTurn ? abilitiesHeight : healthHeight);
        }

        private string Cooldown(int turns) => turns <= 0 ? readyText : string.Format(cooldownFormat, turns);

        //Filled hearts for remaining health and dimmed hearts for lost health.
        private string Hearts(int remaining)
        {
            remaining = Mathf.Clamp(remaining, 0, maxHearts);
            string lost = new string('♥', maxHearts - remaining);
            return new string('♥', remaining) + (lost.Length > 0 ? "<color=" + lostHeartColor + ">" + lost + "</color>" : "");
        }

        //Built at runtime next to the HUD and styled from it, so no scene needs extra authored objects.
        private void Build()
        {
            var go = new GameObject("Monster Mode Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel = (RectTransform)go.transform;
            panel.SetParent(hud.parent, false);
            panel.anchorMin = hud.anchorMin;
            panel.anchorMax = hud.anchorMax;
            panel.pivot = hud.pivot;
            var background = go.GetComponent<Image>();
            var hudBackground = hud.GetComponent<Image>();
            if (hudBackground != null)
            {
                background.sprite = hudBackground.sprite;
                background.type = hudBackground.type;
                background.color = hudBackground.color;
            }
            else background.color = new Color(0.1f, 0.12f, 0.14f, 0.92f);
            background.raycastTarget = false;
            health = AddText("Health", TextAnchor.MiddleLeft);
            abilities = AddText("Abilities", TextAnchor.MiddleLeft);
        }

        private Text AddText(string name, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(panel, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, 0f);
            rect.offsetMax = new Vector2(-padding, 0f);
            var text = go.GetComponent<Text>();
            text.font = styleSource != null ? styleSource.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = styleSource != null ? styleSource.fontSize : 16;
            text.color = styleSource != null ? styleSource.color : Color.white;
            text.alignment = alignment;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.lineSpacing = 1.15f;
            return text;
        }
    }
}
