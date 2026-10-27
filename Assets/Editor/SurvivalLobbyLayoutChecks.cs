// Checks the Survival setup screen at different sizes so its controls remain visible and usable.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Escape4Now.Multiplayer;
using Escape4Now.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SurvivalLobbyLayoutChecks
{
    private const string Running = "SurvivalLobbyLayoutChecks.Running";
    private static Mouse mouse;
    private static double deadline;
    private static InputSettings.BackgroundBehavior background;
    private static InputSettings.EditorInputBehaviorInPlayMode editorInput;

    static SurvivalLobbyLayoutChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse = InputSystem.AddDevice<Mouse>();
            EditorApplication.update += Watchdog;
            Object.FindFirstObjectByType<LocalMultiplayerSession>().StartCoroutine(Execute());
        };
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SurvivalMode.unity");
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        NativeUiChecks.SetSize(1280, 720);
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Execute()
    {
        var stack = new Stack<IEnumerator>(); stack.Push(Check());
        while (true)
        {
            object current;
            try
            {
                if (stack.Count == 0) { Finish(0); yield break; }
                var next = stack.Peek();
                if (!next.MoveNext()) { stack.Pop(); continue; }
                current = next.Current;
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
            }
            catch (Exception failure) { Debug.LogException(failure); Finish(1); yield break; }
            yield return current;
        }
    }

    private static IEnumerator Check()
    {
        for (int frame = 0; frame < 12; frame++) yield return null;
        var context = Object.FindFirstObjectByType<MatchUIContext>();
        var view = new SerializedObject(context.GetComponentInChildren<LobbyUI>(true));
        var buttons = new List<Toggle> { (Toggle)view.FindProperty("singlePlayer").objectReferenceValue };
        var counts = view.FindProperty("playerCounts");
        for (int i = 0; i < counts.arraySize; i++) buttons.Add((Toggle)counts.GetArrayElementAtIndex(i).objectReferenceValue);
        var panel = (GameObject)view.FindProperty("panel").objectReferenceValue;
        var content = (RectTransform)panel.transform.Find("Content");
        var layout = content.GetComponent<VerticalLayoutGroup>();
        var start = content.Find("Start Match").GetComponent<Button>();
        Require(layout != null && content.GetComponent<ContentSizeFitter>() != null, "Lobby rows use saved Unity layout components");
        Directory.CreateDirectory("Logs/SurvivalLobbyPreviews");
        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(715, 467), new Vector2Int(720, 960) })
        {
            NativeUiChecks.SetSize(size.x, size.y);
            for (int frame = 0; frame < 10; frame++) yield return null;
            for (int count = 1; count <= buttons.Count; count++)
            {
                if (!buttons[count - 1].isOn) yield return Click(buttons[count - 1]);
                for (int frame = 0; frame < 6; frame++) yield return null;
                Canvas.ForceUpdateCanvases();
                Require(context.Session.PlayerCount == count, "Native count selector chooses " + count);
                Rect previous = default;
                for (int i = 0; i < buttons.Count; i++)
                {
                    var root = (RectTransform)buttons[i].transform;
                    Rect bounds = MatchUIContext.ScreenRect(root);
                    Require(i == 0 || !bounds.Overlaps(previous), "Count buttons do not overlap");
                    Require(Mathf.Approximately(root.rect.width, ((RectTransform)buttons[0].transform).rect.width), "Count buttons have equal widths");
                    foreach (var child in buttons[i].GetComponentsInChildren<RectTransform>(true))
                    {
                        if (child == root) continue;
                        Rect childBounds = MatchUIContext.ScreenRect(child);
                        Require(childBounds.xMin >= bounds.xMin - 1 && childBounds.xMax <= bounds.xMax + 1
                            && childBounds.yMin >= bounds.yMin - 1 && childBounds.yMax <= bounds.yMax + 1,
                            "Selection fill and label stay within the button");
                    }
                    previous = bounds;
                }
                var slots = view.FindProperty("slots");
                Rect last = default;
                for (int i = 0; i < slots.arraySize; i++)
                {
                    var slot = (GameObject)slots.GetArrayElementAtIndex(i).FindPropertyRelative("root").objectReferenceValue;
                    Require(slot.activeSelf == (i < count), "Only selected player rows are visible");
                    if (!slot.activeSelf) continue;
                    Rect bounds = MatchUIContext.ScreenRect((RectTransform)slot.transform);
                    Require(i == 0 || bounds.yMin >= last.yMax, "Player rows do not overlap"); last = bounds;
                }
                Rect startBounds = MatchUIContext.ScreenRect((RectTransform)start.transform);
                float gap = (startBounds.yMin - last.yMax) / content.lossyScale.y;
                Require(gap >= -1 && gap <= layout.spacing + 1, "Start Match follows the last visible row without empty player space");
                Rect form = MatchUIContext.ScreenRect(content);
                Require(form.xMin >= -1 && form.yMin >= -1 && form.xMax <= Screen.width + 1 && form.yMax <= Screen.height + 1, "Entire lobby fits the viewport");
                ScreenCapture.CaptureScreenshot("Logs/SurvivalLobbyPreviews/" + size.x + "x" + size.y + "-" + count + ".png");
                for (int frame = 0; frame < 4; frame++) yield return null;
            }
        }
        yield return Click(start);
        Require(context.Session.MatchStarted && context.Session.Participants.Count == 4, "Start Match retains its existing callback");
        Debug.Log("SURVIVAL LOBBY LAYOUT CHECKS PASSED: 1-4 players, equal contained controls, compact rows, native pointer Start, three viewport sizes.");
    }

    private static IEnumerator Click(Component component)
    {
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4]; ((RectTransform)component.transform).GetWorldCorners(corners);
        Vector2 position = (corners[0] + corners[2]) * .5f;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null; yield return null;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("FAIL: " + message); }
    private static void Watchdog() { if (EditorApplication.timeSinceStartup > deadline) { Debug.LogError("Lobby layout checks timed out."); Finish(1); } }
    private static void Finish(int result)
    {
        SessionState.SetBool(Running, false); EditorApplication.update -= Watchdog;
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
        EditorApplication.Exit(result);
    }
}
