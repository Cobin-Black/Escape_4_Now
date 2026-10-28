// Shared match setup and input references. Each game mode supplies its own objective, exit rules and result.

using System.Collections.Generic;
using Escape4Now.Input;
using Escape4Now.Map;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.GameModes
{
    public readonly struct MatchResult
    {
        public readonly bool Won;
        public readonly string Title;
        public readonly string Message;
        public MatchResult(bool won, string title, string message)
        { Won = won; Title = title; Message = message; }
    }

    //Mode rules are separate; participants, movement, turns and inventory remain reusable.
    public abstract class GameModeController : GameMode
    {
        [Header("Shared Match References")]
        [SerializeField] private TurnSystemController turns;
        [SerializeField] private GameTimer timer;
        [SerializeField] private PlayerCharacter playerTemplate;
        [SerializeField] private GameplayInputBindings inputBindings;
        [Header("Match Startup")]
        [SerializeField, Tooltip("Gameplay components held until Start Match: map, spawners, player, inventory, turns, timer, and mode-specific systems. UI views manage their own visibility.")]
        private MonoBehaviour[] gameplayComponents = new MonoBehaviour[0];
        private readonly List<InputAction> enabledActions = new List<InputAction>();

        public TurnSystemController Turns => turns;
        public GameTimer Timer => timer;
        public PlayerCharacter PlayerTemplate => playerTemplate;
        public GameplayInputBindings InputBindings => inputBindings;
        public IReadOnlyList<MonoBehaviour> GameplayComponents => gameplayComponents;
        public abstract IPlayerGridMap GridMap { get; }
        public virtual bool SupportsLan => false;
        public virtual bool SupportsBots => false;
        public virtual int MinimumPlayers => 2;
        public virtual int MaximumPlayers => 4;
        public virtual int MaximumTurnParticipants => MaximumPlayers;
        public virtual PlayerCharacter[] BuildTurnParticipants(PlayerCharacter[] participants) => participants;
        public virtual bool PrepareMatch(int count, out string error) { error = ""; return true; }
        public virtual void BeginMatch(IReadOnlyList<PlayerCharacter> participants) { }
        public virtual bool IsParticipantActive(PlayerCharacter player) => player != null && !player.HasReachedExit;
        public virtual bool OnPlayerEscaped(PlayerCharacter player) => false;
        public virtual int MovementCost(Vector2Int cell) => 1;
        public virtual int AdjustRollMovement(PlayerCharacter player, int movement) => movement;
        public virtual bool FinishMovementTurn(PlayerCharacter player) => true;
        public virtual bool AutoEndStrandedTurn => false;
        public abstract bool TimerExpirationIsWin { get; }
        public abstract override string GetObjective(PlayerCharacter player);
        public abstract override bool CanUseExit(PlayerCharacter player);
        public abstract bool TryGetResult(IReadOnlyList<PlayerCharacter> participants, out MatchResult result);

        // Check the mode's assigned scene references before trying to start a match.
        public virtual bool ValidateConfiguration(out string error)
        {
            if (GridMap == null || turns == null || timer == null || playerTemplate == null)
            { error = "Assign the mode's map, turn system, timer and player template in the Inspector."; return false; }
            if (inputBindings == null || !inputBindings.IsConfigured)
            { error = "Assign a complete Gameplay Input Bindings asset on the mode component."; return false; }
            var assigned = new HashSet<MonoBehaviour>(gameplayComponents ?? new MonoBehaviour[0]);
            if (!assigned.Contains(GridMap as MonoBehaviour) || !assigned.Contains(turns)
                || !assigned.Contains(timer) || !assigned.Contains(playerTemplate)
                || !assigned.Contains(playerTemplate.Inventory))
            { error = "Match Startup must include the map, turns, timer, player and inventory."; return false; }
            error = "";
            return true;
        }

        // Remember which actions this component enabled so cleanup does not disable actions owned by another system.
        public void EnableMatchInputs()
        {
            foreach (InputAction action in inputBindings.Actions)
                if (action != null && !action.enabled)
                { action.Enable(); enabledActions.Add(action); }
        }

        protected virtual void OnDestroy()
        {
            //Do not disable project-wide actions or actions another system already owned.
            foreach (InputAction action in enabledActions) action.Disable();
        }
    }
}
