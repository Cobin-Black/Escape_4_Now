using System;
using System.Collections.Generic;

namespace Escape4Now.Map
{
    // Validate every destination before moving anyone. Grid and real-time modes supply their own safety rules.
    public static class SharedPositionEffects
    {
        public static bool TryPermute<T>(IReadOnlyList<T> positions, int[] destinations,
            Func<int,T,bool> canPlace, Action<int,T> place)
        {
            if (positions == null || destinations == null || positions.Count < 2 || positions.Count != destinations.Length)
                return false;
            var used = new HashSet<int>();
            var targets = new T[positions.Count];
            for (int i=0;i<positions.Count;i++)
            {
                int index=destinations[i];
                if (index<0 || index>=positions.Count || index==i || !used.Add(index)) return false;
                targets[i]=positions[index];
                if (!canPlace(i,targets[i])) return false;
            }
            for (int i=0;i<targets.Length;i++) place(i,targets[i]);
            return true;
        }
    }
}
