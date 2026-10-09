using System;

namespace Escape4Now.Player
{
    //Remembers pending turn effects and temporary protection from direct player actions.
    public sealed class PlayerEventState
    {
        private bool frozen;
        private bool nextLuckyRoll;
        private bool luckyRoll;
        private bool nextBadRoll;
        private bool badRoll;
        public bool HasSecondChance { get; private set; }
        public bool HasRollBonus { get; private set; }
        public bool HasBadRoll => badRoll;

        public bool HasLuckyRoll => luckyRoll;
        public bool IsProtectedFromPlayers { get; private set; }

        //Used by successful direct actions and by the host's network state.
        internal void SetPlayerProtection(bool value) { IsProtectedFromPlayers = value; }

        //Skips the next turn. Getting Freeze again does not add extra skipped turns.
        public void GiveFreeze() { frozen = true; }

        //Saves Lucky Roll for the next turn the player can take. Extra Lucky Rolls do not stack.
        public void GiveLuckyRoll() { nextLuckyRoll = true; nextBadRoll = false; }

        //The newest roll effect replaces the opposite effect instead of stacking.
        public void GiveBadRoll() { nextBadRoll = true; nextLuckyRoll = false; }

        //These bonuses are saved until used. Receiving another copy does not stack.
        public void GiveSecondChance() { HasSecondChance = true; }
        public void GiveRollBonus() { HasRollBonus = true; }

        //Blocks one negative event, but never a global event.
        public bool BlockNegativeEvent()
        {
            if (!HasSecondChance) return false;
            HasSecondChance = false;
            return true;
        }

        //Returns false to skip a frozen turn; otherwise makes any saved Lucky Roll ready.
        public bool BeginTurn()
        {
            IsProtectedFromPlayers = false;
            luckyRoll = false;
            badRoll = false;
            if (frozen)
            {
                frozen = false;
                return false;
            }
            luckyRoll = nextLuckyRoll;
            nextLuckyRoll = false;
            badRoll = nextBadRoll;
            nextBadRoll = false;
            return true;
        }

        //Accepts dice from 1 to 6. Lucky Roll keeps the higher roll and is then removed.
        public int UseRoll(int first, int second)
        {
            if (first < 1 || first > 6 || second < 1 || second > 6)
            {
                throw new ArgumentOutOfRangeException("Dice must be between 1 and 6.");
            }
            int movement = luckyRoll ? Math.Max(first, second) : badRoll ? Math.Min(first, second) : first;
            luckyRoll = false;
            badRoll = false;
            if (HasRollBonus) movement++;
            HasRollBonus = false;
            return movement;
        }
    }
}
