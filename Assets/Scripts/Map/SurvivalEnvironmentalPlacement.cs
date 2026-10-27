// Builds connected groups from tiles that have already passed the outdoor placement safety checks.

using System.Collections.Generic;
using UnityEngine;

namespace Escape4Now.Map
{
    public static class SurvivalEnvironmentalPlacement
    {
        // Reject blocked or reserved ground, with an extra option to protect the main routes.
        public static bool IsEligible(SurvivalMapLayout map, SurvivalObjectiveLayout objectives, Vector2Int cell, bool protectRoutes)
        {
            return map.IsWalkable(cell.x, cell.y) && !objectives.IsReserved(cell)
                && (!protectRoutes || !map.IsProtectedRoute(cell.x, cell.y));
        }
        // Grow through neighboring allowed cells so the result is one connected region.
        public static List<Vector2Int> Patch(Vector2Int center, List<Vector2Int> candidates, int count)
        {
            var allowed = new HashSet<Vector2Int>(candidates);
            var result = new List<Vector2Int>(); var pending = new Queue<Vector2Int>();
            if (!allowed.Remove(center)) return result;
            pending.Enqueue(center);
            while (pending.Count > 0 && result.Count < count)
            {
                var cell = pending.Dequeue(); result.Add(cell);
                foreach (var step in SurvivalObjectiveLayout.Steps)
                    if (allowed.Remove(cell + step)) pending.Enqueue(cell + step);
            }
            return result;
        }
        // Mix closeness to the starting cell with seeded randomness to avoid the same box-shaped growth each time.
        public static List<Vector2Int> NaturalPatch(Vector2Int center,List<Vector2Int> candidates,int count,System.Random random)
        {
            var allowed=new HashSet<Vector2Int>(candidates);
            var result=new List<Vector2Int>();var frontier=new List<Vector2Int>();
            if(!allowed.Remove(center))return result;
            frontier.Add(center);
            while(frontier.Count>0&&result.Count<count)
            {
                int chosen=0;double best=double.MaxValue;
                for(int i=0;i<frontier.Count;i++)
                {
                    double score=(frontier[i]-center).sqrMagnitude+random.NextDouble()*2;
                    if(score<best){best=score;chosen=i;}
                }
                var cell=frontier[chosen];frontier.RemoveAt(chosen);result.Add(cell);
                foreach(var step in SurvivalObjectiveLayout.Steps)if(allowed.Remove(cell+step))frontier.Add(cell+step);
            }
            return result;
        }
    }
}
