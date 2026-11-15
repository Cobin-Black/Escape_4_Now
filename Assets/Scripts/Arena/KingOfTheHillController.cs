
using Escape4Now.GameModes;
using UnityEngine;
using Escape4Now.Items;
using System.Collections.Generic;

namespace Escape4Now.Arena
{
    // King of the Hill manages scoring, hill control, and respawning.
    public sealed class KingOfTheHillController : ArenaMatch
    {
        [Header("King of the Hill Rules")]
        [SerializeField, TextArea]
        private string objective = "Control the hill and reach 100 points to win.";

        [SerializeField] private int scoreToWin = 100;
        [SerializeField] private float hillDuration = 30f;
        [SerializeField] private float respawnDuration = 10f;

        [Header("Item Spawning")]
        [SerializeField] private FreeForAllItemSpawner itemSpawner;

        [Header("Hill")]
        [SerializeField] private KingOfTheHillZone hillPrefab;

        private KingOfTheHillZone activeHill;
        private float hillTimer;
        private readonly Dictionary<ArenaParticipant, int> scores = new();
        private readonly Dictionary<ArenaParticipant, float> scoringTimers = new();
        private readonly Dictionary<ArenaParticipant, float> respawnTimers = new();

        // Temporarily retain the existing enum value until we update GameModeType.
        public override GameModeType Type => GameModeType.FreeForAll;
        public override string Objective => objective;
        public int ScoreToWin => scoreToWin;

        // Spawn the existing arena items.
        protected override void BeginRules()
        {
            scores.Clear();
            scoringTimers.Clear();
            respawnTimers.Clear();

            foreach (ArenaParticipant participant in Players)
            {
                scores[participant] = 0;
                scoringTimers[participant] = 0f;
            }

            if (itemSpawner != null)
                itemSpawner.SpawnItems(this);

            if (activeHill != null)
                Destroy(activeHill.gameObject);

            activeHill = null;

            if (hillPrefab != null)
            {
                activeHill = Instantiate(hillPrefab, transform);
                activeHill.Initialize(Map);
                hillTimer = hillDuration;
            }
        }

        // Start the respawn countdown when a player is eliminated.
        protected override void ParticipantEliminated(ArenaParticipant player)
        {
            respawnTimers[player] = respawnDuration;
            scoringTimers[player] = 0f;

            var inventory = player.GetComponent<PlayerInventory>();

            if (inventory != null)
                inventory.ClearArenaInventory();
        }

        public int GetScore(ArenaParticipant player)
        {
            return scores.TryGetValue(player, out int score) ? score : 0;
        }

        // Get the remaining respawn time for a player.
        public int GetRespawnSeconds(ArenaParticipant player)
        {
            if (respawnTimers.TryGetValue(player, out float remaining))
                return Mathf.CeilToInt(remaining);

            return 0;
        }

        // Count down eliminated players and respawn them.
        private void UpdateRespawns(float dt)
        {
            foreach (ArenaParticipant player in Players)
            {
                if (!respawnTimers.TryGetValue(player, out float remaining))
                    continue;

                remaining -= dt;

                if (remaining > 0f)
                {
                    respawnTimers[player] = remaining;
                    continue;
                }

                if (!TryFindRespawnPosition(player, out Vector2 spawnPosition))
                {
                    respawnTimers[player] = 1f;
                    continue;
                }

                player.Respawn(spawnPosition);
                respawnTimers.Remove(player);
            }
        }

        // Find a random valid respawn location.
        private bool TryFindRespawnPosition(ArenaParticipant player, out Vector2 position)
        {
            List<Vector2Int> validPositions = new List<Vector2Int>();

            for (int x = 0; x < Map.Layout.Width; x++)
            {
                for (int y = 0; y < Map.Layout.Height; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    if (!Map.Layout.Walkable(cell))
                        continue;

                    if (!CanOccupy(player, cell))
                        continue;

                    validPositions.Add(cell);
                }
            }

            if (validPositions.Count == 0)
            {
                position = Vector2.zero;
                return false;
            }

            position = validPositions[Map.Random.Next(validPositions.Count)];
            return true;
        }

        // Allow players to continue using their existing items.
        protected override void Tick(float dt)
        {
            UpdateRespawns(dt);

            foreach (ArenaParticipant participant in Players)
            {
                if (!participant.Alive || !participant.Input.UseItemPressed)
                    continue;

                var inventory = participant.GetComponent<PlayerInventory>();

                if (inventory != null)
                    inventory.UseFirstItem();
            }

            if (activeHill == null)
                return;

            // Award one point for every full second inside the hill.
            foreach (ArenaParticipant participant in Players)
            {
                if (!activeHill.Contains(participant))
                {
                    scoringTimers[participant] = 0f;
                    continue;
                }

                scoringTimers[participant] += dt;

                while (scoringTimers[participant] >= 1f)
                {
                    scoringTimers[participant] -= 1f;
                    scores[participant]++;

                    participant.UpdateScoreLabel(scores[participant]);

                    Debug.Log(participant.name + " Score: " + scores[participant]);

                    if (scores[participant] >= scoreToWin)
                    {
                        EndMatch(participant.name + " wins King of the Hill!", participant);
                        return;
                    }
                }
            }

            // Relocate the hill every 30 seconds.
            hillTimer -= dt;

            if (hillTimer <= 0)
            {
                activeHill.Relocate();
                hillTimer = hillDuration;

                // Reset partial scoring progress after relocation.
                foreach (ArenaParticipant participant in Players)
                    scoringTimers[participant] = 0f;
            }
        }
    }
}
