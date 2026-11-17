using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.Arena
{
    // Each participant owns an action copy. Keyboard layouts and gamepads never feed another slot.
    // With a controller the player uses the project's Player actions (Move, Attack, Potato Pass) on that controller only.
    // Without one they use their own keyboard layout from the arena controls asset.
    [DisallowMultipleComponent]
    public sealed class ArenaLocalInput : MonoBehaviour
    {
        private const string GamepadMapName = "Player";
        private const string MoveAction = "Move";
        private const string PassAction = "Potato Pass";
        private const string AttackAction = "Attack";
        // Older keyboard layouts only have one action key named Tag; it doubles as Pass and Attack.
        private const string LegacyActionKey = "Tag";

        private InputActionAsset owned;
        private InputAction move, passAction, attackAction;
        public Vector2 Move => move != null ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1) : Vector2.zero;
        public bool PassPressed => passAction != null && passAction.WasPressedThisFrame();
        public bool AttackPressed => attackAction != null && attackAction.WasPressedThisFrame();
        // Kept for existing callers.
        public bool TagPressed => PassPressed;
        public Gamepad Gamepad { get; private set; }
        public bool UsesKeyboard => Gamepad == null;
        public int KeyboardLayout { get; private set; } = -1;

        // Controller player: a private copy of the Player map that only listens to this controller.
        public void ConfigureGamepad(InputActionAsset gamepadTemplate, Gamepad pad)
        {
            Release();
            Gamepad = pad;
            owned = Instantiate(gamepadTemplate);
            owned.Disable();
            var map = owned.FindActionMap(GamepadMapName, true);
            map.devices = new InputDevice[] { pad };
            move = map.FindAction(MoveAction, true);
            passAction = map.FindAction(PassAction, false);
            attackAction = map.FindAction(AttackAction, false);
            map.Enable();
        }

        // Player 1 on keyboard: the project's Player map keyboard bindings (Move, Potato Pass, Attack), keyboard only.
        // Keys another keyboard player's backup layout uses (for example the arrow keys) are switched off in this copy
        // so the two players never share a key.
        public void ConfigurePrimaryKeyboard(InputActionAsset primaryTemplate, ICollection<string> reservedKeys)
        {
            Release();
            KeyboardLayout = 0;
            owned = Instantiate(primaryTemplate);
            owned.Disable();
            var map = owned.FindActionMap(GamepadMapName, true);
            if (Keyboard.current != null)
                map.devices = new InputDevice[] { Keyboard.current };
            move = map.FindAction(MoveAction, true);
            passAction = map.FindAction(PassAction, false);
            attackAction = map.FindAction(AttackAction, false);
            if (reservedKeys != null && reservedKeys.Count > 0)
                foreach (InputAction action in new[] { move, passAction, attackAction })
                    DisableReservedKeys(action, reservedKeys);
            map.Enable();
        }

        private static void DisableReservedKeys(InputAction action, ICollection<string> reservedKeys)
        {
            if (action == null) return;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite || string.IsNullOrEmpty(binding.path)) continue;
                if (reservedKeys.Contains(NormalizePath(binding.path)))
                    action.ApplyBindingOverride(i, "");
            }
        }

        // The keyboard keys a backup layout (Player2..Player4 map) uses, so Player 1's copy can avoid them.
        public static void CollectLayoutKeys(InputActionAsset keyboardTemplate, int layout, HashSet<string> keys)
        {
            InputActionMap map = keyboardTemplate != null ? keyboardTemplate.FindActionMap("Player" + (layout + 1)) : null;
            if (map == null) return;
            foreach (InputBinding binding in map.bindings)
                if (!binding.isComposite && !string.IsNullOrEmpty(binding.path) && binding.path.StartsWith("<Keyboard>"))
                    keys.Add(NormalizePath(binding.path));
        }

        private static string NormalizePath(string path) => path.Trim().ToLowerInvariant();

        // Keyboard player 2-4 without a controller: the backup shared-keyboard layout (Player2..Player4 maps), keyboard only.
        public void ConfigureKeyboard(InputActionAsset keyboardTemplate, int layout)
        {
            Release();
            KeyboardLayout = layout;
            owned = Instantiate(keyboardTemplate);
            owned.Disable();
            var map = owned.FindActionMap("Player" + (layout + 1), true);
            // Limit by device rather than binding group so bindings added without a group still work.
            if (Keyboard.current != null)
                map.devices = new InputDevice[] { Keyboard.current };
            else
                map.bindingMask = InputBinding.MaskByGroup("Keyboard");
            move = map.FindAction(MoveAction, true);
            passAction = map.FindAction(PassAction, false) ?? map.FindAction(LegacyActionKey, false);
            attackAction = map.FindAction(AttackAction, false) ?? map.FindAction(LegacyActionKey, false);
            map.Enable();
        }

        // Disable only the actions owned by this participant.
        private void Release()
        {
            if (owned != null)
            {
                owned.Disable();
                Destroy(owned);
            }

            owned = null;
            move = passAction = attackAction = null;
            Gamepad = null;
            KeyboardLayout = -1;
        }

        private void OnDestroy() => Release();
    }
}
