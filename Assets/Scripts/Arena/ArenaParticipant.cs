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
        [SerializeField, Range(0, 1), Tooltip("Sprite opacity during the off part of each blink.")]
        private float invincibilityFlashAlpha = .2f;
        private float invincibilityTimer;
        private SpriteRenderer[] flashRenderers;
        private float[] flashBaseAlphas;
        private bool flashing, facingBack, facingRight;
        public PlayerCharacter Character { get; private set; }
        public ArenaLocalInput Input { get; private set; }
        public ArenaMatch Match { get; private set; }
        public Vector2 Position { get; private set; }
        public bool Alive { get; private set; }
        public bool IsBot { get; private set; }
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
        public float InvincibilityRemaining => invincibilityTimer;
        // Attack along the same floor axis as the character's visible diagonal facing.
        public Vector2 AttackDirection => facingBack
            ? (facingRight ? Vector2.right : Vector2.up)
            : (facingRight ? Vector2.down : Vector2.left);

        // The host owns protection; guests only copy its timer and show the same blinking.
        public void ApplyOnlineInvincibility(float seconds)
        {
            invincibilityTimer = Mathf.Max(0, seconds);
            if (invincibilityTimer <= 0) StopInvincibilityFlash();
        }

        private void Face(Vector2 velocity)
        {
            if (velocity.sqrMagnitude <= .0001f) return;
            FacingDirection = velocity.normalized;
            if (Mathf.Abs(velocity.y) > .0001f) facingBack = velocity.y > 0;
            if (Mathf.Abs(velocity.x) > .0001f) facingRight = velocity.x > 0;
            Character.Appearance?.FaceToward(velocity);
        }

        private Vector2 screenVelocity, eventVelocity;
        private Vector2 remoteMove;
        private bool remoteTag, remoteUseItem;
        private float lastInput;
        public int MovementRevision { get; private set; }

        // The guest keeps the same movement state as the host when correcting a prediction.
        public struct MovementState
        {
            public Vector2 position, motorVelocity, eventVelocity;
        }

        public MovementState CaptureMovement() => new MovementState
        {
            position = Position, motorVelocity = screenVelocity, eventVelocity = eventVelocity
        };

        public void RestoreMovement(MovementState state)
        {
            SetPosition(state.position);
            screenVelocity = state.motorVelocity;
            eventVelocity = state.eventVelocity;
        }

        public void SetPresentationOffset(Vector3 offset) => transform.position = Match.Map.World(Position) + Vector3.back + offset;
        // Local Potato Pass and a pass request from the network use the same check.
        public bool TagPressed
        {
            get
            {
                if (!Match.Online || IsBot) return Input.PassPressed;
                bool pressed = remoteTag && Time.unscaledTime - lastInput < .5f;
                remoteTag = false;
                return pressed;
            }
        }

        // King of the Hill uses Attack for the held item, with the same action online.
        public bool UseItemPressed
        {
            get
            {
                if (!Match.Online || IsBot) return Input.AttackPressed;
                bool pressed = remoteUseItem && Time.unscaledTime - lastInput < .5f;
                remoteUseItem = false;
                return pressed;
            }
        }

        public void SetOnlineInput(Vector2 move, bool tag, bool useItem = false)
        {
            remoteMove = Vector2.ClampMagnitude(move, 1);
            remoteTag |= tag;
            remoteUseItem |= useItem;
            lastInput = Time.unscaledTime;
        }

        public void ApplyOnlineState(Vector2 position, Vector2 velocity, int health, bool alive)
        {
            Place(position);
            ScreenVelocity = velocity;
            Face(velocity);
            Character.ApplyReplicatedHealth(health);
            Alive = alive;
            gameObject.SetActive(alive);
        }
        public event Action<ArenaParticipant> Eliminated;
        public int OnlineItemType { get; set; } = -1;
        public int OnlineItemUses { get; set; }
        public int OnlineItemMaxUses { get; set; }
        public int OnlineSwingCount { get; private set; }
        public Vector3 OnlineSwingPosition { get; private set; }
        public Vector2 OnlineSwingDirection { get; private set; }
        public void RecordOnlineSwing()
        {
            OnlineSwingCount++;
            OnlineSwingPosition = transform.position;
            OnlineSwingDirection = Match.Map.ScreenDirection(AttackDirection).normalized;
        }
        // Reuse the existing character identity, skin and health instead of creating a second player system.
        // A bot is driven by ArenaBotController through this player's input, locally and on the online host.
        public void Initialize(ArenaMatch match, int slot, string displayName, Color color, Vector2 position, bool isBot = false)
        {
            Match = match;
            Slot = slot;
            Alive = true;
            IsBot = isBot;
            invincibilityTimer = 0;
            StopInvincibilityFlash();
            Character = GetComponent<PlayerCharacter>();
            Input = GetComponent<ArenaLocalInput>();
            if (isBot) Input.ConfigureBot();
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
            // Bots never take a keyboard layout or controller.
            if (IsBot)
                Input.ConfigureBot();
            else if (pad != null)
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
                StopInvincibilityFlash();
                return;
            }
            if (Match.Paused)
                return;

            // Count down invincibility after taking damage in FFA.
            if (invincibilityTimer > 0)
                invincibilityTimer = Mathf.Max(0, invincibilityTimer - Time.deltaTime);
            if (invincibilityTimer > 0) UpdateInvincibilityFlash();
            else StopInvincibilityFlash();

            // Guests animate the host's protection timer without running damage or movement rules.
            if (Match.Replica) return;

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
            // Online people move from the input their computer sent; bots keep reading their own input on the host.
            if (Match.Online && !IsBot) direction = Time.unscaledTime - lastInput < .5f ? remoteMove : Vector2.zero;
            SimulateMovement(direction, Time.deltaTime);
        }

        // Local players, the host, and guest predictions share acceleration, event effects and collision.
        public void SimulateMovement(Vector2 direction, float seconds)
        {
            if (!Alive || Match == null || !Match.Playing || Match.Paused || seconds <= 0) return;
            direction = Vector2.ClampMagnitude(direction, 1);
            if (direction.magnitude < inputDeadzone)
                direction = Vector2.zero;
            Vector2 target = direction * speed * SpeedMultiplier;
            // A short ramp smooths direction changes. No ordinary inertia survives input release.
            screenVelocity = direction == Vector2.zero || acceleration <= 0 ? target : Vector2.MoveTowards(screenVelocity, target, acceleration * seconds);
            Vector2 desired = Match.Map.GridDirection(screenVelocity);
            eventVelocity = Match.Events != null ? Match.Events.FilterMovement(this, desired, eventVelocity, seconds) : desired;
            Move(eventVelocity * seconds, seconds);
            Face(ScreenVelocity);
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

        private void OnDisable() => StopInvincibilityFlash();

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
        public void Move(Vector2 delta) => Move(delta, Time.deltaTime);

        public void Move(Vector2 delta, float seconds)
        {
            // Visual smoothing on a guest must not change the distance moved by the motor.
            Vector3 before = Match.Map.World(Position) + Vector3.back;
            float safeStep = Mathf.Min(collisionStep, Match.Map.Settings.playerRadius);
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / Mathf.Max(.01f, safeStep)));
            delta /= steps;
            for (int i = 0; i < steps; i++)
                Position = ArenaMovementCollision.Step(this, Position, delta, slideContacts, contactSearchSteps, contactSkin);
            SetPosition(Position);
            ScreenVelocity = (Vector2)(transform.position - before) / Mathf.Max(.0001f, seconds);
        }

        public void Place(Vector2 point)
        {
            // A spawn, swap or teleport starts a new movement history on guests.
            MovementRevision++;
            SetPosition(point);
        }

        private void SetPosition(Vector2 point)
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
            {
                MovementRevision++;
                Move(impulse);
            }
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
