using System.Collections.Generic;
using Escape4Now.Accounts;
using UnityEngine;
using static Escape4Now.Menus.ArcadeMenuKit;

namespace Escape4Now.Menus
{
    //The main menu's leaderboard: one tab per mode. Local and online games are shown apart, each with its own
    //wins, losses and win rate, so online games never mix into the local numbers. Race and Monster tabs add the fastest time.
    public sealed class LeaderboardPanel
    {
        private const int Rows = 10;
        private const float RowHeight = 34f;
        private static readonly string[] Boards = { AccountStats.Race, AccountStats.FreeForAll, AccountStats.HotPotato, AccountStats.MonsterRole, AccountStats.Survivor };
        private static readonly string[] Tabs = { "RACE", AccountStats.ModeName(AccountStats.FreeForAll), "HOT POTATO", "AS MONSTER", "AS SURVIVOR" };
        //Columns from the left of the panel: #, PLAYER, LOCAL W / L / WIN %, ONLINE W / L / WIN %, FASTEST.
        //The rank and name read left to right; every number is centered under its heading.
        private static readonly float[] ColumnX = { 6f, 40f, 260f, 304f, 350f, 424f, 468f, 514f, 581f };
        private static readonly float[] ColumnWidth = { 28f, 210f, 44f, 44f, 56f, 44f, 44f, 56f, 70f };
        private const int NameColumn = 1;
        private const int FastestColumn = 8;
        //Group headings above the two sets of columns.
        private const float LocalX = 258f, OnlineX = 422f, GroupWidth = 150f;
        private const string Note = "Ranked by all wins. Local and online games are counted separately.";

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
            GUIStyle headerStyle = Text(MonoBoldFont, 12);
            Group(new Rect(inner.x + LocalX, y, GroupWidth, 18f), "LOCAL", Muted, headerStyle);
            Group(new Rect(inner.x + OnlineX, y, GroupWidth, 18f), "ONLINE", Muted, headerStyle);
            y += 20f;
            string[] headers = { "#", "PLAYER", "W", "L", "WIN %", "W", "L", "WIN %", "FASTEST" };
            GUIStyle centeredHeader = Text(MonoBoldFont, 12, TextAnchor.MiddleCenter);
            for (int i = 0; i < headers.Length; i++)
                if (i != FastestColumn || ShowsFastest)
                    Label(Cell(inner.x, y, 20f, i), headers[i], i <= NameColumn ? headerStyle : centeredHeader, Muted);

            GUIStyle rowStyle = Text(MonoFont, 15);
            GUIStyle numberStyle = Text(MonoFont, 15, TextAnchor.MiddleCenter);
            for (int i = 0; i < entries.Count; i++)
            {
                BoardEntry entry = entries[i];
                Rect row = new Rect(inner.x, y + 24f + i * RowHeight, inner.width, RowHeight - 4f);
                bool me = entry.Name == AccountService.DisplayName;
                Fill(row, me ? WithAlpha(accent, 0.2f) : PanelDark);
                string[] cells =
                {
                    entry.Rank.ToString(), FitName(entry.Name, rowStyle, ColumnWidth[NameColumn]),
                    entry.LocalWins.ToString(), entry.LocalLosses.ToString(), Rate(entry.LocalWins, entry.LocalLosses),
                    entry.OnlineWins.ToString(), entry.OnlineLosses.ToString(), Rate(entry.OnlineWins, entry.OnlineLosses),
                    entry.Fastest > 0f ? FormatTime(entry.Fastest) : "--"
                };
                for (int c = 0; c < cells.Length; c++)
                    if (c != FastestColumn || ShowsFastest)
                        Label(Cell(row.x, row.y, row.height, c), cells[c], c <= NameColumn ? rowStyle : numberStyle, Color.white);
            }
            float noteY = y + 24f + entries.Count * RowHeight;
            Label(new Rect(inner.x, noteY, inner.width, 18f), Note, Text(MonoFont, 11), Muted);
        }

        private static Rect Cell(float left, float top, float height, int column) =>
            new Rect(left + ColumnX[column], top, ColumnWidth[column], height);

        //Accounts without a username get a generated name like "StrongBowingBoat#74201". The number is dropped,
        //and anything still too wide ends with "..." so it never runs into the numbers.
        private static string FitName(string name, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(name)) return "";
            int tag = name.LastIndexOf('#');
            if (tag > 0 && int.TryParse(name.Substring(tag + 1), out _)) name = name.Substring(0, tag);
            if (style.CalcSize(new GUIContent(name)).x <= width) return name;
            for (int length = name.Length - 1; length > 0; length--)
            {
                string shortened = name.Substring(0, length) + "...";
                if (style.CalcSize(new GUIContent(shortened)).x <= width) return shortened;
            }
            return name.Substring(0, 1);
        }

        //A heading with a thin line under it, spanning its three columns.
        private static void Group(Rect r, string text, Color color, GUIStyle style)
        {
            Label(new Rect(r.x, r.y, r.width, r.height - 3f), text, Text(style.font, style.fontSize, TextAnchor.MiddleCenter), color);
            Fill(new Rect(r.x + 4f, r.yMax - 2f, r.width - 8f, 1f), WithAlpha(color, 0.6f));
        }

        private static string Rate(int wins, int losses)
        {
            int played = wins + losses;
            return played > 0 ? Mathf.RoundToInt(wins * 100f / played) + "%" : "--";
        }

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
