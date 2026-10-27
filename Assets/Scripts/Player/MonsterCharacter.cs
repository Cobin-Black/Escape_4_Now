using System.Collections;
using System.Collections.Generic;
using Escape4Now.Map;
using Escape4Now.TurnSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.Player
{
    public sealed class MonsterCharacter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SurvivalMap map;
        [SerializeField] private TurnSystemController turnSystem;
        [SerializeField] private PlayerCharacter[] players;

        [Header("Movement")]
        [SerializeField] private Vector2Int gridPosition;
        [SerializeField, Min(1)] private int maximumMoves = 6;
        [SerializeField, Min(0.01f)] private float moveDuration = 0.15f;

        [Header("Control")]
        [SerializeField] private bool aiControlled;

        [Header("Charge")]
        [SerializeField, Min(2)] private int chargeDistance = 6;
        [SerializeField, Min(0)] private int chargeCooldown = 2;

        [Header("Roar")]
        [SerializeField, Min(1)] private int roarRange = 3;
        [SerializeField, Min(0)] private int roarCooldown = 3;

        [Header("Stomp")]
        [SerializeField, Min(1)] private int stompRange = 1;
        [SerializeField, Min(0)] private int stompCooldown = 2;

        private int movesUsed;
        private int chargeReadyTurn;
        private int roarReadyTurn;
        private int stompReadyTurn;
        private int monsterTurn;
        private bool isMoving;
        private bool turnStarted;
        private Coroutine movementRoutine;
        private int rolledMoves;
        private bool hasRolled;
        private bool isRolling;

        // Returns whether the Monster's dice are currently animating.
        public bool IsRolling => isRolling;

        // Returns the Monster's actual dice result.
        public int LastRoll => rolledMoves;

        // Tracks whether the player is choosing a Charge direction.
        private bool isChoosingChargeDirection;

        // Tracks whether the Monster must skip its next turn.
        private bool mustSkipNextTurn;

        // Tracks whether the current movement is a Charge.
        private bool isCharging;

        // Tracks whether Charge hit a wall or obstacle.
        private bool chargeCrashed;

        public Vector2Int GridPosition => gridPosition;
        public bool IsMoving => isMoving;

        // Returns whether the Monster has rolled for its current turn.
        public bool HasRolled => hasRolled;

        // Returns the Monster's remaining movement allowance.
        public int MovesRemaining => Mathf.Max(0, rolledMoves - movesUsed);

        // Returns the number of turns until each ability is ready.
        public int ChargeCooldownRemaining => Mathf.Max(0, chargeReadyTurn - monsterTurn);
        public int RoarCooldownRemaining => Mathf.Max(0, roarReadyTurn - monsterTurn);
        public int StompCooldownRemaining => Mathf.Max(0, stompReadyTurn - monsterTurn);



        private void Start()
        {
            if (map == null)
                map = FindFirstObjectByType<SurvivalMap>();

            if (turnSystem == null)
                turnSystem = FindFirstObjectByType<TurnSystemController>();

            if (players == null || players.Length == 0)
                players = FindObjectsByType<PlayerCharacter>(
                    FindObjectsSortMode.None);

            if (map == null || turnSystem == null)
            {
                Debug.LogError("MonsterCharacter needs a SurvivalMap and TurnSystemController.");
                enabled = false;
                return;
            }

            transform.position = GetFootprintWorldPosition(gridPosition);
        }

        // Initializes the Monster for a new Survival Mode match.
        public void Initialize(SurvivalMap survivalMap, TurnSystemController turns,
            PlayerCharacter[] participants, Vector2Int spawnPosition)
        {
            map = survivalMap;
            turnSystem = turns;
            players = participants;
            gridPosition = spawnPosition;
            movesUsed = 0;
            turnStarted = false;
            isMoving = false;

            transform.position = GetFootprintWorldPosition(gridPosition);
            Debug.Log("Monster initialized. Grid: " + gridPosition
    + ", World: " + transform.position);
        }

        private void Update()
        {
            if (map == null || turnSystem == null || isMoving)
                return;

            if (Time.timeScale <= 0f)
                return;

            if (Escape4Now.Multiplayer.LocalMultiplayerSession.GameplayPaused)
                return;

            if (turnSystem.CurrentTurnOwner != TurnOwner.Enemy)
            {
                turnStarted = false;
                return;
            }

            if (!turnStarted)
                BeginMonsterTurn();

            if (!turnStarted || turnSystem.CurrentTurnOwner != TurnOwner.Enemy)
                return;

            if (aiControlled)
                TakeAITurn();
            else
                ReadMonsterInput();
        }


        // Checks whether all four tiles of the Monster's footprint are walkable.
        private bool IsFootprintWalkable(Vector2Int anchor)
        {
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    Vector2Int cell = anchor + new Vector2Int(x, y);

                    if (!map.IsWalkable(cell))
                        return false;
                }
            }

            return true;
        }

        // Returns the world position at the center of the Monster's footprint.
        private Vector3 GetFootprintWorldPosition(Vector2Int anchor)
        {
            return map.GridToWorld(new Vector2(anchor.x + 0.5f, anchor.y + 0.5f));
        }

        // Checks whether a tile is inside the square affected by Roar and Stomp.
        private bool IsInsideAbilityArea(Vector2Int cell)
        {
            return cell.x >= gridPosition.x - 2
                && cell.x <= gridPosition.x + 3
                && cell.y >= gridPosition.y - 2
                && cell.y <= gridPosition.y + 3;
        }

        // Measures the Manhattan distance from a tile to the nearest tile in the Monster's footprint.
        private int GetDistanceToFootprint(Vector2Int cell)
        {
            int distanceX = 0;
            int distanceY = 0;

            if (cell.x < gridPosition.x)
                distanceX = gridPosition.x - cell.x;
            else if (cell.x > gridPosition.x + 1)
                distanceX = cell.x - (gridPosition.x + 1);

            if (cell.y < gridPosition.y)
                distanceY = gridPosition.y - cell.y;
            else if (cell.y > gridPosition.y + 1)
                distanceY = cell.y - (gridPosition.y + 1);

            return distanceX + distanceY;
        }

        // Damages players on tiles newly entered by the Monster, without repeating damage on overlapping tiles.
        private bool DamagePlayersOnNewTiles(Vector2Int previousAnchor, Vector2Int newAnchor)
        {
            bool hitPlayer = false;

            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    Vector2Int cell = newAnchor + new Vector2Int(x, y);

                    if (cell.x >= previousAnchor.x && cell.x <= previousAnchor.x + 1
                        && cell.y >= previousAnchor.y && cell.y <= previousAnchor.y + 1)
                        continue;

                    if (DamagePlayersAt(cell))
                        hitPlayer = true;
                }
            }

            return hitPlayer;
        }


        // Resets movement and requires a dice roll at the start of each turn.
        private void BeginMonsterTurn()
        {
            // Skips the Monster's next turn after a Charge crash.
            if (mustSkipNextTurn)
            {
                mustSkipNextTurn = false;
                turnStarted = false;
                turnSystem.AdvanceTurn();
                return;
            }

            turnStarted = true;
            movesUsed = 0;
            rolledMoves = 0;
            hasRolled = false;
            monsterTurn++;
        }

        // Sets the Monster's movement allowance from its dice roll.
        public void SetMovementFromRoll(int roll)
        {
            if (!turnStarted ||
                turnSystem.CurrentTurnOwner != TurnOwner.Enemy ||
                hasRolled)
                return;

            rolledMoves = Mathf.Clamp(roll, 1, maximumMoves);
            hasRolled = true;
        }

        // Reads movement input and lets the Monster choose its Charge direction.
        private void ReadMonsterInput()
        {
            if (Keyboard.current == null)
                return;

            // Rolls once per Monster turn, from 1 to 4.
            if (!hasRolled && !isRolling && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                StartCoroutine(RollDice());
                return;
            }

            // Choose the direction after activating Charge with Q.
            if (isChoosingChargeDirection)
            {
                if (Keyboard.current.wKey.wasPressedThisFrame ||
                    Keyboard.current.upArrowKey.wasPressedThisFrame)
                    TryCharge(Vector2Int.up);

                else if (Keyboard.current.sKey.wasPressedThisFrame ||
                         Keyboard.current.downArrowKey.wasPressedThisFrame)
                    TryCharge(Vector2Int.down);

                else if (Keyboard.current.aKey.wasPressedThisFrame ||
                         Keyboard.current.leftArrowKey.wasPressedThisFrame)
                    TryCharge(Vector2Int.left);

                else if (Keyboard.current.dKey.wasPressedThisFrame ||
                         Keyboard.current.rightArrowKey.wasPressedThisFrame)
                    TryCharge(Vector2Int.right);

                return;
            }

            // Activates Charge direction selection.
            if (Keyboard.current.qKey.wasPressedThisFrame &&
                monsterTurn >= chargeReadyTurn && !isMoving)
            {
                isChoosingChargeDirection = true;
                return;
            }

            // Normal movement.
            if (Keyboard.current.wKey.wasPressedThisFrame ||
                Keyboard.current.upArrowKey.wasPressedThisFrame)
                TryMove(Vector2Int.up);

            else if (Keyboard.current.sKey.wasPressedThisFrame ||
                     Keyboard.current.downArrowKey.wasPressedThisFrame)
                TryMove(Vector2Int.down);

            else if (Keyboard.current.aKey.wasPressedThisFrame ||
                     Keyboard.current.leftArrowKey.wasPressedThisFrame)
                TryMove(Vector2Int.left);

            else if (Keyboard.current.dKey.wasPressedThisFrame ||
                     Keyboard.current.rightArrowKey.wasPressedThisFrame)
                TryMove(Vector2Int.right);

            // Activates Roar.
            if (Keyboard.current.eKey.wasPressedThisFrame)
                TryRoar();

            // Activates Stomp.
            if (Keyboard.current.rKey.wasPressedThisFrame)
                TryStomp();

        }

        // Moves the Monster one tile if its entire footprint can fit, then damages players on newly entered tiles.
        private void TryMove(Vector2Int direction)
        {
            if (!hasRolled || movesUsed >= rolledMoves || isMoving)
                return;

            Vector2Int destination = gridPosition + direction;

            if (!IsFootprintWalkable(destination))
                return;

            StartMovement(new List<Vector2Int> { destination });
        }

        // Charges in the selected direction without using normal movement.
        private void TryCharge(Vector2Int direction)
        {
            isChoosingChargeDirection = false;

            if (monsterTurn < chargeReadyTurn || isMoving)
                return;

            var path = new List<Vector2Int>();
            Vector2Int destination = gridPosition;
            bool hitObstacle = false;

            for (int i = 0; i < chargeDistance; i++)
            {
                destination += direction;

                if (!IsFootprintWalkable(destination))
                {
                    hitObstacle = true;
                    break;
                }

                path.Add(destination);
            }

            chargeReadyTurn = monsterTurn + chargeCooldown + 1;

            if (hitObstacle)
                chargeCrashed = true;

            if (path.Count == 0)
            {
                if (chargeCrashed)
                {
                    chargeCrashed = false;
                    mustSkipNextTurn = true;
                    EndMonsterTurn();
                }

                return;
            }

            isCharging = true;
            StartMovement(path);
        }

        // Slows living players inside the Monster's two-tile Roar area.
        private void TryRoar()
        {
            if (monsterTurn < roarReadyTurn || isMoving)
                return;

            bool affectedPlayer = false;

            foreach (PlayerCharacter player in players)
            {
                if (player == null || !player.isActiveAndEnabled
                    || player.Health <= 0)
                    continue;

                if (IsInsideAbilityArea(player.GridPosition))
                {
                    player.ApplyMonsterSlow();
                    affectedPlayer = true;
                    Debug.Log("Roar affected player: " + player.name);
                }
            }
            roarReadyTurn = monsterTurn + roarCooldown + 1;
        }

        // Damages every living player inside the Monster's two-tile Stomp area.
        private void TryStomp()
        {
            if (monsterTurn < stompReadyTurn || isMoving)
            {
                Debug.Log("Stomp is on cooldown or the Monster is moving.");
                return;
            }

            bool hitPlayer = false;

            foreach (PlayerCharacter player in players)
            {
                if (player == null || !player.isActiveAndEnabled
                    || player.Health <= 0)
                    continue;

                Debug.Log("Stomp check: Monster " + gridPosition
                    + ", Player " + player.name + " " + player.GridPosition);

                if (IsInsideAbilityArea(player.GridPosition))
                {
                    Debug.Log("Stomp hit player: " + player.name);

                    player.TakeDamage();
                    hitPlayer = true;
                }
            }

            stompReadyTurn = monsterTurn + stompCooldown + 1;
        }

        // Starts moving through the selected path one tile at a time.
        private void StartMovement(List<Vector2Int> path)
        {
            if (movementRoutine != null)
                StopCoroutine(movementRoutine);

            movementRoutine = StartCoroutine(MoveAlongPath(path));
        }

        // Animates movement and checks for player collisions after each tile.
        private IEnumerator MoveAlongPath(List<Vector2Int> path)
        {
            isMoving = true;

            foreach (Vector2Int destination in path)
            {
                Vector2Int previousAnchor = gridPosition;

                Vector3 start = transform.position;
                Vector3 end = GetFootprintWorldPosition(destination);
                float elapsed = 0f;

                while (elapsed < moveDuration)
                {
                    elapsed += Time.deltaTime;
                    transform.position = Vector3.Lerp(
                        start, end, elapsed / moveDuration);
                    yield return null;
                }

                transform.position = end;
                gridPosition = destination;

                // Charge movement does not use the dice-roll allowance.
                if (!isCharging)
                    movesUsed++;

                bool hitPlayer = DamagePlayersOnNewTiles(
                    previousAnchor, gridPosition);

                if (hitPlayer && !isCharging)
                    break;

                // Ends a normal movement turn when the roll is used up.
                if (!isCharging && hasRolled && movesUsed >= rolledMoves)
                    break;
            }

            isMoving = false;
            movementRoutine = null;

            // A Charge crash ends the current turn and schedules a skipped turn.
            if (isCharging && chargeCrashed)
            {
                isCharging = false;
                chargeCrashed = false;
                mustSkipNextTurn = true;
                EndMonsterTurn();
                yield break;
            }

            // Save whether this was a Charge before resetting its state.
            bool wasCharging = isCharging;
            isCharging = false;

            // Only normal movement can end the turn because the dice roll is used up.
            if (!wasCharging && hasRolled && movesUsed >= rolledMoves)
            {
                EndMonsterTurn();
            }
        }

        // Damages every living player occupying the specified tile.
        private bool DamagePlayersAt(Vector2Int position)
        {
            bool hitPlayer = false;

            foreach (PlayerCharacter player in players)
            {
                if (player == null || !player.isActiveAndEnabled
                    || player.Health <= 0 || player.GridPosition != position)
                    continue;

                Debug.Log("Monster hit " + player.name + " at " + position);
                player.TakeDamage();
                hitPlayer = true;
            }

            return hitPlayer;
        }

        // Gets the direction toward the closest player relative to the Monster's footprint.
        private Vector2Int GetMovementDirection()
        {
            PlayerCharacter target = FindClosestPlayer();

            if (target == null)
                return Vector2Int.zero;

            Vector2Int targetPosition = target.GridPosition;

            int nearestX = Mathf.Clamp(
                targetPosition.x, gridPosition.x, gridPosition.x + 1);

            int nearestY = Mathf.Clamp(
                targetPosition.y, gridPosition.y, gridPosition.y + 1);

            int differenceX = targetPosition.x - nearestX;
            int differenceY = targetPosition.y - nearestY;

            if (differenceX == 0 && differenceY == 0)
                return Vector2Int.zero;

            if (Mathf.Abs(differenceX) >= Mathf.Abs(differenceY)
                && differenceX != 0)
            {
                return new Vector2Int(
                    differenceX > 0 ? 1 : -1, 0);
            }

            return new Vector2Int(
                0, differenceY > 0 ? 1 : -1);
        }

        // Finds the living player closest to the Monster's footprint.
        private PlayerCharacter FindClosestPlayer()
        {
            PlayerCharacter closest = null;
            int shortestDistance = int.MaxValue;

            foreach (PlayerCharacter player in players)
            {
                if (player == null || !player.isActiveAndEnabled
                    || player.Health <= 0)
                    continue;

                int distance = GetDistanceToFootprint(player.GridPosition);

                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    closest = player;
                }
            }

            return closest;
        }

        // Rolls the Monster's dice and displays the result after a short animation.
        private IEnumerator RollDice()
        {
            isRolling = true;
            yield return new WaitForSecondsRealtime(0.6f);

            SetMovementFromRoll(Random.Range(1, 5));
            isRolling = false;

            Debug.Log("Monster rolled " + rolledMoves);
        }

        // Lets the AI move toward the closest player during its turn.
        private void TakeAITurn()
        {
            if (isMoving)
                return;

            PlayerCharacter target = FindClosestPlayer();

            if (target == null)
            {
                EndMonsterTurn();
                return;
            }

            if (monsterTurn >= stompReadyTurn
                && IsInsideAbilityArea(target.GridPosition))
            {
                TryStomp();

                if (!isMoving)
                    EndMonsterTurn();

                return;
            }

            if (!hasRolled || movesUsed >= rolledMoves)
            {
                EndMonsterTurn();
                return;
            }

            Vector2Int direction = GetMovementDirection();

            if (direction == Vector2Int.zero)
            {
                EndMonsterTurn();
                return;
            }

            Vector2Int destination = gridPosition + direction;

            if (IsFootprintWalkable(destination))
                TryMove(direction);
            else
                EndMonsterTurn();
        }

        // Finishes the Monster turn and returns control to the player turn cycle.
        private void EndMonsterTurn()
        {
            if (isMoving)
                return;

            turnStarted = false;
            turnSystem.AdvanceTurn();
        }
    }
}

