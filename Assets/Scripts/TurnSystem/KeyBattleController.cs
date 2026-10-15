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
        [Header("Battle Window")]
        [SerializeField, Min(240f)] private float windowWidth = 360f;
        [SerializeField, Min(140f)] private float resultWindowHeight = 180f;
        [SerializeField, Min(80f)] private float opponentWindowBaseHeight = 100f;
        [SerializeField, Min(80f)] private float opponentRowHeight = 40f;
        [SerializeField, Min(0f)] private float windowPadding = 15f;
        [SerializeField, Min(0f)] private float contentSpacing = 10f;
        [SerializeField, Range(10, 32)] private int headingFontSize = 18;

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
        private GUIStyle headingStyle;
        public string NetworkMessage => state == BattleState.Result ? battleMessage : "";

        private void OnValidate()
        {
            windowWidth = Mathf.Clamp(windowWidth, 240f, 1000f);
            resultWindowHeight = Mathf.Clamp(resultWindowHeight, 140f, 700f);
            opponentWindowBaseHeight = Mathf.Clamp(opponentWindowBaseHeight, 80f, 500f);
            opponentRowHeight = Mathf.Clamp(opponentRowHeight, 24f, 100f);
            windowPadding = Mathf.Clamp(windowPadding, 0f, 80f);
            contentSpacing = Mathf.Clamp(contentSpacing, 0f, 60f);
            headingFontSize = Mathf.Clamp(headingFontSize, 10, 32);
            headingStyle = null;
        }

        // Checks for input to start a battle or close the results.
        private void Update()
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.Active || Time.timeScale <= 0f
                || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused) return;
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
            if (Time.timeScale <= 0f || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused
                || Escape4Now.Multiplayer.LanMultiplayerSession.IsClient || state != BattleState.None
                || player == null || target == null || !target.CanBeTargetedBy(player)) return false;
            challenger = player;
            StartBattle(target);
            return state == BattleState.Result;
        }

        //Only the challenger may dismiss a network battle result.
        public bool FinishNetworkBattle(PlayerCharacter player)
        {
            if (Time.timeScale <= 0f || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused
                || Escape4Now.Multiplayer.LanMultiplayerSession.IsClient || state != BattleState.Result
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
            if (Escape4Now.Multiplayer.LanMultiplayerSession.Active || Time.timeScale <= 0f) return;
            if (state == BattleState.None)
                return;
            if (headingStyle == null)
            {
                headingStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = headingFontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            float height = state == BattleState.ChooseOpponent
                ? opponentWindowBaseHeight + eligibleOpponents.Count * opponentRowHeight
                : resultWindowHeight;

            Rect panel = new Rect(
                (Screen.width - windowWidth) / 2f,
                (Screen.height - height) / 2f,
                windowWidth,
                height);

            GUI.Box(panel, "");

            GUILayout.BeginArea(new Rect(
                panel.x + windowPadding,
                panel.y + windowPadding,
                panel.width - windowPadding * 2f,
                panel.height - windowPadding * 2f));

            if (state == BattleState.ChooseOpponent)
            {
                GUILayout.Label("Choose an opponent to challenge:", headingStyle);

                foreach (PlayerCharacter player in eligibleOpponents)
                {
                    if (player != null && GUILayout.Button(player.name))
                        StartBattle(player);
                }
            }
            else if (state == BattleState.Result)
            {
                GUILayout.Label("KEY-STEALING BATTLE", headingStyle);
                GUILayout.Space(contentSpacing);
                GUILayout.Label(battleMessage);
                GUILayout.Space(contentSpacing * 1.5f);
                GUILayout.Label("Press Enter to continue.");
            }

            GUILayout.EndArea();
        }
    }
}
