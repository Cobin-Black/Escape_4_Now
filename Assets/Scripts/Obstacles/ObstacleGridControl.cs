using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Obstacles
{
    //Places an obstacle on a map tile, blocks that tile, and fades the obstacle when a player stands behind it.
    [ExecuteAlways]
    [RequireComponent(typeof(ObstacleCubeMesh), typeof(PolygonCollider2D))]
    public sealed class ObstacleGridControl : MonoBehaviour
    {
        //Map the obstacle sits on and the tile it covers.
        [SerializeField] private IsometricMapTemplate mapTemplate;
        [SerializeField] private Vector2Int gridPosition = new Vector2Int(5, 5);
        [SerializeField] private ObstacleFacing facing = ObstacleFacing.South;

        //Whether players will be able to interact with this obstacle.
        [SerializeField] private bool isInteractable;

        //How see-through the obstacle gets while a player is hidden behind it.
        [SerializeField, Range(0f, 1f)] private float fadedAlpha = 0.3f;

        private ObstacleCubeMesh cubeMesh;
        private PolygonCollider2D footprintCollider;
        private MaterialPropertyBlock fadePropertyBlock;
        private Vector3 lastSnappedPosition;
        private Vector2Int registeredPosition;
        private bool ownsRegisteredPosition;
        private IsometricMapTemplate registeredMap;
        private IsometricMapTemplate subscribedMap;

        //Read-only obstacle details for other scripts.
        public Vector2Int GridPosition => gridPosition;
        public ObstacleFacing Facing => facing;
        public bool IsInteractable
        {
            get => isInteractable;
            set => isInteractable = value;
        }

        //Snaps onto the map, blocks the tile, and starts listening for player moves.
        private void OnEnable()
        {
            ApplyPlacement();
        }

        //Frees the tile and stops listening when the obstacle is turned off or removed.
        private void OnDisable()
        {
            ReleaseRegisteredPosition();
            SubscribeToMap(null);
        }

        //Reapplies placement after Inspector changes. Waits a frame in the editor because Unity blocks moving objects during OnValidate.
        private void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= DelayedApplyPlacement;
            UnityEditor.EditorApplication.delayCall += DelayedApplyPlacement;
#else
            ApplyPlacement();
#endif
        }

        //Snaps to the nearest tile when the obstacle is dragged in the Scene view.
        private void Update()
        {
            if (Application.isPlaying || mapTemplate == null || transform.position == lastSnappedPosition)
            {
                return;
            }

            gridPosition = mapTemplate.WorldToGrid(transform.position);
            ApplyPlacement();
        }

        //Moves the obstacle to another tile. Returns false if the tile is outside the floor or already blocked.
        public bool SetGridPosition(Vector2Int newGridPosition)
        {
            if (mapTemplate == null || !mapTemplate.IsInteriorFloor(newGridPosition))
            {
                return false;
            }

            if (newGridPosition != registeredPosition && mapTemplate.IsBlocked(newGridPosition))
            {
                return false;
            }

            gridPosition = newGridPosition;
            ApplyPlacement();
            return true;
        }

        //Turns the obstacle so its front face points another way.
        public void SetFacing(ObstacleFacing newFacing)
        {
            facing = newFacing;
            ApplyPlacement();
        }

        //Runs a placement that was waiting for OnValidate to finish, unless the obstacle was deleted first.
        private void DelayedApplyPlacement()
        {
            if (this == null)
            {
                return;
            }

            ApplyPlacement();
        }

        //Keeps the obstacle on the floor, updates its mesh and collider, and records its tile with the map.
        private void ApplyPlacement()
        {
            if (cubeMesh == null)
            {
                cubeMesh = GetComponent<ObstacleCubeMesh>();
            }

            if (footprintCollider == null)
            {
                footprintCollider = GetComponent<PolygonCollider2D>();
            }

            if (mapTemplate != null)
            {
                gridPosition = ClampToInteriorFloor(gridPosition);
                Vector3 worldPosition = mapTemplate.GridToWorld(gridPosition);
                transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            }

            lastSnappedPosition = transform.position;

            //Uses the same drawing order as a wall on this tile, so players in front draw over it and players behind draw under it.
            int order = 500 - (gridPosition.x + gridPosition.y) * 10;
            cubeMesh.Configure(mapTemplate, facing, order);

            footprintCollider.points = cubeMesh.GetFootprintPoints();
            footprintCollider.isTrigger = false;

            if (isActiveAndEnabled)
            {
                RegisterPosition();
                SubscribeToMap(mapTemplate);
            }

            RefreshFade();
        }

        //Moves a requested tile onto the nearest floor tile inside the border walls.
        private Vector2Int ClampToInteriorFloor(Vector2Int cell)
        {
            int x = Mathf.Clamp(cell.x, 1, Mathf.Max(1, mapTemplate.Width - 2));
            int y = Mathf.Clamp(cell.y, 1, Mathf.Max(1, mapTemplate.Height - 2));
            return new Vector2Int(x, y);
        }

        //Blocks this obstacle's tile on the map, freeing the old one if it moved.
        private void RegisterPosition()
        {
            bool alreadyRegistered = ownsRegisteredPosition && registeredMap == mapTemplate && registeredPosition == gridPosition;
            if (mapTemplate == null || alreadyRegistered)
            {
                return;
            }

            ReleaseRegisteredPosition();
            ownsRegisteredPosition = mapTemplate.RegisterBlockedPosition(gridPosition);
            registeredPosition = gridPosition;
            registeredMap = mapTemplate;

            if (!ownsRegisteredPosition)
            {
                Debug.LogWarning($"{name} is on tile {gridPosition}, which another obstacle already covers.", this);
            }
        }

        //Frees the tile this obstacle blocked, but never one that belongs to another obstacle.
        private void ReleaseRegisteredPosition()
        {
            if (ownsRegisteredPosition && registeredMap != null)
            {
                registeredMap.UnregisterBlockedPosition(registeredPosition);
            }

            ownsRegisteredPosition = false;
            registeredMap = null;
        }

        //Listens to one map for player moves, dropping any map it listened to before.
        private void SubscribeToMap(IsometricMapTemplate map)
        {
            if (subscribedMap == map)
            {
                return;
            }

            if (subscribedMap != null)
            {
                subscribedMap.OccupantsChanged -= RefreshFade;
            }

            subscribedMap = map;

            if (subscribedMap != null)
            {
                subscribedMap.OccupantsChanged += RefreshFade;
            }
        }

        //Fades the obstacle while any player stands on a tile it covers up.
        private void RefreshFade()
        {
            if (cubeMesh == null)
            {
                return;
            }

            MeshRenderer meshRenderer = cubeMesh.GetComponent<MeshRenderer>();
            fadePropertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(fadePropertyBlock);
            fadePropertyBlock.SetColor("_Color", new Color(1f, 1f, 1f, IsPlayerBehind() ? fadedAlpha : 1f));
            meshRenderer.SetPropertyBlock(fadePropertyBlock);
        }

        //Checks the tiles behind the obstacle that its height can hide.
        private bool IsPlayerBehind()
        {
            if (mapTemplate == null)
            {
                return false;
            }

            //Each step back on the grid moves half a tile up the screen, so taller obstacles hide more rows.
            float rowHeight = mapTemplate.TileHeight * 0.5f;
            float topEdge = cubeMesh.TopEdgeHeight;
            int maxDepth = Mathf.CeilToInt(topEdge / rowHeight);

            for (int dx = 0; dx <= maxDepth; dx++)
            {
                for (int dy = 0; dy <= maxDepth; dy++)
                {
                    //Skip this tile, tiles too far up to be covered, and tiles too far left or right to overlap.
                    bool isOwnTile = dx == 0 && dy == 0;
                    bool isAboveTop = (dx + dy) * rowHeight >= topEdge;
                    bool isOffToTheSide = Mathf.Abs(dx - dy) > 1;
                    if (isOwnTile || isAboveTop || isOffToTheSide)
                    {
                        continue;
                    }

                    if (mapTemplate.IsOccupied(gridPosition + new Vector2Int(dx, dy)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
