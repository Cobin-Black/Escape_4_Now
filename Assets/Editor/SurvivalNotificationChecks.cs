// Checks that ongoing messages follow their effects and that short messages stop aging while paused.

using System;
using Escape4Now.Map;
using Escape4Now.Player;
using UnityEditor;
using UnityEngine;

public static class SurvivalNotificationChecks
{
    public static void Run()
    {
        try
        {
            var feed = new SurvivalEventNotifications();
            var state = new PlayerEventState();
            state.GiveLuckyRoll();
            feed.Add("Lucky Roll", 4, () => state.HasLuckyEffect);
            Require(feed.Tick(20, 2, "+{0} active").Contains("Lucky Roll"), "Pending effects outlive the instant toast duration");
            state.BeginTurn();
            Require(feed.Tick(0, 2, "+{0} active").Contains("Lucky Roll"), "Starting a turn preserves an unconsumed effect");
            state.UseRoll(2, 5);
            Require(feed.Tick(0, 2, "+{0} active") == "", "Consuming the effect clears its notification");
            int turns = 3;
            feed.Add("Sprinklers", 4, () => turns > 0);
            feed.Add("Warp", 4);
            Require(feed.Tick(0, 2, "+{0} active").Contains("Warp"), "Pause does not age an instant notification");
            Require(feed.Tick(5, 2, "+{0} active") == "Sprinklers", "Instant feedback expires without removing the ongoing area event");
            turns = 0;
            Require(feed.Tick(0, 2, "+{0} active") == "", "Environmental expiry clears feedback");
            Debug.Log("SURVIVAL NOTIFICATION CHECKS PASSED: pending effects, consumption, concurrent area events, instant results and paused time.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("FAIL: " + message); }
}
