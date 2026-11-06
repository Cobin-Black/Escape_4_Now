using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.Leaderboards;
using UnityEngine;

namespace Escape4Now.Accounts
{
    //One player's result from a finished match. Slot is their lobby slot, where 0 is Player 1.
    //Seconds is how long they took to win, or 0 when the win has no time, such as in the arena modes.
    public readonly struct MatchStat
    {
        public readonly int Slot;
        public readonly string Mode;
        public readonly bool Won;
        public readonly int Players;
        public readonly float Seconds;
        public MatchStat(int slot, string mode, bool won, int players, float seconds = 0f)
        { Slot = slot; Mode = mode; Won = won; Players = players; Seconds = seconds; }
    }

    //One line of a player's match history, as stored in Cloud Save.
    [Serializable]
    public sealed class MatchRecord
    {
        public string mode;
        public bool won;
        public int players;
        public long playedAt;
    }

    //The extra columns stored with a leaderboard score.
    [Serializable]
    public sealed class BoardMetadata
    {
        public string name;
        public int wins;
        public int losses;
        public float fastest;
    }

    //One row of a leaderboard. Fastest is 0 when the player has no timed win.
    public sealed class BoardEntry
    {
        public int Rank;
        public string Name;
        public int Wins;
        public int Losses;
        public float Fastest;
    }

    //Saves match results online. Totals and history live in Cloud Save, and each leaderboard score
    //carries the player's username, losses and fastest time for the in-game leaderboard.
    public static class AccountStats
    {
        //Combined with "wins-" these are the leaderboard IDs set up on cloud.unity.com.
        public const string Race = "race";
        public const string FreeForAll = "freeforall";
        public const string HotPotato = "hotpotato";
        public const string MonsterRole = "monster-role";
        public const string Survivor = "survivor";

        private const string HistoryKey = "match-history";
        private const int MaxHistory = 50;
        private const string SavingMessage = "Saving stats...";
        private const string SavedMessage = "Stats saved.";
        private const string FailedMessage = "Stats could not be saved.";

        public static bool IsSaving { get; private set; }
        //Shown under the match result. Empty when nobody in the match has an account.
        public static string Status { get; private set; } = "";

        //Domain reload is off in this project, so values would otherwise carry over between Play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            IsSaving = false;
            Status = "";
        }

        public static string ModeName(string mode)
        {
            switch (mode)
            {
                case Race: return "RACE";
                case FreeForAll: return "FREE FOR ALL";
                case HotPotato: return "HOT POTATO";
                case MonsterRole: return "MONSTER - AS MONSTER";
                case Survivor: return "MONSTER - AS SURVIVOR";
                default: return "UNKNOWN";
            }
        }

        //Saves the results of every player with an account. Guests are left out here.
        public static void Report(IEnumerable<MatchStat> results)
        {
            List<MatchStat> stats = results.Where(stat => AccountService.IsLinked(stat.Slot)).OrderBy(stat => stat.Slot).ToList();
            Status = "";
            if (stats.Count > 0) _ = SaveAll(stats);
        }

        private static async Task SaveAll(List<MatchStat> stats)
        {
            IsSaving = true;
            Status = SavingMessage;
            bool saved = await AccountService.RunAsEachAccount(stats.Select(stat => stat.Slot), slot => Save(stats.First(stat => stat.Slot == slot)));
            Status = saved ? SavedMessage : FailedMessage;
            IsSaving = false;
        }

        //Must run while this player's account is the one signed in.
        private static async Task Save(MatchStat stat)
        {
            string winsKey = "wins-" + stat.Mode;
            string lossesKey = "losses-" + stat.Mode;
            string fastestKey = "fastest-" + stat.Mode;
            var saved = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { winsKey, lossesKey, fastestKey, HistoryKey });

            int wins = Read<int>(saved, winsKey) + (stat.Won ? 1 : 0);
            int losses = Read<int>(saved, lossesKey) + (stat.Won ? 0 : 1);
            float fastest = Read<float>(saved, fastestKey);
            if (stat.Won && stat.Seconds > 0f && (fastest <= 0f || stat.Seconds < fastest)) fastest = stat.Seconds;

            List<MatchRecord> history = Read<List<MatchRecord>>(saved, HistoryKey) ?? new List<MatchRecord>();
            history.Insert(0, new MatchRecord { mode = stat.Mode, won = stat.Won, players = stat.Players, playedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
            if (history.Count > MaxHistory) history.RemoveRange(MaxHistory, history.Count - MaxHistory);

            await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>
            {
                { winsKey, wins }, { lossesKey, losses }, { fastestKey, fastest }, { HistoryKey, history }
            });
            //Losses are sent too, which is why the leaderboards must use Keep Latest Score.
            var metadata = new BoardMetadata { name = AccountService.LinkedName(stat.Slot), wins = wins, losses = losses, fastest = fastest };
            await LeaderboardsService.Instance.AddPlayerScoreAsync(winsKey, wins, new AddPlayerScoreOptions { Metadata = metadata });
        }

        private static T Read<T>(Dictionary<string, Item> saved, string key) =>
            saved.TryGetValue(key, out Item item) ? item.Value.GetAs<T>() : default;

        //The signed-in player's matches, newest first. Returns null if they could not be loaded.
        public static async Task<List<MatchRecord>> LoadHistoryAsync()
        {
            try
            {
                var saved = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { HistoryKey });
                return Read<List<MatchRecord>>(saved, HistoryKey) ?? new List<MatchRecord>();
            }
            catch (Exception error)
            {
                Debug.LogWarning("Could not load the match history: " + error.Message);
                return null;
            }
        }

        //The top rows of a mode's leaderboard. Returns null if they could not be loaded.
        public static async Task<List<BoardEntry>> LoadBoardAsync(string mode, int rows)
        {
            try
            {
                var page = await LeaderboardsService.Instance.GetScoresAsync("wins-" + mode, new GetScoresOptions { Limit = rows, IncludeMetadata = true });
                return page.Results.Select(ToBoardEntry).ToList();
            }
            catch (Exception error)
            {
                Debug.LogWarning("Could not load the leaderboard: " + error.Message);
                return null;
            }
        }

        //Scores saved before the extra columns existed have no metadata, so they fall back to the basic score.
        private static BoardEntry ToBoardEntry(Unity.Services.Leaderboards.Models.LeaderboardEntry entry)
        {
            BoardMetadata extra = string.IsNullOrEmpty(entry.Metadata) ? new BoardMetadata() : JsonUtility.FromJson<BoardMetadata>(entry.Metadata);
            return new BoardEntry
            {
                Rank = entry.Rank + 1,
                Name = string.IsNullOrEmpty(extra.name) ? entry.PlayerName : extra.name,
                Wins = (int)entry.Score,
                Losses = extra.losses,
                Fastest = extra.fastest
            };
        }
    }
}
