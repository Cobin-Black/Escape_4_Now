using System.Collections;
using System.Collections.Generic;
using Escape4Now.Map;
using Escape4Now.Player;
using Escape4Now.Items;
using Escape4Now.Obstacles;
using Escape4Now.Battle;
using UnityEngine;
using Escape4Now.TurnSystem;

namespace Escape4Now.Bots
{
    public sealed class BotController : MonoBehaviour
    {
        private PlayerCharacter player;
        private IsometricMapTemplate map;
        private Coroutine turnRoutine;
        private KeyStealBattleController battleController;

        private readonly HashSet<ObstacleStorage> searchedObjects =
            new HashSet<ObstacleStorage>();

        private void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            map = FindFirstObjectByType<IsometricMapTemplate>();
            battleController = FindFirstObjectByType<KeyStealBattleController>();
        }

        public void ResetSearchMemory()
        {
            searchedObjects.Clear();
        }

        private void Update()
        {
            if (Escape4Now.Multiplayer.LanMultiplayerSession.Active)
                return;

            if (turnRoutine != null || player == null || map == null)
                return;

            if (battleController != null && battleController.IsBattleActive)
                return;

            if (!player.IsCurrentTurn || player.HasReachedExit)
                return;

            turnRoutine = StartCoroutine(TakeTurn());
        }

        private IEnumerator TakeTurn()
        {
            yield return null;

            if (!player.HasRolled)
            {
                TryUseKeyStealer();
                TryUseMovementItem();

                if (!player.HasRolled)
                    player.RollDice();
            }

            while (player.IsCurrentTurn
                && !player.HasReachedExit
                && player.MovesRemaining > 0)
            {
                if (player.IsMoving)
                {
                    yield return null;
                    continue;
                }

                if (player.Inventory != null && player.Inventory.HasKey())
                {
                    Vector2Int exit = FindExit();

                    if (exit.x >= 0)
                    {
                        MoveToward(exit);
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    PlayerCharacter carrier = FindKeyCarrier();

                    if (carrier != null)
                    {
                        if (carrier.GridPosition == player.GridPosition)
                        {
                            if (battleController != null)
                                battleController.TryBotBattle(player, carrier);

                            break;
                        }

                        MoveToward(carrier.GridPosition);
                    }
                    else
                    {
                        ObstacleStorage storage = FindUnsearchedObject();

                        if (storage == null)
                            break;

                        if (IsNextTo(storage.GridPosition))
                        {
                            if (storage.SearchForBot(player))
                            {
                                searchedObjects.Add(storage);
                            }
                            else if (storage.HasItem
                                && !(storage.StoredItemPrefab is KeyItem)
                                && player.Inventory.IsFull)
                            {
                                searchedObjects.Add(storage);
                            }
                            else
                            {
                                break;
                            }
                        }
                        else
                        {
                            Vector2Int destination =
                                FindAdjacentDestination(storage.GridPosition);

                            if (destination.x < 0)
                                break;

                            MoveToward(destination);
                        }
                    }
                }

                while (player.IsMoving)
                    yield return null;

                yield return null;
            }

            if (player.IsCurrentTurn
            && !player.HasReachedExit
            && !player.IsMoving
            && !player.IsUsingItem()
            && !player.IsInMenu()
            && (battleController == null || !battleController.IsBattleActive))
            {
                TurnSystemController turnSystem =
                    FindFirstObjectByType<TurnSystemController>();

                if (turnSystem != null)
                    turnSystem.AdvanceTurn();
            }

            turnRoutine = null;
        }


        // Uses Key Stealer only when another player has a key.
        private void TryUseKeyStealer()
        {
            if (player.Inventory == null
                || player.Inventory.HasKey()
                || !player.CanUseItem())
            {
                return;
            }

            if (FindKeyCarrier() == null)
                return;

            foreach (Item item in player.Inventory.Items)
            {
                if (item is KeyStealerItem keyStealer)
                {
                    keyStealer.Use(player.Inventory);
                    return;
                }
            }
        }

        // Uses one movement item before rolling the normal dice.
        private void TryUseMovementItem()
        {
            if (player.Inventory == null || !player.CanUseItem())
                return;

            // Prefer Double Dice because it can provide up to 12 moves.
            foreach (Item item in player.Inventory.Items)
            {
                if (item is DoubleDiceItem doubleDice)
                {
                    doubleDice.Use(player.Inventory);

                    if (player.HasRolled)
                        return;
                }
            }

            // Use Custom Dice if Double Dice was not used.
            foreach (Item item in player.Inventory.Items)
            {
                if (item is CustomDiceItem customDice)
                {
                    if (customDice.UseForBot(player.Inventory))
                        return;
                }
            }
        }


        private void MoveToward(Vector2Int destination)
        {
            if (!map.TryFindPath(
                player.GridPosition,
                destination,
                out List<Vector2Int> path))
                return;

            if (path.Count == 0)
                return;

            int moves = player.MovesRemaining;

            if (path.Count <= moves)
            {
                player.MoveToGridPosition(destination);
            }
            else
            {
                Vector2Int nextDestination = path[moves - 1];
                player.MoveToGridPosition(nextDestination);
            }
        }

        private PlayerCharacter FindKeyCarrier()
        {
            PlayerCharacter[] players =
                FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);

            foreach (PlayerCharacter other in players)
            {
                if (other == null || other == player
                    || !other.isActiveAndEnabled
                    || other.HasReachedExit
                    || other.Inventory == null)
                    continue;

                if (other.Inventory.HasKey())
                    return other;
            }

            return null;
        }

        private Vector2Int FindExit()
        {
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    if (map.IsExit(cell))
                        return cell;
                }
            }

            return new Vector2Int(-1, -1);
        }

        private ObstacleStorage FindUnsearchedObject()
        {
            ObstacleStorage[] objects =
                map.GetComponentsInChildren<ObstacleStorage>();

            ObstacleStorage closest = null;
            int shortestDistance = int.MaxValue;

            foreach (ObstacleStorage storage in objects)
            {
                if (storage == null || searchedObjects.Contains(storage)
                || storage.HasBeenSearched || !storage.CanInteract)
                    continue;

                Vector2Int position = storage.GridPosition;

                if (FindAdjacentDestination(position).x < 0)
                    continue;

                int distance =
                    Mathf.Abs(position.x - player.GridPosition.x)
                    + Mathf.Abs(position.y - player.GridPosition.y);

                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    closest = storage;
                }
            }

            return closest;
        }


        private Vector2Int FindAdjacentDestination(Vector2Int position)
        {
            Vector2Int[] directions =
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            Vector2Int best = new Vector2Int(-1, -1);
            int shortestPath = int.MaxValue;

            foreach (Vector2Int direction in directions)
            {
                Vector2Int cell = position + direction;

                if (!map.IsWalkable(cell))
                    continue;

                if (!map.TryFindPath(
                    player.GridPosition,
                    cell,
                    out List<Vector2Int> path))
                    continue;

                if (path.Count == 0 || path.Count >= shortestPath)
                    continue;

                shortestPath = path.Count;
                best = cell;
            }

            return best;
        }


        private bool IsNextTo(Vector2Int position)
        {
            Vector2Int current = player.GridPosition;

            return Mathf.Abs(current.x - position.x)
                + Mathf.Abs(current.y - position.y) == 1;
        }
    }
}
