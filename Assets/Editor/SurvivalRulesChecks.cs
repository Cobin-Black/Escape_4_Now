// Editor tests for shared puzzles, individual escapes, deaths, timeout losses and puzzle UI controls.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Escape4Now.GameModes;
using Escape4Now.Map;
using Escape4Now.Multiplayer;
using Escape4Now.Player;
using Escape4Now.Puzzles;
using Escape4Now.TurnSystem;
using Escape4Now.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SurvivalRulesChecks
{
    private const string Running = "SurvivalRulesChecks.Running";
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static double deadline;
    private static InputSettings.BackgroundBehavior background;
    private static InputSettings.EditorInputBehaviorInPlayMode editorInput;
    static SurvivalRulesChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false))
                return;
            deadline = EditorApplication.timeSinceStartup + 150;
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            EditorApplication.update += Watchdog;
            if (SessionState.GetBool("SurvivalRulesChecks.UI", false))
                mouse = InputSystem.AddDevice<Mouse>();
            Object.FindFirstObjectByType<SurvivalModeController>().StartCoroutine(Execute(SessionState.GetBool("SurvivalRulesChecks.UI", false) ? CheckUI() : Check()));
        };
    }

    public static void Run()
    {
        SessionState.SetBool("SurvivalRulesChecks.UI", false);
        EditorSceneManager.OpenScene("Assets/Scenes/SurvivalMode.unity");
        var session = new SerializedObject(Object.FindFirstObjectByType<LocalMultiplayerSession>());
        session.FindProperty("spawnAIMonsterWhenUnassigned").boolValue = false;
        session.ApplyModifiedPropertiesWithoutUndo();
        var map = new SerializedObject(Object.FindFirstObjectByType<SurvivalMap>());
        map.FindProperty("useFixedSeed").boolValue = true;
        map.FindProperty("fixedSeed").intValue = 42;
        map.ApplyModifiedPropertiesWithoutUndo();
        if (!Application.isBatchMode)
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
            NativeUiChecks.SetSize(1280, 720);
        }

        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    public static void UI()
    {
        Run();
        SessionState.SetBool("SurvivalRulesChecks.UI", true);
    }

    public static void Generation()
    {
        try
        {
            var rules = AssetDatabase.LoadAssetAtPath<SurvivalRulesSettings>("Assets/Settings/Survival/Match Rules.asset");
            var eventSettings = AssetDatabase.LoadAssetAtPath<SurvivalEventSettings>("Assets/Settings/Survival/Event Settings.asset");
            var generated = new SurvivalGenerationSettings();
            for (int seed = 0; seed < 256; seed++)
            {
                var map = new SurvivalMapLayout(seed, generated);
                for (int count = 1; count <= 4; count++)
                {
                    var plan = new SurvivalObjectiveLayout(map, rules, count);
                    var matchEvents = new SurvivalMatchEventPlan(map, plan, eventSettings);
                    var events = new SurvivalEventLayout(map, plan, eventSettings, matchEvents);
                    SurvivalMatchEventPlanChecks.Check(map, plan, eventSettings, matchEvents, events);
                    var hazards = new HashSet<Vector2Int>(events.ForcedMovementCells);
                    Require(plan.ValidateConnectivity(hazards), "Moving events have alternate routes", false);
                    foreach (var pair in events.Tiles)
                    {
                        Require(plan.IsWalkable(pair.Key) && !plan.IsReserved(pair.Key), "Event placement excludes objectives and spawns", false);
                        Require(pair.Value.Trigger == SurvivalEventTrigger.Tile, "Only standard Survival events use floor tiles", false);
                    }
                    var mudCandidates = new List<Vector2Int>();
                    for (int y = 1; y < map.Height - 1; y++) for (int x = 1; x < map.Width - 1; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (SurvivalEnvironmentalPlacement.IsEligible(map, plan, cell, true) && !events.Tiles.ContainsKey(cell))
                            mudCandidates.Add(cell);
                    }
                    if (mudCandidates.Count > 0)
                        foreach (var cell in SurvivalEnvironmentalPlacement.Patch(mudCandidates[0], mudCandidates, eventSettings.mudPatchTiles.y))
                            Require(plan.IsWalkable(cell) && !plan.IsReserved(cell) && !map.IsProtectedRoute(cell.x, cell.y), "Environmental patch excludes all protected spaces and routes", false);
                    Require(plan.Tasks.Count == rules.ForSurvivors(count).required + rules.ForSurvivors(count).extraStations, "Correct objective budget", false);
                    Require(plan.Exits.Count >= 2 && plan.ValidateConnectivity(null), "Reachable tasks, spawns, areas and exits", false);
                    var firstExit = plan.Exits[0]; var secondExit = plan.Exits[1];
                    Require(firstExit.x == 1 && secondExit.x == map.Width - 2 ||
                        secondExit.x == 1 && firstExit.x == map.Width - 2 ||
                        firstExit.y == 1 && secondExit.y == map.Height - 2 ||
                        secondExit.y == 1 && firstExit.y == map.Height - 2,
                        "Escape gates occupy opposite campus sides", false);
                    var occupied = new HashSet<Vector2Int>();
                    var types = new HashSet<SurvivalPuzzleType>();
                    foreach (var task in plan.Tasks)
                    {
                        Require(occupied.Add(task.Cell) && !plan.IsWalkable(task.Cell) && task.Definition.Allows(map.GetArea(task.Cell.x, task.Cell.y)), "Compatible non-overlapping station", false);
                        Require(types.Add(task.Definition.type), "Prefer unique puzzle types", false);
                        Require(task.InteractionCells.Count >= 1, "Valid interaction space", false);
                        foreach (var cell in task.InteractionCells)
                            Require(plan.IsWalkable(cell) && plan.IsReserved(cell), "Protected interaction space", false);
                    }

                    foreach (var exit in plan.Exits)
                        Require(plan.IsWalkable(exit) && plan.IsReserved(exit) && !plan.IsSpawn(exit), "Safe exits", false);
                    if (seed < 8)
                    {
                        var repeat = new SurvivalObjectiveLayout(map, rules, count);
                        for (int i = 0; i < plan.Exits.Count; i++)
                            Require(plan.Exits[i] == repeat.Exits[i], "Fixed-seed gate reproducibility", false);
                        for (int i = 0; i < plan.Tasks.Count; i++)
                            Require(plan.Tasks[i].Cell == repeat.Tasks[i].Cell && plan.Tasks[i].Definition == repeat.Tasks[i].Definition, "Fixed-seed station reproducibility", false);
                    }
                }
            }

            var minimal = new SurvivalGenerationSettings();
            foreach (var area in minimal.Areas)
                if (!area.Required)
                    area.Chance = 0;
            for (int seed = 0; seed < 64; seed++)
            {
                var map = new SurvivalMapLayout(seed, minimal);
                for (int survivors = 1; survivors <= 4; survivors++)
                {
                    var plan = new SurvivalObjectiveLayout(map, rules, survivors);
                    var events = new SurvivalEventLayout(map, plan, eventSettings);
                    Require(plan.ValidateConnectivity(new HashSet<Vector2Int>(events.ForcedMovementCells)), "Missing optional areas still give reachable compatible objectives", false);
                }
            }

            foreach (var definition in rules.puzzles)
                for (int seed = 0; seed < 128; seed++)
                {
                    var puzzle = new PuzzleSession(definition, seed);
                    Require(!puzzle.Solved, "Puzzle starts unsolved", false);
                    Solve(puzzle);
                    Require(puzzle.Solved, "Puzzle is solvable", false);
                    if (definition.type == SurvivalPuzzleType.Circuit)
                        Require(RenderedCircuitConnected(puzzle), "Solved circuits connect in the same top-to-bottom coordinates as the UI", false);
                }

            var circuit = Object.Instantiate(Array.Find(rules.puzzles, definition => definition.type == SurvivalPuzzleType.Circuit));
            for (int size = 2; size <= 4; size++)
            {
                circuit.circuitSize = size;
                for (int seed = 0; seed < 128; seed++)
                {
                    var puzzle = new PuzzleSession(circuit, seed);
                    Solve(puzzle);
                    Require(puzzle.Solved && RenderedCircuitConnected(puzzle), "All circuit difficulties have visually valid solutions", false);
                }
            }

            Object.DestroyImmediate(circuit);
            var duplicateRules = Object.Instantiate(rules);
            duplicateRules.puzzles = new[] { rules.puzzles[0], rules.puzzles[1], rules.puzzles[2] };
            duplicateRules.ForSurvivors(1).required = 2;
            duplicateRules.ForSurvivors(1).extraStations = 2;
            var duplicatePlan = new SurvivalObjectiveLayout(new SurvivalMapLayout(42, generated), duplicateRules, 1);
            SurvivalTaskPlacement repeated = null;
            foreach (var task in duplicatePlan.Tasks)
                foreach (var other in duplicatePlan.Tasks)
                    if (task != other && task.Definition.type == other.Definition.type) repeated = task;
            Require(repeated != null && duplicatePlan.TryCompletePuzzleType(repeated), "Repeated-type fixture completes one puzzle type");
            foreach (var task in duplicatePlan.Tasks)
                Require(task.Completed == (task.Definition.type == repeated.Definition.type), "Only stations of the solved type are retired", false);
            Require(!duplicatePlan.TryCompletePuzzleType(repeated), "A completed type cannot award progress twice");
            duplicateRules.puzzles = new[] { rules.puzzles[0], rules.puzzles[1] };
            bool insufficientTypesRejected = false;
            try { duplicateRules.Validate(1); } catch (InvalidOperationException) { insufficientTypesRejected = true; }
            Require(insufficientTypesRejected, "Inspector settings must leave an extra distinct type available");
            Object.DestroyImmediate(duplicateRules);
            SurvivalMatchEventPlanChecks.VerifyVariety();
            Debug.Log("SURVIVAL GENERATION CHECKS PASSED: 320 campuses, 1280 objective/event layouts, optional-area absence, all nine puzzle types over 128 seeds.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static IEnumerator Check()
    {
        yield return null;
        yield return null;
        var mode = Object.FindFirstObjectByType<SurvivalModeController>();
        var session = Object.FindFirstObjectByType<LocalMultiplayerSession>();
        var map = (SurvivalMap)mode.GridMap;
        var controlledEvents = Object.Instantiate((SurvivalEventSettings)new SerializedObject(mode.Events).FindProperty("settings").objectReferenceValue);
        controlledEvents.globalEventChance = controlledEvents.environmentalEventChance = 0;
        controlledEvents.useAllEnabledEventsForDebugging = true;
        foreach (var rule in controlledEvents.events)
            if (rule.type == SurvivalEventType.Mud) rule.instancesPerMatch = new Vector2Int(1, 8);
        Set(mode.Events, "settings", controlledEvents);
        int count = int.Parse(Argument("-survivalPlayers", "4"));
        int monsterSlot = int.Parse(Argument("-monsterSlot", "-1"));
        bool duplicateTasks = Argument("-duplicateTasks", "false") == "true";
        if (duplicateTasks)
        {
            var fixture = Object.Instantiate(mode.Rules);
            fixture.puzzles = new[] { mode.Rules.puzzles[0], mode.Rules.puzzles[1], mode.Rules.puzzles[2] };
            foreach (var budget in fixture.taskCounts)
            {
                budget.required = 2;
                budget.extraStations = 2;
            }
            Set(mode, "rules", fixture);
        }
        PlayerCharacter external = null;
        if (Argument("-externalMonster", "false") == "true")
        {
            external = Object.Instantiate(mode.PlayerTemplate);
            external.name = "External role fixture";
            external.enabled = true;
            external.gameObject.SetActive(true);
            Require(mode.ConfigureExternalMonster(external), "An externally supplied actor can join the startup turn sequence");
        }

        if (monsterSlot >= 0)
            Require(mode.SetSetupRole(monsterSlot, SurvivalRole.Monster), "Any setup slot can be assigned the external monster role");
        Require(!session.TryStartLocalMatch(0) && !session.TryStartLocalMatch(5), "Invalid participant counts rejected");
        Require(session.TryStartLocalMatch(count), "Survival starts: " + session.Status);
        for (int i = 0; i < 8; i++)
            yield return null;
        if (external != null)
            Require(mode.Turns.CopyTurnOrder().Length == 5 && mode.ActiveSurvivors.Count == 4, "Four survivors plus one external turn slot; no AI implementation");
        Require(mode.Objectives.Tasks.Count > mode.Objectives.Required && mode.Objectives.Exits.Count == 2, "Extra shared tasks and two exits");
        Require(!mode.Timer.ExpirationIsWin && mode.Timer.TimeRemaining > 590, "Inspector-authored ten-minute clock; expiry is not a win");
        Require(!mode.ExitsUnlocked && !mode.OnPlayerEscaped(mode.ActiveSurvivors[0]), "No escape before completing tasks and reaching an exit");
        var gates = Object.FindObjectsByType<SurvivalExitGate>(FindObjectsSortMode.None);
        Require(gates.Length == mode.Objectives.Exits.Count, "Each generated exit has a visible gate");
        foreach (var gate in gates) Require(!gate.IsUnlocked, "Gates start closed");
        if (Argument("-eventTriggers", "false") == "true")
        {
            yield return SurvivalEventTriggerChecks.Check(mode, session);
            yield break;
        }
        if (!Application.isBatchMode)
        {
            Directory.CreateDirectory("Logs/SurvivalRulesPreviews");
            ScreenCapture.CaptureScreenshot("Logs/SurvivalRulesPreviews/locked-" + count + ".png");
            for (int i = 0; i < 5; i++) yield return null;
        }
        foreach (var pair in mode.Events.Tiles)
            Require(!mode.Objectives.IsReserved(pair.Key) && map.IsWalkable(pair.Key), "Events exclude reserved cells", false);
        var order = mode.Turns.CopyTurnOrder();
        int initialRound = mode.Turns.TurnNumber;
        for (int i = 0; i < order.Length; i++)
        {
            Require(mode.Turns.CurrentPlayerIndex == order[i], "Every actor gets exactly one turn in the fixed sequence", false);
            var current = mode.ActiveTurnParticipant;
            Require(current != null, "HUD has the active actor, including an external actor", false);
            mode.Turns.EndTurnAfterBattleLoss(current);
        }

        Require(mode.Turns.CurrentPlayerIndex == order[0] && mode.Turns.TurnNumber == initialRound + 1, "Order repeats without reshuffling and round counter advances");
        if (Argument("-expiryOnly", "false") == "true")
        {
            var current = ActiveSurvivor(mode, session);
            yield return null;
            yield return null;
            current.SetGridPosition(mode.Objectives.Tasks[0].InteractionCells[0]);
            Require(mode.Tasks.TryInteract(current), "Begin timeout-boundary puzzle fixture");
            Set(mode.Timer, "timeRemaining", .01f);
            yield return new WaitForSecondsRealtime(.1f);
            Require(session.TryGetResult(out var timeout) && !timeout.Won, "No escape means timeout loss, including solo play");
            Require(mode.Tasks.Puzzle == null && mode.CompletedPuzzles == 0, "Expiry closes a puzzle without awarding completion");
            Debug.Log("SURVIVAL RULE CHECKS PASSED: timeout, " + count + " participants.");
            yield break;
        }

        if (Argument("-allDead", "false") == "true")
        {
            while (mode.ActiveSurvivors.Count > 0)
            {
                var victim = mode.ActiveSurvivors[0];
                Require(mode.MarkSurvivorKilled(victim), "External kill hook retires one survivor");
                if (mode.ActiveSurvivors.Count > 0)
                    Require(!LocalMultiplayerSession.MatchHasEnded && Time.timeScale == 1, "A death does not end a match with survivors remaining");
            }

            Require(session.TryGetResult(out var dead) && !dead.Won && Time.timeScale == 0, "All survivors dead ends the match as a loss");
            Debug.Log("SURVIVAL RULE CHECKS PASSED: all-dead outcome.");
            yield break;
        }

        var actor = ActiveSurvivor(mode, session);
        yield return null;
        yield return null;
        foreach (var cell in ((SurvivalMap)mode.GridMap).GetSpawnCandidates())
        {
            if (map.Layout.GetArea(cell.x, cell.y) != OutdoorAreaKind.None || mode.Objectives.IsReserved(cell))
                continue;
            bool open = true;
            foreach (var step in SurvivalObjectiveLayout.Steps)
                if (!map.IsWalkable(cell + step) || !SurvivalEnvironmentalPlacement.IsEligible(map.Layout, mode.Objectives, cell + step, true)
                    || map.Layout.GetSurface((cell + step).x, (cell + step).y) != OutdoorSurface.Grass || mode.Events.Tiles.ContainsKey(cell + step))
                    open = false;
            if (!open || !actor.SetGridPosition(cell))
                continue;
            var config = (SurvivalEventSettings)new SerializedObject(mode.Events).FindProperty("settings").objectReferenceValue;
            var mud = Array.Find(config.events, entry => entry.type == SurvivalEventType.Mud);
            typeof(SurvivalEventController).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mode.Events, new object[] { mud, actor });
            //Mud no longer follows the actor. Supply an eligible surrounded-terrain fixture
            //to exercise the existing no-affordable-step turn rule independently of patch shape.
            var ground = mode.Events.Environment.ActiveEffects.First(effect => effect.Type == SurvivalEventType.Mud);
            foreach (var step in SurvivalObjectiveLayout.Steps) ground.Cells.Add(cell + step);
            int beforeRound = mode.Turns.TurnNumber, beforeSlot = mode.Turns.CurrentPlayerIndex;
            actor.SetMovesFromItem(1);
            yield return null;
            yield return null;
            Require(mode.Turns.CurrentPlayerIndex != beforeSlot || mode.Turns.TurnNumber > beforeRound, "Unaffordable terrain automatically ends a turn without the optional End Turn button");
            break;
        }

        actor = ActiveSurvivor(mode, session);
        yield return null;
        yield return null;
        var first = mode.Objectives.Tasks[0];
        Require(actor.SetGridPosition(first.InteractionCells[0]) && !actor.HasRolled, "Start beside a task before rolling");
        int round = mode.Turns.TurnNumber, slot = mode.Turns.CurrentPlayerIndex;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        Require(mode.Tasks.Puzzle != null && actor.IsInMenu() && !actor.HasRolled, "Existing Interact action opens a task instead of rolling");
        float time = mode.Timer.TimeRemaining;
        yield return new WaitForSecondsRealtime(.12f);
        Require(mode.Timer.TimeRemaining < time, "Timer continues during a puzzle");
        session.PauseGame();
        time = mode.Timer.TimeRemaining;
        yield return new WaitForSecondsRealtime(.12f);
        Require(mode.Timer.TimeRemaining == time && mode.Tasks.Puzzle != null, "Pause freezes the timer without losing the attempt");
        session.ResumeGame();
        mode.Tasks.ClosePuzzle();
        yield return null;
        yield return null;
        Require(mode.CompletedPuzzles == 0 && !first.Completed && !actor.IsInMenu(), "Cancel keeps task incomplete and clears control lock");
        Require(mode.Turns.TurnNumber > round || mode.Turns.CurrentPlayerIndex != slot, "Closing consumes the turn, including solo play");
        for (int i = 0; i < mode.Objectives.Required; i++)
        {
            actor = ActiveSurvivor(mode, session);
            yield return null;
            yield return null;
            SurvivalTaskPlacement task = null;
            foreach (var candidate in mode.Objectives.Tasks)
            {
                if (candidate.Completed) continue;
                if (task == null) task = candidate;
                if (duplicateTasks && i == 0)
                    foreach (var other in mode.Objectives.Tasks)
                        if (other != candidate && other.Definition.type == candidate.Definition.type) task = candidate;
                if (!duplicateTasks || i > 0) break;
            }
            Require(actor.SetGridPosition(task.InteractionCells[0]) && mode.Tasks.TryInteract(actor), "A survivor can attempt any unfinished task");
            Solve(mode.Tasks.Puzzle);
            mode.Tasks.FinishSolvedPuzzle();
            yield return null;
            yield return null;
            Require(task.Completed && mode.CompletedPuzzles == i + 1, "Completion persists once for the whole team");
            var stationMarkers = (List<SurvivalWorldMarker>)typeof(SurvivalTaskController)
                .GetField("stationMarkers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mode.Tasks);
            mode.Tasks.SetTaskNamesVisible(false);
            mode.Tasks.SetTaskNamesVisible(true);
            for (int station = 0; station < mode.Objectives.Tasks.Count; station++)
            {
                var placement = mode.Objectives.Tasks[station];
                if (placement.Definition.type == task.Definition.type)
                {
                    Require(placement.Completed, "Every station of the solved type is complete", false);
                    Require(!(bool)typeof(SurvivalModeController).GetMethod("CompleteTask", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(mode, new object[] { placement, ActiveSurvivor(mode, session) }), "Solved types cannot increase shared progress again", false);
                }
                var caption = stationMarkers[station].GetComponentInChildren<TextMesh>();
                Require(caption.GetComponent<Renderer>().enabled == !placement.Completed &&
                    caption.GetComponentInChildren<SpriteRenderer>().enabled == !placement.Completed,
                    "Completed station names stay hidden when reveal is toggled back on", false);
            }
        }

        Require(mode.ExitsUnlocked && mode.CompletedPuzzles < mode.Objectives.Tasks.Count, "Enough tasks unlock exits without requiring the extra stations");
        foreach (var gate in gates) Require(gate.IsUnlocked, "Shared puzzle completion opens every gate");
        actor = ActiveSurvivor(mode, session);
        yield return null;
        yield return null;
        Require(!mode.Tasks.CanInteract(actor), "Extra stations are no longer required after unlocking");
        if (!Application.isBatchMode)
        {
            Directory.CreateDirectory("Logs/SurvivalRulesPreviews");
            ScreenCapture.CaptureScreenshot("Logs/SurvivalRulesPreviews/unlocked-" + count + ".png");
            for (int i = 0; i < 5; i++)
                yield return null;
        }

        CheckEvents(mode, actor);

        Require(actor.SetGridPosition(mode.Objectives.Exits[0]), "Physically reach an unlocked exit");
        yield return null;
        yield return null;
        Require(mode.OutcomeOf(actor) == SurvivorOutcome.Escaped && !actor.enabled, "Escape retires only that survivor");
        if (mode.ActiveSurvivors.Count > 0)
        {
            Require(Time.timeScale == 1 && !LocalMultiplayerSession.MatchHasEnded, "Other survivors keep playing after an escape");
            if (count > 2)
            {
                var victim = mode.ActiveSurvivors[0];
                int completed = mode.CompletedPuzzles;
                Require(mode.MarkSurvivorKilled(victim) && !victim.enabled && mode.OutcomeOf(victim) == SurvivorOutcome.Dead, "External elimination hook removes a survivor");
                Require(mode.CompletedPuzzles == completed && mode.OutcomeOf(actor) == SurvivorOutcome.Escaped, "Death does not erase objectives or another survivor's win");
            }

            if (mode.ActiveSurvivors.Count > 0)
            {
                Set(mode.Timer, "timeRemaining", .01f);
                yield return new WaitForSecondsRealtime(.1f);
                Require(mode.ActiveSurvivors.Count == 0, "Remaining survivors lose at timeout");
            }
        }

        Require(session.TryGetResult(out var result) && result.Won && result.Message.Contains(actor.name), "Individual escaped winner remains in final results");
        Require(mode.OutcomeOf(actor) == SurvivorOutcome.Escaped && Time.timeScale == 0, "Escaped outcome is immutable and completed match stops");
        Debug.Log("SURVIVAL RULE CHECKS PASSED: " + count + " participants, shared puzzles, cancel, clock, effects, exit, death and results; monster slot " + monsterSlot);
    }

    private static PlayerCharacter ActiveSurvivor(SurvivalModeController mode, LocalMultiplayerSession session)
    {
        for (int i = 0; i < mode.Turns.CopyTurnOrder().Length * 2; i++)
        {
            var current = mode.ActiveTurnParticipant;
            if (mode.IsActiveSurvivor(current))
                return current;
            mode.Turns.EndTurnAfterBattleLoss(current);
        }

        throw new Exception("No active survivor turn");
    }

    private static void CheckEvents(SurvivalModeController mode, PlayerCharacter actor)
    {
        var settings = (SurvivalEventSettings)new SerializedObject(mode.Events).FindProperty("settings").objectReferenceValue;
        int completed = mode.CompletedPuzzles;
        Set(actor, "resolvingStep", true);
        try
        {
            foreach (var rule in settings.events)
            {
                if (!rule.Supported || !rule.enabled)
                    continue;
                Set(actor, "movesRemaining", 4);
                typeof(SurvivalEventController).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mode.Events, new object[] { rule, actor });
                if (rule.type == SurvivalEventType.Trip)
                {
                    Require(actor.MovesRemaining == 4 && !actor.EventState.HasSecondChance, "Second Chance consumes one negative event");
                    typeof(SurvivalEventController).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mode.Events, new object[] { rule, actor });
                    Require(actor.MovesRemaining == 0, "Trip removes remaining movement only");
                }

                Require(mode.CompletedPuzzles == completed, "Event preserves shared completion", false);
                Require(mode.MovementCost(actor.GridPosition) >= 1 && mode.MovementCost(actor.GridPosition) <= settings.maximumTileCost, "Area costs remain traversable", false);
            }
        }
        finally
        {
            Set(actor, "resolvingStep", false);
        }

        actor.EventState.GiveFreeze();
        actor.EventState.GiveFreeze();
        Require(!actor.EventState.BeginTurn() && actor.EventState.BeginTurn(), "Freeze cannot stack skipped turns");
        actor.EventState.GiveMovementBonus(2);
        actor.EventState.BeginTurn();
        Require(actor.EventState.UseRoll(4, 4) == 6, "Energy bonus applies next turn");
        actor.EventState.GiveMovementPenalty(2);
        actor.EventState.BeginTurn();
        Require(actor.EventState.UseRoll(5, 5) == 3, "Exhaustion applies next turn");
        foreach (var task in mode.Objectives.Tasks)
        {
            bool reachable = false;
            foreach (var cell in task.InteractionCells)
                if (actor.GridPosition == cell || mode.GridMap.TryFindPath(actor.GridPosition, cell, out _))
                    reachable = true;
            Require(reachable, "Temporary blockers preserve task access", false);
        }

        foreach (var exit in mode.Objectives.Exits)
            Require(actor.GridPosition == exit || mode.GridMap.TryFindPath(actor.GridPosition, exit, out _), "Temporary blockers preserve exit access", false);
    }

    public static void Solve(PuzzleSession puzzle)
    {
        switch (puzzle.Definition.type)
        {
            case SurvivalPuzzleType.MatchWires:
            case SurvivalPuzzleType.SortSupplies:
            case SurvivalPuzzleType.Batteries:
                for (int i = 0; i < puzzle.Left.Length; i++)
                    puzzle.Connect(i, Array.IndexOf(puzzle.Right, puzzle.Left[i]));
                break;
            case SurvivalPuzzleType.ButtonSequence:
                puzzle.Tick(20);
                foreach (int button in puzzle.Sequence)
                    puzzle.PressSequence(button);
                break;
            case SurvivalPuzzleType.EnterCode:
                puzzle.SubmitCode(puzzle.Code);
                break;
            case SurvivalPuzzleType.ArrangeNumbers:
                for (int i = 0; i < puzzle.Values.Length; i++)
                    puzzle.SwapNumbers(i, Array.IndexOf(puzzle.Values, i));
                break;
            case SurvivalPuzzleType.Breaker:
                for (int i = 0; i < puzzle.Switches.Length; i++)
                    if (puzzle.Switches[i] != puzzle.Targets[i])
                        puzzle.FlipSwitch(i);
                break;
            case SurvivalPuzzleType.PowerSwitches:
                int mask = 0;
                for (int candidate = 0; candidate < (1 << puzzle.Switches.Length); candidate++)
                {
                    var copy = (bool[])puzzle.Switches.Clone();
                    for (int i = 0; i < copy.Length; i++)
                        if ((candidate & (1 << i)) != 0)
                        {
                            copy[i] = !copy[i];
                            copy[(i + 1) % copy.Length] = !copy[(i + 1) % copy.Length];
                        }

                    if (Array.TrueForAll(copy, value => value))
                    {
                        mask = candidate;
                        break;
                    }
                }

                for (int i = 0; i < puzzle.Switches.Length; i++)
                    if ((mask & (1 << i)) != 0)
                        puzzle.FlipSwitch(i);
                break;
            case SurvivalPuzzleType.Circuit:
                var solution = (int[])typeof(PuzzleSession).GetField("solution", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(puzzle);
                for (int i = 0; i < puzzle.Ports.Length && !puzzle.Solved; i++)
                    for (int turns = 0; turns < 4 && puzzle.Ports[i] != solution[i] && !puzzle.Solved; turns++)
                        puzzle.RotateCircuit(i);
                break;
        }
    }

    private static IEnumerator Execute(IEnumerator checks)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(checks);
        while (true)
        {
            object current;
            try
            {
                if (stack.Count == 0)
                {
                    Finish(0);
                    yield break;
                }

                var next = stack.Peek();
                if (!next.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                current = next.Current;
                if (current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(1);
                yield break;
            }

            yield return current;
        }
    }

    private static IEnumerator CheckUI()
    {
        for (int frame = 0; frame < 12; frame++) yield return null;
        Canvas.ForceUpdateCanvases();
        var context = Object.FindFirstObjectByType<MatchUIContext>();
        var mode = (SurvivalModeController)context.Session.Mode;
        var lobby = context.GetComponentInChildren<LobbyUI>(true);
        var single = (Toggle)new SerializedObject(lobby).FindProperty("singlePlayer").objectReferenceValue;
        yield return Click(single);
        if (context.Session.PlayerCount != 1)
        {
            Directory.CreateDirectory("Logs/SurvivalRulesPreviews");
            ScreenCapture.CaptureScreenshot("Logs/SurvivalRulesPreviews/lobby-click-failure.png");
            Debug.Log("Lobby click state: " + single.isOn + ", interactable " + single.interactable + ", center " + Center(single) + ", viewport " + Screen.width + "x" + Screen.height);
            for (int frame = 0; frame < 4; frame++) yield return null;
        }
        Require(context.Session.PlayerCount == 1, "Native lobby supports one survivor");
        yield return Click(context.transform.Find("Lobby View/Lobby/Content/Start Match").GetComponent<Button>());
        yield return null;
        yield return null;
        var view = context.GetComponentInChildren<SurvivalPuzzleUI>(true);
        var references = new SerializedObject(view);
        var actor = mode.ActiveSurvivors[0];
        var task = mode.Objectives.Tasks[0];
        Directory.CreateDirectory("Logs/SurvivalRulesPreviews");
        int testedPuzzles = 0;
        foreach (var definition in mode.Rules.puzzles)
        {
            if (Argument("-puzzleType", "all") != "all" && Argument("-puzzleType", "all") != definition.type.ToString()) continue;
            testedPuzzles++;
            Require(actor.SetGridPosition(task.InteractionCells[0]), "Move to task UI fixture");
            yield return null;
            yield return null;
            var interact = (Button)references.FindProperty("interact").objectReferenceValue;
            Require(interact.gameObject.activeInHierarchy, "Contextual Interact appears beside task");
            yield return Click(interact);
            Require(mode.Tasks.Puzzle != null, "Native Interact opens a puzzle");
            //View coverage uses one station fixture; normal match completion is tested separately.
            var configuredDefinition = Object.Instantiate(definition);
            if (definition.type == SurvivalPuzzleType.ArrangeNumbers)
                configuredDefinition.numberCount = int.Parse(Argument("-puzzleCount", definition.numberCount.ToString()));
            typeof(SurvivalTaskController).GetProperty("Puzzle").SetValue(mode.Tasks,
                new PuzzleSession(configuredDefinition, int.Parse(Argument("-puzzleSeed", "42"))));
            yield return null;
            yield return null;
            yield return null;
            var puzzle = mode.Tasks.Puzzle;
            ScreenCapture.CaptureScreenshot("Logs/SurvivalRulesPreviews/" + definition.type + "-initial.png");
            for (int frame = 0; frame < 4; frame++) yield return null;
            if (definition.type == SurvivalPuzzleType.MatchWires || definition.type == SurvivalPuzzleType.SortSupplies || definition.type == SurvivalPuzzleType.Batteries)
            {
                var a = (PuzzleTokenUI)references.FindProperty("left").GetArrayElementAtIndex(0).objectReferenceValue;
                var b = (PuzzleTokenUI)references.FindProperty("right").GetArrayElementAtIndex(Array.IndexOf(puzzle.Right, puzzle.Left[0])).objectReferenceValue;
                if (definition.type == SurvivalPuzzleType.MatchWires)
                {
                    yield return Click(a.Button);
                    yield return Click(b.Button);
                }
                else
                    yield return Drag(a, b);
                Require(puzzle.Matched[0], "Pointer matching/dragging updates " + definition.type);
            }
            else if (definition.type == SurvivalPuzzleType.EnterCode)
            {
                var field = (InputField)references.FindProperty("codeInput").objectReferenceValue;
                field.text = puzzle.Code.Substring(0, 2);
                Require(field.characterLimit == definition.codeLength, "Code entry uses Inspector difficulty");
            }
            else if (definition.type == SurvivalPuzzleType.ButtonSequence)
            {
                while (!puzzle.SequenceReady) yield return null;
                yield return null;
                var button = (PuzzleTokenUI)references.FindProperty("cells").GetArrayElementAtIndex(puzzle.Sequence[0]).objectReferenceValue;
                yield return Click(button.Button);
                Require(puzzle.SequenceProgress == 1, "Native sequence buttons accept the remembered order");
            }
            else
            {
                var button = (PuzzleTokenUI)references.FindProperty("cells").GetArrayElementAtIndex(0).objectReferenceValue;
                yield return Click(button.Button);
                Require(button.gameObject.activeInHierarchy || puzzle.Solved, "Native puzzle control responds: " + definition.type);
            }

            foreach (var size in new[]
            {
                new Vector2Int(1280, 720),
                new Vector2Int(720, 960)
            }

            )
            {
                NativeUiChecks.SetSize(size.x, size.y);
                for (int frame = 0; frame < 5; frame++)
                    yield return null;
                var panel = (GameObject)references.FindProperty("panel").objectReferenceValue;
                if (panel.activeInHierarchy)
                {
                    var bounds = MatchUIContext.ScreenRect((RectTransform)panel.transform.Find("Puzzle"));
                    Require(bounds.xMin >= 0 && bounds.yMin >= 0 && bounds.xMax <= Screen.width + 1 && bounds.yMax <= Screen.height + 1, "Puzzle fits " + size);
                    ScreenCapture.CaptureScreenshot("Logs/SurvivalRulesPreviews/" + definition.type + "-" + size.x + "x" + size.y + ".png");
                    for (int frame = 0; frame < 4; frame++)
                        yield return null;
                }
            }

            NativeUiChecks.SetSize(1280, 720);
            for (int frame = 0; frame < 8; frame++)
                yield return null;
            if (mode.Tasks.Puzzle != null)
            {
                yield return Click(context.transform.Find("Pause View/Pause").GetComponent<Button>());
                Require(LocalMultiplayerSession.GameplayPaused && mode.Tasks.Puzzle != null, "Shared native Pause works during puzzle UI");
                yield return Click(context.transform.Find("Pause View/Pause Backdrop/Pause Menu/Resume Game").GetComponent<Button>());
                yield return SolvePuzzleThroughUI(references, puzzle);
                Require(puzzle.Solved && mode.Tasks.Puzzle == null, "Native UI can fully solve " + definition.type);
            }

            yield return null;
            yield return null;
            Require(!actor.IsInMenu(), "Puzzle closing releases player controls");
            Require(puzzle.Solved && !puzzle.Failed && task.Completed, "Puzzle UI awards completion for " + definition.type);
            if (task.Completed)
            {
                typeof(SurvivalTaskPlacement).GetProperty("Completed").SetValue(task, false);
                typeof(SurvivalModeController).GetProperty("CompletedPuzzles").SetValue(mode, 0);
                mode.Tasks.RefreshMarkers();
            }
            Object.Destroy(configuredDefinition);
        }

        if (Argument("-puzzleType", "all") == "all")
            yield return CheckPuzzleFailures(context, mode, references, actor, task);
        Debug.Log("SURVIVAL PUZZLE UI CHECKS PASSED: " + testedPuzzles + " puzzle views fully solved, pointer controls, real drag/drop, shared pause, landscape and portrait.");
    }

    private static IEnumerator CheckPuzzleFailures(MatchUIContext context, SurvivalModeController mode, SerializedObject view, PlayerCharacter actor, SurvivalTaskPlacement task)
    {
        foreach (var type in new[] { SurvivalPuzzleType.MatchWires, SurvivalPuzzleType.ButtonSequence, SurvivalPuzzleType.EnterCode, SurvivalPuzzleType.ArrangeNumbers })
        {
            Require(actor.SetGridPosition(task.InteractionCells[0]), "Return to a retryable station");
            yield return null; yield return null;
            yield return Click((Button)view.FindProperty("interact").objectReferenceValue);
            var definition = Array.Find(mode.Rules.puzzles, candidate => candidate.type == type);
            var puzzle = new PuzzleSession(definition, 42);
            typeof(SurvivalTaskController).GetProperty("Puzzle").SetValue(mode.Tasks, puzzle);
            yield return null; yield return null; yield return null;
            int turn = mode.Turns.TurnNumber;
            if (type == SurvivalPuzzleType.MatchWires)
            {
                yield return Click(((PuzzleTokenUI)view.FindProperty("left").GetArrayElementAtIndex(0).objectReferenceValue).Button);
                int wrong = Array.FindIndex(puzzle.Right, value => value != puzzle.Left[0]);
                yield return Click(((PuzzleTokenUI)view.FindProperty("right").GetArrayElementAtIndex(wrong).objectReferenceValue).Button);
            }
            else if (type == SurvivalPuzzleType.ButtonSequence)
            {
                puzzle.Tick(20);
                yield return null;
                int wrong = (puzzle.Sequence[0] + 1) % definition.Count;
                yield return Click(((PuzzleTokenUI)view.FindProperty("cells").GetArrayElementAtIndex(wrong).objectReferenceValue).Button);
            }
            else if (type == SurvivalPuzzleType.EnterCode)
            {
                var codePanel = (GameObject)view.FindProperty("codePanel").objectReferenceValue;
                string wrong = (puzzle.Code[0] == '0' ? "1" : "0") + puzzle.Code.Substring(1);
                foreach (char digit in wrong) yield return Click(codePanel.transform.Find("Keypad/" + digit).GetComponent<Button>());
                yield return Click(codePanel.GetComponentsInChildren<Button>().First(button => button.name == "Confirm"));
            }
            else
            {
                yield return Click(((PuzzleTokenUI)view.FindProperty("cells").GetArrayElementAtIndex(0).objectReferenceValue).Button);
                yield return Click((Button)view.FindProperty("close").objectReferenceValue);
            }
            yield return null; yield return null;
            Require(mode.Tasks.Puzzle == null && !actor.IsInMenu() && !task.Completed && mode.CompletedPuzzles == 0,
                "Failed or cancelled " + type + " closes without awarding progress and remains retryable");
            Require(mode.Turns.TurnNumber > turn, "Failure/cancellation consumes exactly the current solo turn");
        }
        Debug.Log("PUZZLE FAILURE UI CHECKS PASSED: wrong wires, sequence and code, and cancelled number puzzle.");
    }

    private static IEnumerator SolvePuzzleThroughUI(SerializedObject view, PuzzleSession puzzle)
    {
        var cellRefs = view.FindProperty("cells");
        var leftRefs = view.FindProperty("left");
        var rightRefs = view.FindProperty("right");
        switch (puzzle.Definition.type)
        {
            case SurvivalPuzzleType.MatchWires:
            case SurvivalPuzzleType.SortSupplies:
            case SurvivalPuzzleType.Batteries:
                for (int i = 0; i < puzzle.Left.Length; i++)
                {
                    if (puzzle.Matched[i]) continue;
                    var source = (PuzzleTokenUI)leftRefs.GetArrayElementAtIndex(i).objectReferenceValue;
                    var destination = (PuzzleTokenUI)rightRefs.GetArrayElementAtIndex(Array.IndexOf(puzzle.Right, puzzle.Left[i])).objectReferenceValue;
                    if (puzzle.Definition.type == SurvivalPuzzleType.MatchWires)
                    {
                        yield return Click(source.Button); yield return Click(destination.Button);
                    }
                    else yield return Drag(source, destination);
                    Require(puzzle.Matched[i], "Native matching completes the expected pair");
                }
                break;
            case SurvivalPuzzleType.ArrangeNumbers:
                bool dragged = false;
                for (int i = 0; i < puzzle.Values.Length && !puzzle.Solved; i++)
                {
                    if (puzzle.Values[i] == i) continue;
                    int destination = Array.IndexOf(puzzle.Values, i);
                    var a = (PuzzleTokenUI)cellRefs.GetArrayElementAtIndex(i).objectReferenceValue;
                    var b = (PuzzleTokenUI)cellRefs.GetArrayElementAtIndex(destination).objectReferenceValue;
                    if (!dragged) { yield return Drag(a, b); dragged = true; }
                    else { yield return Click(a.Button); yield return Click(b.Button); }
                    Require(puzzle.Values[i] == i, "Number tiles swap through real pointer input");
                    Require((int)typeof(SurvivalPuzzleUI).GetField("selectedIndex", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view.targetObject) == -1,
                        "Swapping numbers clears the previous selection");
                }
                break;
            case SurvivalPuzzleType.EnterCode:
                var field = (InputField)view.FindProperty("codeInput").objectReferenceValue;
                var codePanel = (GameObject)view.FindProperty("codePanel").objectReferenceValue;
                yield return Click(codePanel.transform.Find("Keypad/CLEAR").GetComponent<Button>());
                Require(field.text == "", "Keypad clear removes partial entry");
                foreach (char digit in puzzle.Code) yield return Click(codePanel.transform.Find("Keypad/" + digit).GetComponent<Button>());
                Require(field.text == puzzle.Code, "Native keypad enters the complete code");
                yield return Click(codePanel.transform.Find("Keypad/0").GetComponent<Button>());
                Require(field.text == puzzle.Code, "Keypad respects configured code length");
                yield return Click(codePanel.transform.Find("Keypad/DEL").GetComponent<Button>());
                Require(field.text.Length == puzzle.Code.Length - 1, "Keypad delete edits one digit");
                yield return Click(codePanel.transform.Find("Keypad/" + puzzle.Code[puzzle.Code.Length - 1]).GetComponent<Button>());
                yield return Click(codePanel.GetComponentsInChildren<Button>().First(button => button.name == "Confirm"));
                break;
            case SurvivalPuzzleType.ButtonSequence:
                while (!puzzle.SequenceReady) yield return null;
                for (int i = puzzle.SequenceProgress; i < puzzle.Sequence.Length; i++)
                    yield return Click(((PuzzleTokenUI)cellRefs.GetArrayElementAtIndex(puzzle.Sequence[i]).objectReferenceValue).Button);
                break;
            case SurvivalPuzzleType.Circuit:
                var solution = (int[])typeof(PuzzleSession).GetField("solution", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(puzzle);
                for (int i = 0; i < puzzle.Ports.Length && !puzzle.Solved; i++)
                    for (int turn = 0; turn < 4 && puzzle.Ports[i] != solution[i] && !puzzle.Solved; turn++)
                        yield return Click(((PuzzleTokenUI)cellRefs.GetArrayElementAtIndex(i).objectReferenceValue).Button);
                break;
            case SurvivalPuzzleType.PowerSwitches:
                int mask = -1;
                for (int candidate = 0; candidate < 1 << puzzle.Switches.Length; candidate++)
                {
                    var state = (bool[])puzzle.Switches.Clone();
                    for (int i = 0; i < state.Length; i++)
                        if ((candidate & (1 << i)) != 0) { state[i] = !state[i]; state[(i + 1) % state.Length] = !state[(i + 1) % state.Length]; }
                    if (Array.TrueForAll(state, value => value)) { mask = candidate; break; }
                }
                Require(mask >= 0, "Displayed power-switch puzzle has a solution");
                for (int i = 0; i < puzzle.Switches.Length && !puzzle.Solved; i++)
                    if ((mask & (1 << i)) != 0) yield return Click(((PuzzleTokenUI)cellRefs.GetArrayElementAtIndex(i).objectReferenceValue).Button);
                break;
            case SurvivalPuzzleType.Breaker:
                for (int i = 0; i < puzzle.Switches.Length && !puzzle.Solved; i++)
                    if (puzzle.Switches[i] != puzzle.Targets[i]) yield return Click(((PuzzleTokenUI)cellRefs.GetArrayElementAtIndex(i).objectReferenceValue).Button);
                break;
        }
        yield return null; yield return null;
    }

    private static Vector2 Center(Component component)
    {
        var corners = new Vector3[4];
        ((RectTransform)component.transform).GetWorldCorners(corners);
        return (corners[0] + corners[2]) * .5f;
    }

    private static IEnumerator Click(Component component)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        var position = Center(component);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        yield return null;
        yield return null;
    }

    private static IEnumerator Drag(Component source, Component target)
    {
        var start = Center(source);
        var end = Center(target);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = start });
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = start }.WithButton(MouseButton.Left));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = start + new Vector2(20, 0) }.WithButton(MouseButton.Left));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = end }.WithButton(MouseButton.Left));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = end });
        yield return null;
        yield return null;
    }

    private static bool RenderedCircuitConnected(PuzzleSession puzzle)
    {
        int size = puzzle.Definition.circuitSize;
        if ((puzzle.Ports[puzzle.StartCell] & 8) == 0 || (puzzle.Ports[puzzle.EndCell] & 2) == 0)
            return false;
        var pending = new Queue<int>();
        var seen = new HashSet<int>
        {
            puzzle.StartCell
        };
        pending.Enqueue(puzzle.StartCell);
        var directions = new[]
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };
        while (pending.Count > 0)
        {
            int cell = pending.Dequeue();
            if (cell == puzzle.EndCell)
                return true;
            for (int d = 0; d < directions.Length; d++)
            {
                var screenCell = new Vector2Int(cell % size, -(cell / size)) + directions[d];
                int row = -screenCell.y;
                if (screenCell.x < 0 || screenCell.x >= size || row < 0 || row >= size)
                    continue;
                int next = row * size + screenCell.x;
                if ((puzzle.Ports[cell] & (1 << d)) != 0 && (puzzle.Ports[next] & (1 << ((d + 2) % 4))) != 0 && seen.Add(next))
                    pending.Enqueue(next);
            }
        }

        return false;
    }

    private static void Set(Object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static string Argument(string key, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    private static void Require(bool condition, string message, bool log = true)
    {
        if (!condition)
            throw new Exception("FAIL: " + message);
        if (log)
            Debug.Log("PASS: " + message);
    }

    private static void Watchdog()
    {
        if (EditorApplication.timeSinceStartup > deadline)
        {
            Debug.LogError("Survival rules checks timed out");
            Finish(1);
        }
    }

    private static void Finish(int result)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Watchdog;
        if (keyboard != null)
            InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = background;
        InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
        if (mouse != null)
            InputSystem.RemoveDevice(mouse);
        Time.timeScale = 1;
        EditorApplication.Exit(result);
    }
}
