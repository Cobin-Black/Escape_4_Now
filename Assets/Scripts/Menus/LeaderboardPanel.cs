using System.Collections.Generic;
using Escape4Now.Accounts;
using UnityEngine;
using static Escape4Now.Menus.ArcadeMenuKit;

namespace Escape4Now.Menus
{
    //The main menu's leaderboard: one tab per mode, showing wins, losses, win rate, fastest time,
    //and finally how many of the wins were online. Online games already count in the wins, losses and win rate.
    public sealed class LeaderboardPanel
    {
        private const int Rows = 10;
        private const float RowHeight = 36f;
        private static readonly string[] Boards = { AccountStats.Race, AccountStats.FreeForAll, AccountStats.HotPotato, AccountStats.MonsterRole, AccountStats.Survivor };
        private static readonly string[] Tabs = { "RACE", AccountStats.ModeName(AccountStats.FreeForAll), "HOT POTATO", "AS MONSTER", "AS SURVIVOR" };
        //Column positions from the left of the panel: #, PLAYER, WINS, LOSSES, WIN RATE, FASTEST, ONLINE WINS.
        private static readonly float[] Columns = { 0f, 40f, 240f, 305f, 390f, 475f, 560f };
        private const int FastestColumn = 5;
        private const int OnlineColumn = 6;
        private const string OnlineNote = "Wins, losses and win rate include online games.";

        private int board;
        private bool requested;
        private bool loading;
        private int loadNumber;
        private List<BoardEntry> entries;

        //Only Race and Monster matches have a timer.
        private bool ShowsFastest => Boards[board] != AccountStats.FreeForAll && Boards[board] != AccountStats.HotPotato;

        public void Draw(Rect inner, GUIStyle title, Color heading, Color accent)
        {
            Label(new Rect(inner.x, inner.y, 480f, 44f), "LEADERBOARD", title, heading);
            if (!AccountService.IsSignedIn)
            {
                Message(inner, "Log in to see the leaderboards.");
                return;
            }
            if (!requested) Load();

            float tabWidth = (inner.width - 8f * (Tabs.Length - 1)) / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
            {
                if (TabButton(new Rect(inner.x + i * (tabWidth + 8f), inner.y + 54f, tabWidth, 34f), Tabs[i], i == board, accent) && i != board)
                {
                    board = i;
                    Load();
                }
            }

            if (loading) Message(inner, "Loading...");
            else if (entries == null) Message(inner, "Could not load the leaderboard. Check your internet connection.");
            else if (entries.Count == 0) Message(inner, "Nobody has played this mode yet.");
            else DrawTable(inner, accent);
        }

        private void DrawTable(Rect inner, Color accent)
        {
            float y = inner.y + 100f;
            string[] headers = { "#", "PLAYER", "WINS", "LOSSES", "WIN RATE", "FASTEST", "ONLINE WINS" };
            GUIStyle headerStyle = Text(MonoBoldFont, 12);
            for (int i = 0; i < headers.Length; i++)
                if (i != FastestColumn || ShowsFastest) Label(new Rect(inner.x + ColumnX(i), y, 120f, 20f), headers[i], headerStyle, Muted);

            GUIStyle rowStyle = Text(MonoFont, 15);
            for (int i = 0; i < entries.Count; i++)
            {
                BoardEntry entry = entries[i];
                Rect row = new Rect(inner.x, y + 24f + i * RowHeight, inner.width, RowHeight - 4f);
                bool me = entry.Name == AccountService.DisplayName;
                Fill(row, me ? WithAlpha(accent, 0.2f) : PanelDark);
                int played = entry.Wins + entry.Losses;
                string[] cells =
                {
                    entry.Rank.ToString(), entry.Name, entry.Wins.ToString(), entry.Losses.ToString(),
                    played > 0 ? Mathf.RoundToInt(entry.Wins * 100f / played) + "%" : "--",
                    entry.Fastest > 0f ? FormatTime(entry.Fastest) : "--",
                    //"5 of 12" shows online wins are part of the wins total, not extra wins.
                    entry.OnlineWins + " of " + entry.Wins
                };
                for (int c = 0; c < cells.Length; c++)
                    if (c != FastestColumn || ShowsFastest) Label(new Rect(row.x + ColumnX(c) + 6f, row.y, 120f, row.height), cells[c], rowStyle, Color.white);
            }
            //Answers "does online change the ratio?" right under the table.
            float noteY = y + 24f + entries.Count * RowHeight;
            Label(new Rect(inner.x, noteY, inner.width, 18f), OnlineNote, Text(MonoFont, 11), Muted);
        }

        //Online wins move into the fastest-time position on tabs without a timer, so there is no gap.
        private float ColumnX(int column) => column == OnlineColumn && !ShowsFastest ? Columns[FastestColumn] : Columns[column];

        private static void Message(Rect inner, string text)
        {
            Label(new Rect(inner.x, inner.y + 130f, inner.width, 60f), text, Text(MonoFont, 16, TextAnchor.UpperCenter, true), Soft);
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.CeilToInt(seconds);
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        private async void Load()
        {
            requested = true;
            loading = true;
            int number = ++loadNumber;
            List<BoardEntry> loaded = await AccountStats.LoadBoardAsync(Boards[board], Rows);
            //A newer tab was picked while this one loaded.
            if (number != loadNumber) return;
            entries = loaded;
            loading = false;
        }
    }
}
