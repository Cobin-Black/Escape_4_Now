
using Escape4Now.Arena;
using UnityEngine;

namespace Escape4Now.Items
{
    // Moves a spitball through the arena and detects collisions.
    public sealed class SpitballProjectile : MonoBehaviour
    {
        [SerializeField] private float speed = 10f;
        [SerializeField] private float maxRange = 8f;
        [SerializeField] private float hitRadius = 0.25f;
        [SerializeField] private float knockback = 1.5f;
        [SerializeField] private float slowMultiplier = 0.6f;
        [SerializeField] private float slowDuration = 2f;

        private ArenaParticipant owner;
        private ArenaMatch match;
        private Vector2 position;
        private Vector2 direction;
        private float distanceTraveled;

        // Set up the projectile when it is fired.
        public void Initialize(ArenaParticipant shooter)
        {
            owner = shooter;
            match = shooter.Match;
            position = shooter.Position;
            direction = match.Map.GridDirection(shooter.FacingDirection).normalized;

            transform.position = match.Map.World(position) + Vector3.back;
        }

        private void Update()
        {
            if (match == null || !match.Playing)
            {
                Destroy(gameObject);
                return;
            }

            if (match.Paused)
                return;

            float distance = speed * Time.deltaTime;
            float stepSize = 0.1f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / stepSize));
            Vector2 step = direction * (distance / steps);

            for (int i = 0; i < steps; i++)
            {
                Vector2 next = position + step;

                // Stop when the projectile reaches a wall or obstacle.
                if (!match.Map.Layout.Fits(next, hitRadius))
                {
                    Destroy(gameObject);
                    return;
                }

                // Check whether the projectile hits another player.
                foreach (ArenaParticipant target in match.Players)
                {
                    if (target == owner || !target.Alive)
                        continue;

                    float radius = match.Map.Settings.playerRadius + hitRadius;

                    if (Vector2.Distance(next, target.Position) <= radius)
                    {
                        // Check the Eraser shield before damage consumes a block.
                        bool blocked = target.HasEraserShield;

                        target.Damage(1);

                        if (target.Alive && !blocked)
                        {
                            target.Knockback(direction * knockback);
                            target.SpeedMultiplier = slowMultiplier;
                            target.EffectRemaining = slowDuration;
                        }

                        Destroy(gameObject);
                        return;
                    }
                }

                position = next;
                distanceTraveled += step.magnitude;

                if (distanceTraveled >= maxRange)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            transform.position = match.Map.World(position) + Vector3.back;
        }
    }
}
