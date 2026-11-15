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
        public Vector2 Move => move != null ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1) : Vector2.zero;
        public bool TagPressed => tagAction != null && tagAction.WasPressedThisFrame();
        public bool UseItemPressed => useItemAction != null && useItemAction.WasPressedThisFrame();

        // Give this slot its own action copy and keyboard layout or explicitly assigned gamepad.
        public void Configure(InputActionAsset template, int slot, int gamepad)
        {
            Release();
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
