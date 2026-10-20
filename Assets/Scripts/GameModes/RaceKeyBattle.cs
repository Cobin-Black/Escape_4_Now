using System.Collections.Generic;
using Escape4Now.Bots;
using Escape4Now.Items;
using Escape4Now.Multiplayer;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.GameModes
{
    [DisallowMultipleComponent]
    public sealed class RaceKeyBattle : MonoBehaviour
    {
        [Header("Race References")]
        [SerializeField] private Race_Gamemode mode;
        [Header("Bot Result Timing")]
        [SerializeField, Min(0f), Tooltip("Seconds before a bot closes its battle result automatically.")]
        private float botResultDisplaySeconds = 2f;
        [Header("Result Copy")]
        [SerializeField, TextArea] private string challengerWinFormat = "{0} wins!\nRolls: {1} - {2}\nThe key has been stolen!";
        [SerializeField, TextArea] private string defenderWinFormat = "{0} wins!\nRolls: {1} - {2}\nThe challenger loses!";
        [SerializeField] private string transferFailedMessage = "The key could not be transferred.";

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
        private float botResultCloseTime;

        public bool IsBattleActive => state != BattleState.None;
        public bool IsChoosingOpponent => state == BattleState.ChooseOpponent;
        public IReadOnlyList<PlayerCharacter> EligibleOpponents => eligibleOpponents;
        public string NetworkMessage => state == BattleState.Result ? battleMessage : "";
        // Checks for input to start a battle or close the results.
        private void Update()
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.Active || Time.timeScale <= 0f
                || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused) return;
            var bindings = mode != null ? mode.InputBindings : null;
            InputAction battleAction = bindings != null ? bindings.KeyBattle : null;
            bool pressed = battleAction != null && battleAction.WasPressedThisFrame();

            if (state == BattleState.Result)
            {
                BotController bot = challenger != null
                    ? challenger.GetComponent<BotController>()
                    : null;

                if (bot != null && bot.enabled)
                {
                    if (Time.time >= botResultCloseTime)
                        CloseBattle();
                }
                else if (pressed)
                {
                    CloseBattle();
                }

                return;
            }

            if (state != BattleState.None)
                return;

            if (!pressed)
                return;

            PlayerCharacter[] players =
                FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);

            foreach (PlayerCharacter player in players)
            {
                if (player == null || !player.IsCurrentTurn)
                    continue;

                if (player.IsBotControlled || !player.isActiveAndEnabled
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
        public void ChooseOpponent(PlayerCharacter target)
        {
            if (state == BattleState.ChooseOpponent && Time.timeScale > 0f
                && !LocalMultiplayerSession.GameplayPaused && eligibleOpponents.Contains(target))
                StartBattle(target);
        }

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
                    battleMessage = string.Format(challengerWinFormat, challenger.name, challengerRoll, opponentRoll);
                }
                else
                {
                    if (stolenKey != null)
                        opponent.Inventory.AddKey(stolenKey);

                    battleMessage = transferFailedMessage;
                }
            }
            else
            {
                challengerLost = true;

                battleMessage = string.Format(defenderWinFormat, opponent.name, challengerRoll, opponentRoll);
            }

            challenger.SetInMenu(true);
            state = BattleState.Result;

            BotController bot = challenger.GetComponent<BotController>();

            if (bot != null && bot.enabled)
                botResultCloseTime = Time.time + botResultDisplaySeconds;
        }

        // Allows a bot to challenge a key carrier on the same tile.
        public bool TryBotBattle(PlayerCharacter bot, PlayerCharacter target)
        {
            if (LanMultiplayerSession.Active
                || state != BattleState.None
                || bot == null
                || target == null
                || bot == target
                || !bot.IsCurrentTurn
                || bot.IsMoving
                || bot.HasReachedExit
                || bot.Inventory == null
                || bot.Inventory.HasKey()
                || target.Inventory == null
                || !target.Inventory.HasKey()
                || bot.GridPosition != target.GridPosition
                || !target.CanBeTargetedBy(bot))
            {
                return false;
            }

            challenger = bot;
            StartBattle(target);
            return state == BattleState.Result;
        }

        //The host checks the same rules for a connected player's battle request.
        public bool TryNetworkBattle(PlayerCharacter player, PlayerCharacter target)
        {
            if (Time.timeScale <= 0f || LocalMultiplayerSession.GameplayPaused
                || LanMultiplayerSession.IsClient || state != BattleState.None
                || player == null || target == null || !target.CanBeTargetedBy(player)) return false;
            challenger = player;
            StartBattle(target);
            return state == BattleState.Result;
        }

        //Only the challenger may dismiss a network battle result.
        public bool FinishNetworkBattle(PlayerCharacter player)
        {
            if (Time.timeScale <= 0f || LocalMultiplayerSession.GameplayPaused
                || LanMultiplayerSession.IsClient || state != BattleState.Result
                || player == null || player != challenger) return false;
            CloseBattle();
            return true;
        }

        //Closes the result and ends the challenger's turn after a loss.
        public void CloseBattle()
        {
            if (Time.timeScale <= 0f || LocalMultiplayerSession.GameplayPaused) return;
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
                    mode != null ? mode.Turns : null;

                if (turnSystem != null)
                    turnSystem.EndTurnAfterBattleLoss(playerToEndTurn);
            }
        }

    }
}
