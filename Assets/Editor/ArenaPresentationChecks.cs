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
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Test the saved interface with real Input System pointer events, not direct button callbacks.
[InitializeOnLoad]
public static class ArenaPresentationChecks
{
    private const string Running = "ArenaPresentationChecks.Running";
    private static Mouse mouse;
    private static double deadline;
    // Resume the registered checks after Unity enters Play Mode.
    static ArenaPresentationChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false))
                return;
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Watchdog;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse = InputSystem.AddDevice<Mouse>();
            var runner = new GameObject("Presentation checks").AddComponent<ArenaCheckRunner>();
            Object.DontDestroyOnLoad(runner.gameObject);
            runner.StartCoroutine(Guard(Check()));
        };
    }

    // Start the saved-scene checks for arena setup, HUD layout and camera framing.
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/FreeForAll.unity");
        NativeUiChecks.SetSize(1280, 720);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    // Advance the check steps and report failures through the normal validation cleanup.
    private static IEnumerator Guard(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        while (stack.Count > 0)
        {
            object next;
            try
            {
                var current = stack.Peek();
                if (!current.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                next = current.Current;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(1);
                yield break;
            }

            if (next is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }

            yield return next;
        }

        Debug.Log("ARENA PRESENTATION PASSED: native setup, pause/resume, Free For All health, Hot Potato timer, portraits and HUD bounds.");
        Finish(0);
    }

    // Exercise arena setup, HUD layout and camera framing using the saved scene objects.
    private static IEnumerator Check()
    {
        Directory.CreateDirectory("Logs/ArenaPresentationPreviews");
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
            yield return null;
            var match = Object.FindFirstObjectByType<ArenaMatch>();
            var ui = Object.FindFirstObjectByType<ArenaUI>();
            var pool = Object.Instantiate(match.Events.Pool);
            pool.enableEvents = false;
            Set(match.Events, "pool", pool);
            var mapSettings = Object.Instantiate(match.Map.Settings);
            mapSettings.fixedSeed = true;
            mapSettings.seed = 1234;
            Set(match.Map, "settings", mapSettings);
            var lobby = (RectTransform)ui.transform.Find("Match Setup");
            var screen = (RectTransform)lobby.Find("Lobby Screen");
            var rows = screen.Find("Player Rows");
            Require(rows.GetChild(0).gameObject.activeSelf && rows.GetChild(1).gameObject.activeSelf && !rows.GetChild(2).gameObject.activeSelf, "Two locker cards by default");
            yield return Click(screen.Find("Available Lockers/Add Player 4").GetComponent<Button>());
            Require(rows.GetChild(3).gameObject.activeSelf, "The fourth locker can add a player");
            CheckBounds(screen);
            yield return Capture(scene + "-setup-4");
            yield return Click(screen.Find("Player Count/Count Buttons/3 PLAYERS").GetComponent<Button>());
            Require(!rows.GetChild(3).gameObject.activeSelf, "Three-player setup");
            yield return Click(screen.Find("Player Count/Count Buttons/2 PLAYERS").GetComponent<Button>());
            yield return Capture(scene + "-setup-2");
            Require(rows.GetChild(0).Find("Skin Portrait").GetComponent<Image>().sprite != null, "Reuse real skin portraits in setup");
            var skin = rows.GetChild(0).Find("Skin Selection").GetComponent<Dropdown>();
            int initialSkin = skin.value;
            yield return Click(rows.GetChild(0).Find(">").GetComponent<Button>());
            Require(skin.value != initialSkin, "Locker arrows select the player's skin");
            var controls = rows.GetChild(0).Find("Controls").GetComponent<Dropdown>();
            Require(controls.options.Count > 1, "Each local locker still has controls for keyboard and gamepads");
            yield return Click(screen.Find("Player Count/Count Buttons/4 PLAYERS").GetComponent<Button>());
            yield return Click(screen.Find("START MATCH").GetComponent<Button>());
            Require(match.Playing && match.Remaining == 4, "Native Start Match keeps existing player setup");
            yield return new WaitForSeconds(.8f);
            Canvas.ForceUpdateCanvases();
            var hud = (RectTransform)ui.transform.Find("Match HUD");
            CheckBounds(hud);
            Require(hud.rect.height < 280, "HUD stays compact for four players");
            foreach (var text in match.Map.GetComponentsInChildren<TextMesh>())
                Require(text.name == "Scoreboard display", "No floating room or area labels");
            Require(match.Map.Layout.groupedProps.Count >= 4, "Actual themed obstacles appear in the play area");
            Require(match.Map.Layout.Connected(), "Added cover preserves connected routes");
            var health = hud.Find("Player Health");
            if (match is HotPotatoController)
            {
                Require(!health.gameObject.activeSelf && !hud.Find("Health Divider").gameObject.activeSelf,
                    "Hot Potato hides health and its divider");
                Require(hud.Find("Potato Details").gameObject.activeInHierarchy, "Hot Potato still shows the round, timer and holder");
            }
            else
            {
                for (int i = 0; i < 4; i++)
                    Require(health.GetChild(i).gameObject.activeInHierarchy, "Free For All shows every participant's health");
                match.Players[1].Damage(1);
                yield return null;
                yield return null;
                Require(health.GetChild(1).Find("HP").GetComponent<Text>().text == "4/5", "HUD follows Player 2's own health");
                Require(health.GetChild(0).Find("HP").GetComponent<Text>().text == "5/5", "Other players keep their own health");
                var hearts = health.GetChild(1).Find("Hearts").GetComponent<Text>();
                Require(hearts.text.StartsWith(new string ('♥', 4)) && hearts.text.Contains("<color="), "Filled and empty shared hearts");
            }
            foreach (var player in match.Players)
            {
                foreach (var label in player.GetComponentsInChildren<TextMesh>())
                    Require(match is KingOfTheHillController && int.TryParse(label.text, out _),
                        "King of the Hill shows the score without adding a player-name label");
                Require(player.GetComponent<ArenaPlayerIndicator>() != null, "Colored player ring component");
            }

            Require(!ui.transform.Find("Show Events").gameObject.activeSelf, "Event assistance button is disabled by default");
            var debugUI = new SerializedObject(ui);
            debugUI.FindProperty("showEventButton").boolValue = true;
            debugUI.ApplyModifiedPropertiesWithoutUndo();
            yield return null;
            yield return Click(ui.transform.Find("Show Events").GetComponent<Button>());
            Require(match.Events.EventLocationsVisible, "Show Events reveals assistance only");
            yield return Click(ui.transform.Find("Show Events").GetComponent<Button>());
            Require(!match.Events.EventLocationsVisible, "Show Events can be hidden again");
            debugUI.Update();
            debugUI.FindProperty("showEventButton").boolValue = false;
            debugUI.ApplyModifiedPropertiesWithoutUndo();
            yield return null;
            if (scene == "FreeForAll")
            {
                var sign = System.Array.Find(match.Map.GetComponentsInChildren<TextMesh>(), t => t.name == "Scoreboard display");
                Require(sign != null && sign.text == "GYM", "Short readable gym sign replaces tiny team scores");
                var digits = System.Array.Find(match.Map.GetComponentsInChildren<TextMesh>(), t => t.name == "Scoreboard display" && t.text == "00");
                Require(digits != null && System.Array.Exists(match.Map.GetComponentsInChildren<TextMesh>(), t => t.text == "HOME"), "Gym board has actual score labels and digits");
                float pixels = digits.GetComponent<Renderer>().bounds.size.y * Screen.height / (2 * Camera.main.orthographicSize);
                Require(pixels >= 8, "Gym lettering is readable at the full-map camera distance");
            }

            CheckMapFrame(match);
            yield return Capture(scene + "-gameplay");
            yield return Click(ui.transform.Find("PAUSE").GetComponent<Button>());
            Require(match.Paused, "Native Pause action");
            yield return Click(ui.transform.Find("Pause Menu/RESUME").GetComponent<Button>());
            Require(!match.Paused, "Native Resume action");
            NativeUiChecks.SetSize(720, 960);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(.3f);
            Canvas.ForceUpdateCanvases();
            CheckBounds(hud);
            CheckMapFrame(match);
            yield return Capture(scene + "-portrait");
            NativeUiChecks.SetSize(1280, 720);
            yield return null;
            match.ReturnToSetup();
            yield return null;
            yield return Click(screen.Find("Player Count/Count Buttons/2 PLAYERS").GetComponent<Button>());
            yield return Click(screen.Find("START MATCH").GetComponent<Button>());
            yield return new WaitForSeconds(.4f);
            CheckMapFrame(match);
            Require(hud.rect.height < (scene == "FreeForAll" ? 180 : 245), "Two-player HUD stays compact");
            yield return Capture(scene + "-gameplay-2");
            match.ReturnToSetup();
            yield return null;
        }
    }

    // Verify the camera keeps the generated map inside the visible play area.
    private static void CheckMapFrame(ArenaMatch match)
    {
        var layout = match.Map.Layout;
        foreach (var cell in new[]
        {
            new Vector2(-.5f, -.5f),
            new Vector2(layout.Width - .5f, -.5f),
            new Vector2(-.5f, layout.Height - .5f),
            new Vector2(layout.Width - .5f, layout.Height - .5f)
        }

        )
        {
            var point = Camera.main.WorldToViewportPoint(match.Map.World(cell));
            Require(point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1, "Whole playable map fits the camera");
        }
    }

    // Drive the actual UI input path so the check includes the control's event handling.
    private static IEnumerator Click(Selectable target)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)target.transform;
        Vector2 point = rect.TransformPoint(rect.rect.center);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
        yield return null;
        yield return null;
    }

    private static IEnumerator Capture(string name)
    {
        for (int i = 0; i < 3; i++)
            yield return null;
        ScreenCapture.CaptureScreenshot("Logs/ArenaPresentationPreviews/" + name + ".png");
        for (int i = 0; i < 3; i++)
            yield return null;
    }

    private static void CheckBounds(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Require(corners[0].x >= 0 && corners[0].y >= 0 && corners[2].x <= Screen.width && corners[2].y <= Screen.height, "UI fits the current screen");
    }

    // Assign serialized test or authoring values through Unity's saved-property interface.
    private static void Set(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    // Stop this check with a clear message when a required behavior is missing.
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    // Fail a stuck check after its deadline so batch validation cannot wait forever.
    private static void Watchdog()
    {
        if (EditorApplication.timeSinceStartup > deadline)
        {
            Debug.LogError("Presentation checks timed out");
            Finish(1);
        }
    }

    // Clean up the check and return its result code to the batch runner.
    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Watchdog;
        EditorApplication.Exit(code);
    }
}
