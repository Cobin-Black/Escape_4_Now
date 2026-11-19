using System;
using System.Collections.Generic;
using System.Linq;
using Escape4Now.Arena;
using Escape4Now.Items;
using UnityEngine;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // King of the Hill adds hill location, scores, respawns, and items to the shared arena state.
    public sealed class OnlineKingOfTheHillMatch : OnlineArenaMatch
    {
        private KingOfTheHillController Hill => (KingOfTheHillController)Arena;
        private readonly Dictionary<int, SpitballProjectile> projectiles = new();
        private readonly int[] shownSwings = new int[4];

        [Serializable]
        private sealed class HillSnapshot : Snapshot
        {
            public bool hillActive;
            public Vector2 hillCenter;
            public FreeForAllItemSpawner.OnlinePickup[] pickups;
            public Projectile[] projectiles;
        }

        [Serializable]
        private sealed class Projectile
        {
            public int id;
            public Vector3 position;
        }

        // Send the active hill, floor pickups and projectile positions alongside the arena state.
        protected override Snapshot CaptureMode()
        {
            Vector2? center = Hill.HillCenter;
            return new HillSnapshot
            {
                hillActive = center.HasValue,
                hillCenter = center.GetValueOrDefault(),
                pickups = Hill.Items?.CaptureOnlinePickups(),
                projectiles = FindObjectsByType<SpitballProjectile>(FindObjectsSortMode.None)
                    .Select(projectile => new Projectile
                    {
                        id = projectile.GetInstanceID(),
                        position = projectile.transform.position
                    }).ToArray()
            };
        }

        protected override Snapshot ReadSnapshot(string payload, int count) => JsonUtility.FromJson<HillSnapshot>(payload);

        // Send this player's score, respawn, shield, item charges and latest ruler swing.
        protected override void CapturePlayer(ArenaParticipant player, Actor actor)
        {
            actor.score = Hill.GetScore(player);
            actor.respawn = Hill.GetRespawnSeconds(player);
            actor.shieldBlocks = player.EraserBlocksRemaining;
            actor.swing = player.OnlineSwingCount;
            actor.swingPosition = player.OnlineSwingPosition;
            actor.swingDirection = player.OnlineSwingDirection;
            if (player.Character.Inventory.Items.FirstOrDefault() is FreeForAllItem item)
            {
                actor.itemType = (int)item.ItemType;
                actor.itemUses = item.UsesRemaining;
                actor.itemMaxUses = item.MaxUses;
            }
        }

        // Update the guest HUD and play new attack cues without repeating host damage checks.
        protected override void ApplyPlayer(ArenaParticipant player, Actor actor)
        {
            player.OnlineItemType = actor.itemType;
            player.OnlineItemUses = actor.itemUses;
            player.OnlineItemMaxUses = actor.itemMaxUses;
            player.ActivateEraserShield(actor.shieldBlocks);
            Hill.ApplyOnlineScore(player, actor.score, actor.respawn);
            if (actor.swing > shownSwings[player.Slot])
            {
                shownSwings[player.Slot] = actor.swing;
                Hill.Items?.FindPrefab<RulerItem>()?.ShowSwing(actor.swingPosition, actor.swingDirection);
            }
        }

        // Apply the host's hill, item pickups and projectile visuals to the shared local components.
        protected override void ApplyMode(Snapshot snapshot)
        {
            var state = (HillSnapshot)snapshot;
            Hill.ApplyOnlineHill(state.hillActive, state.hillCenter);
            Hill.Items?.ApplyOnlinePickups(state.pickups);
            ApplyProjectiles(state.projectiles);
        }

        // Only the host runs collision. Guests move copies of the same projectile prefab.
        private void ApplyProjectiles(Projectile[] states)
        {
            var visible = new HashSet<int>();
            if (states != null)
                foreach (var state in states)
                {
                    if (state == null) continue;
                    visible.Add(state.id);
                    if (!projectiles.TryGetValue(state.id, out var projectile))
                    {
                        var prefab = Hill.Items?.FindPrefab<SpitballItem>()?.ProjectilePrefab;
                        if (prefab == null) continue;
                        projectile = Instantiate(prefab, state.position, Quaternion.identity, transform);
                        projectile.enabled = false;
                        var collider = projectile.GetComponent<Collider2D>();
                        if (collider != null) collider.enabled = false;
                        projectiles.Add(state.id, projectile);
                    }
                    projectile.transform.position = state.position;
                }
            foreach (int id in projectiles.Keys.ToArray())
            {
                if (visible.Contains(id)) continue;
                Destroy(projectiles[id].gameObject);
                projectiles.Remove(id);
            }
        }

    }
}
