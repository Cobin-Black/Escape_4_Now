using Escape4Now.GameModes;
using Escape4Now.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.UI
{
    public sealed class RaceBattleUI : MonoBehaviour
    {
        [Header("Unity UI References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text message;
        [SerializeField] private Button[] opponents;
        [SerializeField] private Button continueButton;
        [SerializeField] private string chooseText = "Choose an opponent to challenge:";
        private RaceKeyBattle battle;
        private void Start()
        {
            var mode = context.Session != null ? context.Session.Mode as Race_Gamemode : null;
            battle = mode != null ? mode.KeyBattle : null;
            for (int i = 0; i < opponents.Length; i++)
            {
                int index = i;
                opponents[i].onClick.AddListener(() =>
                {
                    if (battle != null && index < battle.EligibleOpponents.Count)
                        battle.ChooseOpponent(battle.EligibleOpponents[index]);
                });
            }
        }

        private void LateUpdate()
        {
            panel.SetActive(battle != null && battle.IsBattleActive && !LanMultiplayerSession.Active
                && !context.Finished && !LocalMultiplayerSession.GameplayPaused);
            if (!panel.activeSelf) return;
            bool choosing = battle.IsChoosingOpponent;
            message.text = choosing ? chooseText : battle.NetworkMessage;
            continueButton.gameObject.SetActive(!choosing);
            for (int i = 0; i < opponents.Length; i++)
            {
                bool show = choosing && i < battle.EligibleOpponents.Count;
                opponents[i].gameObject.SetActive(show);
                if (show) opponents[i].GetComponentInChildren<Text>().text = battle.EligibleOpponents[i].name;
            }
        }

        public void Continue() { battle?.CloseBattle(); }
    }
}
