// Checks the tutorial lessons and their controls without changing the normal match rules.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Escape4Now.GameModes;
using Escape4Now.Multiplayer;
using Escape4Now.Player;
using Escape4Now.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class TutorialGameplayChecks
{
    private const string Running = "TutorialGameplayChecks.Running";
    private static Keyboard keyboard;
    private static int expectedSection, sceneHandle;
    private static double deadline;
    private static InputSettings.BackgroundBehavior background;
    private static InputSettings.EditorInputBehaviorInPlayMode editorInput;

    static TutorialGameplayChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            keyboard = InputSystem.AddDevice<Keyboard>();
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            expectedSection = 1;
            sceneHandle = -1;
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Tutorial.unity");
        if (!Application.isBatchMode)
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
            NativeUiChecks.SetSize(1280, 720);
        }
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Tutorial checks timed out.");
            Scene scene = SceneManager.GetActiveScene();
            if (expectedSection == 6 && scene.name == "Race_Gamemode")
            {
                var session = Object.FindFirstObjectByType<LocalMultiplayerSession>();
                if (session == null) return;
                Require(!session.MatchStarted && Time.timeScale == 1f, "Finish opens the Race lobby without auto-starting or leaving it paused");
                Debug.Log("TUTORIAL GAMEPLAY CHECKS PASSED: five real lessons, configured actions, locker search, Warp, exit, and Race handoff.");
                Finish(0);
                return;
            }
            if (scene.handle == sceneHandle) return;
            var tutorial = Object.FindFirstObjectByType<Tutorial_Functionality>();
            if (tutorial == null) return;
            Require((int)tutorial.CurrentSection == expectedSection, "Scene reload reaches the expected lesson");
            sceneHandle = scene.handle;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            tutorial.StartCoroutine(Execute(tutorial));
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }

    private static IEnumerator Execute(Tutorial_Functionality tutorial)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(Check(tutorial));
        while (stack.Count > 0)
        {
            object current;
            try
            {
                var checks = stack.Peek();
                if (!checks.MoveNext()) { stack.Pop(); continue; }
                current = checks.Current;
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); yield break; }
            yield return current;
        }
    }

    private static IEnumerator Check(Tutorial_Functionality tutorial)
    {
        yield return null; yield return null;
        var context = Object.FindFirstObjectByType<MatchUIContext>();
        PlayerCharacter player = context.ActivePlayer;
        Require(player != null && player.GetComponent<SpriteRenderer>().enabled, "Player becomes visible during the actual lesson");
        var overlay = Object.FindFirstObjectByType<TutorialOverlay>();
        var settings = new SerializedObject(overlay);
        var card = ((GameObject)settings.FindProperty("panel").objectReferenceValue).GetComponent<RectTransform>();
        Require(card.GetComponent<ContentSizeFitter>() != null, "Lesson card sizes to its content");
        Require(settings.FindProperty("keycaps").arraySize == 4 && settings.FindProperty("progressDots").arraySize == 5,
            "Tutorial retains keycaps and five progress indicators");
        var firstCap = (Text)settings.FindProperty("keycaps").GetArrayElementAtIndex(0).objectReferenceValue;
        yield return Capture(tutorial.CurrentSection + "-start");
        if (tutorial.CurrentSection == TutorialSection.Movement)
        {
            Require(firstCap.text == "SPACE", "Roll hint shows the keyboard binding only");
            var action = player.InputBindings.RollDice;
            action.ApplyBindingOverride(0, "<Keyboard>/r");
            yield return null; yield return null;
            Require(firstCap.text == "R", "Tutorial keycaps follow Input System rebinding");
            action.RemoveBindingOverride(0);
            yield return Press(Key.Space);
            Require(player.HasRolled, "Configured Roll action starts the movement lesson");
            while (player.IsRollAnimating) yield return null;
            string[] movement = { "W", "A", "S", "D" };
            for (int i = 0; i < movement.Length; i++)
                Require(((Text)settings.FindProperty("keycaps").GetArrayElementAtIndex(i).objectReferenceValue).text == movement[i],
                    "Movement shows one primary key for each direction");
            yield return SpendMoves(player);
        }
        else if (tutorial.CurrentSection == TutorialSection.Items)
        {
            Require(tutorial.TutorialItemPickedUp && player.Inventory.Items.Count == 1, "Items lesson starts with its original Double Dice");
            Rect inventory = Object.FindFirstObjectByType<ItemPickupBar>().PanelRect;
            NativeUiChecks.CheckInventoryLayout(context);
            Require(inventory.width > 0 && inventory.xMin > Screen.width * .5f && inventory.yMax > Screen.height * .8f,
                "Tutorial inventory shares the bottom-right position with Race and Survival");
            Require(firstCap.text == "E", "Use-item hint does not mix keyboard and controller labels");
            if (!Application.isBatchMode)
            {
                foreach (var size in new[] { new Vector2Int(960, 540), new Vector2Int(720, 960) })
                {
                    NativeUiChecks.SetSize(size.x, size.y);
                    for (int frame = 0; frame < 15; frame++) yield return null;
                    Rect cardBounds = MatchUIContext.ScreenRect(card);
                    var bar = Object.FindFirstObjectByType<ItemPickupBar>();
                    NativeUiChecks.CheckInventoryLayout(context);
                    Require(!cardBounds.Overlaps(bar.PanelRect) && !cardBounds.Overlaps(bar.NoticeRect),
                        "Lesson avoids inventory and item notice at " + size);
                    Require(cardBounds.xMin >= 0 && cardBounds.yMin >= 0 && cardBounds.xMax <= Screen.width && cardBounds.yMax <= Screen.height,
                        "Tutorial card fits " + size);
                    yield return Capture("Items-" + size.x + "x" + size.y);
                }
                NativeUiChecks.SetSize(1280, 720);
                for (int frame = 0; frame < 15; frame++) yield return null;
            }
            yield return Press(Key.E);
            Require(player.Inventory.Items.Count == 0 && player.HasRolled && player.MovesRemaining >= 2,
                "Actual Use Item input consumes Double Dice and gives its bonus moves");
            while (player.IsRollAnimating) yield return null;
            yield return SpendMoves(player);
        }
        else if (tutorial.CurrentSection == TutorialSection.Obstacles)
        {
            while (tutorial.TutorialObstacle == null) yield return null;
            Require(tutorial.TutorialObstacle.HasItem, "Locker lesson places exactly its teaching item");
            yield return Press(Key.Enter);
            Require(tutorial.TutorialObstacle.IsMenuOpen, "Interact opens the adjacent tutorial locker");
            yield return Press(Key.Enter);
            Require(!tutorial.TutorialObstacle.HasItem && player.Inventory.Items.Count == 1, "Second Interact takes the locker item once");
            Require(tutorial.CurrentSection == TutorialSection.Obstacles, "Taking an item does not skip the next lesson");
        }
        else if (tutorial.CurrentSection == TutorialSection.EventTriggers)
        {
            while (!tutorial.IsSectionComplete)
            {
                if (!player.HasRolled)
                {
                    yield return Press(Key.Space);
                    while (player.IsRollAnimating) yield return null;
                }
                yield return Press(Key.W);
                while (player.IsMoving) yield return null;
            }
            Require(player.GridPosition != new Vector2Int(2, 4), "The Warp lesson performs the teleport, not just a UI skip");
        }
        else
        {
            var map = player.GridMap;
            Vector2Int exit = default;
            bool found = false;
            for (int y = 0; y < map.Height; y++) for (int x = 0; x < map.Width; x++)
                if (map.IsExit(new Vector2Int(x, y))) { exit = new Vector2Int(x, y); found = true; }
            Require(found, "Final lesson restores the exit door");
            var approach = new Vector2Int(Mathf.Clamp(exit.x, 1, map.Width - 2), Mathf.Clamp(exit.y, 1, map.Height - 2));
            Require(player.SetGridPosition(approach), "Final exit approach is walkable");
            yield return Press(Key.Space);
            while (player.IsRollAnimating) yield return null;
            Vector2Int direction = exit - approach;
            yield return Press(direction.y < 0 ? Key.S : direction.y > 0 ? Key.W : direction.x < 0 ? Key.A : Key.D);
            while (player.IsMoving) yield return null;
            Require(player.HasReachedExit, "Walking through the door completes the tutorial");
        }
        yield return null; yield return null;
        Require(tutorial.IsSectionComplete && player.IsInMenu(), "A completed lesson waits for Continue");
        Require(tutorial.CurrentSection == (TutorialSection)expectedSection, "Lesson completion never double-advances");
        if (tutorial.CurrentSection == TutorialSection.EventTriggers)
            yield return NativeUiChecks.CheckEventNotice(context, "Tutorial-events-lesson");
        var body = (Text)settings.FindProperty("body").objectReferenceValue;
        Require(body.preferredHeight <= body.rectTransform.rect.height + 1, "Restored tutorial copy fits its card");
        yield return Capture(tutorial.CurrentSection + "-complete");
        expectedSection++;
        yield return Press(Key.Enter);
    }

    private static IEnumerator SpendMoves(PlayerCharacter player)
    {
        int safety = 0;
        while (player.HasRolled && safety++ < 20)
        {
            yield return Press(player.GridPosition.y < 4 ? Key.W : Key.S);
            while (player.IsMoving) yield return null;
        }
        Require(!player.HasRolled, "Real movement consumes the lesson's full roll budget");
    }

    private static IEnumerator Press(Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null; yield return null;
    }

    private static IEnumerator Capture(string name)
    {
        if (Application.isBatchMode) yield break;
        for (int frame = 0; frame < 4; frame++) yield return null;
        Directory.CreateDirectory("Logs/TutorialPreviews");
        ScreenCapture.CaptureScreenshot("Logs/TutorialPreviews/" + name + ".png");
        for (int frame = 0; frame < 4; frame++) yield return null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Debug.Log("PASS: " + message);
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = background;
        InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
        Time.timeScale = 1f;
        EditorApplication.Exit(code);
    }
}
