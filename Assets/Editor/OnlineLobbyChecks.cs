using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Escape4Now.Menus;
using Escape4Now.Multiplayer.RelayTesting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Renders the real lobby, exercises its controls, and checks safe Ready/start behavior.
[InitializeOnLoad]
public static class OnlineLobbyChecks
{
    private const string Running = "OnlineLobbyChecks.Running";
    private static double deadline;
    static OnlineLobbyChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Watchdog;
            var runner = new GameObject("Online lobby validation").AddComponent<OnlineLobbyCheckRunner>();
            Object.DontDestroyOnLoad(runner.gameObject);
            runner.StartCoroutine(Guard(Check()));
        };
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Race_Gamemode.unity");
        SessionState.SetBool(Running, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        NativeUiChecks.SetSize(1280, 720);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Check()
    {
        yield return null;
        yield return null;
        Directory.CreateDirectory("Logs/OnlineLobbyPreviews");
        Require(Object.FindFirstObjectByType<ArcadeLobbyScreen>() != null, "Local Race still opens its arcade lobby");
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Logs/OnlineLobbyPreviews/Local-race.png");
        yield return SceneManager.LoadSceneAsync("OnlineRace");
        yield return null;
        Require(Object.FindFirstObjectByType<ArcadeLobbyScreen>() == null, "Local lobby stays out of Online Race");
        var view = Object.FindFirstObjectByType<OnlineRaceLobbyView>();
        var session = Object.FindFirstObjectByType<OnlineRaceSession>();
        Require(Reference<GameObject>(view, "setup").activeSelf, "Host/join cards are the initial view");
        Require(!Reference<GameObject>(view, "diagnosticsPanel").activeSelf, "Diagnostics are hidden initially");
        var sizes = Reference<Toggle[]>(view, "roomSizes");
        Require(sizes[0].isOn, "Room size starts at two players");
        sizes[2].isOn = true;
        Require(!sizes[0].isOn && !sizes[1].isOn, "Room sizes behave as a single selection");
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Logs/OnlineLobbyPreviews/Setup-wide.png");
        NativeUiChecks.SetSize(987, 699);
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Logs/OnlineLobbyPreviews/Setup-narrow.png");
        var corners = new Vector3[4];
        Reference<GameObject>(view, "panel").GetComponent<RectTransform>().GetWorldCorners(corners);
        Require(corners[0].x >= -1 && corners[2].x <= Screen.width + 1 && corners[0].y >= -1 && corners[2].y <= Screen.height + 1,
            "Whole lobby fits the narrow window");
        Reference<Button>(view, "openDiagnostics").onClick.Invoke();
        yield return null;
        Require(Reference<GameObject>(view, "diagnosticsPanel").activeSelf, "Connection settings opens");
        Require(!Reference<CanvasGroup>(view, "mainControls").interactable, "Modal blocks underlying gamepad controls");
        Reference<Dropdown>(view, "protocol").Show();
        yield return null;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Logs/OnlineLobbyPreviews/Connection-settings.png");
        Require(GameObject.Find("Dropdown List") != null, "Protocol dropdown opens its native options");
        Reference<Dropdown>(view, "protocol").Hide();
        Reference<Button>(view, "closeDiagnostics").onClick.Invoke();
        Require(Reference<CanvasGroup>(view, "mainControls").interactable, "Closing restores lobby interaction");
        NativeUiChecks.SetSize(1280, 720);
        Reference<Button>(view, "host").onClick.Invoke();
        float limit = Time.realtimeSinceStartup + 45;
        while ((!session.Relay.Active || session.Relay.Busy) && string.IsNullOrEmpty(session.Relay.Error) && Time.realtimeSinceStartup < limit)
            yield return null;
        Require(session.Relay.Active && !session.Relay.Busy, "Actual Relay host starts: " + session.Relay.Error);
        yield return null;
        Require(Reference<GameObject>(view, "room").activeSelf && !Reference<GameObject>(view, "setup").activeSelf,
            "Connected room replaces the setup cards");
        Require(session.Capacity == 4, "The selected room size reaches the host");
        Require(!Reference<Button>(view, "start").interactable, "A host alone cannot start the match");
        Reference<Button>(view, "ready").onClick.Invoke();
        yield return null;
        Require(session.Ready, "Ready button changes the real session state");
        Require(!Reference<Button>(view, "start").interactable, "Ready host still waits for the guest");
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Logs/OnlineLobbyPreviews/Room-ready.png");
        Reference<Button>(view, "back").onClick.Invoke();
        limit = Time.realtimeSinceStartup + 10;
        while (SceneManager.GetActiveScene().name != "MainMenu" && Time.realtimeSinceStartup < limit) yield return null;
        yield return null;
        yield return null;
        Require(SceneManager.GetActiveScene().name == "MainMenu", "Back returns to the main menu");
        Require(Object.FindFirstObjectByType<Unity.Netcode.NetworkManager>() == null, "Returning does not leak the persistent network manager");
        Debug.Log("ONLINE LOBBY CHECKS PASSED: wide/narrow layouts, protocol options, real hosting, ready gating, back/cleanup.");
    }

    private static T Reference<T>(object target, string field) => (T)target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception("Online lobby check failed: " + message); }
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
    private static void Watchdog() { if (EditorApplication.timeSinceStartup > deadline) Finish(2); }
    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Watchdog;
        EditorApplication.Exit(code);
    }
}

public sealed class OnlineLobbyCheckRunner : MonoBehaviour { }
