using Escape4Now.GameModes;
using UnityEngine;
using Escape4Now.Items;

namespace Escape4Now.Arena
{
    // Free For All owns its last-player-standing rule; shared match setup stays in ArenaMatch.
    public sealed class FreeForAllController : ArenaMatch
    {
        [Header("Free For All Rules")]
        [SerializeField, TextArea]
        private string objective = "Be the last player standing.";
        [Header("Item Spawning")]
        [SerializeField] private FreeForAllItemSpawner itemSpawner;
        public override GameModeType Type => GameModeType.FreeForAll;
        public override string Objective => objective;

        // Spawn FreeForAll items after the arena and players are ready.
        protected override void BeginRules()
        {
            if (itemSpawner != null)
                itemSpawner.SpawnItems(this);
        }

        // Combat will call ArenaParticipant.Damage, Knockback or Eliminate. No attack rules are invented here.
        protected override void Tick(float dt)
        {
            foreach (ArenaParticipant participant in Players)
            {
                if (!participant.Alive || !participant.Input.UseItemPressed)
                    continue;

                var inventory = participant.GetComponent<Escape4Now.Items.PlayerInventory>();

                if (inventory != null)
                    inventory.UseFirstItem();
            }

            CheckLastPlayer();
        }
    }
}
