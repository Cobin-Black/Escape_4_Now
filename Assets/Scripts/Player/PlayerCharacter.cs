using Escape4Now.Map;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Player
{
    // Draws the player and moves it between floor tiles.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerCharacter : MonoBehaviour
    {
        // Starting tile, appearance, and movement speed.
        [SerializeField] private IsometricMapTemplate mapTemplate;
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

        // Read-only movement state for the click and turn scripts.
        public Vector2Int GridPosition => gridPosition;
        public bool IsMoving => moveRoutine != null;

        // Checks settings and places the player on its starting tile.
        private void Awake()
        {
            ValidateSettings();
            SetupMarker();
            SnapToGridPosition();
        }

        // Checks settings after changes in the Inspector.
        private void OnValidate()
        {
            ValidateSettings();
        }

        // Keeps sizes and speed positive and within useful limits.
        private void ValidateSettings()
        {
            markerWidth = float.IsNaN(markerWidth) ? 0.38f : Mathf.Clamp(markerWidth, 0.1f, 10f);
            markerHeight = float.IsNaN(markerHeight) ? 0.72f : Mathf.Clamp(markerHeight, 0.1f, 10f);
            moveSpeed = float.IsNaN(moveSpeed) ? 4f : Mathf.Clamp(moveSpeed, 0.1f, 100f);
        }

        // Places the player on an open tile without playing a movement animation.
        public bool SetGridPosition(Vector2Int newGridPosition)
        {
            if (IsMoving || mapTemplate == null || !mapTemplate.IsWalkable(newGridPosition))
            {
                return false;
            }

            gridPosition = newGridPosition;
            SnapToGridPosition();
            return true;
        }

        // Starts a move only when the player is ready and a valid route exists.
        public bool MoveToGridPosition(Vector2Int newGridPosition)
        {
            if (!isActiveAndEnabled || IsMoving || mapTemplate == null)
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

        // Creates the player's rectangle and sets its size and drawing order.
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

        // Draws a black rectangle with a visible border.
        private Sprite CreateMarkerSprite()
        {
            Texture2D texture = new Texture2D(12, 24)
            {
                filterMode = FilterMode.Point
            };
            markerTexture = texture;

            // Draw a simple standing rectangle with an outline.
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

        // Lines the player's feet up with the center of its current tile.
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

        // Slides through each tile in the route and records completed steps.
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
            }
            moveRoutine = null;
        }

        // Cancels an interrupted move and returns to the last completed tile.
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

        // Releases the generated image when the player is removed.
        private void OnDestroy()
        {
            ReleaseMarker();
        }

        // Removes only the sprite and texture created by this script.
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

