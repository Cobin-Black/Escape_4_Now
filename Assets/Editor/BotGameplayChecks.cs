using System;
using System.Collections;
using System.Collections.Generic;
using Escape4Now.Bots;
using Escape4Now.GameModes;
using Escape4Now.Multiplayer;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class BotGameplayChecks
{
    public static IEnumerator Run()
    {
        yield return null;
        int count = 4;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-schoolPlayers") count = int.Parse(args[i + 1]);
        var session = UnityEngine.Object.FindFirstObjectByType<LocalMultiplayerSession>();
        Require(session.Mode is Race_Gamemode && session.Mode.SupportsBots, "Race uses the merged mode and supports bots");
        Require(new SerializedObject(session).FindProperty("playerIsBot").arraySize == 4, "Bot defaults are editable in the Inspector");
        session.SetPlayerCount(count);
        for (int i = 0; i < count; i++) { session.SetBotPlayer(i, true); Require(session.IsBotPlayer(i), "Lobby selects bot slot"); }
        if (!Application.isBatchMode)
        {
            System.IO.Directory.CreateDirectory("Logs/BotPreviews");
            for (int i = 0; i < 10; i++) yield return null;
            ScreenCapture.CaptureScreenshot("Logs/BotPreviews/race-bot-lobby.png");
            for (int i = 0; i < 10; i++) yield return null;
        }
        Require(session.TryStartLocalMatch(count), "Bot match starts: " + session.Status);
        var turns = session.Mode.Turns;
        int[] order = turns.CopyTurnOrder();
        var initialPositions = new Dictionary<PlayerCharacter, Vector2Int>();
        foreach (PlayerCharacter player in session.Participants)
        {
            Require(player.IsBotControlled && player.GetComponent<BotController>().enabled, "Selected slot has the real BotController");
            initialPositions[player] = player.GridPosition;
            var settings = new SerializedObject(player);
            settings.FindProperty("moveSpeed").floatValue = 100f;
            settings.FindProperty("diceRollAnimationDuration").floatValue = .02f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        session.PauseGame();
        yield return null;
        float frozenTime = session.Mode.Timer.TimeRemaining;
        yield return new WaitForSecondsRealtime(.1f);
        Require(session.Mode.Timer.TimeRemaining == frozenTime, "Bot match clock respects pause");
        foreach (PlayerCharacter player in session.Participants)
            Require(player.GridPosition == initialPositions[player] && !player.HasRolled, "Bots cannot act while paused");
        session.ResumeGame();
        bool rolled = false, moved = false;
        double deadline = Time.realtimeSinceStartupAsDouble + 30;
        while (turns.TurnNumber < 2 && !LocalMultiplayerSession.MatchHasEnded && Time.realtimeSinceStartupAsDouble < deadline)
        {
            foreach (PlayerCharacter player in session.Participants)
            {
                rolled |= player.HasRolled;
                moved |= player.GridPosition != initialPositions[player];
            }
            yield return null;
        }
        Require(rolled && moved && (turns.TurnNumber >= 2 || LocalMultiplayerSession.MatchHasEnded), "Bots roll, move, and finish turns without human input");
        int[] repeatedOrder = turns.CopyTurnOrder();
        for (int i = 0; i < count; i++) Require(order[i] == repeatedOrder[i], "Bots retain the seeded turn order");
        //Use the teammate's shared Pause action; no mode-specific binding is introduced.
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            if (!LocalMultiplayerSession.MatchHasEnded)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                Require(LocalMultiplayerSession.GameplayPaused && Time.timeScale == 0f, "Existing Escape action pauses Race");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                Require(!LocalMultiplayerSession.GameplayPaused && Time.timeScale == 1f, "Existing Escape action resumes Race");
            }
        }
        finally { InputSystem.RemoveDevice(keyboard); }
        if (!Application.isBatchMode)
        {
            ScreenCapture.CaptureScreenshot("Logs/BotPreviews/race-bot-gameplay.png");
            for (int i = 0; i < 10; i++) yield return null;
        }
        Debug.Log("RACE BOT CHECKS PASSED: " + count + " players, merged bots, Inspector slots, seeded turns, real movement and shared pause input.");
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("FAIL: " + message); }
}
