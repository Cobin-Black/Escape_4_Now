using System;
using System.Collections.Generic;
using Escape4Now.Accounts;
using UnityEngine;
using static Escape4Now.Menus.ArcadeMenuKit;

namespace Escape4Now.Menus
{
    //The Account section's match history: the signed-in player's recent games, newest first.
    public sealed class MatchHistoryPanel
    {
        private const int RowsPerPage = 8;
        private const float RowHeight = 38f;
        //Column positions from the left of the panel.
        private const float ModeColumn = 130f;
        private const float PlayersColumn = 430f;
        private const float ResultColumn = 540f;
        //The page buttons share the bottom row with the log out button, so they stay on the left.
        private const float PageButtonWidth = 90f;
        private const float PageLabelWidth = 130f;

        private bool requested;
        private bool loading;
        private int page;
        private List<MatchRecord> matches;

        //The area runs from under the Account title to the bottom of the panel.
        public void Draw(Rect area, Color accent)
        {
            Label(new Rect(area.x, area.y, area.width, 24f), "MATCH HISTORY", Text(Bungee, 16), Color.white);
            if (!AccountService.IsSignedIn)
            {
                Message(area, "Log in to keep a match history.");
                return;
            }
            if (!requested) Load();

            if (loading) Message(area, "Loading...");
            else if (matches == null) Message(area, "Could not load your matches. Check your internet connection.");
            else if (matches.Count == 0) Message(area, "Your finished matches will show up here.");
            else DrawPage(area, accent);
        }

        private void DrawPage(Rect area, Color accent)
        {
            GUIStyle headerStyle = Text(MonoBoldFont, 12);
            float y = area.y + 30f;
            Label(new Rect(area.x, y, 120f, 20f), "DATE", headerStyle, Muted);
            Label(new Rect(area.x + ModeColumn, y, 280f, 20f), "GAME MODE", headerStyle, Muted);
            Label(new Rect(area.x + PlayersColumn, y, 100f, 20f), "PLAYERS", headerStyle, Muted);
            Label(new Rect(area.x + ResultColumn, y, 100f, 20f), "RESULT", headerStyle, Muted);

            GUIStyle rowStyle = Text(MonoFont, 14);
            GUIStyle resultStyle = Text(MonoBoldFont, 14);
            int first = page * RowsPerPage;
            int last = Mathf.Min(first + RowsPerPage, matches.Count);
            for (int i = first; i < last; i++)
            {
                MatchRecord match = matches[i];
                Rect row = new Rect(area.x, y + 24f + (i - first) * RowHeight, area.width, RowHeight - 4f);
                Fill(row, PanelDark);
                string date = DateTimeOffset.FromUnixTimeSeconds(match.playedAt).LocalDateTime.ToString("MMM d, yyyy");
                Label(new Rect(row.x + 6f, row.y, 120f, row.height), date, rowStyle, Soft);
                Label(new Rect(row.x + ModeColumn, row.y, 290f, row.height), AccountStats.ModeName(match.mode), rowStyle, Color.white);
                Label(new Rect(row.x + PlayersColumn, row.y, 100f, row.height), match.players.ToString(), rowStyle, Color.white);
                Label(new Rect(row.x + ResultColumn, row.y, 100f, row.height), match.won ? "WIN" : "LOSS", resultStyle, match.won ? accent : Red);
            }

            int pages = (matches.Count + RowsPerPage - 1) / RowsPerPage;
            if (pages <= 1) return;
            float buttonsY = area.yMax - 50f;
            if (TabButton(new Rect(area.x, buttonsY, PageButtonWidth, 36f), "NEWER", false, accent) && page > 0) page--;
            Label(new Rect(area.x + PageButtonWidth, buttonsY, PageLabelWidth, 36f), "PAGE " + (page + 1) + " OF " + pages, Text(MonoFont, 13, TextAnchor.MiddleCenter), Muted);
            if (TabButton(new Rect(area.x + PageButtonWidth + PageLabelWidth, buttonsY, PageButtonWidth, 36f), "OLDER", false, accent) && page < pages - 1) page++;
        }

        private static void Message(Rect area, string text)
        {
            Label(new Rect(area.x, area.y + 120f, area.width, 60f), text, Text(MonoFont, 16, TextAnchor.UpperCenter, true), Soft);
        }

        private async void Load()
        {
            requested = true;
            loading = true;
            matches = await AccountStats.LoadHistoryAsync();
            loading = false;
        }
    }
}
