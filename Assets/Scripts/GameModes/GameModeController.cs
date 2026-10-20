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
        [SerializeField, Tooltip("Enabled gameplay components to hold until Start Match. Include the map, spawners, HUD, player, inventory, turns and timer.")]
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
        public abstract bool TimerExpirationIsWin { get; }
        public abstract override string GetObjective(PlayerCharacter player);
        public abstract override bool CanUseExit(PlayerCharacter player);
        public abstract bool TryGetResult(IReadOnlyList<PlayerCharacter> participants, out MatchResult result);

        public bool ValidateConfiguration(out string error)
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
