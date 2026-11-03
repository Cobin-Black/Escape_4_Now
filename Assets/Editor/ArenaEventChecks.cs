using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Escape4Now.Arena;
using Escape4Now.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ArenaEventChecks
{
    private const string Key = "ArenaEventChecks.Running";
    private static double deadline;
    static ArenaEventChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false))
                return;
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update += Watchdog;
            var runner = new GameObject("Event checks").AddComponent<ArenaCheckRunner>();
            Object.DontDestroyOnLoad(runner.gameObject);
            runner.StartCoroutine(Guard(Check()));
        };
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/FreeForAll.unity");
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        NativeUiChecks.SetSize(1280, 720);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Guard(IEnumerator routine)
    {
        while (true)
        {
            object next;
            try
            {
                if (!routine.MoveNext())
                    break;
                next = routine.Current;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(1);
                yield break;
            }

            yield return next;
        }

        Debug.Log(Quick ? "ARENA EVENT SETTINGS CHECKS PASSED: concurrency, cancellation, cleanup and seed reproduction." : "ARENA EVENTS PASSED: separate pools, supported effects, warnings, Pause, swaps, clocks, blocking safety, lifetime cleanup and seeded selection.");
        Finish(0);
    }

    private static IEnumerator Check()
    {
        Directory.CreateDirectory("Logs/ArenaEventPreviews");
        // Verify shared position changes are atomic when any destination is invalid.
        var positions = new[]
        {
            1,
            2
        };
        int moved = 0;
        Require(!SharedPositionEffects.TryPermute(positions, new[] { 1, 0 }, (i, p) => i == 0, (i, p) => moved++), "Reject incomplete position exchange");
        Require(moved == 0, "No partial swap");
        foreach (string scene in new[]
        {
            "FreeForAll",
            "HotPotato"
        }

        )
        {
            var load = SceneManager.LoadSceneAsync(scene);
            while (!load.isDone)
                yield return null;
            yield return null;
            var match = Object.FindFirstObjectByType<ArenaMatch>();
            var source = match.Events.Pool;
            Require(source != null && source.visuals != null, "Saved event presentation");
            foreach (var entry in source.entries)
                Require(entry.DisabledReason.Length > 0 || entry.Supports(match.Map.Settings.room), "Mode-specific pool");
            foreach (int count in Quick ? Array.Empty<int>() : new[]
            {
                2,
                3,
                4
            }

            )
            {
                foreach (var original in source.entries)
                {
                    if (!original.enabled)
                        continue;
                    var pool = Object.Instantiate(source);
                    pool.count = new Vector2Int(1, 1);
                    pool.maximumSimultaneous = 1;
                    pool.entries = new[]
                    {
                        JsonUtility.FromJson<ArenaEventEntry>(JsonUtility.ToJson(original))
                    };
                    // JsonUtility copies values, but keep saved Unity object references explicitly.
                    var rule = pool.entries[0];
                    rule.obstaclePrefab = original.obstaclePrefab;
                    rule.warningSound = original.warningSound;
                    rule.activationSound = original.activationSound;
                    rule.warningSeconds = .25f;
                    rule.duration = .8f;
                    rule.cooldown = 0;
                    rule.maximumPerMatch = 2;
                    SetPool(match.Events, pool);
                    bool began = false;
                    for (int attempt = 0; attempt < 12 && !began; attempt++)
                    {
                        Start(match, count);
                        match.enabled = false;
                        match.Events.enabled = false;
                        // Disabling the director clears the automatic session; begin a deterministic manual clock.
                        match.Events.Begin(match);
                        began = match.Events.TryStart(rule);
                        if (!began)
                        {
                            match.ReturnToSetup();
                            yield return null;
                        }
                    }

                    Require(began, scene + " can place " + rule.kind + " for " + count);
                    var state = match.Events.Active[0];
                    Require(state.Warning, "Warning precedes effect");
                    match.Events.SetEventLocationsVisible(true);
                    yield return null;
                    Require(match.Events.GetComponentsInChildren<ArenaEventView>().Length > 0, "Existing event view supplies map assistance");
                    match.Events.SetEventLocationsVisible(false);
                    Require(match.Events.Active.Count == 1 && !state.Ended, "Hiding event assistance does not disable the event");
                    Require(!match.Events.TryStart(rule), "No duplicate simultaneous event");
                    float warning = state.WarningRemaining;
                    match.SetPaused(true);
                    match.Events.Step(2);
                    Require(state.WarningRemaining == warning, "Pause freezes warning");
                    match.SetPaused(false);
                    var first = match.Players[0];
                    var second = match.Players[1];
                    var before = new Dictionary<ArenaParticipant, Vector2>();
                    foreach (var p in match.Players)
                        before[p] = p.Position;
                    var hot = match as HotPotatoController;
                    float clock = hot != null ? hot.RemainingSeconds : 0;
                    var holder = hot != null ? hot.Holder : null;
                    match.Events.Step(.3f);
                    Require(match.Events.Active.Count == 1 && !state.Warning, "Event activates: " + rule.kind);
                    if (rule.kind == ArenaEventKind.PlayerSwap)
                    {
                        int changes = 0;
                        foreach (var p in match.Players)
                            if (p.Position != before[p])
                                changes++;
                        Require(changes == 2, "Exactly two active players swap");
                    }

                    if (rule.kind == ArenaEventKind.PotatoShuffle)
                        Require(hot.Holder != holder, "Potato transfer chooses another survivor");
                    if (rule.kind == ArenaEventKind.ExtraTime)
                        Require(hot.RemainingSeconds > clock, "Extra time");
                    if (rule.kind == ArenaEventKind.TimeCut)
                        Require(hot.RemainingSeconds < clock && hot.RemainingSeconds >= pool.minimumPotatoTimeAfterCut, "Fair time cut");
                    // Let the reused falling animation land using its own live animation clock.
                    yield return new WaitForSeconds(.5f);
                    if (rule.IsBlock)
                        Require(match.Map.Layout.blocked[state.Cells[0].x, state.Cells[0].y], "Physical event blocks its marked tile");
                    Require(match.Map.Layout.Connected(), "Routes remain connected during " + rule.kind);
                    if (rule.kind == ArenaEventKind.SlipperyFloor)
                    {
                        first.Place(state.Cells[0]);
                        Vector2 filtered = match.Events.FilterMovement(first, Vector2.right * 4, Vector2.zero, .02f);
                        Require(filtered.x > 0 && filtered.x < 4, "Wet ground reduces immediate steering response");
                    }

                    if (rule.kind == ArenaEventKind.HazardZone)
                    {
                        first.Place(state.Cells[0]);
                        int health = first.Character.Health;
                        match.Events.Step(.05f);
                        Require(first.Character.Health == health - 1, "Warned hazard applies a limited health hit");
                    }

                    if (rule.kind == ArenaEventKind.BallBarrage && state.Cells.Count >= 3)
                    {
                        Vector2 point = (Vector2)state.Cells[0] + state.Direction * 1.2f;
                        if (match.CanOccupy(first, point))
                        {
                            first.Place(point);
                            match.Events.Step(.1f);
                            match.Events.Step(.1f);
                            Require(Vector2.Distance(first.Position, point) > .05f, "Basketballs physically knock a player back");
                        }
                    }

                    float elapsed = state.Elapsed;
                    match.SetPaused(true);
                    match.Events.Step(5);
                    Require(state.Elapsed == elapsed, "Pause freezes duration");
                    match.SetPaused(false);
                    if (scene == "FreeForAll" && count == 4 && (rule.kind == ArenaEventKind.SlipperyFloor || rule.kind == ArenaEventKind.BallBarrage))
                    {
                        match.Events.Step(.1f);
                        yield return null;
                        ScreenCapture.CaptureScreenshot("Logs/ArenaEventPreviews/" + rule.kind + ".png");
                        yield return null;
                    }

                    if (scene == "HotPotato" && count == 4 && (rule.kind == ArenaEventKind.Spotlight || rule.kind == ArenaEventKind.Blackout))
                    {
                        yield return null;
                        ScreenCapture.CaptureScreenshot("Logs/ArenaEventPreviews/" + rule.kind + ".png");
                        yield return null;
                    }

                    match.Events.Step(1);
                    Require(match.Events.Active.Count == 0, "Temporary event expires");
                    Require(match.Map.Layout.Connected(), "Routes remain connected after expiry");
                    if (rule.IsBlock)
                        Require(match.Map.Layout.Walkable(state.Cells[0]), "Temporary block removed");
                    match.ReturnToSetup();
                    yield return null;
                    Object.Destroy(pool);
                    Debug.Log("EVENT PASS: " + scene + " " + count + " players " + rule.kind);
                }
            }

            // Occupying a warned landing tile must cancel the block, including a last-second entry during the fall.
            var blockRule = Array.Find(source.entries, e => e.kind == (scene == "FreeForAll" ? ArenaEventKind.FallingEquipment : ArenaEventKind.FallingProp));
            foreach (bool duringFall in new[]
            {
                false,
                true
            }

            )
            {
                var safetyPool = Object.Instantiate(source);
                safetyPool.entries = new[]
                {
                    blockRule
                };
                safetyPool.count = Vector2Int.one;
                SetPool(match.Events, safetyPool);
                Start(match, 4);
                match.enabled = false;
                match.Events.enabled = false;
                match.Events.Begin(match);
                Require(match.Events.TryStart(blockRule), "Find landing for occupancy safety check");
                var state = match.Events.Active[0];
                if (duringFall)
                    match.Events.Step(blockRule.warningSeconds + .01f);
                match.Players[0].Place(state.Cells[0]);
                if (!duringFall)
                    match.Events.Step(blockRule.warningSeconds + .01f);
                yield return new WaitForSeconds(.5f);
                match.Events.Step(0);
                Require(match.Events.Active.Count == 0 && match.Map.Layout.Walkable(state.Cells[0]), "Unsafe landing is skipped instead of trapping its occupant");
                match.ReturnToSetup();
                yield return null;
                Object.Destroy(safetyPool);
            }

            // Fixed-seed selection is independent from frame timing.
            var concurrency = Object.Instantiate(source);
            concurrency.entries = new[]
            {
                Array.Find(source.entries, e => e.kind == ArenaEventKind.PlayerSwap),
                Array.Find(source.entries, e => e.IsBlackout)
            };
            concurrency.count = new Vector2Int(2, 2);
            concurrency.maximumSimultaneous = 1;
            SetPool(match.Events, concurrency);
            Start(match, 3);
            match.enabled = false;
            match.Events.enabled = false;
            match.Events.Begin(match);
            Require(match.Events.TryStart(concurrency.entries[0]), "Start first selected warning");
            Require(!match.Events.TryStart(concurrency.entries[1]), "Simultaneous cap rejects a different selected event");
            var around = new List<Vector2Int>();
            var center = Vector2Int.RoundToInt(match.Players[0].Position);
            foreach (var d in ArenaLayout.Directions)
                if (match.Map.Layout.Walkable(center + d))
                    around.Add(center + d);
            Require(!ArenaEventSafety.CanChange(match, around), "Never surround a player with temporary blockers");
            Require(match.Map.Layout.Connected(), "Rejected route edits restore the original map");
            concurrency.enableEvents = false;
            match.Events.Step(.01f);
            Require(match.Events.Active.Count == 0, "Disabling events clears their pending and active effects");
            match.ReturnToSetup();
            yield return null;
            Object.Destroy(concurrency);
            var seeded = Object.Instantiate(source);
            SetPool(match.Events, seeded);
            match.Map.Settings.fixedSeed = true;
            match.Map.Settings.seed = 1234;
            Start(match, 3);
            string one = Selection(match.Events);
            match.ReturnToSetup();
            yield return null;
            Start(match, 3);
            Require(one == Selection(match.Events), "Fixed seed repeats event selection");
            match.ReturnToSetup();
            yield return null;
        }
    }

    private static string Selection(ArenaEventDirector director)
    {
        string s = "";
        foreach (var rule in director.Selected)
            s += rule.kind + ";";
        return s;
    }

    private static bool Quick => Array.IndexOf(Environment.GetCommandLineArgs(), "-arenaQuick") >= 0;

    private static void SetPool(ArenaEventDirector director, ArenaEventPool pool)
    {
        var data = new SerializedObject(director);
        data.FindProperty("pool").objectReferenceValue = pool;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Start(ArenaMatch match, int count)
    {
        match.enabled = true;
        var names = new string[count];
        var pads = new int[count];
        var skins = new int[count];
        for (int i = 0; i < count; i++)
        {
            names[i] = "Player " + (i + 1);
            pads[i] = skins[i] = -1;
        }

        Require(match.StartMatch(count, names, pads, skins), match.Error);
        foreach (var player in match.Players)
            player.enabled = false;
    }

    private static void Require(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void Watchdog()
    {
        if (EditorApplication.timeSinceStartup > deadline)
        {
            Debug.LogError("Event checks timed out");
            Finish(1);
        }
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Watchdog;
        EditorApplication.Exit(code);
    }
}
