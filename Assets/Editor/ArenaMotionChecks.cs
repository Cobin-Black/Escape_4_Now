using System;
using System.Collections;
using System.IO;
using Escape4Now.Arena;
using Escape4Now.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Focus on actual input, wall contact, visual-only stride and the explosion's visible phases.
[InitializeOnLoad]
public static class ArenaMotionChecks
{
    private const string RunKey = "ArenaMotionChecks.Running";
    private static double deadline;
    private static Keyboard keyboard;
    static ArenaMotionChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunKey, false))
                return;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Watchdog;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            var runner = new GameObject("Arena motion checks").AddComponent<ArenaCheckRunner>();
            Object.DontDestroyOnLoad(runner.gameObject);
            runner.StartCoroutine(Guard(Check()));
        };
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/FreeForAll.unity");
        NativeUiChecks.SetSize(1280, 720);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        SessionState.SetBool(RunKey, true);
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

        Debug.Log("ARENA MOTION PASSED: screen-speed consistency, immediate release, contact sliding, collision stress, visual-only stride, pause, explosion flash/shockwave/smoke and quiet audio.");
        Finish(0);
    }

    private static IEnumerator Check()
    {
        Directory.CreateDirectory("Logs/ArenaMotionPreviews");
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
            var settings = Object.Instantiate(match.Map.Settings);
            settings.fixedSeed = true;
            settings.seed = 1234;
            Set(match.Map, "settings", settings);
            var pool = Object.Instantiate(match.Events.Pool);
            pool.enableEvents = false;
            Set(match.Events, "pool", pool);
            Require(match.StartMatch(2, new[] { "Player 1", "Player 2" }, new[] { -1, -1 }, new[] { -1, -1 }), match.Error);
            yield return null;
            var actor = match.Players[0];
            var other = match.Players[1];
            Vector2 open = Vector2.zero;
            bool found = false;
            foreach (var cell in match.Map.Layout.Cells())
                if (match.Map.Layout.ClearSquare(cell, 2) && Vector2.Distance(cell, other.Position) > 3)
                {
                    open = cell;
                    found = true;
                    break;
                }

            Require(found, "Open movement test area");
            actor.Place(open);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return new WaitForSeconds(.2f);
            float horizontal = actor.ScreenVelocity.magnitude;
            Require(horizontal > 1, "Movement responds to native input");
            var drawing = actor.transform.Find("Animated arena body");
            Require(drawing != null && drawing.GetComponent<SpriteRenderer>().enabled, "Existing skin is rendered by the motion view");
            Require(!actor.GetComponent<SpriteRenderer>().enabled, "Only one body is drawn");
            Require(drawing.localRotation != Quaternion.identity || drawing.localPosition.sqrMagnitude > 0, "Walking has subtle visible motion");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
            var stopped = actor.Position;
            yield return new WaitForSeconds(.06f);
            Require(actor.Position == stopped, "Ordinary input release has no drift");
            actor.Place(open);
            actor.ClearRoundEffects();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
            yield return new WaitForSeconds(.2f);
            Require(Mathf.Abs(actor.ScreenVelocity.magnitude - horizontal) < .08f, "Diagonal speed matches horizontal speed");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
            bool wallFound = false;
            Vector2 wallStart = Vector2.zero;
            foreach (var cell in match.Map.Layout.Cells())
                if (!match.Map.Layout.Walkable(cell + Vector2Int.right) && !match.Map.Layout.Walkable(cell + Vector2Int.right + Vector2Int.up) && match.Map.Layout.Walkable(cell + Vector2Int.up) && Vector2.Distance(cell, other.Position) > 2)
                {
                    wallStart = (Vector2)cell + Vector2.right * (.5f - settings.playerRadius - .01f);
                    if (match.CanOccupy(actor, wallStart))
                    {
                        wallFound = true;
                        break;
                    }
                }

            Require(wallFound, "Find a real generated diagonal wall");
            actor.Place(wallStart);
            actor.Move(new Vector2(.3f, .3f));
            Require(actor.Position.y > wallStart.y + .03f, "Moving into a wall continues along its diagonal tangent");
            Require(match.Map.Layout.Fits(actor.Position, settings.playerRadius), "Wall slide preserves the footprint");
            var random = new System.Random(678);
            for (int i = 0; i < 600; i++)
            {
                var delta = new Vector2((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f) * .25f;
                actor.Move(delta);
                Require(match.Map.Layout.Fits(actor.Position, settings.playerRadius), "Collision stress cannot enter a blocked tile");
                Require((actor.Position - other.Position).magnitude >= settings.playerRadius * 2 - .001f, "Collision stress cannot overlap another player");
            }

            actor.Place(open);
            actor.ClearRoundEffects();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return new WaitForSeconds(.08f);
            match.SetPaused(true);
            yield return null;
            Vector3 pose = drawing.localPosition;
            var pausePosition = actor.Position;
            yield return new WaitForSeconds(.08f);
            Require(actor.Position == pausePosition && drawing.localPosition == pose, "Pause freezes movement and stride");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            match.SetPaused(false);
            yield return null;
            ScreenCapture.CaptureScreenshot("Logs/ArenaMotionPreviews/" + scene + "-motion.png");
            yield return null;
            if (match is HotPotatoController hot)
            {
                hot.AdjustTime(-100);
                yield return null;
                yield return null;
                var burst = Object.FindFirstObjectByType<SurvivalWorldBurst>();
                Require(burst != null, "Expiry emits a burst");
                var animation = burst.GetComponent<ArenaExplosionAnimation>();
                Require(animation != null, "Explosion has a phased animation");
                var refs = new SerializedObject(animation);
                var ring = (LineRenderer)refs.FindProperty("shockwave").objectReferenceValue;
                var flash = (MeshRenderer)refs.FindProperty("flash").objectReferenceValue;
                Require(ring.enabled && flash.enabled, "Initial flash and shockwave are visible");
                Require(!ShaderUtil.ShaderHasError(flash.sharedMaterial.shader), "Explosion shader compiles on the active graphics backend");
                float initial = ring.transform.localScale.x;
                ScreenCapture.CaptureScreenshot("Logs/ArenaMotionPreviews/Explosion-flash.png");
                yield return null;
                yield return new WaitForSeconds(.22f);
                Require(ring.enabled && !flash.enabled && ring.transform.localScale.x > initial, "Shockwave grows after the flash fades");
                ScreenCapture.CaptureScreenshot("Logs/ArenaMotionPreviews/Explosion-wave.png");
                yield return null;
                var source = burst.GetComponent<AudioSource>();
                Require(source.clip != null && source.volume <= .12f, "Audio stays quiet");
                yield return new WaitForSeconds(.4f);
                Require(!ring.enabled && burst.GetComponentInChildren<ParticleSystem>().IsAlive(true), "Smoke outlasts the initial flash/wave");
                ScreenCapture.CaptureScreenshot("Logs/ArenaMotionPreviews/Explosion-smoke.png");
                yield return null;
                yield return new WaitForSeconds(1);
                Require(Object.FindFirstObjectByType<SurvivalWorldBurst>() == null, "Explosion cleans up");
            }

            match.ReturnToSetup();
            yield return null;
        }
    }

    private static void Set(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
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
            Debug.LogError("Arena motion checks timed out");
            Finish(1);
        }
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(RunKey, false);
        EditorApplication.update -= Watchdog;
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(code);
    }
}
