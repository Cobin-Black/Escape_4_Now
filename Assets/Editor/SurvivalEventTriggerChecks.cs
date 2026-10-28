// Tests outdoor effects, movement costs, notifications, Pause and effect expiry in a running Survival match.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Escape4Now.GameModes;
using Escape4Now.Items;
using Escape4Now.Map;
using Escape4Now.Multiplayer;
using Escape4Now.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static class SurvivalEventTriggerChecks
{
    public static IEnumerator Check(SurvivalModeController mode, LocalMultiplayerSession session)
    {
        var map = (SurvivalMap)mode.GridMap;
        var events = mode.Events;
        var environment = events.Environment;
        var settings = (SurvivalEventSettings)new SerializedObject(events).FindProperty("settings").objectReferenceValue;
        var actor = mode.ActiveTurnParticipant;
        while (!mode.IsActiveSurvivor(actor)) { mode.Turns.EndTurnAfterBattleLoss(actor); actor = mode.ActiveTurnParticipant; }
        var original = actor.GridPosition;
        var monsterPositions = Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None)
            .Where(player => mode.RoleOf(player) == SurvivalRole.Monster).ToDictionary(player => player, player => player.GridPosition);
        Require(settings.events.Count(rule => rule.Trigger == SurvivalEventTrigger.Tile) == 11, "Exactly eleven event types retain floor triggers");
        foreach (var rule in events.Tiles.Values) Require(rule.Trigger == SurvivalEventTrigger.Tile, "No environment, pickup or global event becomes a floor diamond");
        Require(events.Tiles.Count <= settings.eventCount && settings.eventCount == 10, "Shipped campus uses a reduced configurable floor-tile budget");
        Require(environment.ActiveEffects.Any(effect => effect.Type == SurvivalEventType.Mud), "Generated campus contains physical muddy patches");
        string fingerprint = Fingerprint(environment, map);
        var repeatRoot = new GameObject("Repeated environmental layout fixture");
        var repeatEnvironment = repeatRoot.AddComponent<SurvivalEnvironmentController>();
        var repeatPlan = new SurvivalMatchEventPlan(map.Layout, mode.Objectives, settings);
        new SurvivalEventLayout(map.Layout, mode.Objectives, settings, repeatPlan);
        repeatEnvironment.Initialize(events, mode, map, settings, repeatPlan);
        Require(Fingerprint(repeatEnvironment, map) == fingerprint, "Fixed seeds reproduce Mud, Track markings and pickup positions");
        Object.Destroy(repeatRoot);
        yield return null; yield return null;
        foreach (var effect in environment.ActiveEffects)
            foreach (var cell in effect.Cells)
            {
                Require(!map.Objectives.IsReserved(cell) && map.IsWalkable(cell), "Initial environment avoids objective and spawn reservations");
                if (effect.Type == SurvivalEventType.Mud) Require(!map.Layout.IsProtectedRoute(cell.x, cell.y), "Mud avoids protected routes");
                Require(environment.MovementCost(cell) > 1, "Crossing affected ground spends additional movement");
            }
        var mudRenderers = map.GetComponentsInChildren<MeshRenderer>().Where(renderer => renderer.name == "Muddy Ground").ToArray();
        Require(mudRenderers.Length == environment.ActiveEffects.Count(effect => effect.Type == SurvivalEventType.Mud),
            "Each muddy region renders as one merged surface rather than repeated floor tiles");
        Require(mudRenderers.All(renderer => renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0
            && renderer.GetComponentsInChildren<Collider2D>().Length == 0), "Merged ground has real geometry and adds no blocking colliders");
        events.SetEventLocationsVisible(true); events.SetEventLocationsVisible(false);
        Require(mudRenderers.Length > 0 && mudRenderers.All(renderer => renderer.enabled), "Event reveal toggles do not erase environmental visuals");
        DirectoryCapture("clean-campus"); yield return null; yield return null;
        yield return CaptureDetail(mudRenderers[0].bounds.center, "mud-detail");

        var boostCells = environment.TrackBoostCells.ToArray();
        Require(boostCells.Length > 0 && boostCells.All(cell => map.Layout.GetSurface(cell.x, cell.y) == OutdoorSurface.Track), "Boost markings belong to actual Track surface");
        Require(environment.GetComponentsInChildren<LineRenderer>().Any(line => line.name == "Lane-aligned boost paint" && line.enabled),
            "Track boosts render visible lane-aligned paint instead of stacked tile prefabs");
        yield return CaptureDetail(environment.GetComponentsInChildren<LineRenderer>().First(line => line.name == "Lane-aligned boost paint").bounds.center,
            "track-boost-detail");
        var boost = Rule(settings, SurvivalEventType.TrackBoost);
        Set(actor, "resolvingStep", true); Set(actor, "movesRemaining", 4);
        Require(actor.SetGridPosition(boostCells[0]), "Reach marked Track section");
        events.ResolveStep(actor, Vector2Int.right);
        Require(actor.MovesRemaining == 4 + boost.amount, "Track section grants configured movement");
        events.ResolveStep(actor, Vector2Int.right);
        Require(actor.MovesRemaining == 4 + boost.amount, "Track bonus cannot be farmed repeatedly within one turn");
        Set(actor, "resolvingStep", false); actor.SetGridPosition(original);

        var pool = environment.PoolSlipCells.ToList();
        foreach (var cell in pool) Require(map.Layout.GetSurface(cell.x, cell.y) == OutdoorSurface.Deck && !map.Objectives.IsReserved(cell), "Wet patches mark only safe Pool deck tiles");
        if (pool.Count > 0)
        {
            var slip = Rule(settings, SurvivalEventType.PoolsideSlip); slip.activationChance = 1;
            actor.SetGridPosition(pool[0]); Set(actor, "resolvingStep", true); Set(actor, "movesRemaining", 4);
            events.ResolveStep(actor, Vector2Int.right);
            Require(actor.MovesRemaining == Math.Max(0, 4 - slip.amount), "Deck crossing uses the configured Poolside Slip penalty");
            Set(actor, "resolvingStep", false); actor.SetGridPosition(original);
        }
        var pickup = Object.FindFirstObjectByType<EnergyDrinkItem>();
        Require(pickup != null && actor.Inventory != null, "Energy Drink exists as a physical inventory pickup");
        pickup.PickUp(actor.Inventory);
        Require(actor.Inventory.HasItem(pickup) && !pickup.gameObject.activeSelf, "Collecting the can uses existing inventory capacity and removes its world visual");
        var keyboard = (Keyboard)typeof(SurvivalRulesChecks).GetField("keyboard", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        Require(actor.Inventory.Items.Count == 0, "Existing Use Item action consumes the Energy Drink");
        actor.EventState.BeginTurn();
        Require(actor.EventState.UseRoll(4, 4) == 4 + Rule(settings, SurvivalEventType.EnergyDrink).amount, "Energy Drink grants movement on the next turn");

        Expire(environment, settings);
        var branch = Rule(settings, SurvivalEventType.FallenBranch);
        Require(events.TryActivateRule(branch), "A physical branch can spawn on a validated alternate walkway");
        var branchEffect = environment.ActiveEffects.Single(effect => effect.Type == SurvivalEventType.FallenBranch);
        var branchCell = branchEffect.Cells.Single();
        Require(!map.IsWalkable(branchCell) && !map.Objectives.IsReserved(branchCell), "Branch blocks a real safe tile");
        var hazards = new HashSet<Vector2Int>(branchEffect.Cells);
        foreach (var pair in events.Tiles) if (SurvivalEventLayout.MovesPlayers(pair.Value.type)) hazards.Add(pair.Key);
        Require(map.Objectives.ValidateConnectivity(hazards), "Branch preserves tasks, both exits, actors and moving-event bypasses");
        DirectoryCapture("fallen-branch"); yield return null; yield return null;
        Expire(environment, settings); yield return null;
        Require(map.IsWalkable(branchCell), "Expired branch restores the walkway");
        Require(!events.TryActivateRule(branch), "A strong branch event cannot exceed its match occurrence limit");
        Require(events.TryActivateRule(Rule(settings, SurvivalEventType.Sprinklers)), "A generated suitable area can activate its sprinklers without a floor event");
        Require(events.CurrentMessage.Contains("Sprinklers"), "Environmental activation uses the normal HUD notification feed");
        var water = environment.ActiveEffects.Single(effect => effect.Type == SurvivalEventType.Sprinklers);
        Require(water.Cells.Count > 0 && water.Cells.Count <= settings.maximumSprinklerTiles, "Sprinkler extent respects its Inspector budget");
        foreach (var cell in water.Cells) Require(!map.Objectives.IsReserved(cell) && map.IsWalkable(cell) && mode.MovementCost(cell) > 1, "Sprinklers affect safe walkable tiles only");
        yield return new WaitForSeconds(.8f);
        DirectoryCapture("sprinklers"); yield return null; yield return null;
        Expire(environment, settings);
        yield return null;
        Require(!events.CurrentMessage.Contains("Sprinklers"), "Sprinkler feedback disappears when the area effect expires");
        bool carsExist = map.Layout.Props.Any(prop => prop.Kind == OutdoorPropKind.Car && map.Layout.GetArea(prop.X, prop.Y) == OutdoorAreaKind.Parking);
        bool alarm = events.TryActivateRule(Rule(settings, SurvivalEventType.CarAlarm));
        Require(alarm == carsExist, "Car Alarm only activates when a generated Parking car exists");
        if (alarm)
        {
            var sound = map.GetComponentsInChildren<SurvivalAmbientEffect>().First(effect => effect.name.StartsWith("Car Alarm"));
            Require(sound.GetComponentInParent<SurvivalPropVisual>().Kind == OutdoorPropKind.Car && sound.GetComponent<AudioSource>().clip != null,
                "Alarm flashes and sounds on the actual car, without controlling a monster");
        }
        Require(!events.TryActivateRule(Rule(settings, SurvivalEventType.OpenGate)), "Escape gates cannot be opened by an unrelated event");
        Require(events.TryActivateRule(Rule(settings, SurvivalEventType.Blackout)) && events.BlackoutTurns > 0, "Blackout activates globally without stepping on a tile");
        session.PauseGame();
        int frozenBlackout = events.BlackoutTurns;
        environment.CountTurn();
        typeof(SurvivalEventController).GetMethod("CountTurn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(events, null);
        yield return new WaitForSecondsRealtime(.15f);
        Require(events.BlackoutTurns == frozenBlackout, "Pause freezes global turn durations");
        Require(!events.TryActivateRule(Rule(settings, SurvivalEventType.RainStorm)), "Pause prevents new scheduled effects");
        session.ResumeGame();
        Require(events.TryActivateRule(Rule(settings, SurvivalEventType.RainStorm)), "Rain starts as global weather");
        Require(mode.AdjustRollMovement(actor, 6) == Math.Max(settings.minimumRollMovement, 6 - Rule(settings, SurvivalEventType.RainStorm).amount), "Rain applies the configurable movement penalty");
        Require(map.GetComponentsInChildren<SurvivalAmbientEffect>().Any(effect => effect.name.StartsWith("Rain Weather")), "Rain has a native weather visual");
        var rainEffects = (IList)typeof(SurvivalEventController).GetField("temporary", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(events);
        var rain = rainEffects[0];
        int rainTurns = (int)rain.GetType().GetField("turns").GetValue(rain);
        events.ApplyNetworkDisplay("", 0);
        Require(!events.TryActivateRule(Rule(settings, SurvivalEventType.Blackout)), "Blackout cannot exceed its match activation quota even after darkness ends");
        yield return new WaitForSeconds(.8f);
        Require((int)rain.GetType().GetField("turns").GetValue(rain) == rainTurns, "Real-time seconds do not consume global event turns");
        DirectoryCapture("rain"); yield return null; yield return null;
        Require(events.TryActivateRule(Rule(settings, SurvivalEventType.SchoolBell)), "School Bell triggers without a tile");
        foreach (var survivor in mode.ActiveSurvivors)
        {
            survivor.EventState.BeginTurn();
            Require(survivor.EventState.UseRoll(4, 4) == 4 + Rule(settings, SurvivalEventType.SchoolBell).amount, "Every survivor receives the bell's next-turn bonus");
        }
        var positions = new HashSet<Vector2Int>(mode.ActiveSurvivors.Select(player => player.GridPosition));
        bool shuffled = events.TryActivateRule(Rule(settings, SurvivalEventType.LocationShuffle));
        Require(shuffled == (mode.ActiveSurvivors.Count > 1), "Location Shuffle needs multiple survivors");
        Require(positions.SetEquals(mode.ActiveSurvivors.Select(player => player.GridPosition)), "Global shuffle reuses survivors' existing positions");
        foreach (var monster in monsterPositions) Require(monster.Key.GridPosition == monster.Value, "Global survivor effects leave the external monster in its existing position");
        Require(mode.CompletedPuzzles == 0 && !mode.ExitsUnlocked, "All event trigger types preserve puzzle progress and exit locks");

        var scheduleSettings = Object.Instantiate(settings);
        scheduleSettings.events = new[] { Rule(settings, SurvivalEventType.SchoolBell) };
        scheduleSettings.globalEventChance = 1; scheduleSettings.environmentalEventChance = 0;
        scheduleSettings.globalIntervalTurns = new Vector2Int(1, 1);
        var scheduler = (SurvivalEventScheduler)new SerializedObject(events).FindProperty("scheduler").objectReferenceValue;
        scheduler.Initialize(events, scheduleSettings, map.LayoutSeed); scheduler.CountTurn();
        actor.EventState.BeginTurn();
        Require(actor.EventState.UseRoll(4, 4) == 4 + Rule(settings, SurvivalEventType.SchoolBell).amount, "Manager timing and weights activate a global event automatically");
        scheduleSettings.globalEventChance = 0; scheduler.Initialize(events, scheduleSettings, map.LayoutSeed); scheduler.CountTurn();
        actor.EventState.BeginTurn(); Require(actor.EventState.UseRoll(4, 4) == 4, "Zero scheduler chance suppresses global activation");
        scheduleSettings.events = new SurvivalEventRule[0];
        scheduleSettings.globalIntervalTurns = new Vector2Int(2, 5);
        scheduleSettings.environmentalIntervalTurns = new Vector2Int(1, 4);
        scheduleSettings.globalEventChance = .5f; scheduleSettings.environmentalEventChance = .65f;
        var timing = new List<Vector2Int>();
        for (int repeat = 0; repeat < 2; repeat++)
        {
            scheduler.Initialize(events, scheduleSettings, map.LayoutSeed);
            for (int tick = 0; tick < 16; tick++)
            {
                scheduler.CountTurn();
                var value = new Vector2Int((int)typeof(SurvivalEventScheduler).GetField("globalTurns", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scheduler),
                    (int)typeof(SurvivalEventScheduler).GetField("environmentalTurns", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scheduler));
                if (repeat == 0) timing.Add(value);
                else Require(value == timing[tick], "Fixed seed reproduces global and environmental timing");
            }
        }
        Object.Destroy(scheduleSettings);
        Debug.Log("SURVIVAL EVENT TRIGGER CHECKS PASSED: " + mode.ActiveSurvivors.Count + " survivors, terrain safety, costs, pickups, area mechanics, globals, schedules, audio, pause and expiry.");
    }
    private static SurvivalEventRule Rule(SurvivalEventSettings settings, SurvivalEventType type) => Array.Find(settings.events, rule => rule.type == type);
    private static string Fingerprint(SurvivalEnvironmentController environment, SurvivalMap map)
    {
        return string.Join(";", environment.ActiveEffects.SelectMany(effect => effect.Cells.Select(cell => effect.Type + ":" + cell)).OrderBy(value => value))
            + "|" + string.Join(";", environment.TrackBoostCells.Select(cell => cell.ToString()).OrderBy(value => value))
            + "|" + string.Join(";", environment.GetComponentsInChildren<EnergyDrinkItem>().Select(item => map.WorldToGrid(item.transform.position).ToString()).OrderBy(value => value));
    }
    private static void Expire(SurvivalEnvironmentController environment, SurvivalEventSettings settings)
    { for (int i = 0; i <= settings.events.Max(rule => rule.durationTurns); i++) environment.CountTurn(); }
    private static void DirectoryCapture(string name)
    {
        System.IO.Directory.CreateDirectory("Logs/SurvivalEnvironmentPreviews");
        ScreenCapture.CaptureScreenshot("Logs/SurvivalEnvironmentPreviews/" + name + ".png");
    }
    private static IEnumerator CaptureDetail(Vector3 center, string name)
    {
        var camera = Camera.main;
        if (camera == null) yield break;
        var position = camera.transform.position;
        float size = camera.orthographicSize;
        camera.transform.position = new Vector3(center.x, center.y, position.z);
        camera.orthographicSize = 2.2f;
        for (int frame = 0; frame < 4; frame++) yield return null;
        DirectoryCapture(name);
        for (int frame = 0; frame < 4; frame++) yield return null;
        camera.transform.position = position;
        camera.orthographicSize = size;
        yield return null;
    }
    private static void Set(Object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Require(bool condition, string description)
    { if (!condition) throw new InvalidOperationException("FAIL: " + description); Debug.Log("PASS: " + description); }
}
