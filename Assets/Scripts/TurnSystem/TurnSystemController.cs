// Repeats the match's chosen turn order, counts rounds and skips participants who can no longer play.

using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.TurnSystem
{
    //Lists the types of turns supported by the game.
    public enum TurnOwner
    {
        Player,
        Enemy
    }

    //Tracks identity slots, the match's fixed turn sequence, and round transitions.
    public sealed class TurnSystemController : MonoBehaviour
    {
        //Player order, optional enemy turn, and current turn details.
        [SerializeField] private PlayerCharacter[] players = new PlayerCharacter[0];
        [SerializeField] private Escape4Now.GameModes.GameModeController gameMode;
        [SerializeField, Tooltip("Shuffle once at match start using the map seed. The chosen order repeats for the entire match without changing player identities.")]
        private bool randomizeStartingOrder = true;
        [SerializeField] private bool includeEnemyTurn = false;
        [SerializeField] private string enemyTurnName = "Enemy";
        [Header("Optional Controls")]
        [SerializeField, Tooltip("Show the native End Turn button. Normal turns still finish automatically when movement runs out.")]
        private bool showEndTurnButton;
        [SerializeField] private TurnOwner currentTurnOwner = TurnOwner.Player;
        [SerializeField] private int currentPlayerIndex;
        [SerializeField] private int turnNumber = 1;
        [SerializeField] private string currentTurnName = "Player One";

        private int turnStartedFrame;
        private int[] turnOrder;
        private bool hasNetworkTurnOrder;
        private int enemyOrderPosition, enemyResumePosition, enemySeed;
        public void ConfigureEnemyTurn(bool enabled, string name, int seed)
        {
            includeEnemyTurn = enabled;
            if (!string.IsNullOrWhiteSpace(name)) enemyTurnName = name;
            enemySeed = seed;
        }
        private readonly System.Collections.Generic.HashSet<PlayerCharacter> removed = new System.Collections.Generic.HashSet<PlayerCharacter>();
        private bool Eligible(PlayerCharacter player) => player != null && player.isActiveAndEnabled && player.Health > 0
            && !player.HasReachedExit && !removed.Contains(player) && (gameMode == null || gameMode.IsParticipantActive(player));
        // Retire this participant without reshuffling the order of everyone who remains.
        public void RemoveFromTurnCycle(PlayerCharacter player)
        {
            if (player == null || !removed.Add(player)) return;
            if (currentTurnOwner != TurnOwner.Player || currentPlayerIndex < 0 || currentPlayerIndex >= GetPlayerCount() || players[currentPlayerIndex] != player) return;
            PlayerTurnEnded?.Invoke();
            EnsureTurnOrder();
            int next = System.Array.IndexOf(turnOrder, currentPlayerIndex) + 1;
            ContinueCycle(next);
        }

        //Registers the separate players created for this local match.
        // Copy the participant list and choose the fixed order for this match.
        public void SetPlayers(PlayerCharacter[] participants, int? seed = null)
        {
            if (participants == null || participants.Length < (gameMode != null ? gameMode.MinimumPlayers : 1)
                || participants.Length > (gameMode != null ? gameMode.MaximumTurnParticipants : 4))
                throw new System.ArgumentException("Unsupported participant count for this mode.");
            var unique = new System.Collections.Generic.HashSet<PlayerCharacter>();
            foreach (PlayerCharacter participant in participants)
                if (participant == null || !unique.Add(participant))
                    throw new System.ArgumentException("Each player must be assigned once.");
            players = (PlayerCharacter[])participants.Clone();
            removed.Clear();
            turnOrder = PlayerTurnOrder.Create(players.Length, seed ?? GetMatchSeed(), randomizeStartingOrder);
            enemyOrderPosition = includeEnemyTurn && randomizeStartingOrder
                ? new System.Random(unchecked(enemySeed ^ 0x45A1)).Next(players.Length + 1) : players.Length;
            hasNetworkTurnOrder = false;
            currentPlayerIndex = turnOrder[0];
            currentTurnOwner = TurnOwner.Player;
            turnNumber = 1;
            RefreshCurrentTurnName();
        }

        //A key used on the previous turn must not also control the next player.
        // Wait until the frame after a turn changes so one button press cannot control two players.
        public bool CanReadInput(PlayerCharacter player)
        {
            return Time.timeScale > 0f
                && !Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused
                && IsPlayersTurn(player) && Time.frameCount > turnStartedFrame;
        }

        //Notifies shared effects when a player finishes or skips a turn.
        public event System.Action PlayerTurnEnded;

        //Read-only turn details for other scripts.
        public TurnOwner CurrentTurnOwner => currentTurnOwner;
        public bool ShowEndTurnButton => showEndTurnButton;
        //This remains the identity slot used by inventories and LAN ownership, not a turn position.
        public int CurrentPlayerIndex => currentPlayerIndex;
        public int TurnNumber => turnNumber;
        public string CurrentTurnName => currentTurnName;

        //Starts with the first player in this match's chosen sequence.
        private void Start()
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.IsClient) return;

            if (players != null && players.Length > 0)
                RestartTurnCycle();
        }

        public int[] CopyTurnOrder()
        {
            EnsureTurnOrder();
            return (int[])turnOrder.Clone();
        }

        //Copies the host's sequence and turn label without running local turn effects.
        public bool ApplyNetworkTurn(int index, int round, int[] order)
        {
            if (!PlayerTurnOrder.IsValid(order, GetPlayerCount()) || index < 0 || index >= GetPlayerCount() || round < 1)
                return false;
            if (hasNetworkTurnOrder)
                for (int i = 0; i < order.Length; i++)
                    if (order[i] != turnOrder[i]) return false;
            if (!hasNetworkTurnOrder) turnOrder = (int[])order.Clone();
            hasNetworkTurnOrder = true;
            currentPlayerIndex = index;
            turnNumber = round;
            currentTurnOwner = TurnOwner.Player;
            RefreshCurrentTurnName();
            return true;
        }

        private int GetMatchSeed()
        {
            if (gameMode != null && gameMode.GridMap != null) return gameMode.GridMap.LayoutSeed;
            //Standalone tutorials predate match mode components.
            var map = FindFirstObjectByType<Escape4Now.Map.IsometricMapTemplate>();
            if (map != null) return map.LayoutSeed;
            return System.Guid.NewGuid().GetHashCode();
        }

        private void EnsureTurnOrder()
        {
            if (GetPlayerCount() == 0) { turnOrder = new int[0]; return; }
            if (turnOrder == null || turnOrder.Length != GetPlayerCount())
                turnOrder = PlayerTurnOrder.Create(GetPlayerCount(), GetMatchSeed(), randomizeStartingOrder);
        }

        //Keeps the selected player and turn count within valid limits.
        private void OnValidate()
        {
            currentPlayerIndex = Mathf.Clamp(currentPlayerIndex, 0, Mathf.Max(0, GetPlayerCount() - 1));
            turnNumber = Mathf.Max(1, turnNumber);
            RefreshCurrentTurnName();
        }

        public void EndTurnAfterBattleLoss(PlayerCharacter player)
        {
            if (Time.timeScale <= 0f || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused)
                return;
            if (!IsPlayersTurn(player))
                return;

            if (player.IsMoving)
                return;

            AdvanceTurn(true);
        }

        //Changes turns after movement has finished.
        [ContextMenu("Advance Turn")]
        public void AdvanceTurn()
        {
            AdvanceTurn(false);
        }

        private void AdvanceTurn(bool battleLoss)
        {
            if (Time.timeScale <= 0f || Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused)
                return;

            int playerCount = GetPlayerCount();

            if (playerCount == 0)
            {
                return;
            }

            foreach (PlayerCharacter player in players)
            {
                if (player != null && player.IsMoving)
                {
                    return;
                }
            }

            if (currentTurnOwner == TurnOwner.Player
                && currentPlayerIndex >= 0
                && currentPlayerIndex < playerCount)
            {
                PlayerCharacter current = players[currentPlayerIndex];

                if (current == null)
                {
                    return;
                }

                // Only enforce normal turn requirements for living players.
                if (current.Health > 0)
                {
                    if (current.HasReachedExit)
                    {
                        return;
                    }

                    if (!current.HasRolled && !battleLoss)
                    {
                        return;
                    }

                    if (current.IsMoving || current.IsUsingItem() || current.IsInMenu())
                    {
                        return;
                    }
                }
            }

            PlayerTurnEnded?.Invoke();

            if (currentTurnOwner == TurnOwner.Enemy)
            {
                int next = enemyResumePosition;
                if (next >= playerCount) { next = 0; turnNumber++; }
                StartPlayerTurn(next, true);
                return;
            }

            EnsureTurnOrder();
            int nextOrderPosition = System.Array.IndexOf(turnOrder, currentPlayerIndex) + 1;

            ContinueCycle(nextOrderPosition);
        }

        private void ContinueCycle(int next)
        {
            if (next >= GetPlayerCount())
            {
                if (includeEnemyTurn && enemyOrderPosition == GetPlayerCount())
                { enemyResumePosition = next; StartEnemyTurn(); return; }
                next = 0;
                turnNumber++;
            }
            StartPlayerTurn(next);
        }

        //Restarts the chosen sequence without reshuffling it.
        [ContextMenu("Restart Turn Cycle")]
        // Restart the existing turn cycle; this is not a fresh random shuffle.
        public void RestartTurnCycle()
        {
            foreach (PlayerCharacter player in players ?? new PlayerCharacter[0])
            {
                if (player != null && player.IsMoving) return;
            }
            currentTurnOwner = TurnOwner.Player;
            turnNumber = 1;
            StartPlayerTurn(0);
        }

        //Checks that this player owns the current turn.
        public bool IsPlayersTurn(PlayerCharacter player)
        {
            return currentTurnOwner == TurnOwner.Player
                && player != null
                && !removed.Contains(player)
                && (gameMode == null || gameMode.IsParticipantActive(player))
                && currentPlayerIndex >= 0
                && currentPlayerIndex < GetPlayerCount()
                && players[currentPlayerIndex] == player;
        }

        //Selects a turn position while keeping participant slots unchanged.
        // Walk through the saved order, skipping inactive or frozen participants without getting stuck.
        private void StartPlayerTurn(int orderPosition, bool resumeEnemy = false)
        {
            turnStartedFrame = Time.frameCount;
            currentTurnOwner = TurnOwner.Player;
            int count = GetPlayerCount();
            EnsureTurnOrder();
            if (count == 0) { RefreshCurrentTurnName(); return; }
            if (!System.Array.Exists(players, Eligible)) { currentPlayerIndex = -1; currentTurnName = ""; return; }
            orderPosition = Mathf.Clamp(orderPosition, 0, count - 1);
            currentPlayerIndex = turnOrder[orderPosition];
            //Two passes allow every frozen player to skip once, even in a one-player game.
            for (int attempts = 0; attempts < count * 2; attempts++)
            {
                if (includeEnemyTurn && orderPosition == enemyOrderPosition && !resumeEnemy)
                { enemyResumePosition = orderPosition; StartEnemyTurn(); return; }
                resumeEnemy = false;
                PlayerCharacter player = players[currentPlayerIndex];
                if (Eligible(player))
                {
                    if (player.BeginEventTurn())
                    {
                        RefreshCurrentTurnName();
                        return;
                    }
                    //A frozen turn still counts as one skipped player turn.
                    PlayerTurnEnded?.Invoke();
                }
                orderPosition++;
                if (orderPosition >= count)
                {
                    orderPosition = 0;
                    if (includeEnemyTurn && enemyOrderPosition == count)
                    {
                        enemyResumePosition = count;
                        StartEnemyTurn();
                        return;
                    }
                    turnNumber++;
                }
                currentPlayerIndex = turnOrder[orderPosition];
            }
            RefreshCurrentTurnName();
        }

        //Prevents forced event movement from placing two players on the same tile.
        public bool IsOccupiedByOtherPlayer(PlayerCharacter movingPlayer, Vector2Int cell)
        {
            if (players == null) return false;
            foreach (PlayerCharacter player in players)
            {
                if (player != null && player != movingPlayer && player.isActiveAndEnabled && player.GridPosition == cell)
                    return true;
            }
            return false;
        }

        //Selects the optional enemy turn and updates its label.
        private void StartEnemyTurn()
        {
            currentTurnOwner = TurnOwner.Enemy;
            RefreshCurrentTurnName();
        }

        //Returns zero when no player list is assigned.
        private int GetPlayerCount()
        {
            if (players == null)
            {
                return 0;
            }

            return players.Length;
        }

        //Uses the current player's name or the enemy label for the display.
        private void RefreshCurrentTurnName()
        {
            if (currentTurnOwner == TurnOwner.Enemy)
            {
                currentTurnName = enemyTurnName;
                return;
            }

            if (players != null && currentPlayerIndex >= 0 && currentPlayerIndex < players.Length && players[currentPlayerIndex] != null)
            {
                currentTurnName = players[currentPlayerIndex].name;
                return;
            }

            currentTurnName = "Player One";
        }
    }
}
