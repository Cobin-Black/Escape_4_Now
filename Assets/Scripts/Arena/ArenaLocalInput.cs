
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.Arena
{
    // Each participant has their own input actions.
    // Bots use their own movement and action inputs.
    [DisallowMultipleComponent]
    public sealed class ArenaLocalInput : MonoBehaviour
    {
        private const string GamepadMapName = "Player";
        private const string MoveAction = "Move";
        private const string PassAction = "Potato Pass";
        private const string AttackAction = "Attack";
        private const string LegacyActionKey = "Tag";

        private InputActionAsset owned;
        private InputAction move, passAction, attackAction;

        private bool isBot;
        private Vector2 botMovement;
        private bool botUseItem;
        private bool botTag;

        public bool IsBot => isBot;

        public Vector2 Move => isBot
            ? botMovement
            : move != null
                ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1)
                : Vector2.zero;

        public bool PassPressed => isBot
            ? botTag
            : passAction != null && passAction.WasPressedThisFrame();

        public bool AttackPressed => isBot
            ? botUseItem
            : attackAction != null && attackAction.WasPressedThisFrame();

        // Keep compatibility with existing game mode code.
        public bool TagPressed => PassPressed;
        public bool UseItemPressed => AttackPressed;

        public Gamepad Gamepad { get; private set; }
        public bool UsesKeyboard => !isBot && Gamepad == null;
        public int KeyboardLayout { get; private set; } = -1;

        // Configure a player using a gamepad.
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

        // Configure Player 1 using the primary keyboard controls.
        public void ConfigurePrimaryKeyboard(
            InputActionAsset primaryTemplate,
            ICollection<string> reservedKeys)
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
            {
                foreach (InputAction action in new[] { move, passAction, attackAction })
                    DisableReservedKeys(action, reservedKeys);
            }

            map.Enable();
        }

        // Prevent players from sharing reserved keyboard keys.
        private static void DisableReservedKeys(
            InputAction action,
            ICollection<string> reservedKeys)
        {
            if (action == null)
                return;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];

                if (binding.isComposite || string.IsNullOrEmpty(binding.path))
                    continue;

                if (reservedKeys.Contains(NormalizePath(binding.path)))
                    action.ApplyBindingOverride(i, "");
            }
        }

        // Collect keyboard keys used by another player's layout.
        public static void CollectLayoutKeys(
            InputActionAsset keyboardTemplate,
            int layout,
            HashSet<string> keys)
        {
            InputActionMap map = keyboardTemplate != null
                ? keyboardTemplate.FindActionMap("Player" + (layout + 1))
                : null;

            if (map == null)
                return;

            foreach (InputBinding binding in map.bindings)
            {
                if (!binding.isComposite &&
                    !string.IsNullOrEmpty(binding.path) &&
                    binding.path.StartsWith("<Keyboard>"))
                {
                    keys.Add(NormalizePath(binding.path));
                }
            }
        }

        private static string NormalizePath(string path)
        {
            return path.Trim().ToLowerInvariant();
        }

        // Configure Players 2-4 using their keyboard layouts.
        public void ConfigureKeyboard(InputActionAsset keyboardTemplate, int layout)
        {
            Release();

            KeyboardLayout = layout;
            owned = Instantiate(keyboardTemplate);
            owned.Disable();

            var map = owned.FindActionMap("Player" + (layout + 1), true);

            if (Keyboard.current != null)
                map.devices = new InputDevice[] { Keyboard.current };
            else
                map.bindingMask = InputBinding.MaskByGroup("Keyboard");

            move = map.FindAction(MoveAction, true);
            passAction = map.FindAction(PassAction, false)
                ?? map.FindAction(LegacyActionKey, false);
            attackAction = map.FindAction(AttackAction, false)
                ?? map.FindAction(LegacyActionKey, false);

            map.Enable();
        }

        // Enable bot control instead of keyboard or gamepad input.
        public void ConfigureBot()
        {
            Release();

            isBot = true;
            botMovement = Vector2.zero;
            botUseItem = false;
            botTag = false;
        }

        // Set the movement direction chosen by the bot.
        public void SetBotMovement(Vector2 direction)
        {
            if (!isBot)
                return;

            botMovement = Vector2.ClampMagnitude(direction, 1f);
        }

        // Allow bots to activate their items.
        public void SetBotUseItem(bool useItem)
        {
            if (!isBot)
                return;

            botUseItem = useItem;
        }

        // Allow bots to press the Tag button in Hot Potato.
        public void SetBotTag(bool tag)
        {
            if (!isBot)
                return;

            botTag = tag;
        }

        // Disable the input actions owned by this participant.
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

            isBot = false;
            botMovement = Vector2.zero;
            botUseItem = false;
            botTag = false;
        }

        private void OnDestroy()
        {
            Release();
        }
    }
}
