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

    //Tracks whose turn it is and shows the turn controls.
    public sealed class TurnSystemController : MonoBehaviour
    {
        //Player order, optional enemy turn, and current turn details.
        [SerializeField] private PlayerCharacter[] players = new PlayerCharacter[0];
        [SerializeField] private bool includeEnemyTurn = false;
        [SerializeField] private string enemyTurnName = "Enemy";
        [SerializeField] private bool showTurnDisplay = true;
        [SerializeField] private TurnOwner currentTurnOwner = TurnOwner.Player;
        [SerializeField] private int currentPlayerIndex;
        [SerializeField] private int turnNumber = 1;
        [SerializeField] private string currentTurnName = "Player One";

        private GUIStyle turnDisplayStyle;
        private int turnStartedFrame;

        //Registers the separate players created for this local match.
        public void SetPlayers(PlayerCharacter[] participants)
        {
            if (participants == null || participants.Length < 2 || participants.Length > 4)
                throw new System.ArgumentException("A local match needs two to four players.");
            var unique = new System.Collections.Generic.HashSet<PlayerCharacter>();
            foreach (PlayerCharacter participant in participants)
                if (participant == null || !unique.Add(participant))
                    throw new System.ArgumentException("Each player must be assigned once.");
            players = (PlayerCharacter[])participants.Clone();
        }

        //A key used on the previous turn must not also control the next player.
        public bool CanReadInput(PlayerCharacter player)
        {
            return IsPlayersTurn(player) && Time.frameCount > turnStartedFrame;
        }

        //Notifies shared effects when a player finishes or skips a turn.
        public event System.Action PlayerTurnEnded;

        //Read-only turn details for other scripts.
        public TurnOwner CurrentTurnOwner => currentTurnOwner;
        public int CurrentPlayerIndex => currentPlayerIndex;
        public int TurnNumber => turnNumber;
        public string CurrentTurnName => currentTurnName;

        //Starts the scene on Player One's turn.
        private void Start()
        {
            RestartAtPlayerOne();
        }

        //Keeps the selected player and turn count within valid limits.
        private void OnValidate()
        {
            currentPlayerIndex = Mathf.Clamp(currentPlayerIndex, 0, Mathf.Max(0, GetPlayerCount() - 1));
            turnNumber = Mathf.Max(1, turnNumber);
            RefreshCurrentTurnName();
        }

        //Draws the current turn label and End Turn button.
        private void OnGUI()
        {
            if (!showTurnDisplay)
            {
                return;
            }

            if (turnDisplayStyle == null)
            {
                turnDisplayStyle = new GUIStyle(GUI.skin.label);
                turnDisplayStyle.fontSize = 24;
                turnDisplayStyle.fontStyle = FontStyle.Bold;
                turnDisplayStyle.normal.textColor = Color.white;
            }

            GUI.Label(new Rect(16f, 16f, 420f, 40f), $"Turn {turnNumber}: {currentTurnName}", turnDisplayStyle);

            if (Escape4Now.Multiplayer.LocalMultiplayerSession.AllowTurnButtons
                && GUI.Button(new Rect(16f, 58f, 120f, 32f), "End Turn"))
            {
                AdvanceTurn();
            }
        }

        public void EndTurnAfterBattleLoss(PlayerCharacter player)
        {
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

            if (currentTurnOwner == TurnOwner.Player)
            {
                PlayerTurnEnded?.Invoke();
            }

            if (currentTurnOwner == TurnOwner.Enemy)
            {
                turnNumber++;
                StartPlayerTurn(0);
                return;
            }

            int nextPlayerIndex = currentPlayerIndex + 1;

            if (nextPlayerIndex < playerCount)
            {
                StartPlayerTurn(nextPlayerIndex);
                return;
            }

            if (includeEnemyTurn)
            {
                StartEnemyTurn();
                return;
            }

            turnNumber++;
            StartPlayerTurn(0);
        }

        //Returns to Player One and resets the turn count.
        [ContextMenu("Restart At Player One")]
        public void RestartAtPlayerOne()
        {
            foreach (PlayerCharacter player in players ?? new PlayerCharacter[0])
            {
                if (player != null && player.IsMoving) return;
            }
            currentTurnOwner = TurnOwner.Player;
            currentPlayerIndex = 0;
            turnNumber = 1;
            StartPlayerTurn(0);
        }

        //Checks that this player owns the current turn.
        public bool IsPlayersTurn(PlayerCharacter player)
        {
            return currentTurnOwner == TurnOwner.Player
                && player != null
                && currentPlayerIndex >= 0
                && currentPlayerIndex < GetPlayerCount()
                && players[currentPlayerIndex] == player;
        }

        //Selects a player using an index inside the player list.
        private void StartPlayerTurn(int playerIndex)
        {
            turnStartedFrame = Time.frameCount;
            currentTurnOwner = TurnOwner.Player;
            int count = GetPlayerCount();
            currentPlayerIndex = Mathf.Clamp(playerIndex, 0, Mathf.Max(0, count - 1));
            //Two passes allow every frozen player to skip once, even in a one-player game.
            for (int attempts = 0; attempts < count * 2; attempts++)
            {
                PlayerCharacter player = players[currentPlayerIndex];
                if (player != null && player.isActiveAndEnabled && !player.HasReachedExit)
                {
                    if (player.BeginEventTurn())
                    {
                        RefreshCurrentTurnName();
                        return;
                    }
                    //A frozen turn still counts as one skipped player turn.
                    PlayerTurnEnded?.Invoke();
                }
                currentPlayerIndex++;
                if (currentPlayerIndex >= count)
                {
                    currentPlayerIndex = 0;
                    if (includeEnemyTurn)
                    {
                        StartEnemyTurn();
                        return;
                    }
                    turnNumber++;
                }
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