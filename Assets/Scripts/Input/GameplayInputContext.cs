using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.Input
{
    [DefaultExecutionOrder(-11000)]
    [DisallowMultipleComponent]
    public sealed class GameplayInputContext : MonoBehaviour
    {
        [SerializeField] private GameplayInputBindings bindings;
        [SerializeField, Tooltip("Match scenes use their mode's profile; standalone tutorials use Bindings.")]
        private Escape4Now.GameModes.GameModeController mode;
        private static GameplayInputContext current;
        private readonly List<InputAction> enabledActions = new List<InputAction>();
        private GameplayInputBindings Profile => mode != null ? mode.InputBindings : bindings;
        public static GameplayInputBindings Current => current != null ? current.Profile : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetContext() { current = null; }

        private void OnEnable()
        {
            current = this;
            if (Profile == null) return;
            foreach (var action in Profile.Actions)
            {
                if (action == null || action.enabled) continue;
                action.Enable();
                enabledActions.Add(action);
            }
        }

        private void OnDisable()
        {
            foreach (var action in enabledActions) action.Disable();
            enabledActions.Clear();
            if (current == this) current = null;
        }
    }
}
