// Exercises real input, collision, elimination, round timers and the saved arena scenes.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Escape4Now.Arena;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ArenaChecks
{
    private const string Running = "ArenaChecks.Running";
    private static double deadline;
    private static Keyboard keyboard;
    static ArenaChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false))
                return;
            deadline = EditorApplication.timeSinceStartup + 150;
            EditorApplication.update += Watchdog;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            var runner = new GameObject("Arena validation runner").AddComponent<ArenaCheckRunner>();
            Object.DontDestroyOnLoad(runner.gameObject);
            runner.StartCoroutine(Guard(Check()));
        };
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/FreeForAll.unity");
        SessionState.SetBool(Running, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        NativeUiChecks.SetSize(1280, 720);
        EditorApplication.isPlaying = true;
    }

    public static void Generation()
    {
        try
        {
            foreach (string name in new[]
            {
                "FreeForAll",
                "HotPotato"
            }

            )
            {
                var settings = AssetDatabase.LoadAssetAtPath<ArenaMapSettings>("Assets/Settings/Arena/" + name + " Map.asset");
                for (int seed = 0; seed < 200; seed++)
                {
                    var random = new System.Random(seed);
                    var layout = new ArenaLayout(settings, random);
                    Require(layout.Width == settings.size.x && layout.Height == settings.size.y, "Inspector map size is honored");
                    Require(layout.Connected(), name + " disconnected at " + seed);
                    Require(settings.room != ArenaRoom.Auditorium || settings.seatQuarterTurns == 2, "Chairs face the stage");
                    foreach (var placed in layout.groupedProps)
                        Require(placed.quarterTurns >= 0 && placed.quarterTurns < 4, "Valid logical prop facing");
                    Require(layout.groupedProps.Count >= 4, name + " missing cover groups at " + seed);
                    for (int count = 2; count <= 4; count++)
                    {
                        Require(layout.TrySpawns(count, settings, random, out var spawns), name + " spawns at " + seed);
                        foreach (var cell in spawns)
                            Require(layout.Fits(cell, settings.playerRadius), "Spawn overlaps geometry");
                        for (int a = 0; a < spawns.Count; a++)
                            for (int b = a + 1; b < spawns.Count; b++)
                                Require(Vector2.Distance(spawns[a], spawns[b]) >= settings.minimumSpawnDistance, "Spawn separation");
                    }

                    var repeat = new ArenaLayout(settings, new System.Random(seed));
                    Require(layout.groupedProps.Count == repeat.groupedProps.Count, "Seeded obstacle count");
                    for (int i = 0; i < layout.groupedProps.Count; i++)
                        Require(layout.groupedProps[i].cell == repeat.groupedProps[i].cell && layout.groupedProps[i].prefab == repeat.groupedProps[i].prefab && layout.groupedProps[i].quarterTurns == repeat.groupedProps[i].quarterTurns, "Seeded obstacle appearance");
                    for (int x = 0; x < layout.Width; x++)
                        for (int y = 0; y < layout.Height; y++)
                            Require(layout.blocked[x, y] == repeat.blocked[x, y], "Seed reproduction");
                }
            }

            Debug.Log("ARENA GENERATION PASSED: 400 layouts, 2/3/4 player spawns, connectivity, footprints, fixed seeds.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
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

        Debug.Log("ARENA PLAY CHECKS PASSED: keyboard/gamepad isolation, collisions, Pause, 2–4 player rounds and winners.");
        Finish(0);
    }

    private static IEnumerator Check()
    {
        yield return null;
        yield return null;
        var match = Object.FindFirstObjectByType<ArenaMatch>();
        DisableRandomEvents(match);
        Directory.CreateDirectory("Logs/ArenaPreviews");
        ScreenCapture.CaptureScreenshot("Logs/ArenaPreviews/FreeForAll-lobby.png");
        yield return null;
        for (int count = 2; count <= 4; count++)
        {
            Start(match, count);
            yield return null;
            yield return null;
            Require(match.Remaining == count, "FFA count");
            var a = match.Players[0];
            var b = match.Players[1];
            Vector2 beforeA = a.Position, beforeB = b.Position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.UpArrow));
            yield return null;
            yield return null;
            yield return null;
            Require(a.Position != beforeA && b.Position != beforeB, "Two keyboard players should move together");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            beforeA = a.Position;
            match.SetPaused(true);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return null;
            yield return null;
            Require(a.Position == beforeA, "Pause movement");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            match.SetPaused(false);
            var start = a.Position;
            a.Move(new Vector2(-100, 0));
            Require(match.Map.Layout.Fits(a.Position, match.Map.Settings.playerRadius), "Continuous collision cannot cross walls");
            a.Place(start);
            Require(!match.CanOccupy(a, b.Position), "Players cannot overlap");
            foreach (var input in new[]
            {
                Vector2.up,
                Vector2.right,
                new Vector2(1, 1).normalized,
                new Vector2(-1, 1).normalized
            }

            )
            {
                Vector2 grid = match.Map.GridDirection(input);
                float visible = Vector3.Distance(match.Map.World(grid), match.Map.World(Vector2.zero));
                Require(Mathf.Abs(visible - 1) < .001f, "Consistent horizontal, vertical and diagonal screen speed");
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
            var stopped = a.Position;
            yield return null;
            yield return null;
            Require(a.Position == stopped, "No drift after releasing normal movement");
            if (count == 4)
            {
                yield return new WaitForSeconds(.9f);
                CheckCamera(match);
                ScreenCapture.CaptureScreenshot("Logs/ArenaPreviews/FreeForAll-game.png");
                yield return null;
            }

            for (int i = 1; i < count; i++)
                match.Players[i].Damage(match.Players[i].Character.Health);
            Require(!match.Playing && match.Result.Contains(a.name), "FFA last player wins");
            match.ReturnToSetup();
            yield return null;
        }

        // Explicitly paired gamepads must never drive both players from one device.
        var pad1 = InputSystem.AddDevice<Gamepad>();
        var pad2 = InputSystem.AddDevice<Gamepad>();
        var pads = new List<Gamepad>(Gamepad.all);
        Require(match.StartMatch(2, new[] { "A", "B" }, new[] { pads.IndexOf(pad1), pads.IndexOf(pad2) }, new[] { -1, -1 }), match.Error);
        yield return null;
        var p0 = match.Players[0];
        var p1 = match.Players[1];
        Vector2 p0Before = p0.Position, p1Before = p1.Position;
        InputSystem.QueueStateEvent(pad1, new GamepadState { leftStick = Vector2.up });
        yield return null;
        yield return null;
        Require(p0.Position != p0Before && p1.Position == p1Before, "Gamepad isolation");
        match.ReturnToSetup();
        yield return null;
        // Keep the test runner alive while switching to the separately authored auditorium scene.
        var potato = CheckPotato();
        while (potato.MoveNext())
            yield return potato.Current;
        var menu = SceneManager.LoadSceneAsync("MainMenu");
        while (!menu.isDone)
            yield return null;
        yield return null;
        yield return null;
        ScreenCapture.CaptureScreenshot("Logs/ArenaPreviews/MainMenu.png");
        yield return null;
    }

    private static IEnumerator CheckPotato()
    {
        var load = SceneManager.LoadSceneAsync("HotPotato");
        while (!load.isDone)
            yield return null;
        yield return null;
        var mode = Object.FindFirstObjectByType<HotPotatoController>();
        DisableRandomEvents(mode);
        Set(mode, "roundSeconds", 4f);
        Set(mode, "roundDelay", .15f);
        Set(mode, "passCooldown", .1f);
        for (int count = 2; count <= 4; count++)
        {
            Start(mode, count);
            yield return null;
            var holder = mode.Holder;
            Require(holder != null && holder.Alive, "Initial potato holder");
            var target = mode.Players.Find(p => p != holder);
            foreach (var cell in mode.Map.Layout.Cells())
                if (mode.Map.Layout.ClearSquare(cell, 1))
                {
                    holder.Place(cell);
                    target.Place((Vector2)cell + Vector2.right * .8f);
                    break;
                }

            yield return new WaitForSeconds(.15f);
            Require(mode.Holder == holder, "Contact should not transfer potato");
            var keys = new[]
            {
                Key.F,
                Key.RightCtrl,
                Key.O,
                Key.Numpad0
            };
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[holder.Slot]));
            yield return null;
            yield return null;
            Require(mode.Holder == target, "Tag input transfers potato");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            mode.SetPaused(true);
            float paused = mode.RemainingSeconds;
            yield return new WaitForSeconds(.15f);
            Require(mode.RemainingSeconds == paused, "Pause freezes countdown");
            mode.SetPaused(false);
            if (count == 4)
            {
                yield return new WaitForSeconds(.6f);
                CheckCamera(mode);
                var marker = new SerializedObject(Object.FindFirstObjectByType<PotatoHolderView>());
                Require(((GameObject)marker.FindProperty("marker").objectReferenceValue).activeInHierarchy, "Holder marker visible");
                Require(((TextMesh)marker.FindProperty("countdown").objectReferenceValue).text.Length > 0, "Holder countdown visible");
                ScreenCapture.CaptureScreenshot("Logs/ArenaPreviews/HotPotato-game.png");
                yield return null;
            }

            int expected = count;
            while (mode.Playing)
            {
                var doomed = mode.Holder;
                mode.AdjustTime(-100);
                yield return null;
                yield return null;
                expected--;
                Require(!doomed.Alive && mode.Remaining == expected, "One elimination per explosion");
                if (expected > 1)
                {
                    yield return new WaitForSeconds(.2f);
                    Require(mode.Round == count - expected + 1 && mode.Holder != null && mode.Holder.Alive, "Next round");
                }
            }

            Require(expected == 1 && !string.IsNullOrEmpty(mode.Result), "Potato last player wins");
            mode.ReturnToSetup();
            yield return null;
        }
    }

    private static void Start(ArenaMatch match, int count)
    {
        var names = new string[count];
        var pads = new int[count];
        var skins = new int[count];
        for (int i = 0; i < count; i++)
        {
            names[i] = "Player " + (i + 1);
            pads[i] = -1;
            skins[i] = -1;
        }

        Require(match.StartMatch(count, names, pads, skins), match.Error);
    }

    private static void CheckCamera(ArenaMatch match)
    {
        foreach (var player in match.Players)
            if (player.Alive)
            {
                var point = Camera.main.WorldToViewportPoint(player.transform.position);
                Require(point.x > .01f && point.x < .99f && point.y > .01f && point.y < .99f, "All active players stay on screen beside the HUD: " + player.name + " at " + point + ", safe " + match.Map.Settings.cameraSafeViewport + ", camera " + Camera.main.transform.position + ", zoom " + Camera.main.orthographicSize);
            }
    }

    private static void Set(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void DisableRandomEvents(ArenaMatch match)
    {
        var configuration = Object.Instantiate(match.Events.Pool);
        configuration.enableEvents = false;
        var data = new SerializedObject(match.Events);
        data.FindProperty("pool").objectReferenceValue = configuration;
        data.ApplyModifiedPropertiesWithoutUndo();
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
            Debug.LogError("Arena checks timed out.");
            Finish(1);
        }
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Watchdog;
        EditorApplication.Exit(code);
    }
}

public sealed class ArenaCheckRunner : MonoBehaviour
{
}
