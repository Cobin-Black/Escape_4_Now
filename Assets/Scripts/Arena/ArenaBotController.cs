using System.Collections.Generic;
using Escape4Now.Items;
using UnityEngine;

namespace Escape4Now.Arena
{
    // Computer-controlled arena player for King of the Hill and Hot Potato. It drives the participant's
    // ArenaLocalInput in bot mode; online, only the host runs it and guests see the bot through the normal updates.
    public sealed class ArenaBotController : MonoBehaviour
    {
        private ArenaParticipant player;
        private ArenaLocalInput input;
        private Vector2 targetPosition;
        private bool hasTarget;
        private Vector2 lastHillCenter;
        private bool roamingHill;
        private float roamTimer;
        private List<Vector2Int> currentPath = new List<Vector2Int>();
        private int pathIndex;
        private Vector2 lastPathTarget;
        private float pathTimer;
        // Control how often the bot searches for an escape position.
        private float escapeTimer;
        [SerializeField, Min(.05f), Tooltip("Seconds between escape searches while running from the potato holder.")]
        private float escapeInterval = .5f;
        [SerializeField, Min(.05f), Tooltip("Faster escape searches while Spotlight makes the potato holder faster.")]
        private float spotlightEscapeInterval = .25f;
        private ArenaParticipant lastPotatoHolder;
        // Time remaining before each attack can be used again.
        private float rulerCooldown;
        private float spitballCooldown;

        private void Awake()
        {
            player = GetComponent<ArenaParticipant>();
            input = GetComponent<ArenaLocalInput>();
        }

        // Straight ahead from the way this bot is facing, in floor (grid) space, the same direction its items attack in.
        private Vector2 AttackDirection => player.AttackDirection;

        private void Update()
        {
            if (player == null || input == null || !input.IsBot)
                return;

            if (player.Match == null || !player.Match.Playing ||
                player.Match.Paused || !player.Alive)
            {
                input.SetBotMovement(Vector2.zero);
                return;
            }

            // Count down attack cooldowns.
            rulerCooldown = Mathf.Max(0f, rulerCooldown - Time.deltaTime);
            spitballCooldown = Mathf.Max(0f, spitballCooldown - Time.deltaTime);

            // Use an item if one is available.
            if (player.Match is KingOfTheHillController)
            {
                TryUseEraser();

                if (rulerCooldown <= 0f)
                    TryUseRuler();

                if (spitballCooldown <= 0f)
                    TryUseSpitball();
            }

            // Chase the opponent or run away from the potato holder.
            if (player.Match is HotPotatoController hotPotato)
            {
                bool holdingPotato = IsHoldingPotato(hotPotato);

                // Update the escape timer.
                escapeTimer -= Time.deltaTime;

                // Immediately update when the potato changes hands.
                if (lastPotatoHolder != hotPotato.Holder)
                {
                    lastPotatoHolder = hotPotato.Holder;
                    escapeTimer = 0f;
                }

                if (!hotPotato.BetweenRounds)
                {
                    if (holdingPotato)
                    {
                        ChasePotatoTarget(hotPotato);
                    }
                    else if (escapeTimer <= 0f)
                    {
                        RunFromPotatoHolder(hotPotato);
                        // A spotlit holder closes in faster, so check the escape route more often.
                        bool fastHolder = player.Match.Events != null && player.Match.Events.SpotlightSpeed(hotPotato.Holder) > 1;
                        escapeTimer = fastHolder ? spotlightEscapeInterval : escapeInterval;
                    }
                }
                else
                {
                    input.SetBotMovement(Vector2.zero);
                    ClearTarget();
                }

                TryPassPotato(hotPotato);
            }

            if (player.Match is KingOfTheHillController hill && hill.ActiveHill != null)
            {
                Vector2 center = hill.ActiveHill.Center;

                // Chase nearby opponents when holding a Ruler.
                bool chasingOpponent = TryChaseWithRuler();

                // Find a firing position when holding a Spitball.
                bool positioningSpitball = false;

                if (!chasingOpponent &&
                    IsInsideHill(hill) &&
                    TryFindSpitballTarget(out ArenaParticipant opponent) &&
                    TryFindSpitballPosition(hill, opponent, out Vector2 firingPosition))
                {
                    SetTarget(firingPosition);
                    positioningSpitball = true;
                }

                // Detect when the hill moves.
                if (center != lastHillCenter)
                {
                    lastHillCenter = center;
                    roamingHill = false;
                    roamTimer = 0f;
                }

                // Decide whether to collect an item or enter the hill.
                if (!roamingHill && !chasingOpponent && !positioningSpitball)
                {
                    PlayerInventory inventory = GetComponent<PlayerInventory>();

                    float distanceToHill =
                        Vector2.Distance(player.Position, center);

                    float distanceToHillEdge =
                        Mathf.Max(0f, distanceToHill - hill.ActiveHill.Radius);

                    bool shouldGetItem = false;
                    Vector2 itemPosition = Vector2.zero;

                    // Only search for items when the inventory is empty.
                    if (inventory != null && inventory.Items.Count == 0 && TryFindClosestItem(hill, out itemPosition, out int itemDistance))
                    {
                        // Enter the hill first if it is very close.
                        // Otherwise, collect an item if it is a short detour.
                        if (distanceToHillEdge > 1.5f &&
                            itemDistance <= distanceToHillEdge + 2f)
                        {
                            shouldGetItem = true;
                        }
                    }

                    if (shouldGetItem)
                    {
                        SetTarget(itemPosition);
                    }
                    else
                    {
                        SetTarget(center);

                        if (Vector2.Distance(player.Position, center) < 0.5f)
                        {
                            roamingHill = true;
                            roamTimer = 0f;
                        }
                    }
                }

                // Collect nearby items while patrolling the hill.
                if (roamingHill && !chasingOpponent && !positioningSpitball)
                {
                    PlayerInventory inventory = GetComponent<PlayerInventory>();
                    bool collectingItem = false;

                    if (inventory != null && inventory.Items.Count == 0 &&
                        TryFindClosestItem(hill, out Vector2 itemPosition, out int itemDistance))
                    {
                        // Only leave the patrol route for nearby items.
                        if (itemDistance <= 3)
                        {
                            SetTarget(itemPosition);
                            collectingItem = true;
                        }
                    }

                    // Patrol normally when no nearby item is worth collecting.
                    if (!collectingItem)
                    {
                        roamTimer -= Time.deltaTime;

                        if (roamTimer <= 0f ||
                            Vector2.Distance(player.Position, targetPosition) < 0.3f)
                        {
                            Vector2 randomOffset = Random.insideUnitCircle *
                                                   (hill.ActiveHill.Radius * 0.65f);

                            SetTarget(center + randomOffset);
                            roamTimer = 2f;
                        }
                    }
                }
            }

            if (!hasTarget)
            {
                input.SetBotMovement(Vector2.zero);
                return;
            }

            // Recalculate the path when the destination changes or every second.
            pathTimer -= Time.deltaTime;

            if (pathTimer <= 0f ||
                Vector2.Distance(lastPathTarget, targetPosition) > 0.5f)
            {
                Vector2Int start = Vector2Int.RoundToInt(player.Position);
                Vector2Int goal = Vector2Int.RoundToInt(targetPosition);

                currentPath = FindPath(start, goal);
                pathIndex = 0;
                lastPathTarget = targetPosition;
                pathTimer = 1f;
            }

            // Follow the next tile in the path.
            Vector2 destination = targetPosition;

            if (pathIndex < currentPath.Count)
            {
                destination = currentPath[pathIndex];

                if (Vector2.Distance(player.Position, destination) < 0.2f)
                {
                    pathIndex++;

                    if (pathIndex < currentPath.Count)
                        destination = currentPath[pathIndex];
                    else
                        destination = targetPosition;
                }
            }

            Vector2 direction = destination - player.Position;

            if (direction.magnitude < 0.2f)
            {
                input.SetBotMovement(Vector2.zero);
                return;
            }

            // Convert grid movement into screen movement.
            Vector2 screenDirection =
                player.Match.Map.ScreenDirection(direction.normalized);

            input.SetBotMovement(screenDirection.normalized);
        }

        // Set the destination the bot should move toward.
        public void SetTarget(Vector2 position)
        {
            targetPosition = position;
            hasTarget = true;
        }


        private List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
        {
            ArenaLayout layout = player.Match.Map.Layout;

            var path = new List<Vector2Int>();

            if (layout == null || !layout.Walkable(start) || !layout.Walkable(goal))
                return path;

            var queue = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();

            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();

                if (current == goal)
                    break;

                foreach (Vector2Int direction in ArenaLayout.Directions)
                {
                    Vector2Int next = current + direction;

                    if (!layout.Walkable(next) || !visited.Add(next))
                        continue;

                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (!visited.Contains(goal))
                return path;

            Vector2Int step = goal;

            while (step != start)
            {
                path.Add(step);
                step = previous[step];
            }

            path.Reverse();

            return path;
        }

        // Find the closest reachable item on the arena floor.
        private bool TryFindClosestItem(
            KingOfTheHillController hill,
            out Vector2 itemPosition,
            out int itemDistance)
        {
            itemPosition = Vector2.zero;
            itemDistance = int.MaxValue;

            if (hill.ItemSpawner == null)
                return false;

            Vector2Int start = Vector2Int.RoundToInt(player.Position);
            bool found = false;

            foreach (var entry in hill.ItemSpawner.AvailableItems)
            {
                FreeForAllItem item = entry.Key;

                if (item == null)
                    continue;

                FreeForAllItemPickup pickup =
                    item.GetComponent<FreeForAllItemPickup>();

                if (pickup != null && pickup.Collected)
                    continue;

                Vector2 position = entry.Value;
                Vector2Int goal = Vector2Int.RoundToInt(position);

                List<Vector2Int> path = FindPath(start, goal);

                // Ignore unreachable items.
                if (path.Count == 0 && start != goal)
                    continue;

                if (path.Count < itemDistance)
                {
                    itemDistance = path.Count;
                    itemPosition = position;
                    found = true;
                }
            }

            return found;
        }

        // Use an Eraser when the bot does not have a shield.
        private void TryUseEraser()
        {
            PlayerInventory inventory = GetComponent<PlayerInventory>();

            if (inventory == null || player.HasEraserShield)
                return;

            foreach (Item item in inventory.Items)
            {
                if (item is EraserItem eraser)
                {
                    eraser.Use(inventory);
                    return;
                }
            }
        }

        // Attack a nearby opponent with the Ruler.
        private void TryUseRuler()
        {
            PlayerInventory inventory = GetComponent<PlayerInventory>();

            if (inventory == null)
                return;

            foreach (Item item in inventory.Items)
            {
                if (item is not RulerItem ruler)
                    continue;

                foreach (ArenaParticipant opponent in player.Match.Players)
                {
                    if (opponent == player || !opponent.Alive || opponent.IsInvincible)
                        continue;

                    Vector2 difference = opponent.Position - player.Position;

                    // Only attack nearby opponents.
                    if (difference.magnitude > 1.5f || difference.magnitude < 0.01f)
                        continue;

                    // Only attack opponents in front of the bot.
                    if (Vector2.Angle(AttackDirection, difference) > 45f)
                        continue;

                    ruler.Use(inventory);
                    rulerCooldown = 1f;
                    return;
                }
            }
        }

        // Fire a Spitball when an opponent is directly ahead.
        private void TryUseSpitball()
        {
            PlayerInventory inventory = GetComponent<PlayerInventory>();

            if (inventory == null)
                return;

            foreach (Item item in inventory.Items)
            {
                if (item is not SpitballItem spitball)
                    continue;

                Vector2 direction = AttackDirection;

                foreach (ArenaParticipant opponent in player.Match.Players)
                {
                    if (opponent == player || !opponent.Alive || opponent.IsInvincible)
                        continue;

                    Vector2 difference = opponent.Position - player.Position;
                    float distance = difference.magnitude;

                    // Check the Spitball's range.
                    if (distance < 0.01f || distance > 8f)
                        continue;

                    // Only fire when facing the opponent.
                    float forwardDistance = Vector2.Dot(difference, direction);
                    float sidewaysDistance =
                        Mathf.Abs(difference.x * direction.y -
                                  difference.y * direction.x);

                    float hitRadius =
                        player.Match.Map.Settings.playerRadius + 0.25f;

                    if (forwardDistance <= 0f || sidewaysDistance > hitRadius)
                        continue;

                    // Check for obstacles between the bot and opponent.
                    bool blocked = false;

                    for (float d = 0.1f; d < distance; d += 0.1f)
                    {
                        Vector2 point = player.Position + direction * d;

                        if (!player.Match.Map.Layout.Fits(point, 0.25f))
                        {
                            blocked = true;
                            break;
                        }
                    }

                    if (blocked)
                        continue;

                    spitball.Use(inventory);
                    spitballCooldown = 1.5f;
                    return;
                }
            }
        }

        // Move toward a nearby opponent when holding a Ruler.
        private bool TryChaseWithRuler()
        {
            PlayerInventory inventory = GetComponent<PlayerInventory>();

            if (inventory == null)
                return false;

            bool hasRuler = false;

            foreach (Item item in inventory.Items)
            {
                if (item is RulerItem)
                {
                    hasRuler = true;
                    break;
                }
            }

            if (!hasRuler)
                return false;

            ArenaParticipant closest = null;
            float closestDistance = 4f;

            foreach (ArenaParticipant opponent in player.Match.Players)
            {
                if (opponent == player || !opponent.Alive)
                    continue;

                float distance = Vector2.Distance(player.Position, opponent.Position);

                if (distance < closestDistance)
                {
                    closest = opponent;
                    closestDistance = distance;
                }
            }

            if (closest == null)
                return false;

            // Approach the opponent using normal movement.
            SetTarget(closest.Position);
            return true;
        }

        // Find a nearby opponent when holding a Spitball.
        private bool TryFindSpitballTarget(out ArenaParticipant target)
        {
            target = null;

            PlayerInventory inventory = GetComponent<PlayerInventory>();

            if (inventory == null)
                return false;

            bool hasSpitball = false;

            foreach (Item item in inventory.Items)
            {
                if (item is SpitballItem)
                {
                    hasSpitball = true;
                    break;
                }
            }

            if (!hasSpitball)
                return false;

            float closestDistance = 8f;

            foreach (ArenaParticipant opponent in player.Match.Players)
            {
                if (opponent == player || !opponent.Alive)
                    continue;

                float distance =
                    Vector2.Distance(player.Position, opponent.Position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    target = opponent;
                }
            }

            return target != null;
        }

        // Check whether the bot is inside the hill.
        private bool IsInsideHill(KingOfTheHillController hill)
        {
            if (hill.ActiveHill == null)
                return false;

            float distance = Vector2.Distance(
                player.Position,
                hill.ActiveHill.Center
            );

            return distance <= hill.ActiveHill.Radius;
        }

        // Find a clear firing position inside the hill.
        private bool TryFindSpitballPosition(
            KingOfTheHillController hill,
            ArenaParticipant opponent,
            out Vector2 firingPosition)
        {
            firingPosition = Vector2.zero;

            ArenaLayout layout = player.Match.Map.Layout;
            Vector2 center = hill.ActiveHill.Center;
            float radius = hill.ActiveHill.Radius;

            Vector2Int start = Vector2Int.RoundToInt(player.Position);

            int shortestPath = int.MaxValue;
            bool found = false;

            foreach (Vector2Int cell in layout.Cells())
            {
                Vector2 position = cell;

                // Stay inside the hill.
                if (Vector2.Distance(position, center) > radius - 0.3f)
                    continue;

                if (!layout.Fits(position, player.Match.Map.Settings.playerRadius))
                    continue;

                Vector2 difference = opponent.Position - position;

                // Keep a useful distance from the opponent.
                if (difference.magnitude < 2f || difference.magnitude > 7.5f)
                    continue;

                // Spitballs travel along one of four grid directions.
                Vector2 direction;

                if (Mathf.Abs(difference.x) < 0.5f)
                    direction = difference.y > 0 ? Vector2.up : Vector2.down;
                else if (Mathf.Abs(difference.y) < 0.5f)
                    direction = difference.x > 0 ? Vector2.right : Vector2.left;
                else
                    continue;

                // Check whether an obstacle blocks the shot.
                bool blocked = false;

                for (float distance = 0.25f;
                     distance < difference.magnitude;
                     distance += 0.25f)
                {
                    Vector2 point = position + direction * distance;

                    if (!layout.Fits(point, 0.25f))
                    {
                        blocked = true;
                        break;
                    }
                }

                if (blocked)
                    continue;

                // Find a path that makes the bot approach while facing the opponent.
                List<Vector2Int> path = FindPath(start, cell);

                if (path.Count == 0 && start != cell)
                    continue;

                // Check the direction of the final movement.
                Vector2 finalMovement = Vector2.zero;

                if (path.Count > 0)
                {
                    Vector2 previousPosition =
                        path.Count > 1 ? path[path.Count - 2] : player.Position;

                    finalMovement = position - previousPosition;
                }

                // Prefer paths that end facing the opponent.
                bool facesOpponent =
                    finalMovement.sqrMagnitude > 0.01f &&
                    Vector2.Dot(finalMovement.normalized, direction) > 0.9f;

                if (!facesOpponent)
                    continue;

                if (path.Count < shortestPath)
                {
                    shortestPath = path.Count;
                    firingPosition = position;
                    found = true;
                }
            }

            return found;
        }

        // Check whether this bot is holding the potato.
        private bool IsHoldingPotato(HotPotatoController hotPotato)
        {
            return hotPotato.Holder == player;
        }

        // Chase the nearest player who can receive the potato.
        private void ChasePotatoTarget(HotPotatoController hotPotato)
        {
            ArenaParticipant closest = null;
            float closestDistance = float.MaxValue;

            foreach (ArenaParticipant opponent in hotPotato.Players)
            {
                // Ignore eliminated and shielded players.
                if (opponent == player || !opponent.Alive || opponent.Shield)
                    continue;

                float distance = Vector2.Distance(
                    player.Position,
                    opponent.Position
                );

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = opponent;
                }
            }

            if (closest != null)
            {
                // Use the existing BFS movement system.
                SetTarget(closest.Position);
            }
            else
            {
                ClearTarget();
            }
        }

        // Press Tag while holding the potato.
        private void TryPassPotato(HotPotatoController hotPotato)
        {
            if (hotPotato.Holder != player || hotPotato.BetweenRounds)
            {
                input.SetBotTag(false);
                return;
            }

            // The game mode checks distance, shields, obstacles and cooldown.
            input.SetBotTag(true);
        }

        // Find a reachable position away from the potato holder.
        private void RunFromPotatoHolder(HotPotatoController hotPotato)
        {
            ArenaParticipant holder = hotPotato.Holder;

            if (holder == null || !holder.Alive)
            {
                ClearTarget();
                return;
            }

            ArenaLayout layout = player.Match.Map.Layout;
            Vector2Int start = Vector2Int.RoundToInt(player.Position);

            Vector2 bestPosition = player.Position;
            float bestDistance = -1f;

            // Search for reachable tiles away from the holder.
            foreach (Vector2Int cell in layout.Cells())
            {
                if (!layout.Fits(cell, player.Match.Map.Settings.playerRadius))
                    continue;

                List<Vector2Int> path = FindPath(start, cell);

                if (path.Count == 0 && start != cell)
                    continue;

                float distance = Vector2.Distance(cell, holder.Position);

                // Prefer positions farther from the holder.
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestPosition = cell;
                }
            }

            SetTarget(bestPosition);
        }

        // Stop moving toward the current destination.
        public void ClearTarget()
        {
            hasTarget = false;
            input.SetBotMovement(Vector2.zero);
        }
    }
}
