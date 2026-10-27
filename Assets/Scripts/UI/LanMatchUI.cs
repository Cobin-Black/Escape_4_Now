// Displays LAN setup and match controls, forwarding requests to the existing network session.

using Escape4Now.Multiplayer;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Escape4Now.UI
{
    public sealed class LanMatchUI : MonoBehaviour
    {
        [Header("Unity UI References")]
        [SerializeField] private MatchUIContext context;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text owner;
        [SerializeField] private Text item;
        [SerializeField] private GameObject controls;
        [SerializeField] private GameObject numberChoices;
        [SerializeField] private GameObject battlePanel;
        [SerializeField] private Text battleMessage;
        [SerializeField] private Button battleContinue;
        [SerializeField] private GameObject failurePanel;
        [SerializeField] private Text failureMessage;
        [Header("Copy")]
        [SerializeField] private string ownerFormat = "You are Player {0}";
        [SerializeField] private string itemFormat = "Your item: {0}";
        [SerializeField] private string keySuffix = " | Key collected";
        [SerializeField] private string[] itemNames = { "None", "Double Dice", "Custom Dice", "Key Stealer" };
        private LanMultiplayerSession Session => LanMultiplayerSession.Instance;

        private void LateUpdate()
        {
            var lan = Session;
            bool playing = LanMultiplayerSession.Active && context.Playing && !context.Finished && context.Session.ShowTemporaryUI;
            failurePanel.SetActive(playing && lan.Failed);
            if (failurePanel.activeSelf) failureMessage.text = lan.Status;
            var state = lan != null ? lan.LatestState : null;
            bool valid = playing && !lan.Failed && state != null && lan.LocalPlayerIndex >= 0 && lan.LocalPlayerIndex < state.players.Length;
            bool battle = valid && !string.IsNullOrEmpty(state.battleMessage);
            battlePanel.SetActive(battle && !LocalMultiplayerSession.GameplayPaused);
            panel.SetActive(valid && !battle && !LocalMultiplayerSession.GameplayPaused);
            if (battle)
            {
                battleMessage.text = state.battleMessage;
                battleContinue.interactable = lan.IsMyTurn;
            }
            if (!panel.activeSelf) return;
            var player = state.players[lan.LocalPlayerIndex];
            owner.text = string.Format(ownerFormat, lan.LocalPlayerIndex + 1);
            item.text = string.Format(itemFormat, itemNames[Mathf.Clamp(player.item, 0, itemNames.Length - 1)]) + (player.hasKey ? keySuffix : "");
            controls.GetComponent<CanvasGroup>().interactable = lan.IsMyTurn && !player.moving;
            numberChoices.SetActive(lan.ChoosingNumber);
        }
        private static void ReleaseGameplayFocus() { EventSystem.current?.SetSelectedGameObject(null); }
        public void Roll() { ReleaseGameplayFocus(); if (Session != null && Session.IsMyTurn) Session.Submit("roll"); }
        public void Take() { ReleaseGameplayFocus(); Session?.TakeNearbyItem(); }
        public void Use() { ReleaseGameplayFocus(); Session?.UseItem(); }
        public void Choose(int number) { ReleaseGameplayFocus(); Session?.ChooseNumber(number); }
        public void ContinueBattle() { if (Session != null && Session.IsMyTurn) Session.Submit("battleClose"); }
        public void Leave() { context.Session?.ReturnToSetup(); }
    }
}
