using System;

namespace Escape4Now.Player
{
    //Stores one-use effects separately for each player.
    public sealed class PlayerEventState
    {
        private bool frozen;
        private bool nextSpeedBoost;
        private bool nextLuckyRoll;
        private bool speedBoost;
        private bool luckyRoll;

        public bool HasLuckyRoll => luckyRoll;

        //Saves one skipped turn. Repeated freezes do not add more turns.
        public void GiveFreeze() { frozen = true; }

        //Saves one movement bonus for the next playable turn.
        public void GiveSpeedBoost() { nextSpeedBoost = true; }

        //Saves one double roll for the next playable turn.
        public void GiveLuckyRoll() { nextLuckyRoll = true; }

        //Skips one frozen turn or prepares the bonuses for a normal turn.
        public bool BeginTurn()
        {
            speedBoost = false;
            luckyRoll = false;
            if (frozen)
            {
                frozen = false;
                return false;
            }
            speedBoost = nextSpeedBoost;
            luckyRoll = nextLuckyRoll;
            nextSpeedBoost = false;
            nextLuckyRoll = false;
            return true;
        }

        //Checks both dice and uses the saved bonuses only once.
        public int UseRoll(int first, int second)
        {
            if (first < 1 || first > 6 || second < 1 || second > 6)
            {
                throw new ArgumentOutOfRangeException("Dice must be between 1 and 6.");
            }
            int movement = luckyRoll ? Math.Max(first, second) : first;
            if (speedBoost) movement += 2;
            speedBoost = false;
            luckyRoll = false;
            return movement;
        }
    }
}
