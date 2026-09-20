using UnityEngine;

namespace Escape4Now.Map
{
    // Stores the grid address of one generated tile.
    public sealed class IsometricMapTile : MonoBehaviour
    {
        [SerializeField] private Vector2Int gridPosition;

        private Renderer wallRenderer;
        private Renderer wallCapRenderer;
        private MaterialPropertyBlock wallPropertyBlock;

        // Gives other scripts the tile address without letting them change it directly.
        public Vector2Int GridPosition => gridPosition;

        // Sets the tile address when the map creates this tile.
        public void SetGridPosition(Vector2Int newGridPosition)
        {
            gridPosition = newGridPosition;
        }

        // Remembers this tile's wall renderers so their transparency can be changed later.
        public void SetWallRenderers(Renderer wall, Renderer wallCap)
        {
            wallRenderer = wall;
            wallCapRenderer = wallCap;
        }

        // Fades this tile's wall so a player standing behind it stays visible.
        public void SetWallAlpha(float alpha)
        {
            if (wallRenderer == null && wallCapRenderer == null)
            {
                return;
            }

            wallPropertyBlock ??= new MaterialPropertyBlock();
            Color tint = new Color(1f, 1f, 1f, alpha);
            ApplyAlpha(wallRenderer, tint);
            ApplyAlpha(wallCapRenderer, tint);
        }

        // Overrides one renderer's tint color without touching the shared material.
        private void ApplyAlpha(Renderer target, Color tint)
        {
            if (target == null)
            {
                return;
            }

            target.GetPropertyBlock(wallPropertyBlock);
            wallPropertyBlock.SetColor("_Color", tint);
            target.SetPropertyBlock(wallPropertyBlock);
        }
    }
}

