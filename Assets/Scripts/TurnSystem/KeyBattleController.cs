using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Escape4Now.Player;
using Escape4Now.Items;
using Escape4Now.TurnSystem;

namespace Escape4Now.Battle
{
    public sealed class KeyStealBattleController : MonoBehaviour
    {
        private enum BattleState
        {
            None,
            ChooseOpponent,
            Result
        }

        private BattleState state = BattleState.None;

        private PlayerCharacter challenger;
        private PlayerCharacter opponent;
        private readonly List<PlayerCharacter> eligibleOpponents =
            new List<PlayerCharacter>();

        private string battleMessage = "";
        private int challengerRoll;
        private int opponentRoll;
        private bool challengerLost;
        public string NetworkMessage => state == BattleState.Result ? battleMessage : "";

        // Checks for input to start a battle or close the results.
        private void Update()
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.Active) return;
            if (Keyboard.current == null)
                return;

            if (state == BattleState.Result)
            {
                if (Keyboard.current.enterKey.wasPressedThisFrame)
                    CloseBattle();

                return;
            }

            if (state != BattleState.None)
                return;

            if (!Keyboard.current.enterKey.wasPressedThisFrame)
                return;

            PlayerCharacter[] players =
                FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);

            foreach (PlayerCharacter player in players)
            {
                if (player == null || !player.IsCurrentTurn)
                    continue;

                if (!player.isActiveAndEnabled
                    || player.HasReachedExit
                    || player.IsMoving
                    || player.IsUsingItem()
                    || player.IsInMenu())
                    return;

                challenger = player;
                break;
            }

            if (challenger == null || challenger.Inventory == null)
                return;

            if (challenger.Inventory.HasKey())
            {
                challenger = null;
                return;
            }

            FindEligibleOpponents();

            if (eligibleOpponents.Count == 0)
            {
                challenger = null;
                return;
            }

            if (eligibleOpponents.Count == 1)
            {
                StartBattle(eligibleOpponents[0]);
            }
            else
            {
                challenger.SetInMenu(true);
                state = BattleState.ChooseOpponent;
            }
        }

        // Finds players with a key who share the challenger's tile.
        private void FindEligibleOpponents()
        {
            eligibleOpponents.Clear();

            PlayerCharacter[] players =
                FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);

            foreach (PlayerCharacter player in players)
            {
                if (player == null || player == challenger)
                    continue;

                if (!player.isActiveAndEnabled
                    || player.HasReachedExit
                    || player.Inventory == null
                    || !player.Inventory.HasKey())
                    continue;

                if (player.GridPosition == challenger.GridPosition && player.CanBeTargetedBy(challenger))
                    eligibleOpponents.Add(player);
            }
        }

        // Rolls for both players and gives the key to the winner.
        private void StartBattle(PlayerCharacter target)
        {
            //Only this controller's selection menu may be released before checking the action.
            if (state == BattleState.ChooseOpponent && challenger != null) challenger.SetInMenu(false);
            if (challenger == null || challenger.Inventory == null || challenger.Inventory.HasKey()
                || target == null || target.Inventory == null || !target.Inventory.HasKey()
                || target.GridPosition != challenger.GridPosition || !target.CanBeTargetedBy(challenger))
            {
                CloseBattle();
                return;
            }
            challengerLost = false;
            opponent = target;

            challengerRoll = Random.Range(1, 7);
            opponentRoll = Random.Range(1, 7);

            while (challengerRoll == opponentRoll)
            {
                challengerRoll = Random.Range(1, 7);
                opponentRoll = Random.Range(1, 7);
            }

            if (challengerRoll > opponentRoll)
            {
                KeyItem stolenKey = opponent.Inventory.KeyItems[0];

                Debug.Log(stolenKey == null
                ? "Battle Debug: The opponent's key is null."
                : "Battle Debug: Found key: " + stolenKey.ItemName);

                if (stolenKey != null && opponent.Inventory.RemoveKey() && challenger.Inventory.GiveKey(stolenKey))
                {
                    opponent.RecordSuccessfulTargetedAction(challenger);
                    battleMessage =
                        $"{challenger.name} wins!\n" +
                        $"Rolls: {challengerRoll} - {opponentRoll}\n" +
                        "The key has been stolen!";
                }
                else
                {
                    if (stolenKey != null)
                        opponent.Inventory.AddKey(stolenKey);

                    battleMessage =
                        "The key could not be transferred.";
                }
            }
            else
            {
                challengerLost = true;

                battleMessage =
                    $"{opponent.name} wins!\n" +
                    $"Rolls: {challengerRoll} - {opponentRoll}\n" +
                    "The challenger loses!";
            }

            challenger.SetInMenu(true);
            state = BattleState.Result;
        }

        //The host checks the same rules for a connected player's battle request.
        public bool TryNetworkBattle(PlayerCharacter player, PlayerCharacter target)
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.IsClient || state != BattleState.None
                || player == null || target == null || !target.CanBeTargetedBy(player)) return false;
            challenger = player;
            StartBattle(target);
            return state == BattleState.Result;
        }

        //Only the challenger may dismiss a network battle result.
        public bool FinishNetworkBattle(PlayerCharacter player)
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.IsClient || state != BattleState.Result
                || player == null || player != challenger) return false;
            CloseBattle();
            return true;
        }

        //Closes the result and ends the challenger's turn after a loss.
        private void CloseBattle()
        {
            PlayerCharacter playerToEndTurn = challenger;
            bool shouldEndTurn = challengerLost;

            if (challenger != null)
                challenger.SetInMenu(false);

            challenger = null;
            opponent = null;
            eligibleOpponents.Clear();
            challengerLost = false;
            state = BattleState.None;

            if (shouldEndTurn && playerToEndTurn != null)
            {
                TurnSystemController turnSystem =
                    FindFirstObjectByType<TurnSystemController>();

                if (turnSystem != null)
                    turnSystem.EndTurnAfterBattleLoss(playerToEndTurn);
            }
        }

        // Displays the opponent choices and battle results.
        private void OnGUI()
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.Active) return;
            if (state == BattleState.None)
                return;

            float width = 360f;
            float height = state == BattleState.ChooseOpponent
                ? 100f + eligibleOpponents.Count * 40f
                : 180f;

            Rect panel = new Rect(
                (Screen.width - width) / 2f,
                (Screen.height - height) / 2f,
                width,
                height);

            GUI.Box(panel, "");

            GUILayout.BeginArea(new Rect(
                panel.x + 15f,
                panel.y + 15f,
                panel.width - 30f,
                panel.height - 30f));

            if (state == BattleState.ChooseOpponent)
            {
                GUILayout.Label("Choose an opponent to challenge:");

                foreach (PlayerCharacter player in eligibleOpponents)
                {
                    if (player != null && GUILayout.Button(player.name))
                        StartBattle(player);
                }
            }
            else if (state == BattleState.Result)
            {
                GUILayout.Label("KEY-STEALING BATTLE");
                GUILayout.Space(10f);
                GUILayout.Label(battleMessage);
                GUILayout.Space(15f);
                GUILayout.Label("Press Enter to continue.");
            }

            GUILayout.EndArea();
        }
    }
}
