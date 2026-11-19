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
        yield return CheckSeatOccupancy();
        yield return CheckReconnectsAndProfileResults();
        yield return CheckRematchAndBotFill();
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
            string hostBotName = arena.SlotName(2);
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
            Require(hostBotName.EndsWith("(BOT)") && arena.SlotName(2) == hostBotName,
                scene + " guests see the bot name the host picked (" + arena.SlotName(2) + ")");
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

    // Seats are not contiguous: P2 can leave while P3/P4 stay, and a new player can take the freed seat.
    // Runs the host's real seat assignment with in-process connections, then applies its state on a guest.
    private static IEnumerator CheckSeatOccupancy()
    {
        var type = typeof(OnlineRaceSession).GetNestedType("LobbyMessage", BindingFlags.NonPublic);
        object Profile(string name, int revision) =>
            JsonUtility.FromJson("{\"name\":\"" + name + "\",\"skin\":-1,\"profileRevision\":" + revision + "}", type);
        foreach (string scene in new[] { "OnlineRace", "OnlineSurvival", "OnlineFreeForAll", "OnlineHotPotato" })
        {
            yield return SceneManager.LoadSceneAsync(scene);
            var host = Object.FindFirstObjectByType<OnlineRaceSession>();
            var view = Object.FindFirstObjectByType<OnlineRaceLobbyView>();
            var transport = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LanMultiplayerSession>();
            Require(transport.StartRelayedHost(4, "SEATS"), "Start seat test host");
            var guests = new OnlineArenaLatencyChecks.Connection[3];
            for (int i = 0; i < guests.Length; i++)
            {
                guests[i] = new OnlineArenaLatencyChecks.Connection();
                Require(transport.AcceptRelayedGuest(guests[i]), scene + " accepts guest P" + (i + 2));
                guests[i].Receive(new Escape4Now.Multiplayer.LanMessage { kind = "hello", code = "SEATS" });
            }
            yield return null;
            yield return null;
            for (int i = 0; i < guests.Length; i++)
                Require(transport.GetRelayedSlot(guests[i]) == i + 1, scene + " assigns P" + (i + 2) + " its own seat");
            Call(host, "SyncSeats");
            string[] names = { "LEAVER", "STAYER", "THIRD" };
            for (int slot = 1; slot < 4; slot++) Call(host, "AcceptProfile", slot, Profile(names[slot - 1], 1));
            var readySlots = Reference<System.Collections.Generic.HashSet<int>>(host, "ready");
            readySlots.Add(2);
            readySlots.Add(3);

            // P2 leaves while P3 and P4 stay.
            guests[0].Dispose();
            Call(host, "SyncSeats");
            Require(!host.IsSlotOccupied(1) && host.IsSlotOccupied(2) && host.IsSlotOccupied(3) && host.PlayerCount == 3,
                scene + " P2 leaving keeps P3 and P4 seated");
            Require(host.SlotName(1) == "Player 2" && host.SlotName(2) == "STAYER" && host.SlotName(3) == "THIRD",
                scene + " the freed seat drops the departed player's name only");
            Require(!readySlots.Contains(1) && readySlots.Contains(2) && readySlots.Contains(3),
                scene + " releasing a seat keeps the remaining players' Ready state");
            Require(!transport.IsSlotConnected(1) && transport.IsSlotConnected(2) && transport.IsSlotConnected(3),
                scene + " Fill Bots sees the freed P2 seat as open");
            Require(UniqueSkins(host), scene + " the freed seat keeps a unique placeholder skin for a bot");
            yield return null;
            string open = Reference<string>(view, "emptySlotText");
            Require(RowText(view, 1, "state") == open && RowText(view, 2, "name") == "STAYER" && RowText(view, 3, "name") == "THIRD",
                scene + " host lobby shows P3 and P4 after P2 leaves");

            // A departed player's name no longer blocks a profile; a connected player's name still does.
            Call(host, "AcceptProfile", 3, Profile("LEAVER", 2));
            Require(host.SlotName(3) == "LEAVER", scene + " an old name does not block a valid profile");
            Call(host, "AcceptProfile", 3, Profile("STAYER", 3));
            Require(host.SlotName(3) == "LEAVER", scene + " a connected player's name is still protected");
            object hostState = JsonUtility.FromJson(JsonUtility.ToJson(Call(host, "CaptureLobby")), type);
            var occupied = (int[])type.GetField("occupiedSlots").GetValue(hostState);
            Require(occupied != null && string.Join(",", occupied) == "0,2,3", scene + " the host publishes the occupied seats");

            // A replacement takes the freed seat and starts its own profile revisions.
            yield return null;
            var replacement = new OnlineArenaLatencyChecks.Connection();
            Require(transport.AcceptRelayedGuest(replacement), scene + " accepts a replacement player");
            replacement.Receive(new Escape4Now.Multiplayer.LanMessage { kind = "hello", code = "SEATS" });
            yield return null;
            yield return null;
            Require(transport.GetRelayedSlot(replacement) == 1, scene + " the replacement joins the freed P2 seat");
            Call(host, "SyncSeats");
            Call(host, "AcceptProfile", 1, Profile("NEWCOMER", 1));
            var revisions = (int[])type.GetField("profileRevisions").GetValue(Call(host, "CaptureLobby"));
            Require(host.SlotName(1) == "NEWCOMER" && host.IsSlotOccupied(1) && revisions[1] == 1,
                scene + " the replacement's first profile is accepted and acknowledged");

            // P4 leaves and a new player takes that seat before the lobby checks seats again.
            guests[2].Dispose();
            yield return null;
            var quick = new OnlineArenaLatencyChecks.Connection();
            Require(transport.AcceptRelayedGuest(quick), scene + " accepts a quick replacement");
            quick.Receive(new Escape4Now.Multiplayer.LanMessage { kind = "hello", code = "SEATS" });
            yield return null;
            yield return null;
            Require(transport.GetRelayedSlot(quick) == 3, scene + " the quick replacement takes P4");
            Call(host, "SyncSeats");
            Require(host.SlotName(3) == "Player 4" && !readySlots.Contains(3),
                scene + " a quick replacement does not inherit the previous name or Ready");
            Call(host, "AcceptProfile", 3, Profile("QUICK", 1));
            Require(host.SlotName(3) == "QUICK", scene + " the quick replacement's profile is accepted");

            // Retrying clears the previous room's names, but keeps this player's chosen skin.
            typeof(OnlineRaceSession).GetField("preferredSkin", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(host, 2);
            Call(host, "ResetLobby");
            for (int slot = 0; slot < 4; slot++)
                Require(host.SlotName(slot) == "Player " + (slot + 1), scene + " a retry clears the previous room's names");
            Require((int)Call(host, "RequestedSkin") == 2, scene + " a retry keeps this player's skin choice");

            // A guest in P4 receives the host's seats while P2 was empty.
            yield return SceneManager.LoadSceneAsync(scene);
            var guest = Object.FindFirstObjectByType<OnlineRaceSession>();
            view = Object.FindFirstObjectByType<OnlineRaceLobbyView>();
            transport = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LanMultiplayerSession>();
            var wire = new OnlineArenaLatencyChecks.Connection();
            Require(transport.JoinRelayedHost(wire, "SEATS"), "Start seat test guest");
            wire.Receive(new Escape4Now.Multiplayer.LanMessage { kind = "lobby", player = 3, count = 3, value = 4 });
            yield return null;
            Require(guest.LocalSlot == 3, scene + " guest is seated as P4");
            Call(guest, "ApplyLobbyState", hostState);
            Require(!guest.IsSlotOccupied(1) && guest.IsSlotOccupied(2) && guest.IsSlotOccupied(3) && guest.PlayerCount == 3,
                scene + " guests receive the host's occupied seats");
            yield return null;
            open = Reference<string>(view, "emptySlotText");
            Require(RowText(view, 1, "state") == open && RowText(view, 2, "name") == "STAYER" && RowText(view, 3, "name") == "LEAVER",
                scene + " guest lobby matches the host's seats and names");
        }
        Debug.Log("ONLINE SEAT CHECKS PASSED: middle-seat departures, guest occupancy, replacements, stale names and retries in all four modes.");
    }

    // Fast reconnects must find the freed seat, and a refused name or skin must be explained to the
    // requester only, while the host's accepted profile stays authoritative for everyone.
    private static IEnumerator CheckReconnectsAndProfileResults()
    {
        var type = typeof(OnlineRaceSession).GetNestedType("LobbyMessage", BindingFlags.NonPublic);
        object Profile(string name, int skin, int revision) => JsonUtility.FromJson(
            "{\"name\":\"" + name + "\",\"skin\":" + skin + ",\"profileRevision\":" + revision + "}", type);
        bool Flag(object message, string field) => message != null && (bool)type.GetField(field).GetValue(message);
        var transportType = typeof(Escape4Now.Multiplayer.LanMultiplayerSession);
        var peerType = transportType.GetNestedType("Peer", BindingFlags.NonPublic);
        var hello = new Escape4Now.Multiplayer.LanMessage { kind = "hello", code = "FAST" };
        foreach (string scene in new[] { "OnlineRace", "OnlineSurvival", "OnlineFreeForAll", "OnlineHotPotato" })
        {
            yield return SceneManager.LoadSceneAsync(scene);
            var host = Object.FindFirstObjectByType<OnlineRaceSession>();
            var transport = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LanMultiplayerSession>();
            Require(transport.StartRelayedHost(4, "FAST"), "Start reconnect test host");
            var guests = new OnlineArenaLatencyChecks.Connection[3];
            for (int i = 0; i < guests.Length; i++)
            {
                guests[i] = new OnlineArenaLatencyChecks.Connection();
                Require(transport.AcceptRelayedGuest(guests[i]), scene + " seats guest P" + (i + 2));
                guests[i].Receive(hello);
            }
            yield return null;
            yield return null;
            Call(host, "SyncSeats");
            string[] names = { "ALPHA", "BRAVO", "CHARLIE" };
            for (int slot = 1; slot < 4; slot++) Call(host, "AcceptProfile", slot, Profile(names[slot - 1], -1, 1));

            // P2's connection closes and the replacement arrives in the same frame, before any cleanup.
            guests[0].Dispose();
            var back = new OnlineArenaLatencyChecks.Connection();
            Require(transport.AcceptRelayedGuest(back), scene + " admits a replacement before the closed connection is cleaned up");
            back.Receive(hello);
            yield return null;
            yield return null;
            Require(!back.Closed && transport.GetRelayedSlot(back) == 1 && transport.GetRelayedSlot(guests[1]) == 2
                && transport.GetRelayedSlot(guests[2]) == 3, scene + " the replacement takes P2 and P3/P4 keep their seats");
            Call(host, "SyncSeats");
            Require(host.PlayerCount == 4 && host.SlotName(1) == "Player 2" && host.SlotName(2) == "BRAVO" && host.SlotName(3) == "CHARLIE",
                scene + " remaining players keep their names after a fast reconnect");

            // A closed connection still listed in P4 must not block a newcomer's handshake.
            guests[2].Dispose();
            var late = new OnlineArenaLatencyChecks.Connection();
            Require(transport.AcceptRelayedGuest(late), scene + " admits a player for the freed P4 seat");
            object stale = Activator.CreateInstance(peerType);
            peerType.GetField("wire").SetValue(stale, new OnlineArenaLatencyChecks.Connection());
            ((Escape4Now.Multiplayer.IMatchMessageConnection)peerType.GetField("wire").GetValue(stale)).Dispose();
            peerType.GetField("slot").SetValue(stale, 3);
            ((IList)transportType.GetField("peers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(transport)).Add(stale);
            late.Receive(hello);
            yield return null;
            yield return null;
            Require(!late.Closed && transport.GetRelayedSlot(late) == 3, scene + " the handshake ignores a closed connection's seat");
            Call(host, "SyncSeats");

            // Duplicate names and taken skins are refused privately; the host's profile stays in use.
            var readySlots = Reference<System.Collections.Generic.HashSet<int>>(host, "ready");
            Call(host, "AcceptProfile", 1, Profile("DELTA", -1, 1));
            int bravoSkin = host.ProfileSkinIndex(2), deltaSkin = host.ProfileSkinIndex(1);
            Require(bravoSkin >= 0 && deltaSkin >= 0, scene + " has player skins to choose from");
            readySlots.Add(1);
            object nameResult = Call(host, "AcceptProfile", 1, Profile("bravo", -1, 2));
            Require(Flag(nameResult, "nameRejected") && !Flag(nameResult, "skinRejected") && host.SlotName(1) == "DELTA" && host.SlotName(2) == "BRAVO",
                scene + " a duplicate name is refused and both players keep their names");
            Require(readySlots.Contains(1), scene + " a refused change does not clear Ready");
            object skinResult = Call(host, "AcceptProfile", 1, Profile("DELTA", bravoSkin, 3));
            Require(Flag(skinResult, "skinRejected") && !Flag(skinResult, "nameRejected")
                && host.ProfileSkinIndex(1) == deltaSkin && host.ProfileSkinIndex(2) == bravoSkin,
                scene + " a taken skin is refused without replacing the connected player's skin");
            Require(ReferenceEquals(Call(host, "AcceptProfile", 1, Profile("DELTA", bravoSkin, 3)), skinResult) && readySlots.Contains(1),
                scene + " a retry gets the same result and keeps Ready");
            Require(Call(host, "AcceptProfile", 1, Profile("bravo", -1, 2)) == null && host.SlotName(1) == "DELTA",
                scene + " a stale request cannot replace a newer result");
            object shared = JsonUtility.FromJson(JsonUtility.ToJson(Call(host, "CaptureLobby")), type);
            Require(!Flag(shared, "profileResult") && !Flag(shared, "nameRejected") && !Flag(shared, "skinRejected"),
                scene + " rejections are not broadcast to the room");
            int freeSkin = -1;
            for (int skin = 0; skin < host.ProfileSkinCount && freeSkin < 0; skin++)
            {
                bool held = false;
                for (int slot = 0; slot < 4; slot++) held |= slot != 1 && host.IsSlotOccupied(slot) && host.ProfileSkinIndex(slot) == skin;
                if (!held && skin != deltaSkin) freeSkin = skin;
            }
            object accepted = Call(host, "AcceptProfile", 1, Profile("ECHO", freeSkin, 4));
            var revisions = (int[])type.GetField("profileRevisions").GetValue(Call(host, "CaptureLobby"));
            Require(!Flag(accepted, "nameRejected") && !Flag(accepted, "skinRejected") && host.SlotName(1) == "ECHO"
                && (freeSkin < 0 || host.ProfileSkinIndex(1) == freeSkin) && revisions[1] == 4 && !readySlots.Contains(1),
                scene + " an accepted change is applied, acknowledged and needs Ready again");
            shared = JsonUtility.FromJson(JsonUtility.ToJson(Call(host, "CaptureLobby")), type);

            // The requester sees the explanation; the shared room update never shows it.
            yield return SceneManager.LoadSceneAsync(scene);
            var guest = Object.FindFirstObjectByType<OnlineRaceSession>();
            var view = Object.FindFirstObjectByType<OnlineRaceLobbyView>();
            transport = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LanMultiplayerSession>();
            var wire = new OnlineArenaLatencyChecks.Connection();
            Require(transport.JoinRelayedHost(wire, "FAST"), "Start profile result guest");
            wire.Receive(new Escape4Now.Multiplayer.LanMessage { kind = "lobby", player = 1, count = 4, value = 4 });
            yield return null;
            Call(guest, "TrackAccountName", "BRAVO");
            Call(guest, "ApplyLobbyState", shared);
            Require(!guest.NameRejected && !guest.SkinRejected && Call(view, "ProfileConflictText") == null,
                scene + " the shared room update carries no private rejection");
            object result = JsonUtility.FromJson("{\"profileResult\":true,\"nameRejected\":true,\"name\":\"BRAVO\",\"profileRevision\":0}", type);
            Call(guest, "ApplyProfileResult", result);
            Require(guest.ProfileUpdating && !guest.NameRejected, scene + " a result for an older request is ignored");
            result = JsonUtility.FromJson("{\"profileResult\":true,\"nameRejected\":true,\"skinRejected\":true,\"name\":\"BRAVO\",\"profileRevision\":1}", type);
            Call(guest, "ApplyProfileResult", result);
            string text = (string)Call(view, "ProfileConflictText");
            Require(!guest.ProfileUpdating && guest.NameRejected && guest.RejectedName == "BRAVO" && text != null
                && text.Contains("BRAVO") && text.Contains(Reference<string>(view, "skinTakenMessage")),
                scene + " the requester stops retrying and sees why the name and skin were refused");
            Call(guest, "TrackAccountName", "FOXTROT");
            Require(guest.ProfileUpdating && !guest.NameRejected && Call(view, "ProfileConflictText") == null,
                scene + " choosing another value clears the old explanation");
            Call(guest, "ApplyProfileResult", JsonUtility.FromJson("{\"profileResult\":true,\"name\":\"FOXTROT\",\"profileRevision\":2}", type));
            Require(!guest.ProfileUpdating && !guest.NameRejected && !guest.SkinRejected, scene + " an accepted result syncs without a warning");
        }
        Debug.Log("ONLINE RECONNECT AND PROFILE RESULT CHECKS PASSED: same-frame replacements, closed-seat handshakes, private name/skin rejections, retries and stale results in all four modes.");
    }

    // Fill Bots tops the match up to four players, and "Back to Lobby"/"Play Again" keeps the same room.
    private static IEnumerator CheckRematchAndBotFill()
    {
        var fillBots = typeof(OnlineRaceSession).GetProperty("FillWithBots");
        var preferredSkin = typeof(OnlineRaceSession).GetField("preferredSkin", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (string scene in new[] { "OnlineRace", "OnlineSurvival", "OnlineFreeForAll", "OnlineHotPotato" })
        {
            // Two players with Fill Bots play with two bots; three players with one; no Fill Bots, no bots.
            foreach (int humans in new[] { 2, 3, 0 })
            {
                yield return SceneManager.LoadSceneAsync(scene);
                var session = Object.FindFirstObjectByType<OnlineRaceSession>();
                var transport = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LanMultiplayerSession>();
                bool withBots = humans > 0;
                int roomSize = withBots ? humans : 2;
                Require(transport.StartRelayedHost(roomSize, "BOTS"), "Start bot fill test host");
                var guests = new OnlineArenaLatencyChecks.Connection[roomSize - 1];
                for (int i = 0; i < guests.Length; i++)
                {
                    guests[i] = new OnlineArenaLatencyChecks.Connection();
                    Require(transport.AcceptRelayedGuest(guests[i]), "Seat bot fill guest");
                    guests[i].Receive(new Escape4Now.Multiplayer.LanMessage { kind = "hello", code = "BOTS" });
                }
                yield return null;
                yield return null;
                Require(session.CanFillWithBots, scene + " supports Fill Bots");
                fillBots.SetValue(session, withBots);
                Require(session.MatchSeats == (withBots ? 4 : 2), scene + " shows the match size in the lobby");
                yield return null;
                var view = Object.FindFirstObjectByType<OnlineRaceLobbyView>();
                if (withBots) Require(RowText(view, 3, "state") == "BOT SLOT", scene + " the lobby shows the extra seats as bot seats");
                Require(transport.StartHostedMatch(withBots), scene + " starts with " + roomSize + " players" + (withBots ? " and Fill Bots" : ""));
                int seats = withBots ? 4 : 2;
                Require(transport.Capacity == seats, scene + " the match has " + seats + " players");
                foreach (var guest in guests)
                    Require(guest.LastLobby != null && guest.LastLobby.value == seats, scene + " guests learn the match size before it starts");
                var arena = Object.FindFirstObjectByType<Escape4Now.Arena.ArenaMatch>();
                var local = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LocalMultiplayerSession>();
                var botNames = new System.Collections.Generic.HashSet<string>();
                var online = Object.FindFirstObjectByType<OnlineArenaMatch>();
                for (int slot = 0; slot < seats; slot++)
                {
                    bool bot = slot >= roomSize;
                    if (online != null)
                        Require(arena.Players[slot].IsBot == bot, scene + " seat P" + (slot + 1) + (bot ? " is a bot" : " is a player"));
                    else Require(local.IsBotPlayer(slot) == bot, scene + " seat P" + (slot + 1) + (bot ? " is a bot" : " is a player"));
                    // Bots get a random, tagged name from Bot Names; players keep theirs.
                    string name = online != null ? online.SlotName(slot) : local.GetPlayerName(slot);
                    if (bot) Require(name.EndsWith("(BOT)") && name.Length <= Escape4Now.Player.BotNameList.MaxLength && botNames.Add(name),
                        scene + " bot P" + (slot + 1) + " has its own random bot name (" + name + ")");
                    else Require(name == "Player " + (slot + 1), scene + " player P" + (slot + 1) + " keeps their name (" + name + ")");
                }
                Require(Resources.Load<Escape4Now.Player.BotNameList>("Bot Names") != null, "The editable Bot Names list loads");
            }

            // A guest back in the lobby ignores the previous match's updates and keeps asking for a seat.
            yield return SceneManager.LoadSceneAsync(scene);
            var guestTransport = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LanMultiplayerSession>();
            var wire = new OnlineArenaLatencyChecks.Connection();
            Require(guestTransport.JoinRelayedHost(wire, "SAME42") && wire.Hellos == 1, "Start returning guest");
            bool arenaScene = Object.FindFirstObjectByType<OnlineArenaMatch>() != null || scene == "OnlineSurvival";
            wire.Receive(new Escape4Now.Multiplayer.LanMessage { kind = arenaScene ? "mode-state" : "state", modeState = "{}" });
            yield return null;
            Require(!guestTransport.Failed, scene + " a guest back in the lobby ignores the old match's last updates");
            yield return new WaitForSecondsRealtime(1.2f);
            Require(wire.Hellos >= 2, scene + " a returning guest keeps asking for its seat");
            wire.Receive(new Escape4Now.Multiplayer.LanMessage { kind = "lobby", player = 2, count = 3, value = 3 });
            yield return null;
            int hellos = wire.Hellos;
            yield return new WaitForSecondsRealtime(1.2f);
            Require(guestTransport.LocalPlayerIndex == 2 && wire.Hellos == hellos, scene + " a seated guest stops asking");

            // The host keeps a returning player's seat even while they are still on the results screen.
            yield return SceneManager.LoadSceneAsync(scene);
            var hostTransport = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LanMultiplayerSession>();
            Require(hostTransport.StartRelayedHost(4, "SAME42"), "Start returning-seat host");
            var back = new OnlineArenaLatencyChecks.Connection();
            Require(hostTransport.AcceptReturningGuest(back, 3) && hostTransport.GetRelayedSlot(back) == 3 && hostTransport.IsSlotConnected(3),
                scene + " a returning player gets their previous seat");
            for (int i = 0; i < 3; i++) back.Receive(new Escape4Now.Multiplayer.LanMessage { kind = "ping" });
            yield return null;
            yield return null;
            Require(!back.Closed && back.LastLobby != null && back.LastLobby.player == 3,
                scene + " pings from the results screen keep the seat, and the seat is announced");

            // The real local network: the host goes back to the same room code with the same connection.
            yield return SceneManager.LoadSceneAsync(scene);
            // A real match runs for a while first, so let this scene finish starting.
            yield return null;
            var host = Object.FindFirstObjectByType<OnlineRaceSession>();
            var relay = host.Relay;
            var match = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LanMultiplayerSession>();
            typeof(RelayRoomConnection).GetProperty("Hosting").SetValue(relay, true);
            Call(relay, "ConfigureNetwork");
            relay.Network.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>().SetConnectionData("127.0.0.1", 0);
            Call(relay, "StartTransport", true);
            typeof(RelayRoomConnection).GetProperty("RoomCode").SetValue(relay, "SAME42");
            typeof(RelayRoomConnection).GetField("capacity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(relay, 3);
            relay.AcceptingConnections = false;
            Require(match.StartRelayedHost(3, "SAME42"), "Start same-room host");
            Call(host, "Listen");
            fillBots.SetValue(host, true);
            preferredSkin.SetValue(host, 2);
            var network = relay.Network;
            host.ReturnToRoom();
            float limit = Time.realtimeSinceStartup + 10;
            while ((host != null || Object.FindFirstObjectByType<OnlineRaceSession>() == null) && Time.realtimeSinceStartup < limit) yield return null;
            yield return null;
            yield return null;
            var again = Object.FindFirstObjectByType<OnlineRaceSession>();
            Require(again != null && again.Relay.Active && again.Relay.RoomCode == "SAME42" && again.Relay.Network == network
                && network.IsListening && again.Relay.Capacity == 3, scene + " returns to the same room code and connection");
            Require(again.Relay.AcceptingConnections, scene + " the room can take a new player for the rematch");
            Require(Object.FindObjectsByType<Unity.Netcode.NetworkManager>(FindObjectsSortMode.None).Length == 1,
                scene + " keeps exactly one network manager");
            var againMatch = Object.FindFirstObjectByType<Escape4Now.Multiplayer.LanMultiplayerSession>();
            Require(againMatch.IsActive && againMatch.IsHost && againMatch.Capacity == 3,
                scene + " reopens the lobby as the host (active=" + againMatch.IsActive + " host=" + againMatch.IsHost
                + " capacity=" + againMatch.Capacity + " status=" + again.Status + ")");
            Require(!again.Playing && !again.Ready, scene + " the next match waits for Ready (playing=" + again.Playing + " ready=" + again.Ready + ")");
            Require(again.FillWithBots && (int)preferredSkin.GetValue(again) == 2, scene + " keeps Fill Bots and the chosen skin");
            yield return null;
            var againView = Object.FindFirstObjectByType<OnlineRaceLobbyView>();
            Require(Reference<GameObject>(againView, "room").activeSelf, scene + " shows the room, not host/join");

            // Leaving afterwards still closes the room completely.
            again.Leave();
            limit = Time.realtimeSinceStartup + 10;
            while ((again != null || Object.FindFirstObjectByType<OnlineRaceSession>() == null) && Time.realtimeSinceStartup < limit) yield return null;
            yield return null;
            yield return null;
            var fresh = Object.FindFirstObjectByType<OnlineRaceSession>();
            limit = Time.realtimeSinceStartup + 5;
            while (!fresh.Relay.CanStart && Time.realtimeSinceStartup < limit) yield return null;
            Require(network == null && fresh.Relay.CanStart && !fresh.Relay.Active
                && Object.FindObjectsByType<Unity.Netcode.NetworkManager>(FindObjectsSortMode.None).Length == 1,
                scene + " leaving after a rematch closes the room and its connection");
        }
        Debug.Log("ONLINE REMATCH AND BOT FILL CHECKS PASSED: same room after a match, returning seats and guests, and Fill Bots up to four players in all four modes.");
    }

    private static bool UniqueSkins(OnlineRaceSession session)
    {
        var seen = new System.Collections.Generic.HashSet<int>();
        for (int slot = 0; slot < session.Capacity; slot++)
        {
            int skin = session.ProfileSkinIndex(slot);
            if (skin >= 0 && !seen.Add(skin)) return false;
        }
        return true;
    }

    private static string RowText(OnlineRaceLobbyView view, int slot, string field)
    {
        var rows = (Array)typeof(OnlineRaceLobbyView).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        object row = rows.GetValue(slot);
        return ((Text)row.GetType().GetField(field).GetValue(row)).text;
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
