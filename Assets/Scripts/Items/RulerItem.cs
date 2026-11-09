
using Escape4Now.Arena;
using UnityEngine;

namespace Escape4Now.Items
{
    public sealed class RulerItem : FreeForAllItem
    {
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField, Range(1f, 360f)] private float swingAngle = 90f;
        [SerializeField] private float knockback = 1.5f;
        [SerializeField] private int damage = 1;

        [Header("Swing Visual")]
        [SerializeField] private GameObject swingEffectPrefab;

        // Attack nearby players in the direction the user is facing.
        public override void Use(PlayerInventory inventory)
        {
            if (inventory == null || !inventory.HasItem(this) ||
                UsesRemaining <= 0)
                return;

            ArenaParticipant attacker = inventory.GetComponent<ArenaParticipant>();

            if (attacker == null || !attacker.Alive ||
                attacker.Match == null || !attacker.Match.Playing ||
                attacker.Match.Paused)
                return;

            Vector2 direction = attacker.Match.Map
                .GridDirection(attacker.FacingDirection).normalized;

            foreach (ArenaParticipant target in attacker.Match.Players)
            {
                if (target == attacker || !target.Alive)
                    continue;

                Vector2 difference = target.Position - attacker.Position;

                if (difference.magnitude > attackRange)
                    continue;

                if (Vector2.Dot(direction, difference.normalized) < 0.5f)
                    continue;

                // Hit players within the swing cone centered on the facing direction.
                if (Vector2.Angle(direction, difference) > swingAngle / 2f)
                    continue;

                // Check the Eraser shield before damage consumes a block.
                bool blocked = target.HasEraserShield;

                target.Damage(damage);

                if (target.Alive && !blocked)
                    target.Knockback(direction * knockback);
            }

            // Show the Ruler swing effect.
            if (swingEffectPrefab != null)
            {
                GameObject effect = Instantiate(
                    swingEffectPrefab,
                    attacker.transform.position,
                    Quaternion.identity
                );

                Vector2 visualDirection = attacker.FacingDirection.normalized;

                effect.GetComponent<RulerSwingEffect>()?.Initialize(
                    visualDirection,
                    attackRange,
                    swingAngle
                );
            }

            ConsumeUse(inventory);
        }
    }
}
