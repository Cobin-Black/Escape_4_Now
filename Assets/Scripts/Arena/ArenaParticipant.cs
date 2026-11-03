using System;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Escape4Now.Arena
{
    // One shared player adapter, not a second identity or health system.
    [RequireComponent(typeof(PlayerCharacter), typeof(ArenaLocalInput))]
    public sealed class ArenaParticipant : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(.1f)]
        private float speed = 4;
        [SerializeField, Min(.01f)]
        private float collisionStep = .08f;
        [SerializeField, Min(0), Tooltip("Visible units per second squared. Zero gives instant response; releasing input always stops ordinary movement.")]
        private float acceleration = 48;
        [SerializeField, Range(0, .4f)]
        private float inputDeadzone = .12f;
        [Header("Wall and Corner Sliding")]
        [SerializeField, Range(1, 5)]
        private int slideContacts = 3;
        [SerializeField, Range(3, 10)]
        private int contactSearchSteps = 7;
        [SerializeField, Range(.0001f, .01f)]
        private float contactSkin = .001f;
        [Header("Player Feedback")]
        [SerializeField]
        private UnityEvent onEliminated;
        [SerializeField]
        private UnityEvent onDamaged;
        [Header("Identity Labels (off during arena gameplay)")]
        [SerializeField]
        private bool showNameLabels;
        [SerializeField]
        private TextMesh nameLabel;
        [SerializeField]
        private TextMesh nameShadow;
        public PlayerCharacter Character { get; private set; }
        public ArenaLocalInput Input { get; private set; }
        public ArenaMatch Match { get; private set; }
        public Vector2 Position { get; private set; }
        public bool Alive { get; private set; }
        public int Slot { get; private set; }
        public int InitialHealth { get; private set; }
        public float SpeedMultiplier { get; set; } = 1;
        public float EffectRemaining { get; set; }
        public bool Shield { get; set; }
        public Vector2 ScreenVelocity { get; private set; }

        private Vector2 screenVelocity, eventVelocity;
        public event Action<ArenaParticipant> Eliminated;
        // Reuse the existing character identity, skin and health instead of creating a second player system.
        public void Initialize(ArenaMatch match, int slot, string displayName, Color color, Vector2 position, int gamepad)
        {
            Match = match;
            Slot = slot;
            Alive = true;
            Character = GetComponent<PlayerCharacter>();
            Input = GetComponent<ArenaLocalInput>();
            InitialHealth = Character.Health;
            Character.ConfigureArenaIdentity(slot + 1, displayName, color);
            Input.Configure(match.InputActions, slot, gamepad);
            Place(position);
            if (nameLabel != null)
            {
                nameLabel.gameObject.SetActive(showNameLabels);
                nameLabel.text = name;
                nameLabel.color = color;
            }

            if (nameShadow != null)
            {
                nameShadow.gameObject.SetActive(showNameLabels);
                nameShadow.text = name;
            }
        }

        private void Update()
        {
            if (!Alive || Match == null || !Match.Playing || Match.Paused)
                return;
            if (EffectRemaining > 0)
            {
                EffectRemaining -= Time.deltaTime;
                if (EffectRemaining <= 0)
                {
                    SpeedMultiplier = 1;
                    Shield = false;
                }
            }

            Vector2 direction = Input.Move;
            if (direction.magnitude < inputDeadzone)
                direction = Vector2.zero;
            Vector2 target = direction * speed * SpeedMultiplier;
            // A short ramp smooths direction changes. No ordinary inertia survives input release.
            screenVelocity = direction == Vector2.zero || acceleration <= 0 ? target : Vector2.MoveTowards(screenVelocity, target, acceleration * Time.deltaTime);
            Vector2 desired = Match.Map.GridDirection(screenVelocity);
            eventVelocity = Match.Events != null ? Match.Events.FilterMovement(this, desired, eventVelocity, Time.deltaTime) : desired;
            Move(eventVelocity * Time.deltaTime);
            if (ScreenVelocity.sqrMagnitude > .0001f)
                Character.Appearance?.FaceToward(ScreenVelocity);
        }

        // Small swept steps prevent tunneling; contact normals make wall sliding stable and predictable.
        public void Move(Vector2 delta)
        {
            Vector3 before = transform.position;
            float safeStep = Mathf.Min(collisionStep, Match.Map.Settings.playerRadius);
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / Mathf.Max(.01f, safeStep)));
            delta /= steps;
            for (int i = 0; i < steps; i++)
                Position = ArenaMovementCollision.Step(this, Position, delta, slideContacts, contactSearchSteps, contactSkin);
            Place(Position);
            ScreenVelocity = (Vector2)(transform.position - before) / Mathf.Max(.0001f, Time.deltaTime);
        }

        public void Place(Vector2 point)
        {
            ScreenVelocity = Vector2.zero;
            Position = point;
            transform.position = Match.Map.World(point) + Vector3.back;
            GetComponent<SpriteRenderer>().sortingOrder = Match.Map.Order(point) + (Match.Map.Settings.roomVisuals != null ? Match.Map.Settings.roomVisuals.playerSortingOffset : 5);
        }

        // Forward damage to the existing health system, then notify the match if this player is eliminated.
        public void Damage(int amount)
        {
            if (!Alive || amount <= 0)
                return;
            if (Shield)
            {
                Shield = false;
                return;
            }

            for (int i = 0; i < amount && Character.Health > 0; i++)
                Character.TakeDamage();
            onDamaged?.Invoke();
            if (Character.Health <= 0)
                Eliminate();
        }

        public void Knockback(Vector2 impulse)
        {
            if (Alive)
                Move(impulse);
        }

        [ContextMenu("Testing / Eliminate Player")]
        // Remove this player from gameplay and let the mode decide whether the match is over.
        public void Eliminate()
        {
            if (!Alive)
                return;
            Alive = false;
            onEliminated?.Invoke();
            Eliminated?.Invoke(this);
            gameObject.SetActive(false);
        }

        public void ClearRoundEffects()
        {
            SpeedMultiplier = 1;
            EffectRemaining = 0;
            Shield = false;
            eventVelocity = screenVelocity = Vector2.zero;
            ScreenVelocity = Vector2.zero;
        }
    }
}
