using System;

namespace Escape4Now.TurnSystem
{
    //Turn positions refer to stable participant slots, never renamed or reordered players.
    internal static class PlayerTurnOrder
    {
        public static int[] Create(int playerCount, int seed, bool randomize = true)
        {
            if (playerCount < 1) throw new ArgumentOutOfRangeException(nameof(playerCount));
            var order = new int[playerCount];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            if (!randomize) return order;

            //A separate seeded stream leaves map, dice, item, and event randomness untouched.
            var random = new Random(unchecked(seed ^ 0x754E12B));
            for (int i = order.Length - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                int saved = order[i]; order[i] = order[other]; order[other] = saved;
            }
            return order;
        }

        public static bool IsValid(int[] order, int playerCount)
        {
            if (order == null || order.Length != playerCount) return false;
            var seen = new bool[playerCount];
            foreach (int slot in order)
            {
                if (slot < 0 || slot >= playerCount || seen[slot]) return false;
                seen[slot] = true;
            }
            return true;
        }
    }
}
