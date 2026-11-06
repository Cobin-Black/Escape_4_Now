using Escape4Now.GameModes;
using UnityEngine;

namespace Escape4Now.Arena
{
    // Free For All owns its last-player-standing rule; shared match setup stays in ArenaMatch.
    public sealed class FreeForAllController : ArenaMatch
    {
        [Header("Free For All Rules")]
        [SerializeField, TextArea]
        private string objective = "Be the last player standing.";
        public override GameModeType Type => GameModeType.FreeForAll;
        public override string Objective => objective;

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
