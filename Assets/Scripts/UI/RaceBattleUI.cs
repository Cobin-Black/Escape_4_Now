// Shows the Race key battle result and forwards Continue to the battle controller.

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
        [Header("Battle Dice")]
        [SerializeField, Tooltip("The normal HUD die. Battle rolls copy its saved frame, faces and spacing.")]
        private DiceDisplay diceTemplate;
        [SerializeField] private RaceBattleDice.Look diceLook = new RaceBattleDice.Look();
        private RaceKeyBattle battle;
        private void Start()
        {
            var mode = context.Session != null ? context.Session.Mode as Race_Gamemode : null;
            battle = mode != null ? mode.KeyBattle : null;
            if (mode != null) gameObject.AddComponent<RaceBattleDice>().Setup(context, battle, diceTemplate, diceLook);
            for (int i = 0; i < opponents.Length; i++)
            {
                int index = i;
                opponents[i].onClick.AddListener(() =>
                {
                    if (Online != null) Online.ChooseRaceOpponent(index);
                    else if (battle != null && index < battle.EligibleOpponents.Count)
                        battle.ChooseOpponent(battle.EligibleOpponents[index]);
                });
            }
        }

        private LanMultiplayerSession Online => LanMultiplayerSession.Instance?.UsesRaceMatchUI == true ? LanMultiplayerSession.Instance : null;

        private void LateUpdate()
        {
            var network = Online;
            if (network != null)
            {
                string result = network.LatestState?.battleMessage;
                bool choosingOnline = network.IsMyTurn && network.RaceBattleTargets.Count > 0;
                //The result waits until the dice above the players have landed.
                panel.SetActive((choosingOnline || !string.IsNullOrEmpty(result) && !RaceBattleDice.Rolling)
                    && !context.Finished && !LocalMultiplayerSession.GameplayPaused && !network.Failed);
                if (!panel.activeSelf) return;
                message.text = choosingOnline ? chooseText : result;
                continueButton.gameObject.SetActive(!choosingOnline);
                continueButton.interactable = network.IsMyTurn;
                for (int i = 0; i < opponents.Length; i++)
                {
                    bool show = choosingOnline && i < network.RaceBattleTargets.Count;
                    opponents[i].gameObject.SetActive(show);
                    if (show) opponents[i].GetComponentInChildren<Text>().text = context.Session.GetPlayerName(network.RaceBattleTargets[i]);
                }
                return;
            }
            panel.SetActive(battle != null && battle.IsBattleActive && !battle.IsRollingDice && !LanMultiplayerSession.Active
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

        public void Continue() { if (Online != null) Online.Submit("battleClose"); else battle?.CloseBattle(); }
    }
}
