using System;

namespace Escape4Now.Player
{
    //Remembers whether this player has Freeze or Lucky Roll waiting to be used.
    public sealed class PlayerEventState
    {
        private bool frozen;
        private bool nextLuckyRoll;
        private bool luckyRoll;

        public bool HasLuckyRoll => luckyRoll;

        //Skips the next turn. Getting Freeze again does not add extra skipped turns.
        public void GiveFreeze() { frozen = true; }

        //Saves Lucky Roll for the next turn the player can take. Extra Lucky Rolls do not stack.
        public void GiveLuckyRoll() { nextLuckyRoll = true; }

        //Returns false to skip a frozen turn; otherwise makes any saved Lucky Roll ready.
        public bool BeginTurn()
        {
            luckyRoll = false;
            if (frozen)
            {
                frozen = false;
                return false;
            }
            luckyRoll = nextLuckyRoll;
            nextLuckyRoll = false;
            return true;
        }

        //Accepts dice from 1 to 6. Lucky Roll keeps the higher roll and is then removed.
        public int UseRoll(int first, int second)
        {
            if (first < 1 || first > 6 || second < 1 || second > 6)
            {
                throw new ArgumentOutOfRangeException("Dice must be between 1 and 6.");
            }
            int movement = luckyRoll ? Math.Max(first, second) : first;
            luckyRoll = false;
            return movement;
        }
    }
}
