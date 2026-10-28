using System.Collections.Generic;
using Escape4Now.Items;
using Escape4Now.Multiplayer;
using Escape4Now.Obstacles;
using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.GameModes
{
    //Race: one key is hidden in an obstacle. The first player to carry it through the exit wins.
    //Players on the same tile can battle to steal the key. None of this runs in other gamemodes.
    public sealed class Race_Gamemode : GameModeController
    {
        [Header("Indoor Map")]
        [SerializeField] private Escape4Now.Map.IsometricMapTemplate map;
        [SerializeField] private bool enableLanSupport = true;
        [SerializeField] private bool enableBots = true;
        [Header("Player Setup")]
        [SerializeField, Tooltip("Allow one player to find the key and escape against the timer.")]
        // The lobby and turn setup read this option; solo play still uses the normal Race key and timer rules.
        private bool allowSoloPlay = true;
        [Header("Results")]
        [SerializeField] private string escapedTitleSuffix = "ESCAPED";
        [SerializeField] private string otherPlayersLoseMessage = "All other players lose.";
        [SerializeField] private string soloEscapeMessage = "You found the key and escaped.";
        [SerializeField] private string timeoutTitle = "EVERYONE LOSES";
        [SerializeField, TextArea] private string timeoutMessage = "Time ran out before anyone escaped.";

        public override Escape4Now.Map.IPlayerGridMap GridMap => map;
        public override bool SupportsLan => enableLanSupport;
        public override bool SupportsBots => enableBots;
        public override int MinimumPlayers => allowSoloPlay ? 1 : base.MinimumPlayers;
        public override bool TimerExpirationIsWin => false;

        // Keep Race results separate from Survival, including a suitable message when only one player is playing.
        public override bool TryGetResult(IReadOnlyList<PlayerCharacter> participants, out MatchResult result)
        {
            if (TryGetWinner(participants, out PlayerCharacter winner))
            {
                result = new MatchResult(true, winner.name.ToUpperInvariant() + " " + escapedTitleSuffix,
                    participants.Count == 1 ? soloEscapeMessage : otherPlayersLoseMessage);
                return true;
            }
            if (Timer != null && Timer.HasExpired)
            { result = new MatchResult(false, timeoutTitle, timeoutMessage); return true; }
            result = default;
            return false;
        }
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
        [SerializeField, Tooltip("Optional progress-based wording. Off keeps the match objective consistent for every player.")]
        private bool updateObjectiveAfterKey;
        [SerializeField] private string objectiveBeforeKey = "Find the key, then reach the exit.";
        [SerializeField] private string objectiveAfterKey = "Reach the exit.";

        [Header("Race Systems")]
        [SerializeField] private RaceKeyBattle keyBattle;
        //Toggle values already applied to the match, so a change made in the Inspector can be spotted.
        private readonly bool[] appliedKeyToggles = new bool[4];

        public override GameModeType Type => GameModeType.Race;
        public KeyItem KeyPrefab => keyItemPrefab;
        public bool IsBattleActive => keyBattle != null && keyBattle.IsBattleActive;
        public RaceKeyBattle KeyBattle => keyBattle;
        public string NetworkMessage => keyBattle != null ? keyBattle.NetworkMessage : "";

        //The Race gamemode in the open scene, or null in other gamemodes.
        public static new Race_Gamemode Current => GameMode.Current as Race_Gamemode;

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
            return updateObjectiveAfterKey && player != null && player.Inventory != null && player.Inventory.HasKey()
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

        private void Update() { SyncTestingKeys(); }

        public bool TryBotBattle(PlayerCharacter player, PlayerCharacter target)
            => keyBattle != null && keyBattle.TryBotBattle(player, target);
        public bool TryNetworkBattle(PlayerCharacter player, PlayerCharacter target)
            => keyBattle != null && keyBattle.TryNetworkBattle(player, target);
        public bool FinishNetworkBattle(PlayerCharacter player)
            => keyBattle != null && keyBattle.FinishNetworkBattle(player);
    }
}
