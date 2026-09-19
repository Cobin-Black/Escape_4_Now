using UnityEngine;

namespace Escape4Now.Map
{
    // Stores the grid address of one generated tile.
    public sealed class IsometricMapTile : MonoBehaviour
    {
        [SerializeField] private Vector2Int gridPosition;

        // Gives other scripts the tile address without letting them change it directly.
        public Vector2Int GridPosition => gridPosition;

        // Sets the tile address when the map creates this tile.
        public void SetGridPosition(Vector2Int newGridPosition)
        {
            gridPosition = newGridPosition;
        }
    }
}

