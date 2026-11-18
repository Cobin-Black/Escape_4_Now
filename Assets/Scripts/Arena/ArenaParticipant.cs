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
        [Header("King of the Hill Invincibility")]
        [SerializeField] private float invincibilityDuration = 1f;
        [SerializeField, Min(.02f), Tooltip("Seconds between each on/off blink while invincible.")]
        private float invincibilityFlashInterval = .1f;
        [SerializeField, Range(0, 1), Tooltip("Sprite opacity during the 'off' part of each blink.")]
        private float invincibilityFlashAlpha = .2f;
        private float invincibilityTimer;
        private SpriteRenderer[] flashRenderers;
        private float[] flashBaseAlphas;
        private bool flashing;
        // Tracks the same four diagonal facings the player sprite uses (see PlayerAppearance.FaceToward).
        private bool facingBack, facingRight;
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
        public int EraserBlocksRemaining { get; private set; }
        public bool HasEraserShield => EraserBlocksRemaining > 0;
        public Vector2 FacingDirection { get; private set; } = Vector2.down;
        public Vector2 ScreenVelocity { get; private set; }
        public bool IsInvincible => invincibilityTimer > 0;

        // Straight ahead from the way the sprite is facing, in floor (grid) space.
        // The sprite only faces the four screen diagonals, and each of those runs along one floor axis.
        public Vector2 AttackDirection => facingBack
            ? (facingRight ? Vector2.right : Vector2.up)      // Up-right / up-left
            : (facingRight ? Vector2.down : Vector2.left);    // Down-right / down-left

        private Vector2 screenVelocity, eventVelocity;
        public event Action<ArenaParticipant> Eliminated;
        // Reuse the existing character identity, skin and health instead of creating a second player system.
        // Initialize the player's identity and starting position.
        public void Initialize(ArenaMatch match, int slot, string displayName, Color color, Vector2 position)
        {
            Match = match;
            Slot = slot;
            Alive = true;
            invincibilityTimer = 0;
            StopInvincibilityFlash();
            // Matches the skin's starting pose: front facing, down and to the left.
            facingBack = facingRight = false;
            Character = GetComponent<PlayerCharacter>();
            Input = GetComponent<ArenaLocalInput>();
            InitialHealth = Character.Health;
            Character.ConfigureArenaIdentity(slot + 1, displayName, color);
            Place(position);
            bool showScore = Match is KingOfTheHillController;
            bool showLabel = showScore || showNameLabels;

            if (nameLabel != null)
            {
                nameLabel.gameObject.SetActive(showLabel);
                nameLabel.text = showScore ? "0" : name;
                nameLabel.color = color;
            }

            if (nameShadow != null)
            {
                nameShadow.gameObject.SetActive(showLabel);
                nameShadow.text = showScore ? "0" : name;
            }
        }

        // Use this player's own controller, or their shared-keyboard layout when they have none.
        // Player 1 on keyboard uses the Input System's keyboard bindings; Players 2-4 use the backup layouts.
        public void ConfigureInput(UnityEngine.InputSystem.Gamepad pad, int keyboardLayout, System.Collections.Generic.ICollection<string> reservedKeys = null)
        {
            if (Input == null)
                Input = GetComponent<ArenaLocalInput>();
            if (pad != null)
                Input.ConfigureGamepad(Match.GamepadActions, pad);
            else if (keyboardLayout <= 0)
                Input.ConfigurePrimaryKeyboard(Match.GamepadActions, reservedKeys);
            else
                Input.ConfigureKeyboard(Match.InputActions, keyboardLayout);
            // Keyboard controls are shown on the setup screen (ArenaLobbyView), not during the match.
        }

        private void Update()
        {
            if (!Alive || Match == null || !Match.Playing)
            {
                // Never leave a player see-through once the match is over.
                StopInvincibilityFlash();
                return;
            }

            if (Match.Paused)
                return;

            // Count down invincibility after taking damage in King of the Hill, blinking the sprite the whole time.
            if (invincibilityTimer > 0)
            {
                invincibilityTimer = Mathf.Max(0, invincibilityTimer - Time.deltaTime);
                if (invincibilityTimer > 0)
                    UpdateInvincibilityFlash();
                else
                    StopInvincibilityFlash();
            }

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
            {
                FacingDirection = ScreenVelocity.normalized;
                const float threshold = 0.0001f;
                if (Mathf.Abs(ScreenVelocity.y) > threshold) facingBack = ScreenVelocity.y > 0f;
                if (Mathf.Abs(ScreenVelocity.x) > threshold) facingRight = ScreenVelocity.x > 0f;
                Character.Appearance?.FaceToward(ScreenVelocity);
            }
        }

        // Blink every sprite on the player (body, outline, etc.) on and off while invincible.
        private void UpdateInvincibilityFlash()
        {
            if (!flashing)
            {
                flashRenderers = GetComponentsInChildren<SpriteRenderer>(true);
                flashBaseAlphas = new float[flashRenderers.Length];
                for (int i = 0; i < flashRenderers.Length; i++)
                    flashBaseAlphas[i] = flashRenderers[i].color.a;
                flashing = true;
            }

            float elapsed = invincibilityDuration - invincibilityTimer;
            bool dim = Mathf.FloorToInt(elapsed / invincibilityFlashInterval) % 2 == 0;
            SetFlashAlpha(dim ? invincibilityFlashAlpha : 1f);
        }

        private void StopInvincibilityFlash()
        {
            if (!flashing)
                return;
            SetFlashAlpha(1f);
            flashing = false;
        }

        private void SetFlashAlpha(float multiplier)
        {
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                if (flashRenderers[i] == null)
                    continue;
                Color color = flashRenderers[i].color;
                color.a = flashBaseAlphas[i] * multiplier;
                flashRenderers[i].color = color;
            }
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

        // Activate the Eraser shield with two blocks.
        public void ActivateEraserShield(int blocks)
        {
            EraserBlocksRemaining = blocks;
        }

        // Forward damage to the existing health system, then notify the match if this player is eliminated.
        public void Damage(int amount)
        {
            if (!Alive || amount <= 0)
                return;

            // Ignore damage while invincible in King of the Hill.
            if (Match is KingOfTheHillController && IsInvincible)
                return;

            // Block incoming damage while the Eraser shield is active.
            if (EraserBlocksRemaining > 0)
            {
                EraserBlocksRemaining--;
                return;
            }

            for (int i = 0; i < amount && Character.Health > 0; i++)
                Character.TakeDamage();

            onDamaged?.Invoke();

            // Start invincibility after taking damage in King of the Hill.
            if (Match is KingOfTheHillController && Character.Health > 0)
            {
                invincibilityTimer = invincibilityDuration;
                UpdateInvincibilityFlash();
            }

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
            invincibilityTimer = 0;
            StopInvincibilityFlash();
            onEliminated?.Invoke();
            Eliminated?.Invoke(this);
            gameObject.SetActive(false);
        }

        // Bring an eliminated player back into King of the Hill.
        public void Respawn(Vector2 position)
        {
            if (Alive)
                return;

            Character.RestoreArenaHealth(InitialHealth);

            ClearRoundEffects();

            Alive = true;
            gameObject.SetActive(true);
            Place(position);
        }

        // Update the score displayed above this player's head.
        public void UpdateScoreLabel(int score)
        {
            string scoreText = score.ToString();

            if (nameLabel != null)
                nameLabel.text = scoreText;

            if (nameShadow != null)
                nameShadow.text = scoreText;
        }

        public void ClearRoundEffects()
        {
            SpeedMultiplier = 1;
            EffectRemaining = 0;
            Shield = false;
            EraserBlocksRemaining = 0;
            eventVelocity = screenVelocity = Vector2.zero;
            ScreenVelocity = Vector2.zero;
            invincibilityTimer = 0;
            StopInvincibilityFlash();
        }
    }
}
