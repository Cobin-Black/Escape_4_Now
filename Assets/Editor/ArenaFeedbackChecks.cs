using System;
using System.Collections;
using System.IO;
using Escape4Now.Arena;
using Escape4Now.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Verify normal timed events (not forced event activation) and real potato expiry feedback.
[InitializeOnLoad]
public static class ArenaFeedbackChecks
{
    private const string Key = "ArenaFeedbackChecks.Running";
    private static double deadline;
    static ArenaFeedbackChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false))
                return;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Watchdog;
            var runner = new GameObject("Arena feedback validation").AddComponent<ArenaCheckRunner>();
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

    private static IEnumerator Guard(IEnumerator steps)
    {
        while (true)
        {
            object next;
            try
            {
                if (!steps.MoveNext())
                    break;
                next = steps.Current;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(1);
                yield break;
            }

            yield return next;
        }

        Debug.Log("ARENA FEEDBACK PASSED: normal event scheduling, assistance visibility, mounted gym fixtures, quiet explosion particles/audio, final-round feedback, pause and cleanup.");
        Finish(0);
    }

    private static IEnumerator Check()
    {
        Directory.CreateDirectory("Logs/ArenaFeedbackPreviews");
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
            Require(match.Map.EventController == match.Events, "Map Inspector links the actual mode event controller");
            var inspector = Editor.CreateEditor(match.Events);
            Require(inspector is ArenaEventInspector, "Native event Inspector is registered");
            Object.DestroyImmediate(inspector);
            Require(match.Events.Pool.enableEvents, "Saved event pool stays enabled");
            Start(match, 2);
            yield return null;
            match.enabled = false;
            match.Events.enabled = false;
            match.Events.Begin(match);
            match.Events.SetEventLocationsVisible(true);
            match.Events.Step(1);
            Require(match.Events.Active.Count == 0 && match.Events.HasScheduledEvents, "Events wait for their configured initial delay");
            Require(match.Events.NextEventSeconds > 0, "Quiet period has an inspectable countdown");
            for (int i = 0; i < 80 && match.Events.Active.Count == 0; i++)
                match.Events.Step(.25f);
            Require(match.Events.Active.Count > 0, "Scheduler naturally starts a safe event in " + scene);
            int count = match.Events.Active.Count;
            match.Events.SetEventLocationsVisible(false);
            Require(match.Events.Active.Count == count, "Hiding indicators leaves events active");
            match.Events.SetEventLocationsVisible(true);
            match.SetPaused(true);
            float clock = match.Events.NextEventSeconds;
            match.Events.Step(3);
            Require(match.Events.NextEventSeconds == clock, "Pause freezes event scheduling");
            match.SetPaused(false);
            if (scene == "FreeForAll")
            {
                var style = match.Map.Settings.roomVisuals;
                Require(style.scoreboardFacing == 3 && style.windowMountHeight > 0 && style.scoreboardMountHeight > 0, "Fixtures face inward and mount above the floor");
                Require(match.Map.transform.Find("Generated room") != null, "Existing map render remains intact");
            }

            yield return new WaitForSeconds(.15f);
            ScreenCapture.CaptureScreenshot("Logs/ArenaFeedbackPreviews/" + scene + "-events.png");
            yield return null;
            yield return null;
            match.ReturnToSetup();
            yield return null;
        }

        var mode = Object.FindFirstObjectByType<HotPotatoController>();
        mode.enabled = true;
        // Keep automatic effects out of this focused expiry/pause test.
        var disabled = Object.Instantiate(mode.Events.Pool);
        disabled.enableEvents = false;
        var eventsData = new SerializedObject(mode.Events);
        eventsData.FindProperty("pool").objectReferenceValue = disabled;
        eventsData.ApplyModifiedPropertiesWithoutUndo();
        for (int players = 3; players >= 2; players--)
        {
            Start(mode, players);
            yield return null;
            var doomed = mode.Holder;
            Vector3 position = doomed.transform.position;
            mode.AdjustTime(-100);
            yield return null;
            yield return null;
            Require(!doomed.Alive && mode.Remaining == players - 1, "One elimination per normal potato expiry");
            var burst = Object.FindFirstObjectByType<SurvivalWorldBurst>();
            Require(burst != null && Vector3.Distance(burst.transform.position, position) < 1, "Explosion remains at the eliminated holder");
            var particles = burst.GetComponentInChildren<ParticleSystem>();
            Require(particles != null && particles.IsAlive(true), "Explosion emits actual visible particles");
            var sound = burst.GetComponent<AudioSource>();
            Require(sound != null && sound.clip != null && sound.volume <= .12f && sound.isPlaying, "Explosion has low-volume audio");
            if (players == 3)
            {
                mode.SetPaused(true);
                yield return null;
                yield return null;
                float particleTime = particles.time;
                yield return new WaitForSeconds(.12f);
                Require(Mathf.Abs(particles.time - particleTime) < .01f, "Pause freezes explosion particles");
                mode.SetPaused(false);
                yield return null;
            }
            else
            {
                Require(!mode.Playing && !string.IsNullOrEmpty(mode.Result), "Final explosion preserves the win condition");
                var ui = Object.FindFirstObjectByType<ArenaUI>();
                Require(!ui.transform.Find("Match Result").gameObject.activeSelf, "Brief result delay leaves the explosion visible");
            }

            yield return new WaitForSeconds(.06f);
            var dots = new ParticleSystem.Particle[64];
            int live = particles.GetParticles(dots);
            Require(live > 1, "Explosion emits several sparks rather than a single marker");
            var spread = new Bounds(dots[0].position, Vector3.zero);
            for (int i = 1; i < live; i++)
                spread.Encapsulate(dots[i].position);
            Require(spread.size.x + spread.size.y > .15f, "Explosion spreads visibly across the screen plane");
            ScreenCapture.CaptureScreenshot("Logs/ArenaFeedbackPreviews/HotPotato-explosion-" + players + ".png");
            yield return null;
            yield return new WaitForSeconds(1.35f);
            Require(Object.FindFirstObjectByType<SurvivalWorldBurst>() == null, "Explosion cleans up without leaving effects behind");
            mode.ReturnToSetup();
            yield return null;
        }
    }

    private static void Start(ArenaMatch match, int count)
    {
        var settings = Object.Instantiate(match.Map.Settings);
        settings.fixedSeed = true;
        settings.seed = 1234;
        var data = new SerializedObject(match.Map);
        data.FindProperty("settings").objectReferenceValue = settings;
        data.ApplyModifiedPropertiesWithoutUndo();
        Require(match.StartMatch(count, new[] { "Player 1", "Player 2", "Player 3", "Player 4" }, new[] { -1, -1, -1, -1 }, new[] { -1, -1, -1, -1 }), match.Error);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static void Watchdog()
    {
        if (EditorApplication.timeSinceStartup > deadline)
        {
            Debug.LogError("Feedback checks timed out");
            Finish(1);
        }
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Watchdog;
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(code);
    }
}
