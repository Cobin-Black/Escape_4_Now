using Escape4Now.Map;
using Escape4Now.TurnSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.Player
{
    // Turns mouse clicks into movement requests for the current player.
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class PlayerClickMovementController : MonoBehaviour
    {
        // Scene references needed to select tiles and check turns.
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private IsometricMapTemplate mapTemplate;
        [SerializeField] private TurnSystemController turnSystem;

        private PlayerCharacter playerCharacter;

        // Finds the player component and uses the main camera if needed.
        private void Awake()
        {
            playerCharacter = GetComponent<PlayerCharacter>();

            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
            }
        }

        // Checks for a new left mouse click each frame.
        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            TryMoveToMouseTile(mouse.position.ReadValue());
        }

        // Rejects invalid clicks and requests a move to an open floor tile.
        public bool TryMoveToMouseTile(Vector2 screenPosition)
        {
            if (gameplayCamera == null || mapTemplate == null || playerCharacter == null)
            {
                return false;
            }

            if (!isActiveAndEnabled || !playerCharacter.isActiveAndEnabled || playerCharacter.IsMoving)
            {
                return false;
            }

            // Reject coordinates that are not normal numbers.
            if (float.IsNaN(screenPosition.x) || float.IsInfinity(screenPosition.x)
                || float.IsNaN(screenPosition.y) || float.IsInfinity(screenPosition.y))
            {
                return false;
            }

            if (turnSystem != null && !turnSystem.IsPlayersTurn(playerCharacter))
            {
                return false;
            }

            if (!gameplayCamera.pixelRect.Contains(screenPosition))
            {
                return false;
            }

            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            if (turnSystem != null && turnSystem.IsPointerOverDisplay(guiPosition))
            {
                return false;
            }

            // Find where the click meets the floor, then select that grid tile.
            Ray ray = gameplayCamera.ScreenPointToRay(screenPosition);
            Plane floor = new Plane(Vector3.forward, mapTemplate.transform.position);
            if (!floor.Raycast(ray, out float distance))
            {
                return false;
            }

            if (!mapTemplate.TryGetGridPosition(ray.GetPoint(distance), out Vector2Int gridPosition))
            {
                return false;
            }

            return playerCharacter.MoveToGridPosition(gridPosition);
        }
    }
}

