using Escape4Now.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.TurnSystem
{
    // Lists the types of turns supported by the game.
    public enum TurnOwner
    {
        Player,
        Enemy
    }

    // Tracks whose turn it is and shows the turn controls.
    public sealed class TurnSystemController : MonoBehaviour
    {
        // Player order, optional enemy turn, and current turn details.
        [SerializeField] private PlayerCharacter[] players = new PlayerCharacter[0];
        [SerializeField] private bool includeEnemyTurn = false;
        [SerializeField] private string enemyTurnName = "Enemy";
        [SerializeField] private bool showTurnDisplay = true;
        [SerializeField] private TurnOwner currentTurnOwner = TurnOwner.Player;
        [SerializeField] private int currentPlayerIndex;
        [SerializeField] private int turnNumber = 1;
        [SerializeField] private string currentTurnName = "Player One";

        private GUIStyle turnDisplayStyle;

        // Read-only turn details for other scripts.
        public TurnOwner CurrentTurnOwner => currentTurnOwner;
        public int CurrentPlayerIndex => currentPlayerIndex;
        public int TurnNumber => turnNumber;
        public string CurrentTurnName => currentTurnName;

        // Starts the scene on Player One's turn.
        private void Start()
        {
            RestartAtPlayerOne();
        }

        // Restarts the turn count when Space is pressed.
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                RestartAtPlayerOne();
            }
        }

        // Keeps the selected player and turn count within valid limits.
        private void OnValidate()
        {
            currentPlayerIndex = Mathf.Clamp(currentPlayerIndex, 0, Mathf.Max(0, GetPlayerCount() - 1));
            turnNumber = Mathf.Max(1, turnNumber);
            RefreshCurrentTurnName();
        }

        // Draws the current turn label and End Turn button.
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

            if (GUI.Button(new Rect(16f, 58f, 120f, 32f), "End Turn"))
            {
                AdvanceTurn();
            }
        }

        // Changes turns after movement has finished.
        [ContextMenu("Advance Turn")]
        public void AdvanceTurn()
        {
            int playerCount = GetPlayerCount();
            if (playerCount == 0)
            {
                return;
            }

            // Finish the current move before changing players.
            foreach (PlayerCharacter player in players)
            {
                if (player != null && player.IsMoving)
                {
                    return;
                }
            }

            if (currentTurnOwner == TurnOwner.Enemy)
            {
                StartPlayerTurn(0);
                turnNumber++;
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

            StartPlayerTurn(0);
            turnNumber++;
        }

        // Returns to Player One and resets the turn count.
        [ContextMenu("Restart At Player One")]
        public void RestartAtPlayerOne()
        {
            currentTurnOwner = TurnOwner.Player;
            currentPlayerIndex = 0;
            turnNumber = 1;
            RefreshCurrentTurnName();
        }

        // Checks that this player owns the current turn.
        public bool IsPlayersTurn(PlayerCharacter player)
        {
            return currentTurnOwner == TurnOwner.Player
                && player != null
                && currentPlayerIndex >= 0
                && currentPlayerIndex < GetPlayerCount()
                && players[currentPlayerIndex] == player;
        }

        // Stops clicks on the turn controls from also selecting floor tiles.
        public bool IsPointerOverDisplay(Vector2 position)
        {
            return showTurnDisplay && (new Rect(16f, 16f, 420f, 40f).Contains(position)
                || new Rect(16f, 58f, 120f, 32f).Contains(position));
        }

        // Selects a player using an index inside the player list.
        private void StartPlayerTurn(int playerIndex)
        {
            currentTurnOwner = TurnOwner.Player;
            currentPlayerIndex = Mathf.Clamp(playerIndex, 0, Mathf.Max(0, GetPlayerCount() - 1));
            RefreshCurrentTurnName();
        }

        // Selects the optional enemy turn and updates its label.
        private void StartEnemyTurn()
        {
            currentTurnOwner = TurnOwner.Enemy;
            RefreshCurrentTurnName();
        }

        // Returns zero when no player list is assigned.
        private int GetPlayerCount()
        {
            if (players == null)
            {
                return 0;
            }

            return players.Length;
        }

        // Uses the current player's name or the enemy label for the display.
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

