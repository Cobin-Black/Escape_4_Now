using System.Collections.Generic;
using Escape4Now.Bots;
using Escape4Now.Items;
using Escape4Now.Multiplayer;
using Escape4Now.Obstacles;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.GameModes
{
    //Race: one key is hidden in an obstacle. The first player to carry it through the exit wins.
    //Players on the same tile can battle to steal the key. None of this runs in other gamemodes.
    public sealed class Race_Gamemode : GameMode
    {
        [Header("Key")]
        [SerializeField, Tooltip("The key hidden in a random obstacle each Race match. It only exists in this gamemode.")]
        private KeyItem keyItemPrefab;
        [SerializeField, Tooltip("When enabled, players must carry the key before they can escape.")]
        private bool exitNeedsKey = true;

        [Header("Testing: Who Has The Key")]
        [SerializeField, Tooltip("Gives Player 1 the key, at the start or during Play mode. There is still only one key: "
            + "it moves from whoever had it. Turning it off puts the key back in a random obstacle. "
            + "During Play mode these toggles always show who is holding the key.")]
        private bool player1HasKey;
        [SerializeField, Tooltip("Gives Player 2 the key, at the start or during Play mode.")]
        private bool player2HasKey;
        [SerializeField, Tooltip("Gives Player 3 the key, if three or more people are playing.")]
        private bool player3HasKey;
        [SerializeField, Tooltip("Gives Player 4 the key, if four people are playing.")]
        private bool player4HasKey;

        [Header("Objective Text")]
        [SerializeField] private string objectiveBeforeKey = "Find the key, then reach the exit.";
        [SerializeField] private string objectiveAfterKey = "Reach the exit.";

        [Header("Key Steal Battle Window")]
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
        private float botResultCloseTime;
        private GUIStyle headingStyle;
        //Toggle values already applied to the match, so a change made in the Inspector can be spotted.
        private readonly bool[] appliedKeyToggles = new bool[4];

        public override GameModeType Type => GameModeType.Race;
        public KeyItem KeyPrefab => keyItemPrefab;
        public bool IsBattleActive => state != BattleState.None;
        public string NetworkMessage => state == BattleState.Result ? battleMessage : "";

        //The Race gamemode in the open scene, or null in other gamemodes.
        public static new Race_Gamemode Current => GameMode.Current as Race_Gamemode;

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

        //Runs once the match has started and every player exists.
        private void Start()
        {
            SyncTestingKeys();
        }

        // ---------- Race rules ----------

        public override bool CanUseExit(PlayerCharacter player)
        {
            return !exitNeedsKey || (player != null && player.Inventory != null && player.Inventory.HasKey());
        }

        public override string GetObjective(PlayerCharacter player)
        {
            return player != null && player.Inventory != null && player.Inventory.HasKey()
                ? objectiveAfterKey : objectiveBeforeKey;
        }

        //Hides the one key in an empty obstacle outside the special rooms, unless a tester already gave it out.
        public override void PlaceGuaranteedItems(ItemSpawner spawner)
        {
            if (spawner == null || AnyTestingKeyEnabled()) return;
            if (keyItemPrefab == null)
            {
                Debug.LogError("[Race] Assign the key prefab on the Race_Gamemode.");
                return;
            }
            if (!spawner.TryStoreInRandomObstacle(keyItemPrefab))
                Debug.LogError("[Item System] No empty obstacle was available for the key.");
        }

        private bool AnyTestingKeyEnabled()
        {
            return player1HasKey || player2HasKey || player3HasKey || player4HasKey;
        }

        //Inspector toggles hand the key straight to a player so the exit and stealing can be tested quickly.
        //Afterwards the toggles are set to whoever really holds the key, so they also show pickups and steals.
        private void SyncTestingKeys()
        {
            if (LanMultiplayerSession.IsClient || keyItemPrefab == null) return;
            IReadOnlyList<PlayerCharacter> players = GetParticipants();
            bool[] toggles = { player1HasKey, player2HasKey, player3HasKey, player4HasKey };
            for (int i = 0; i < toggles.Length && i < players.Count; i++)
            {
                PlayerCharacter player = players[i];
                if (toggles[i] == appliedKeyToggles[i] || player == null || player.Inventory == null) continue;
                if (toggles[i] && !player.Inventory.HasKey()) GiveKeyTo(player, players);
                else if (!toggles[i] && player.Inventory.HasKey()) ReturnKeyToObstacle(player);
            }

            for (int i = 0; i < toggles.Length; i++)
            {
                PlayerCharacter player = i < players.Count ? players[i] : null;
                appliedKeyToggles[i] = player != null && player.Inventory != null && player.Inventory.HasKey();
            }
            player1HasKey = appliedKeyToggles[0];
            player2HasKey = appliedKeyToggles[1];
            player3HasKey = appliedKeyToggles[2];
            player4HasKey = appliedKeyToggles[3];
        }

        //Moves the one key to this player: from another player, or out of its obstacle, the same way a pickup gives it.
        private void GiveKeyTo(PlayerCharacter player, IReadOnlyList<PlayerCharacter> players)
        {
            foreach (PlayerCharacter other in players)
            {
                if (other == null || other == player || other.Inventory == null || !other.Inventory.HasKey()) continue;
                KeyItem heldKey = other.Inventory.KeyItems[0];
                if (other.Inventory.RemoveKey() && player.Inventory.GiveKey(heldKey))
                {
                    Debug.Log($"[Race] Testing: the key moved from {other.name} to {player.name}.");
                    return;
                }
                other.Inventory.GiveKey(heldKey);
                return;
            }

            foreach (ObstacleStorage storage in FindObjectsByType<ObstacleStorage>(FindObjectsSortMode.None))
                if (storage.ContainsKey) storage.SetStoredItem(null);
            KeyItem key = Instantiate(keyItemPrefab, transform);
            key.PickUp(player.Inventory);
            if (player.Inventory.HasKey()) Debug.Log($"[Race] Testing: {player.name} was given the key.");
            else Destroy(key.gameObject);
        }

        //Turning a toggle off hides the key in a random obstacle again, so the match stays winnable.
        private void ReturnKeyToObstacle(PlayerCharacter player)
        {
            KeyItem key = player.Inventory.KeyItems[0];
            if (!player.Inventory.RemoveKey()) return;
            if (key != null) Destroy(key.gameObject);
            ItemSpawner spawner = FindFirstObjectByType<ItemSpawner>();
            bool hidden = spawner != null && spawner.TryStoreInRandomObstacle(keyItemPrefab);
            Debug.Log(hidden ? $"[Race] Testing: took the key from {player.name} and hid it in an obstacle again."
                : $"[Race] Testing: took the key from {player.name}, but no empty obstacle was free to hide it in.");
        }

        //Players in their setup order (Player 1, Player 2, ...).
        private static IReadOnlyList<PlayerCharacter> GetParticipants()
        {
            var session = FindFirstObjectByType<LocalMultiplayerSession>();
            if (session != null && session.Participants != null) return session.Participants;
            return FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.InstanceID);
        }

        // ---------- Key steal battle ----------

        // Checks for input to start a battle or close the results.
        private void Update()
        {
            SyncTestingKeys();
            if (LanMultiplayerSession.Active || Time.timeScale <= 0f
                || LocalMultiplayerSession.GameplayPaused) return;

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
                else if (Keyboard.current != null
                    && Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    CloseBattle();
                }

                return;
            }

            if (Keyboard.current == null || state != BattleState.None)
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

            BotController bot = challenger.GetComponent<BotController>();

            if (bot != null && bot.enabled)
                botResultCloseTime = Time.time + 2f;
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
            if (LanMultiplayerSession.Active || Time.timeScale <= 0f) return;
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
