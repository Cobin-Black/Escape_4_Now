using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.Arena
{
    // Each participant owns an action copy. Keyboard layouts and gamepads never feed another slot.
    [DisallowMultipleComponent]
    public sealed class ArenaLocalInput : MonoBehaviour
    {
        private InputActionAsset owned;
        private InputAction move, tagAction, useItemAction;
        private bool isBot;
        private Vector2 botMovement;
        private bool botUseItem;
        private bool botTag;
        public bool IsBot => isBot;
        public Vector2 Move => isBot ? botMovement : move != null ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1) : Vector2.zero;
        public bool TagPressed => isBot ? botTag : tagAction != null && tagAction.WasPressedThisFrame();
        public bool UseItemPressed => isBot ? botUseItem : useItemAction != null && useItemAction.WasPressedThisFrame();

        // Give this slot its own action copy and keyboard layout or explicitly assigned gamepad.
        public void Configure(InputActionAsset template, int slot, int gamepad)
        {
            Release();
            isBot = false;
            botMovement = Vector2.zero;
            owned = Instantiate(template);
            var map = owned.FindActionMap("Player" + (slot + 1), true);
            map.bindingMask = InputBinding.MaskByGroup(gamepad < 0 ? "Keyboard" : "Gamepad");
            if (gamepad >= 0)
                map.devices = new InputDevice[]
                {
                    Gamepad.all[gamepad]
                };
            move = map.FindAction("Move", true);
            tagAction = map.FindAction("Tag", true);
            useItemAction = map.FindAction("UseItem", false);
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

        // Disable only the actions owned by this participant.
        private void Release()
        {
            if (owned != null)
            {
                owned.Disable();
                Destroy(owned);
            }

            owned = null;
            move = tagAction = useItemAction = null;
        }

        private void OnDestroy() => Release();
    }
}
