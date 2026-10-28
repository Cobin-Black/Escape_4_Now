// Applies the player effects that Race and Survival share, so both modes use the same effect state.

using Escape4Now.Player;

namespace Escape4Now.Map
{
    public static class SharedEventEffects
    {
        // Reuse the same pending player effects in both modes; each mode still controls placement and timing.
        public static void Apply(PlayerCharacter player, MapEventType type)
        {
            switch (type)
            {
                case MapEventType.LuckyRoll:
                    player.EventState.GiveLuckyRoll();
                    break;
                case MapEventType.BadRoll:
                    player.EventState.GiveBadRoll();
                    break;
                case MapEventType.SecondChance:
                    player.EventState.GiveSecondChance();
                    break;
                case MapEventType.Freeze:
                    player.EventState.GiveFreeze();
                    break;
            }
        }
    }
}
