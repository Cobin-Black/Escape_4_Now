using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Escape4Now.Menus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Checks real menu actions, seven-entry navigation, and the Online Race route.
[InitializeOnLoad]
public static class OnlineMenuChecks
{
    private const string Running = "OnlineMenuChecks.Running";
    private static Keyboard keyboard;
    private static Gamepad gamepad;
    private static double deadline;
    static OnlineMenuChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 45;
            EditorApplication.update += Watchdog;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            var runner = new GameObject("Online menu check").AddComponent<OnlineMenuCheckRunner>();
            Object.DontDestroyOnLoad(runner.gameObject);
            runner.StartCoroutine(Guard(Check()));
        };
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Running, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        NativeUiChecks.SetSize(1280, 720);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Check()
    {
        yield return null;
        yield return null;
        var menu = Object.FindFirstObjectByType<MainMenuScreen>();
        Require(Section(menu) == "GameMode", "Existing local selection remains the default");
        Directory.CreateDirectory("Logs/OnlineMenuPreviews");
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Logs/OnlineMenuPreviews/Local.png");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
        yield return null;
        yield return null;
        Require(Section(menu) == "Online", "Up from Game Mode selects Online Mode");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Logs/OnlineMenuPreviews/Online.png");
        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
        yield return null;
        yield return null;
        Require(Section(menu) == "Quit", "Gamepad wraps from Online to Quit across seven entries");
        InputSystem.QueueStateEvent(gamepad, new GamepadState());
        yield return null;
        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.down });
        yield return null;
        yield return null;
        Require(Section(menu) == "Online", "Gamepad wraps back to Online");
        InputSystem.QueueStateEvent(gamepad, new GamepadState());
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
        float limit = Time.realtimeSinceStartup + 10;
        while (SceneManager.GetActiveScene().name != "OnlineRace" && Time.realtimeSinceStartup < limit) yield return null;
        Require(SceneManager.GetActiveScene().name == "OnlineRace", "Submit opens the enabled OnlineRace scene");
        yield return null;
        Debug.Log("ONLINE MENU CHECKS PASSED: keyboard, gamepad, seven sections, online scene route.");
    }

    private static string Section(MainMenuScreen menu) => typeof(MainMenuScreen)
        .GetField("section", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu).ToString();

    private static IEnumerator Guard(IEnumerator check)
    {
        while (true)
        {
            object current;
            try { if (!check.MoveNext()) break; current = check.Current; }
            catch (Exception error) { Debug.LogException(error); Finish(1); yield break; }
            yield return current;
        }
        Finish(0);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Online menu check failed: " + message);
    }

    private static void Watchdog() { if (EditorApplication.timeSinceStartup > deadline) Finish(2); }
    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Watchdog;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (gamepad != null) InputSystem.RemoveDevice(gamepad);
        EditorApplication.Exit(code);
    }
}

public sealed class OnlineMenuCheckRunner : MonoBehaviour { }
