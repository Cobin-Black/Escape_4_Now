// The map operations shared by movement, pathfinding and player placement, regardless of game mode.

using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Map
{
    public interface IWeightedPlayerGridMap { int TraversalCost(Vector2Int cell); }
    //Movement and turn setup need grid data, not a particular map generator.
    public interface IPlayerGridMap
    {
        Transform transform { get; }
        int Width { get; }
        int Height { get; }
        float TileHeight { get; }
        int LayoutSeed { get; }
        void PrepareLayout();
        bool TryGetPlayerSpawn(int index, out Vector2Int cell);
        bool IsWalkable(Vector2Int cell);
        bool IsExit(Vector2Int cell);
        bool IsOccupied(Vector2Int cell);
        Vector3 GridToWorld(Vector2Int cell);
        bool TryFindPath(Vector2Int start, Vector2Int end, out List<Vector2Int> path);
        void SetOccupantPosition(Vector2Int previous, Vector2Int next);
        void RemoveOccupant(Vector2Int cell);
        void ReplacePlayerOccupants(IReadOnlyList<Vector2Int> cells);
    }
}
