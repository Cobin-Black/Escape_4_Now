using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.Input
{
    [CreateAssetMenu(menuName = "Escape 4 Now/Gameplay Input Bindings")]
    public sealed class GameplayInputBindings : ScriptableObject
    {
        [Header("Player Actions")]
        [SerializeField] private InputActionReference move;
        [SerializeField] private InputActionReference rollDice;
        [SerializeField] private InputActionReference useItem;
        [SerializeField] private InputActionReference interact;
        [SerializeField] private InputActionReference itemOptionSelect;
        [Header("Race Key Battle")]
        [SerializeField] private InputActionReference keyBattle;
        [Header("Map Pointer")]
        [SerializeField] private InputActionReference point;
        [SerializeField] private InputActionReference click;

        public InputAction Move => move != null ? move.action : null;
        public InputAction RollDice => rollDice != null ? rollDice.action : null;
        public InputAction UseItem => useItem != null ? useItem.action : null;
        public InputAction Interact => interact != null ? interact.action : null;
        public InputAction ItemOptionSelect => itemOptionSelect != null ? itemOptionSelect.action : null;
        public InputAction KeyBattle => keyBattle != null ? keyBattle.action : null;
        public InputAction Point => point != null ? point.action : null;
        public InputAction Click => click != null ? click.action : null;
        public bool IsConfigured => Move != null && RollDice != null && UseItem != null
            && Interact != null && ItemOptionSelect != null && KeyBattle != null && Point != null && Click != null;
        public IEnumerable<InputAction> Actions
        {
            get
            {
                yield return Move; yield return RollDice; yield return UseItem; yield return Interact;
                yield return ItemOptionSelect; yield return KeyBattle; yield return Point; yield return Click;
            }
        }
    }
}
