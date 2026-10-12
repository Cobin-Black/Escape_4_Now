using System;
using System.Collections;
using System.Collections.Generic;
using Escape4Now.Map;
using Escape4Now.Multiplayer;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

//Checks same-device setup and movement without saving changes to the scene.
[InitializeOnLoad]
public static class LocalMultiplayerChecks
{
    private const string Running = "LocalMultiplayerChecks.Running";
    private static IEnumerator checks;
    private static Keyboard keyboard;
    private static double deadline;
    private static InputSettings.BackgroundBehavior previousBackground;
    private static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

    //Restores the test runner after Unity reloads scripts for Play Mode.
    static LocalMultiplayerChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false))
            {
                checks = CheckMatch();
                deadline = EditorApplication.timeSinceStartup + 90;
                previousBackground = InputSystem.settings.backgroundBehavior;
                previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                EditorApplication.update += Tick;
                UnityEngine.Object.FindFirstObjectByType<LocalMultiplayerSession>().StartCoroutine(RunChecks());
            }
        };
    }

    //Run in a separate editor with -executeMethod LocalMultiplayerChecks.Run.
    public static void Run()
    {
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying = true;
    }

    //Prevents a broken test from waiting forever.
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Checks timed out.");
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }

    //Runs input checks on game frames, not Editor UI updates.
    private static IEnumerator RunChecks()
    {
        while (true)
        {
            bool more;
            try { more = checks.MoveNext(); }
            catch (Exception error) { Debug.LogException(error); Finish(1); yield break; }
            if (!more) { Finish(0); yield break; }
            yield return checks.Current;
        }
    }

    //Tests player ownership, valid spawns, connected paths, and a complete round.
    private static IEnumerator CheckMatch()
    {
        yield return null;
        LocalMultiplayerSession session = UnityEngine.Object.FindFirstObjectByType<LocalMultiplayerSession>();
        Require(session != null, "Scene has the multiplayer component");
        int count = 4;
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length - 1; i++)
            if (arguments[i] == "-schoolPlayers" && int.TryParse(arguments[i + 1], out int chosen)) count = chosen;
        Require(!session.TryStartLocalMatch(5), "More than four players are rejected");
        Require(!session.TryStartLocalMatch(1), "At least two players are needed");
        Require(session.TryStartLocalMatch(count), "Selected player count can start");
        Require(!session.TryStartLocalMatch(4), "A running match cannot duplicate players");
        for (int i = 0; i < 10; i++) yield return null;
        PlayerCharacter[] players = UnityEngine.Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);
        Array.Sort(players, (left, right) => string.CompareOrdinal(left.name, right.name));
        TurnSystemController turns = UnityEngine.Object.FindFirstObjectByType<TurnSystemController>();
        IsometricMapTemplate map = UnityEngine.Object.FindFirstObjectByType<IsometricMapTemplate>();
        Require(players.Length == count && map.Width == 18 && map.Height == 18, "Selected players on an 18 by 18 map");
        int doorCount = 0;
        int exitCount = 0;
        Vector2Int exitCell = default;
        foreach (Transform child in map.GetComponentsInChildren<Transform>())
        {
            if (child.name == "Exit door")
            {
                exitCount++;
                Vector2Int cell = map.WorldToGrid(child.parent.position);
                exitCell = cell;
                Require(child.parent.Find("Wall") == null && child.parent.Find("Wall cap") == null,
                    "Exit door replaces the border wall block");
                Require((cell.x == 0 || cell.x == 17) != (cell.y == 0 || cell.y == 17),
                    "Exit door is on a border, not a corner");
                Vector2Int inside = new Vector2Int(Mathf.Clamp(cell.x, 1, 16), Mathf.Clamp(cell.y, 1, 16));
                Require(map.IsWalkable(inside) && !map.IsOccupied(inside), "Exit has a clear floor approach");
                Require(map.IsExit(cell) && map.IsWalkable(cell), "Door uses the existing exit tile");
            }
            if (child.name != "Door frame") continue;
            doorCount++;
            Require(child.GetComponent<Collider>() == null && child.GetComponent<Collider2D>() == null,
                "Visual door has no blocking collider");
        }
        Require(doorCount >= 3, "Connected rooms have visible entrances");
        Require(exitCount == 1, "One working exit door is present");
        MapEventController events = UnityEngine.Object.FindFirstObjectByType<MapEventController>();
        Require(events != null && events.isActiveAndEnabled && events.EventsPlaced,
            "Existing event spawner is active");
        Require(UnityEngine.Object.FindObjectsByType<EventTileMarker>(FindObjectsSortMode.None).Length == 5,
            "All five event tiles are present");
        Require(UnityEngine.Object.FindObjectsByType<Escape4Now.Items.Item>(FindObjectsSortMode.None).Length >= 2,
            "Existing items are present");
        Require(UnityEngine.Object.FindObjectsByType<Escape4Now.Obstacles.ObstacleGridControl>(FindObjectsSortMode.None).Length == 6,
            "All six existing obstacles are present");
        var cells = new HashSet<Vector2Int>();
        for (int i = 0; i < players.Length; i++)
        {
            Require(players[i].isActiveAndEnabled && cells.Add(players[i].GridPosition), "Unique active start");
            Require(map.IsWalkable(players[i].GridPosition) && map.IsOccupied(players[i].GridPosition), "Start is a floor tile");
            Require(players[i].Inventory != null && players[i].Inventory.Player == players[i], "Separate player inventory");
            if (i > 0) Require(map.TryFindPath(players[0].GridPosition, players[i].GridPosition, out _), "Room spawns are connected");
        }
        var available = map.GetAvailableFloorTiles();
        foreach (Vector2Int cell in available)
            Require(map.IsWalkable(cell) && !map.IsOccupied(cell), "Placement hook returns empty floors");
        Require(!map.TryGetRandomFloor(out _, new HashSet<Vector2Int>(available)), "Full reservation fails cleanly");
        Require(!ReferenceEquals(players[0].EventState, players[1].EventState), "Separate player state");
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
        {
            CaptureMap(map, 1280, 720, "wide");
            CaptureMap(map, 960, 720, "standard");
        }
        keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.actions.Enable();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return null;
        yield return null;
        Require(players[0].HasRolled && !players[1].HasRolled, "Only the current player rolls");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Require(!players[1].MoveToGridPosition(players[1].GridPosition + Vector2Int.up), "Inactive player cannot move");
        Require(!players[0].MoveToGridPosition(players[1].GridPosition), "Players cannot share a tile");
        Vector2Int before = players[0].GridPosition;
        Vector2Int destination = before;
        foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            if (map.IsWalkable(before + direction) && !map.IsOccupied(before + direction)
                && !events.HasEvent(before + direction))
            { destination = before + direction; break; }
        Require(destination != before && players[0].MoveToGridPosition(destination), "Active player can move");
        while (players[0].IsMoving) yield return null;
        Require(players[0].GridPosition == destination, "Move ends on the chosen floor");
        if (turns.CurrentPlayerIndex == 0) turns.AdvanceTurn();
        for (int i = 1; i < count; i++)
        {
            Require(turns.CurrentPlayerIndex == i, "Next player's turn");
            players[i].SetMovesFromItem(1);
            turns.AdvanceTurn();
        }
        Require(turns.CurrentPlayerIndex == 0 && turns.TurnNumber == 2, "Round returns to Player 1");
        //Check the last step through the real door after the regular turn checks.
        Vector2Int approach = new Vector2Int(Mathf.Clamp(exitCell.x, 1, 16), Mathf.Clamp(exitCell.y, 1, 16));
        Require(players[0].SetGridPosition(approach), "Player can stand beside the exit");
        players[0].SetMovesFromItem(1);
        Require(players[0].MoveToGridPosition(exitCell), "Player can walk through the exit");
        while (players[0].IsMoving) yield return null;
        Require(players[0].GridPosition == exitCell && Time.timeScale == 0f, "Reaching the door pauses the match for the win");
        Time.timeScale = 1f;
        Debug.Log("SAME-DEVICE AND SCHOOL MAP CHECKS PASSED");
    }

    //Saves the real game camera at two desktop sizes when a graphics device is available.
    private static void CaptureMap(IsometricMapTemplate map, int width, int height, string label)
    {
        Camera camera = Camera.main;
        var target = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        try
        {
            target.Create();
            camera.targetTexture = target;
            map.SendMessage("FitCamera");
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            var colors = new HashSet<Color32>();
            Color32[] pixels = image.GetPixels32();
            for (int i = 0; i < pixels.Length; i += 37) colors.Add(pixels[i]);
            Require(colors.Count > 10, "Rendered map is not blank");
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllBytes("Logs/school-map-" + label + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            target.Release();
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(image);
            map.SendMessage("FitCamera");
        }
    }

    //Names each check in the Unity log.
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Debug.Log("PASS: " + message);
    }

    //Releases test input and closes only the separate test editor.
    private static void Finish(int result)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = previousBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
        EditorApplication.Exit(result);
    }
}
