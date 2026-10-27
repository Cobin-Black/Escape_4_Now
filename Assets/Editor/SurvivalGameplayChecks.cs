// Editor checks for Survival setup, player controls, shared UI and match startup.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Escape4Now.Items;
using Escape4Now.Map;
using Escape4Now.Multiplayer;
using Escape4Now.Obstacles;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using Escape4Now.UI;
using Escape4Now.GameModes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class SurvivalGameplayChecks
{
    private const string Running = "SurvivalGameplayChecks.Running";
    private static double deadline;
    private static Keyboard keyboard;
    private static InputSettings.BackgroundBehavior previousBackground;
    private static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

    static SurvivalGameplayChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            EditorApplication.update += Watchdog;
            UnityEngine.Object.FindFirstObjectByType<LocalMultiplayerSession>().StartCoroutine(RunChecks());
        };
    }

    [MenuItem("Tools/Survival/Validate Playable Match")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/SurvivalMode.unity");
        var sessionFields = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<LocalMultiplayerSession>());
        sessionFields.FindProperty("spawnAIMonsterWhenUnassigned").boolValue = false;
        sessionFields.ApplyModifiedPropertiesWithoutUndo();
        if (!Application.isBatchMode)
        {
            var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(gameViewType).Show();
        }
        var map = UnityEngine.Object.FindFirstObjectByType<SurvivalMap>();
        var settings = new SerializedObject(map);
        settings.FindProperty("useFixedSeed").boolValue = true;
        settings.FindProperty("fixedSeed").intValue = 42;
        settings.ApplyModifiedPropertiesWithoutUndo();
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static void Watchdog()
    {
        if (EditorApplication.timeSinceStartup <= deadline) return;
        Debug.LogError("Survival gameplay checks timed out.");
        Finish(1);
    }

    private static IEnumerator RunChecks()
    {
        IEnumerator checks = CheckMatch();
        while (true)
        {
            bool more;
            try { more = checks.MoveNext(); }
            catch (Exception error) { Debug.LogException(error); Finish(1); yield break; }
            if (!more) { Finish(0); yield break; }
            yield return checks.Current;
        }
    }

    private static IEnumerator CheckMatch()
    {
        yield return null;
        int count = 4;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-survivalPlayers") count = int.Parse(args[i + 1]);
        var session = UnityEngine.Object.FindFirstObjectByType<LocalMultiplayerSession>();
        Require(session.Mode is SurvivalModeController && session.Mode.ValidateConfiguration(out _),
            "Survival scene explicitly wires its own mode and all shared systems");
        Require(LocalMultiplayerSession.InputBindings == session.Mode.InputBindings && session.Mode.InputBindings.IsConfigured,
            "Inspector input references resolve to real Input System actions");
        Require(GameMode.Current == session.Mode, "Survival uses the same GameMode base contract as Race");
        session.SetBotPlayer(0, true);
        Require(!session.Mode.SupportsBots && !session.IsBotPlayer(0), "Race bots are not offered in Survival");
        var map = UnityEngine.Object.FindFirstObjectByType<SurvivalMap>();
        var timer = UnityEngine.Object.FindFirstObjectByType<GameTimer>();
        Require(timer != null && !timer.enabled && !timer.HasExpired, "Countdown waits in the lobby");
        float lobbyTime = timer.TimeRemaining;
        yield return new WaitForSecondsRealtime(.1f);
        Require(timer.TimeRemaining == lobbyTime, "Lobby time does not consume the match clock");
        Require(!session.TryStartLocalMatch(0) && !session.TryStartLocalMatch(5), "Unsupported counts are rejected");
        Require(session.TryStartLocalMatch(count), "Survival starts: " + session.Status);
        Require(!session.TryStartLocalMatch(count), "Starting twice cannot duplicate participants");
        for (int i = 0; i < 10; i++) yield return null;
        NativeUiChecks.CheckPlayerBodies(session);
        Require(session.Participants.Count == count && map.LayoutSeed == 42, "Chosen player count and fixed seed are respected");
        float timeLimit = new SerializedObject(timer).FindProperty("timeLimit").floatValue;
        Require(timer.enabled && !timer.ExpirationIsWin && timer.TimeRemaining > 0f && timer.TimeRemaining < timeLimit,
            "Survival starts its configured countdown; expiry is not a win");
        float runningTime = timer.TimeRemaining;
        yield return new WaitForSecondsRealtime(.1f);
        Require(timer.TimeRemaining < runningTime, "The match clock counts down");
        Require(UnityEngine.Object.FindFirstObjectByType<ItemSpawner>() == null && UnityEngine.Object.FindObjectsByType<Item>(FindObjectsSortMode.None).Length == 0,
            "There is no item spawning yet");
        Require(UnityEngine.Object.FindFirstObjectByType<LanMultiplayerSession>() == null, "Survival does not initialize indoor-only network state");
        var inventoryBar = UnityEngine.Object.FindFirstObjectByType<ItemPickupBar>();
        Require(inventoryBar != null && new SerializedObject(inventoryBar).FindProperty("hideWhenEmpty").boolValue,
            "Shared inventory bar hides empty inventories like the first game mode");
        var spawner = map.GetComponent<SurvivalObstacleSpawner>();
        Require(spawner.HasSpawnedObstacles && spawner.SpawnedObstacleCount == map.Layout.Props.Count && spawner.SpawnedObstacleCount > 0,
            "Dedicated obstacle spawner survives Play Mode reload and creates every planned prop");
        Require(new List<SurvivalPropVisual>(spawner.GetAllPrefabs()).Count == Enum.GetValues(typeof(OutdoorPropKind)).Length,
            "Play Mode reload preserves every outdoor prefab assignment");
        var cells = new HashSet<Vector2Int>();
        foreach (PlayerCharacter player in session.Participants)
        {
            Require(player.enabled && cells.Add(player.GridPosition) && map.IsWalkable(player.GridPosition) && map.IsOccupied(player.GridPosition), "Valid distinct playable spawn");
            Require(player.Inventory != null && player.Inventory.Player == player && player.Inventory.Items.Count == 0, "Separate empty shared inventory");
            var settings = new SerializedObject(player);
            Require(player.Mode == session.Mode && ReferenceEquals(player.Mode.GridMap, map), "Player uses its mode's outdoor grid");
            Require(player.Mode.GetObjective(player).Contains("0/"), "Survival objective shows the shared puzzle count");
            Require(!player.Mode.CanUseExit(player), "Survival cannot accidentally use indoor escape rules");
            Require(!map.IsExit(player.GridPosition), "Survival has no indoor escape win condition");
        }
        SurvivalMapLayout runningLayout = map.Layout;
        Require(!map.GenerateWithSeed(99) && ReferenceEquals(map.Layout, runningLayout), "Regeneration cannot replace a running match's grid");
        Vector2Int firstCell = session.Participants[0].GridPosition;
        Vector2Int secondCell = session.Participants[1].GridPosition;
        Require(session.Participants[1].SetGridPosition(firstCell) && map.IsOccupied(firstCell), "Existing shared-tile behavior works outdoors");
        Require(session.Participants[1].SetGridPosition(secondCell) && map.IsOccupied(firstCell) && map.IsOccupied(secondCell),
            "Leaving a shared tile preserves the remaining player's occupancy");
        bool blockedFound = false;
        for (int y = 0; y < map.Height && !blockedFound; y++) for (int x = 0; x < map.Width && !blockedFound; x++)
            if (!map.IsWalkable(new Vector2Int(x, y)))
            {
                var blocked = new Vector2Int(x, y);
                Require(!map.TryFindPath(firstCell, blocked, out _) && !session.Participants[0].SetGridPosition(blocked), "The same movement logic rejects blocked outdoor tiles");
                blockedFound = true;
            }
        Require(blockedFound, "Generated map contains blocked terrain");
        var turns = UnityEngine.Object.FindFirstObjectByType<TurnSystemController>();
        int[] order = turns.CopyTurnOrder();
        var participants = new PlayerCharacter[count];
        for (int i = 0; i < count; i++) participants[i] = session.Participants[i];
        turns.SetPlayers(participants, 42);
        int[] expected = turns.CopyTurnOrder();
        for (int i = 0; i < count; i++) Require(order[i] == expected[i], "Map seed controls the one-time shuffle");
        turns.RestartTurnCycle();
        yield return null; yield return null;
        //A temporary test item exercises the existing inventory without adding a Survival item spawner.
        var itemObject = new GameObject("Inventory test item");
        itemObject.SetActive(false);
        var item = itemObject.AddComponent<DoubleDiceItem>();
        Require(participants[0].Inventory.AddItem(item) && participants[0].Inventory.IsFull,
            "Existing inventory accepts an item and enforces capacity");
        yield return null; yield return null;
        NativeUiChecks.CheckInventoryLayout(UnityEngine.Object.FindFirstObjectByType<MatchUIContext>());
        Require(!participants[1].Inventory.HasItem(item), "Items stay with their owning player");
        Require(participants[0].Inventory.RemoveItem(item) && !participants[0].Inventory.IsFull, "Existing inventory removes items");
        UnityEngine.Object.Destroy(itemObject);
        var colors = new Color[count];
        for (int i = 0; i < count; i++) colors[i] = new SerializedObject(session.Participants[i]).FindProperty("playerColor").colorValue;
        keyboard = InputSystem.AddDevice<Keyboard>();
        PlayerCharacter active = session.Participants[order[0]];
        PlayerCharacter inactive = session.Participants[order[1]];
        Require(!inactive.RollDice() && !inactive.MoveToGridPosition(active.GridPosition), "Inactive players cannot roll or move");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        Require(active.HasRolled && active.MovesRemaining >= 1 && active.MovesRemaining <= 6, "Existing Space input rolls the active player's die");
        yield return new WaitForSecondsRealtime(1.1f);
        turns.RestartTurnCycle();
        yield return null; yield return null;
        InputAction roll = session.Mode.InputBindings.RollDice;
        string previousOverrides = roll.SaveBindingOverridesAsJson();
        try
        {
            //An Input System rebind must drive gameplay without changing C# key names.
            for (int i = 0; i < roll.bindings.Count; i++) roll.ApplyBindingOverride(i, "<Keyboard>/r");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null; yield return null;
            Require(!active.HasRolled, "Old key stops rolling after an Input System rebind");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return null; yield return null;
            Require(active.HasRolled, "Inspector-assigned action respects a runtime binding override");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
        finally { roll.RemoveAllBindingOverrides(); roll.LoadBindingOverridesFromJson(previousOverrides); }
        yield return new WaitForSecondsRealtime(1.1f);
        turns.AdvanceTurn();
        yield return null; yield return null;
        NativeUiChecks.CheckPlayerBodies(session);
        turns.RestartTurnCycle();
        yield return null; yield return null;
        for (int turn = 0; turn < count * 2; turn++)
        {
            active = session.Participants[order[turn % count]];
            Require(turns.IsPlayersTurn(active) && turns.CurrentTurnName == active.name && turns.TurnNumber == turn / count + 1, "Stable randomized order, correct HUD identity and round counter");
            int rings = 0;
            foreach (PlayerCharacter player in session.Participants)
            {
                var ring = player.transform.Find("Active Turn Ring").GetComponent<SpriteRenderer>();
                if (ring.enabled) { rings++; Require(player == active, "Only the active player's indicator is visible"); }
            }
            Require(rings == 1, "Exactly one active indicator");
            Vector2Int start = active.GridPosition, target = start;
            foreach (Vector2Int step in new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down })
                if (map.IsWalkable(start + step)) { target = start + step; break; }
            Require(target != start && map.TryFindPath(start, target, out List<Vector2Int> path) && path.Count == 1, "Shared pathfinder finds a legal cardinal step");
            active.SetMovesFromItem(1);
            Require(active.MoveToGridPosition(target), "Existing player movement accepts the outdoor grid");
            session.PauseGame();
            Vector3 pausedPosition = active.transform.position;
            float pausedTime = timer.TimeRemaining;
            turns.AdvanceTurn();
            Require(Time.timeScale == 0f && LocalMultiplayerSession.GameplayPaused && turns.IsPlayersTurn(active), "Pause freezes turn advancement");
            yield return new WaitForSecondsRealtime(.1f);
            Require(active.transform.position == pausedPosition, "Pause freezes movement");
            Require(timer.TimeRemaining == pausedTime, "Pause freezes the countdown");
            session.ResumeGame();
            while (active.IsMoving) yield return null;
            yield return null; yield return null;
            Require(timer.TimeRemaining < pausedTime, "Resume continues the same countdown");
            Require(active.GridPosition == target && map.IsOccupied(target), "Movement updates outdoor occupancy");
            Require(turns.CurrentPlayerIndex == order[(turn + 1) % count], "Exhausting movement automatically advances the same turn system");
            for (int i = 0; i < count; i++)
                Require(new SerializedObject(session.Participants[i]).FindProperty("playerColor").colorValue == colors[i], "Random order preserves player identity colors");
        }
        Require(!LocalMultiplayerSession.MatchHasEnded && Time.timeScale == 1f && !timer.HasExpired,
            "The match continues while survival time remains");
        if (!Application.isBatchMode)
        {
            Directory.CreateDirectory("Logs/SurvivalPreviews");
            ScreenCapture.CaptureScreenshot("Logs/SurvivalPreviews/survival-gameplay-" + count + ".png");
            for (int i = 0; i < 30; i++) yield return null;
            Require(inventoryBar.PanelRect.width == 0f, "No empty inventory placeholder is drawn");
            session.PauseGame();
            ScreenCapture.CaptureScreenshot("Logs/SurvivalPreviews/survival-pause-" + count + ".png");
            for (int i = 0; i < 30; i++) yield return null;
            session.ResumeGame();
        }
        participants[0].name = "Alex";
        typeof(GameTimer).GetField("timeRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(timer, .05f);
        yield return new WaitForSecondsRealtime(.15f);
        Require(timer.HasExpired && timer.TimeRemaining == 0f && Time.timeScale == 0f && LocalMultiplayerSession.MatchHasEnded,
            "Reaching zero clamps the clock and ends the survival match");
        Require(session.Mode.TryGetResult(session.Participants, out MatchResult result) && !result.Won,
            "Survivors who have not escaped lose at timeout");
        string resultMessage = result.Message;
        string[] resultLines = resultMessage.Split('\n');
        foreach (PlayerCharacter player in participants)
            Require(Array.IndexOf(resultLines, player.name) >= 0, "Survival results list every survivor's actual name: " + player.name);
        Require(Array.IndexOf(resultLines, "Player 1") < 0, "Survivor names are not hardcoded player numbers");
        PlayerCharacter finalPlayer = participants[0];
        int finalTurn = turns.CurrentPlayerIndex;
        turns.AdvanceTurn();
        session.ResumeGame();
        Require(!finalPlayer.RollDice() && !finalPlayer.MoveToGridPosition(finalPlayer.GridPosition + Vector2Int.right)
            && turns.CurrentPlayerIndex == finalTurn && Time.timeScale == 0f,
            "A finished countdown cannot be resumed and blocks gameplay input");
        if (!Application.isBatchMode)
        {
            ScreenCapture.CaptureScreenshot("Logs/SurvivalPreviews/survival-result-" + count + ".png");
            for (int i = 0; i < 30; i++) yield return null;
        }
        Debug.Log("SURVIVAL GAMEPLAY CHECKS PASSED: " + count + " players, seeded turns, real input, movement, inventories, active indicator, pause/resume, countdown/expiry, no items/monster.");
    }

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Watchdog;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = previousBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
        Time.timeScale = 1f;
        if (Application.isBatchMode || Array.IndexOf(Environment.GetCommandLineArgs(), "-survivalValidationExit") >= 0) EditorApplication.Exit(code);
        else EditorApplication.isPlaying = false;
    }
}
