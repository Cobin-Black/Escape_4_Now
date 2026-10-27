using System;
using Escape4Now.GameModes;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class LobbyUI : MonoBehaviour
    {
        [Serializable]
        private sealed class PlayerSlot
        {
            public GameObject root;
            public InputField name;
            public Image swatch;
            public Toggle bot;
            [Tooltip("Survival only. Makes this player the Monster. With no player checked, the AI controls it.")]
            public Toggle monster;
        }
        [Header("Unity UI References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text title;
        [SerializeField] private Text status;
        [SerializeField] private Toggle[] playerCounts;
        [SerializeField] private PlayerSlot[] slots;
        [SerializeField, Tooltip("Opens the skin picking menu. Hidden when the scene has no Skin Handler.")]
        private Button skinsButton;
        private SkinSelectionUI skinMenu;

        private void Start()
        {
            var session = context.Session;
            if (session == null) return;
            for (int i = 0; i < playerCounts.Length; i++)
            {
                int count = i + 2;
                playerCounts[i].SetIsOnWithoutNotify(session.PlayerCount == count);
                playerCounts[i].onValueChanged.AddListener(value => { if (value) session.SetPlayerCount(count); });
            }
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                var slot = slots[i];
                slot.name.SetTextWithoutNotify(session.GetPlayerName(i));
                slot.name.onValueChanged.AddListener(value => session.SetPlayerName(index, value));
                slot.bot.SetIsOnWithoutNotify(session.IsBotPlayer(i));
                slot.bot.onValueChanged.AddListener(value => session.SetBotPlayer(index, value));
                //Only one player can be the Monster: checking a box moves the Monster to that player,
                //and unchecking the current one hands the Monster back to the AI.
                if (slot.monster != null)
                    slot.monster.onValueChanged.AddListener(value =>
                    {
                        if (value != session.IsMonsterPlayer(index)) session.SetMonsterPlayer(index);
                    });
            }
            if (skinsButton != null) skinsButton.onClick.AddListener(OpenSkins);
        }

        public void OpenSkins()
        {
            var session = context.Session;
            if (session == null || session.Skins == null || session.MatchStarted) return;
            if (skinMenu == null)
                skinMenu = SkinSelectionUI.Create((RectTransform)skinsButton.transform.parent, title.font, session, () =>
                {
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(skinsButton.gameObject);
                });
            skinMenu.Open();
        }

        private void LateUpdate()
        {
            var session = context.Session;
            panel.SetActive(session != null && !session.MatchStarted && session.ShowTemporaryUI);
            if (!panel.activeSelf) return;
            title.text = session.SetupTitle;
            status.text = session.Status;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                slot.root.SetActive(i < session.PlayerCount);
                ShowSkin(slot.swatch, session, i);
                slot.bot.gameObject.SetActive(session.Mode.SupportsBots);
                slot.bot.SetIsOnWithoutNotify(session.IsBotPlayer(i));
                slot.name.interactable = !session.IsBotPlayer(i);
                if (!slot.name.isFocused && slot.name.text != session.GetPlayerName(i))
                    slot.name.SetTextWithoutNotify(session.GetPlayerName(i));

                bool isMonsterMode = session.Mode is SurvivalModeController;

                if (slot.monster != null)
                {
                    slot.monster.gameObject.SetActive(isMonsterMode);
                    slot.monster.interactable = i < session.PlayerCount;
                    slot.monster.SetIsOnWithoutNotify(session.IsMonsterPlayer(i));
                }
            }
            for (int i = 0; i < playerCounts.Length; i++)
                playerCounts[i].SetIsOnWithoutNotify(session.PlayerCount == i + 2);
            if (skinsButton != null) skinsButton.gameObject.SetActive(session.Skins != null);
        }

        //Shows the player's skin where the color swatch was. A faded color means no skin is picked yet.
        private static void ShowSkin(Image swatch, Escape4Now.Multiplayer.LocalMultiplayerSession session, int index)
        {
            Color color = session.GetPlayerColor(index);
            if (session.Skins == null)
            {
                swatch.sprite = null;
                swatch.color = color;
                return;
            }
            PlayerSkin skin = session.Skins.GetPlayerSkin(index);
            swatch.sprite = skin != null ? skin.front : null;
            swatch.preserveAspect = true;
            swatch.color = skin != null ? Color.white : new Color(color.r, color.g, color.b, 0.25f);
        }

        public void StartMatch() { context.Session?.StartLocalMatch(); }
    }
}
