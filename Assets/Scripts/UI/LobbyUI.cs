using System;
using UnityEngine;
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
        }
        [Header("Unity UI References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text title;
        [SerializeField] private Text status;
        [SerializeField] private Toggle[] playerCounts;
        [SerializeField] private PlayerSlot[] slots;

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
            }
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
                slot.swatch.color = session.GetPlayerColor(i);
                slot.bot.gameObject.SetActive(session.Mode.SupportsBots);
                slot.bot.SetIsOnWithoutNotify(session.IsBotPlayer(i));
                slot.name.interactable = !session.IsBotPlayer(i);
                if (!slot.name.isFocused && slot.name.text != session.GetPlayerName(i))
                    slot.name.SetTextWithoutNotify(session.GetPlayerName(i));
            }
            for (int i = 0; i < playerCounts.Length; i++)
                playerCounts[i].SetIsOnWithoutNotify(session.PlayerCount == i + 2);
        }

        public void StartMatch() { context.Session?.StartLocalMatch(); }
    }
}
