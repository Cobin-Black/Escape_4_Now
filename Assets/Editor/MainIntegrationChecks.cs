// Checks that the imported player and monster systems still work with Survival rules and shared objectives.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Escape4Now.GameModes;
using Escape4Now.Map;
using Escape4Now.Multiplayer;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using Escape4Now.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MainIntegrationChecks
{
    private const string Running = "MainIntegrationChecks.Running";
    private static double deadline;
    static MainIntegrationChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 100;
            EditorApplication.update += Watchdog;
            Object.FindFirstObjectByType<SurvivalModeController>().StartCoroutine(Execute());
        };
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SurvivalMode.unity");
        var map = new SerializedObject(Object.FindFirstObjectByType<SurvivalMap>());
        map.FindProperty("useFixedSeed").boolValue = true;
        map.FindProperty("fixedSeed").intValue = 42;
        map.ApplyModifiedPropertiesWithoutUndo();
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }
    public static void ValidationError()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SurvivalMode.unity");
            var mode = Object.FindFirstObjectByType<SurvivalModeController>();
            var fields = new SerializedObject(mode.Events);
            var settings = Object.Instantiate((SurvivalEventSettings)fields.FindProperty("settings").objectReferenceValue);
            settings.events.First(rule => rule.type == SurvivalEventType.PoolsideSlip).prefab = null;
            fields.FindProperty("settings").objectReferenceValue = settings;
            fields.ApplyModifiedPropertiesWithoutUndo();
            var session = Object.FindFirstObjectByType<LocalMultiplayerSession>();
            Require(!session.TryStartLocalMatch(2), "Invalid event configuration prevents startup");
            Require(session.Status.Contains("PoolsideSlip"), "Lobby preserves the exact missing prefab error");
            Debug.Log("SURVIVAL STARTUP VALIDATION CHECK PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static IEnumerator Execute()
    {
        var checks = Check();
        while (true)
        {
            bool next;
            try { next = checks.MoveNext(); }
            catch (Exception error) { Debug.LogException(error); Finish(1); yield break; }
            if (!next) { Finish(0); yield break; }
            yield return checks.Current;
        }
    }
    private static IEnumerator Check()
    {
        yield return null; yield return null;
        int survivors = int.Parse(Argument("-integrationSurvivors", "3"));
        int slot = int.Parse(Argument("-integrationMonsterSlot", "0"));
        bool ai = Argument("-integrationAI", "false") == "true";
        var mode = Object.FindFirstObjectByType<SurvivalModeController>();
        var session = Object.FindFirstObjectByType<LocalMultiplayerSession>();
        var context = Object.FindFirstObjectByType<MatchUIContext>();
        var map = (SurvivalMap)mode.GridMap;
        int count = survivors + (ai ? 0 : 1);
        session.SetPlayerCount(count);
        if (!ai) session.SetMonsterPlayer(slot);
        Require(session.TryStartLocalMatch(count), "Monster setup starts: " + session.Status);
        yield return null; yield return null;
        var monster = mode.Monster;
        Require(monster != null && monster.isActiveAndEnabled && context.Monster == monster, "Incoming monster is connected to the scene and HUD");
        Require(monster.IsAIControlled == ai, "Unassigned monster uses AI; selected slot uses Input System controls");
        Require(session.Participants.Count == survivors && mode.ActiveSurvivors.Count == survivors, "Monster selection preserves survivor counts");
        Require(mode.Objectives.Required == mode.Rules.ForSurvivors(survivors).required, "Shared puzzle budget uses survivor count");
        for (int i = 0; i < survivors; i++)
        {
            int lobbySlot = !ai && i >= slot ? i + 1 : i;
            Require(session.Participants[i].name == session.GetPlayerName(lobbySlot) && session.Participants[i].IdentityColor == session.GetPlayerColor(lobbySlot), "Names and colors remain attached to original lobby slots");
            Require(session.Participants[i].Appearance.HasSkin && session.Skins.GetPlayerSkin(lobbySlot) != null, "Original lobby slot retains its selected sprite skin");
        }
        for (int y = 0; y < mode.Rules.monsterFootprint.y; y++) for (int x = 0; x < mode.Rules.monsterFootprint.x; x++)
        {
            var cell = monster.GridPosition + new Vector2Int(x, y);
            Require(map.IsWalkable(cell) && map.IsOccupied(cell) && mode.Objectives.IsReserved(cell) && !mode.Events.Tiles.ContainsKey(cell), "Whole monster footprint is walkable, occupied and protected");
        }
        monster.enabled = false;
        var order = mode.Turns.CopyTurnOrder();
        var sequence = new List<string>();
        for (int turn = 0; turn < (survivors + 1) * 2; turn++)
        {
            sequence.Add(mode.Turns.CurrentTurnOwner == TurnOwner.Enemy ? "Monster" : mode.Turns.CurrentTurnName);
            if (mode.Turns.CurrentTurnOwner == TurnOwner.Enemy) mode.Turns.AdvanceTurn();
            else mode.Turns.EndTurnAfterBattleLoss(mode.ActiveTurnParticipant);
            yield return null;
        }
        Require(sequence.Take(survivors + 1).SequenceEqual(sequence.Skip(survivors + 1)), "Same randomized survivor/monster rotation repeats");
        Require(sequence.Count(name => name == "Monster") == 2 && mode.Turns.TurnNumber == 3 && order.SequenceEqual(mode.Turns.CopyTurnOrder()), "Exactly one monster turn per round without reshuffling");
        while (mode.Turns.CurrentTurnOwner != TurnOwner.Enemy)
        { mode.Turns.EndTurnAfterBattleLoss(mode.ActiveTurnParticipant); yield return null; }
        var victim = mode.ActiveSurvivors[0];
        var anchor = monster.GridPosition;
        var near = new[] { Vector2Int.left, Vector2Int.down, Vector2Int.right * 2, Vector2Int.up * 2 }
            .Select(offset => anchor + offset).First(cell => map.IsWalkable(cell));
        Require(victim.SetGridPosition(near), "Survivor can enter the incoming monster's ability area");
        int health = victim.Health;
        typeof(MonsterCharacter).GetMethod("TryStomp", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(monster, null);
        Require(victim.Health == health - 1, "Incoming Stomp still damages the survivor");
        var owner = mode.Turns.CurrentTurnOwner;
        while (victim.Health > 0) victim.TakeDamage();
        yield return null; yield return null;
        Require(mode.OutcomeOf(victim) == SurvivorOutcome.Dead && !victim.gameObject.activeSelf, "Incoming health elimination retires a survivor in shared puzzle rules");
        Require(mode.CompletedPuzzles == 0 && !mode.ExitsUnlocked, "Monster damage cannot complete objectives or unlock exits");
        if (survivors > 1)
        {
            Require(mode.ActiveSurvivors.Count == survivors - 1 && !session.TryGetResult(out _), "Remaining survivors continue after a death");
            Require(mode.Turns.CurrentTurnOwner == owner, "Eliminating a survivor during a monster turn does not steal that turn");
            session.PauseGame(); int round = mode.Turns.TurnNumber;
            mode.Turns.AdvanceTurn(); yield return new WaitForSecondsRealtime(.1f);
            Require(mode.Turns.TurnNumber == round && LocalMultiplayerSession.GameplayPaused, "Shared Pause prevents turn advancement");
            session.ResumeGame();
        }
        else Require(session.TryGetResult(out var result) && !result.Won, "Last survivor dying ends with a loss");
        Debug.Log("MAIN INTEGRATION CHECKS PASSED: " + survivors + " survivors, monster slot " + slot + ", health, identities, shared objectives, occupancy, fixed turns and Pause.");
    }
    private static void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException("FAIL: " + label); Debug.Log("PASS: " + label); }
    private static string Argument(string key, string fallback)
    { var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
    private static void Watchdog() { if (EditorApplication.timeSinceStartup > deadline) { Debug.LogError("FAIL: Integration check timeout"); Finish(1); } }
    private static void Finish(int code) { SessionState.SetBool(Running, false); EditorApplication.update -= Watchdog; EditorApplication.Exit(code); }
}
