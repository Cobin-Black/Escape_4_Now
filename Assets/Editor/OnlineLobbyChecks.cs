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
    private const string CheckAllModes = "OnlineLobbyChecks.ModeScreens";
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
            runner.StartCoroutine(Guard(SessionState.GetBool(CheckAllModes, false) ? CheckModeScreens() : Check()));
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

    public static void ModeScreens()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(CheckAllModes, true);
        SessionState.SetBool(Running, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        NativeUiChecks.SetSize(1280, 720);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator CheckModeScreens()
    {
        Directory.CreateDirectory("Logs/OnlineLobbyPreviews");
        yield return CheckRoomRecovery();
        yield return CheckProfileReplication();
        foreach (string scene in new[] { "OnlineRace", "OnlineSurvival", "OnlineFreeForAll", "OnlineHotPotato" })
        {
            yield return SceneManager.LoadSceneAsync(scene);
            yield return null;
            var view = Object.FindFirstObjectByType<OnlineRaceLobbyView>();
            var setup = Reference<GameObject>(view, "setup");
            Require(setup.activeInHierarchy && Reference<Button>(view, "signIn").gameObject.activeInHierarchy,
                scene + " opens with the game account sign-in entry");
            var session = Object.FindFirstObjectByType<OnlineRaceSession>();
            var palette = AssetDatabase.LoadAssetAtPath<Escape4Now.Player.PlayerColorPalette>("Assets/Settings/Player Colors.asset");
            for (int slot = 0; slot < 4; slot++)
                Require(session.SlotColor(slot) == palette.GetColor(slot), scene + " uses the shared slot colors");
            Require(!Reference<Button>(view, "host").interactable && !Reference<Button>(view, "join").interactable,
                scene + " blocks unauthenticated host and join controls");
            Require(session.HostAsync(2).IsCompleted && !session.Relay.Active, scene + " blocks direct unauthenticated hosting");
            Require(session.JoinAsync("ABCDEF").IsCompleted && !session.Relay.Active, scene + " blocks direct unauthenticated joining");
            Require(Reference<Toggle[]>(view, "roomSizes").Length == 3, scene + " keeps all three room sizes");
            Require(Object.FindObjectsByType<OnlineLockerCard>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 4,
                scene + " has the shared arcade locker cards");
            ScreenCapture.CaptureScreenshot("Logs/OnlineLobbyPreviews/" + scene + "-setup.png");
            yield return null;

            if (scene == "OnlineRace")
            {
                var steps = CheckRaceMatchUI();
                while (steps.MoveNext()) yield return steps.Current;
            }

            var arena = Object.FindFirstObjectByType<OnlineArenaMatch>();
            if (arena == null) continue;
            // Keep two human connections and fill the last seat with the same bot used in local play.
            arena.SetBotSlots(new[] { false, false, true });
            Require(arena.StartHost(3), scene + " can start through its arena adapter");
            var hostMatch = Object.FindFirstObjectByType<Escape4Now.Arena.ArenaMatch>();
            Require(hostMatch.Players[2].IsBot && hostMatch.Players[2].GetComponent<Escape4Now.Arena.ArenaBotController>() != null,
                scene + " runs the filled seat's bot on the host");
            yield return null;
            yield return null;
            var hud = Object.FindFirstObjectByType<Escape4Now.Arena.ArenaHudView>().transform;
            bool hotPotato = scene == "OnlineHotPotato";
            Require(hud.Find("Player Health").gameObject.activeInHierarchy == !hotPotato, scene + " has mode-specific health");
            Require(hud.Find("Potato Details").gameObject.activeInHierarchy == hotPotato, scene + " has mode-specific round details");
            if (!hotPotato)
            {
                var hill = Object.FindFirstObjectByType<Escape4Now.Arena.KingOfTheHillController>();
                Require(hill != null && hill.HillCenter.HasValue && hill.Items.CaptureOnlinePickups().Length > 0,
                    "King of the Hill starts with a hill and arena pickups");
            }
            ScreenCapture.CaptureScreenshot("Logs/OnlineLobbyPreviews/" + scene + "-hud.png");
            yield return null;

            // Reopen the scene as a guest and apply what the host put on the wire.
            Require(hotPotato ? arena is OnlineHotPotatoMatch : arena is OnlineKingOfTheHillMatch,
                scene + " uses its own online adapter");
            if (!hotPotato)
            {
                hostMatch.Players[0].Damage(1);
                // Include a real swing in the host update so the guest must build its filled visual too.
                hostMatch.Players[0].RecordOnlineSwing();
            }
            string snapshot = arena.Capture();
            yield return SceneManager.LoadSceneAsync(scene);
            arena = Object.FindFirstObjectByType<OnlineArenaMatch>();
            var guestTransport = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LanMultiplayerSession>();
            var guestWire = new OnlineArenaLatencyChecks.Connection();
            Require(guestTransport.JoinRelayedHost(guestWire, "TEST"), "Create an in-process guest connection");
            guestWire.Receive(new Escape4Now.Multiplayer.LanMessage { kind = "lobby", player = 1, count = 3, value = 3 });
            guestWire.Receive(new Escape4Now.Multiplayer.LanMessage { kind = "mode-state", modeState = snapshot });
            yield return null;
            yield return null;
            Require(arena.Started && !guestTransport.Failed, scene + " guest accepts the host snapshot");
            var guestMatch = Object.FindFirstObjectByType<Escape4Now.Arena.ArenaMatch>();
            Require(!guestMatch.Players[1].IsBot && guestMatch.Players[2].IsBot,
                scene + " preserves human and bot identities on the guest");
            Require(guestMatch.GetComponentsInChildren<Escape4Now.Arena.ArenaBotController>(true).Length == 0,
                scene + " guest never runs its own bot decisions");
            if (hotPotato)
            {
                var potato = Object.FindFirstObjectByType<Escape4Now.Arena.HotPotatoController>();
                Require(potato.Holder != null && potato.RemainingSeconds > 0, "Guest sees the potato holder and timer");
            }
            else
            {
                var hill = Object.FindFirstObjectByType<Escape4Now.Arena.KingOfTheHillController>();
                Require(hill.HillCenter.HasValue && hill.Items.GetComponentsInChildren<Escape4Now.Items.FreeForAllItem>().Length > 0,
                    "Guest sees the hill and pickups");
                Require(hill.Players[0].IsInvincible, "Guest receives the host's invincibility timer");
                var swing = Object.FindFirstObjectByType<Escape4Now.Items.RulerSwingEffect>();
                Require(swing != null && swing.GetComponentInChildren<MeshFilter>() != null,
                    "Guest shows main's filled ruler swing");
            }
            OnlineArenaLatencyChecks.Check(arena, snapshot, guestWire);
        }
        Debug.Log("ONLINE MODE SCREENS PASSED: all four private rooms and both arena HUDs.");
    }

    // Exercise the actual profile messages across scene reloads, without using real account credentials.
    private static IEnumerator CheckRoomRecovery()
    {
        foreach (string scene in new[] { "OnlineRace", "OnlineSurvival", "OnlineFreeForAll", "OnlineHotPotato" })
        {
            yield return SceneManager.LoadSceneAsync(scene);
            var session = Object.FindFirstObjectByType<OnlineRaceSession>();
            var relay = session.Relay;
            var match = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LanMultiplayerSession>();
            var transport = relay.Network.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            Require(!relay.Settings.limitHostWaiting, scene + " keeps friend rooms open while waiting");
            for (int attempt = 0; attempt < 2; attempt++)
            {
                typeof(RelayRoomConnection).GetProperty("Hosting").SetValue(relay, true);
                Call(relay, "ConfigureNetwork");
                transport.SetConnectionData("127.0.0.1", 0);
                Call(relay, "StartTransport", true);
                Require(match.StartRelayedHost(2, "RECOVER"), scene + " starts a fresh gameplay session after cleanup");
                Call(session, "Listen");
                string other = scene == "OnlineHotPotato" ? "OnlineRace" : "OnlineHotPotato";
                var request = new Unity.Netcode.NetworkManager.ConnectionApprovalRequest
                {
                    ClientNetworkId = 42,
                    Payload = System.Text.Encoding.UTF8.GetBytes("E4N:" + relay.Settings.testVersion + ":" + other)
                };
                var response = new Unity.Netcode.NetworkManager.ConnectionApprovalResponse();
                Call(relay, "Approve", request, response);
                Require(!response.Approved && response.Reason.Contains("game mode"), scene + " rejects a different mode clearly");
                Call(relay, "Disconnected", (ulong)42);
                Require(relay.Active && relay.Network.IsListening, scene + " rejected guests do not close the host room");
                typeof(RelayRoomConnection).GetField("startedAt", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(relay, Time.realtimeSinceStartup - relay.Settings.hostWaitingTimeout - 1);
                Call(relay, "Update");
                Require(relay.Active, scene + " does not expire an ordinary waiting room");
                // Force the optional diagnostic limit to exercise the same failure path as a timeout.
                relay.Settings.limitHostWaiting = true;
                Call(relay, "Update");
                relay.Settings.limitHostWaiting = false;
                Require(!relay.Active && !relay.Busy, scene + " timeout ends the attempt");
                float deadline = Time.realtimeSinceStartup + 5;
                while ((!relay.CanStart || Escape4Now.Multiplayer.LanMultiplayerSession.Active) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Require(relay.CanStart && !Escape4Now.Multiplayer.LanMultiplayerSession.Active,
                    scene + " shuts down both layers and enables retry without restarting");
            }
            // A failed client attempt must be reusable too, not just an empty host room.
            typeof(RelayRoomConnection).GetProperty("Hosting").SetValue(relay, false);
            Call(relay, "ConfigureNetwork");
            transport.SetConnectionData("127.0.0.1", 47779);
            Call(relay, "StartTransport", false);
            Call(session, "Listen");
            Call(relay, "Disconnected", (ulong)0);
            float limit = Time.realtimeSinceStartup + 5;
            while (!relay.CanStart && Time.realtimeSinceStartup < limit) yield return null;
            Require(relay.CanStart, scene + " can retry after a rejected client connection");
        }
        Debug.Log("ONLINE ROOM RECOVERY CHECKS PASSED: wrong-mode rejection, waiting rooms, forced timeouts, client failures and repeated local transport starts in all four modes.");
    }

    private static IEnumerator CheckProfileReplication()
    {
        foreach (string scene in new[] { "OnlineRace", "OnlineSurvival", "OnlineFreeForAll", "OnlineHotPotato" })
        {
            yield return SceneManager.LoadSceneAsync(scene);
            var host = Object.FindFirstObjectByType<OnlineRaceSession>();
            var transport = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LanMultiplayerSession>();
            Require(transport.StartRelayedHost(2, "PROFILE"), "Start profile test host");
            Call(host, "TrackAccountName", "HOST LOGIN");
            Call(host, "AcceptProfile", 0, Call(host, "ProfileRequest"));
            Require(host.SlotName(0) == "HOST LOGIN", scene + " publishes a completed login");
            Call(host, "TrackAccountName", "HOST RENAMED");
            Call(host, "AcceptProfile", 0, Call(host, "ProfileRequest"));
            var type = typeof(OnlineRaceSession).GetNestedType("LobbyMessage", BindingFlags.NonPublic);
            object first = JsonUtility.FromJson("{\"name\":\"FRIEND LOGIN\",\"skin\":-1,\"profileRevision\":1}", type);
            Call(host, "AcceptProfile", 1, first);
            string oldState = JsonUtility.ToJson(Call(host, "CaptureLobby"));
            var readySlots = Reference<System.Collections.Generic.HashSet<int>>(host, "ready");
            readySlots.Add(1);
            Call(host, "AcceptProfile", 1, first);
            Require(readySlots.Contains(1), scene + " profile retries do not clear Ready again");
            object renamed = JsonUtility.FromJson("{\"name\":\"FRIEND RENAMED\",\"skin\":-1,\"profileRevision\":2}", type);
            Call(host, "AcceptProfile", 1, renamed);
            Require(!readySlots.Contains(1), scene + " a changed profile needs Ready again");
            Call(host, "AcceptProfile", 1, first);
            Require(host.SlotName(1) == "FRIEND RENAMED", scene + " ignores an older name update");
            string state = JsonUtility.ToJson(Call(host, "CaptureLobby"));

            yield return SceneManager.LoadSceneAsync(scene);
            var guest = Object.FindFirstObjectByType<OnlineRaceSession>();
            transport = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LanMultiplayerSession>();
            var wire = new OnlineArenaLatencyChecks.Connection();
            Require(transport.JoinRelayedHost(wire, "PROFILE"), "Start profile test guest");
            wire.Receive(new Escape4Now.Multiplayer.LanMessage { kind = "lobby", player = 1, count = 2, value = 2 });
            yield return null;
            Call(guest, "TrackAccountName", "FRIEND LOGIN");
            Call(guest, "TrackAccountName", "FRIEND RENAMED");
            Call(guest, "ApplyLobbyState", JsonUtility.FromJson(oldState, type));
            Require(guest.ProfileUpdating, scene + " keeps retrying until the newest profile is acknowledged");
            Call(guest, "ApplyLobbyState", JsonUtility.FromJson(state, type));
            Require(!guest.ProfileUpdating && guest.SlotName(0) == "HOST RENAMED" && guest.SlotName(1) == "FRIEND RENAMED",
                scene + " applies both updated account names and acknowledges the guest");
            Call(guest, "TrackAccountName", "FRIEND RENAMED");
            Require(!guest.ProfileUpdating, scene + " unchanged names do not create repeated updates");
        }
        Debug.Log("ONLINE PROFILE CHECKS PASSED: login, renamed host/guest, stale updates, acknowledgements and readiness in all four modes.");
    }

    private static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    private static IEnumerator CheckRaceMatchUI()
    {
        var local = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LocalMultiplayerSession>();
        var network = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LanMultiplayerSession>();
        var turns = Object.FindFirstObjectByType<Escape4Now.TurnSystem.TurnSystemController>();
        Require(network.StartRelayedHost(2, "TEST"), "Start an in-process Race host");
        var wire = new OnlineArenaLatencyChecks.Connection();
        Require(network.AcceptRelayedGuest(wire), "Accept an in-process Race guest");
        wire.Receive(new Escape4Now.Multiplayer.LanMessage { kind = "hello", code = "TEST" });
        yield return null;
        yield return null;
        for (int i = 0; i < 60; i++) wire.Receive(new Escape4Now.Multiplayer.LanMessage { kind = "ping" });
        yield return null;
        yield return null;
        Require(!wire.Closed && network.GetRelayedSlot(wire) == 1, "The input budget allows immediate movement updates");
        Require(network.StartHostedMatch(), "Start Race with the connected guest");
        float limit = Time.realtimeSinceStartup + 8f;
        while (network.LatestState == null && Time.realtimeSinceStartup < limit) yield return null;
        Require(network.LatestState != null, "Race publishes its match state");
        turns.ApplyNetworkTurn(0, turns.TurnNumber, turns.CopyTurnOrder());
        yield return new WaitForSecondsRealtime(.25f);
        Require(network.IsMyTurn, "The host owns the current turn");
        // A remote player's turn must never select that room slot's controller on this computer.
        var pad = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>();
        Escape4Now.Multiplayer.ControllerHandling.AssignToSlot(pad, 0);
        turns.ApplyNetworkTurn(1, turns.TurnNumber, turns.CopyTurnOrder());
        yield return null;
        Require(Escape4Now.Multiplayer.LocalMultiplayerSession.ActiveInputSlot == 0,
            "Online turn input keeps this computer's first device slot");
        foreach (var actions in Escape4Now.Multiplayer.LocalMultiplayerSession.TurnInputMaps())
        {
            var devices = actions.devices;
            Require(devices.HasValue && devices.Value.Count == 1 && devices.Value[0] == pad,
                "Online controller input excludes other controllers and keyboard gameplay input");
        }
        UnityEngine.InputSystem.InputSystem.RemoveDevice(pad);
        turns.ApplyNetworkTurn(0, turns.TurnNumber, turns.CopyTurnOrder());
        // Wait for the normal state publisher before sending the next turn-checked command.
        yield return new WaitForSecondsRealtime(.25f);
        Require(network.IsMyTurn, "The restored host turn reaches the published state");
        var old = Object.FindFirstObjectByType<Escape4Now.UI.LanMatchUI>(FindObjectsInactive.Include);
        Require(!Reference<GameObject>(old, "panel").activeInHierarchy, "Race hides the duplicate network controls");

        var map = Object.FindFirstObjectByType<Escape4Now.Map.IsometricMapTemplate>();
        var spawner = map.GetComponent<Escape4Now.Items.ItemSpawner>();
        var player = local.Participants[0];
        var storageMenu = Object.FindFirstObjectByType<Escape4Now.UI.StorageMenuUI>(FindObjectsInactive.Include);
        Escape4Now.Obstacles.ObstacleStorage contents = null;
        foreach (var storage in Object.FindObjectsByType<Escape4Now.Obstacles.ObstacleStorage>(FindObjectsSortMode.None))
        {
            if (!storage.CanInteract) continue;
            foreach (var direction in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                var position = storage.GridPosition + direction;
                if (map.IsExit(position) || !player.SetGridPosition(position)) continue;
                contents = storage;
                break;
            }
            if (contents != null) break;
        }
        Require(contents != null, "The generated board has accessible storage");
        var dicePrefab = spawner.GetNetworkPrefab(2);
        Require(dicePrefab != null, "The board has its authored Custom Dice prefab");
        contents.SetStoredItem(dicePrefab);
        network.Submit("inspect", contents.GridPosition.x, contents.GridPosition.y);
        yield return new WaitForSecondsRealtime(.25f);
        Require(Reference<GameObject>(storageMenu, "panel").activeInHierarchy && network.LatestState.storageItem == 2,
            "Inspect shows the original locker panel and item");
        storageMenu.Take();
        yield return new WaitForSecondsRealtime(.25f);
        Require(player.Inventory.Items.Count == 1 && !Reference<GameObject>(storageMenu, "panel").activeInHierarchy,
            "The original Take button transfers the host item and closes the panel");

        var diceMenu = Object.FindFirstObjectByType<Escape4Now.UI.DiceSelectionUI>(FindObjectsInactive.Include);
        network.UseItem();
        yield return null;
        yield return null;
        Require(Reference<GameObject>(diceMenu, "panel").activeInHierarchy, "Custom Dice opens its original selector");
        diceMenu.Next();
        diceMenu.Confirm();
        yield return new WaitForSecondsRealtime(.25f);
        Require(player.LastRoll == 2 && player.Inventory.Items.Count == 0, "The original selector sends the chosen die to the host");
        ScreenCapture.CaptureScreenshot("Logs/OnlineLobbyPreviews/OnlineRace-native-hud.png");
        yield return null;

        // Online key battles must copy the same die that local Race displays in its HUD.
        var race = Object.FindFirstObjectByType<Escape4Now.GameModes.Race_Gamemode>();
        var opponent = local.Participants[1];
        Object.Instantiate(race.KeyPrefab).PickUp(opponent.Inventory);
        Require(opponent.SetGridPosition(player.GridPosition), "The challenger can reach the key holder");
        Require(race.TryNetworkBattle(player, opponent), "Online Race can start a key battle");
        yield return new WaitForSecondsRealtime(.3f);
        NativeUiChecks.CheckBattleDice(Object.FindFirstObjectByType<Escape4Now.UI.MatchUIContext>());
        ScreenCapture.CaptureScreenshot("Logs/OnlineLobbyPreviews/OnlineRace-matching-battle-dice.png");
        Debug.Log("ONLINE RACE HUD PASSED: native locker, item take and custom dice with the legacy panel hidden.");
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
        if (!session.Relay.HasRequiredAccount)
        {
            Require(!Reference<Button>(view, "host").interactable, "Online hosting requires a game account");
            Reference<Button>(view, "signIn").onClick.Invoke();
            yield return null;
            Require(SceneManager.GetActiveScene().name == "StartScreen", "Log In opens the existing account screen");
            Debug.Log("ONLINE LOBBY CHECKS PASSED: room setup, diagnostics and account sign-in gate.");
            yield break;
        }
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
        SessionState.SetBool(CheckAllModes, false);
        EditorApplication.update -= Watchdog;
        EditorApplication.Exit(code);
    }
}

public sealed class OnlineLobbyCheckRunner : MonoBehaviour { }
