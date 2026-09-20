using Escape4Now.Map;
using Escape4Now.TurnSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.Player
{
    //Draws the player, rolls its move budget, and moves it between floor tiles with WASD or arrow keys.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerCharacter : MonoBehaviour
    {
        //Starting tile, appearance, and movement speed.
        [SerializeField] private IsometricMapTemplate mapTemplate;
        [SerializeField] private TurnSystemController turnSystem;
        [SerializeField] private Vector2Int gridPosition = new Vector2Int(2, 2);
        [SerializeField] private Color playerColor = Color.black;
        [SerializeField] private Color outlineColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        [SerializeField, Min(0.1f)] private float markerWidth = 0.38f;
        [SerializeField, Min(0.1f)] private float markerHeight = 0.72f;
        [SerializeField, Min(0.1f)] private float moveSpeed = 4f;

        private SpriteRenderer spriteRenderer;
        private Coroutine moveRoutine;
        private Sprite markerSprite;
        private Texture2D markerTexture;
        private Vector2Int registeredPosition;
        private bool hasReachedExit;
        private bool hasRolled;
        private int lastRoll;
        private int movesRemaining;
        private GUIStyle rollDisplayStyle;

        //Read-only movement state for the turn and map scripts.
        public Vector2Int GridPosition => gridPosition;
        public bool IsMoving => moveRoutine != null;

        //Checks settings and places the player on its starting tile.
        private void Awake()
        {
            ValidateSettings();
            SetupMarker();
            SnapToGridPosition();
            registeredPosition = gridPosition;
            if (mapTemplate != null)
            {
                mapTemplate.SetOccupantPosition(gridPosition, gridPosition);
            }
        }

        //Reads dice roll and movement key input each frame.
        private void Update()
        {
            if (hasReachedExit)
            {
                return;
            }

            HandleDiceRollInput();

            Vector2Int direction = ReadDirectionPressedThisFrame();
            if (direction != Vector2Int.zero)
            {
                TryMoveInDirection(direction);
            }
        }

        //Draws the roll prompt or the current move budget.
        private void OnGUI()
        {
            if (rollDisplayStyle == null)
            {
                rollDisplayStyle = new GUIStyle(GUI.skin.label);
                rollDisplayStyle.fontSize = 20;
                rollDisplayStyle.fontStyle = FontStyle.Bold;
                rollDisplayStyle.normal.textColor = Color.white;
            }

            string message = hasReachedExit
                ? "You reached the exit!"
                : hasRolled
                    ? $"Rolled a {lastRoll}. Moves left: {movesRemaining}"
                    : "Press SPACE to roll the dice";

            GUI.Label(new Rect(16f, 100f, 420f, 30f), message, rollDisplayStyle);
        }

        //Rolls a six-sided die on Space, giving this turn's move budget.
        private void HandleDiceRollInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || hasRolled || IsMoving)
            {
                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                lastRoll = Random.Range(1, 7);
                movesRemaining = lastRoll;
                hasRolled = true;
            }
        }

        //Reads the first movement key pressed this frame, or zero if none was.
        private Vector2Int ReadDirectionPressedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2Int.zero;
            }

            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            {
                return Vector2Int.up;
            }

            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            {
                return Vector2Int.down;
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            {
                return Vector2Int.left;
            }

            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            {
                return Vector2Int.right;
            }

            return Vector2Int.zero;
        }

        //Rejects moves before a roll, outside the player's turn, or past the roll's move budget.
        private bool TryMoveInDirection(Vector2Int direction)
        {
            if (!hasRolled || movesRemaining <= 0)
            {
                return false;
            }

            if (turnSystem != null && !turnSystem.IsPlayersTurn(this))
            {
                return false;
            }

            bool moved = MoveToGridPosition(gridPosition + direction);
            if (moved)
            {
                movesRemaining--;
                if (movesRemaining <= 0)
                {
                    hasRolled = false;
                }
            }

            return moved;
        }

        //Checks settings after changes in the Inspector.
        private void OnValidate()
        {
            ValidateSettings();
        }

        //Keeps sizes and speed positive and within useful limits.
        private void ValidateSettings()
        {
            markerWidth = float.IsNaN(markerWidth) ? 0.38f : Mathf.Clamp(markerWidth, 0.1f, 10f);
            markerHeight = float.IsNaN(markerHeight) ? 0.72f : Mathf.Clamp(markerHeight, 0.1f, 10f);
            moveSpeed = float.IsNaN(moveSpeed) ? 4f : Mathf.Clamp(moveSpeed, 0.1f, 100f);
        }

        //Places the player on an open tile without playing a movement animation.
        public bool SetGridPosition(Vector2Int newGridPosition)
        {
            if (IsMoving || hasReachedExit || mapTemplate == null || !mapTemplate.IsWalkable(newGridPosition))
            {
                return false;
            }

            gridPosition = newGridPosition;
            SnapToGridPosition();
            UpdateMapOccupancy(newGridPosition);
            return true;
        }

        //Starts a move only when the player is ready and a valid route exists.
        public bool MoveToGridPosition(Vector2Int newGridPosition)
        {
            if (!isActiveAndEnabled || IsMoving || hasReachedExit || mapTemplate == null)
            {
                return false;
            }

            if (!mapTemplate.TryFindPath(gridPosition, newGridPosition, out List<Vector2Int> path))
            {
                return false;
            }

            moveRoutine = StartCoroutine(MoveAlongPath(path));
            return true;
        }

        //Creates the player's rectangle and sets its size and drawing order.
        private void SetupMarker()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                return;
            }

            ReleaseMarker();
            markerSprite = CreateMarkerSprite();
            spriteRenderer.sprite = markerSprite;
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 505 - (gridPosition.x + gridPosition.y) * 10;
            transform.localScale = new Vector3(markerWidth, markerHeight, 1f);
        }

        //Draws a black rectangle with a visible border.
        private Sprite CreateMarkerSprite()
        {
            Texture2D texture = new Texture2D(12, 24)
            {
                filterMode = FilterMode.Point
            };
            markerTexture = texture;

            //Draw a simple standing rectangle with an outline.
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    bool isOutline = x == 0 || y == 0 || x == texture.width - 1 || y == texture.height - 1;
                    texture.SetPixel(x, y, isOutline ? outlineColor : playerColor);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0f), 24f);
        }

        //Lines the player's feet up with the center of its current tile.
        private void SnapToGridPosition()
        {
            if (mapTemplate == null || spriteRenderer == null)
            {
                return;
            }

            Vector3 worldPosition = mapTemplate.GridToWorld(gridPosition);
            transform.position = new Vector3(worldPosition.x, worldPosition.y, -1f);
            spriteRenderer.sortingOrder = 505 - (gridPosition.x + gridPosition.y) * 10;
        }

        //Slides through each tile in the route and records completed steps.
        private IEnumerator MoveAlongPath(List<Vector2Int> path)
        {
            // Move through the grid one tile at a time.
            foreach (Vector2Int next in path)
            {
                Vector3 targetPosition = mapTemplate.GridToWorld(next);
                targetPosition.z = -1f;
                while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
                    float row = (transform.position.y - mapTemplate.transform.position.y) / (mapTemplate.TileHeight * 0.5f);
                    spriteRenderer.sortingOrder = 505 - Mathf.RoundToInt(row * 10f);
                    yield return null;
                }
                transform.position = targetPosition;
                gridPosition = next;
                UpdateMapOccupancy(next);

                if (mapTemplate.IsExit(next))
                {
                    ReachExit();
                    yield break;
                }
            }
            moveRoutine = null;
        }

        //Updates which tile the map thinks this player occupies, for wall fading.
        private void UpdateMapOccupancy(Vector2Int newPosition)
        {
            if (mapTemplate == null)
            {
                return;
            }

            mapTemplate.SetOccupantPosition(registeredPosition, newPosition);
            registeredPosition = newPosition;
        }

        //Stops the player at the exit and pauses the game to show the win.
        private void ReachExit()
        {
            hasReachedExit = true;
            moveRoutine = null;
            Debug.Log($"{name} reached the exit. You win!");
            Time.timeScale = 0f;
        }

        //Cancels an interrupted move and returns to the last completed tile.
        private void OnDisable()
        {
            if (moveRoutine == null)
            {
                return;
            }

            StopCoroutine(moveRoutine);
            moveRoutine = null;
            SnapToGridPosition();
        }

        //Releases the generated image and stops tracking this player on the map.
        private void OnDestroy()
        {
            if (mapTemplate != null)
            {
                mapTemplate.RemoveOccupant(registeredPosition);
            }

            ReleaseMarker();
        }

        //Removes only the sprite and texture created by this script.
        private void ReleaseMarker()
        {
            if (Application.isPlaying)
            {
                if (markerSprite != null) Destroy(markerSprite);
                if (markerTexture != null) Destroy(markerTexture);
            }
            else
            {
                if (markerSprite != null) DestroyImmediate(markerSprite);
                if (markerTexture != null) DestroyImmediate(markerTexture);
            }
            markerSprite = null;
            markerTexture = null;
        }
    }
}

