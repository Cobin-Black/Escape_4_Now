// Manages the LAN lobby and match messages, with the host deciding the shared gameplay state.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Escape4Now.Map;
using Escape4Now.TurnSystem;
using UnityEngine;

namespace Escape4Now.Multiplayer
{
    //One host runs the match. Each connection owns one of the remaining player slots.
    [DefaultExecutionOrder(-200)]
    public sealed class LanMultiplayerSession : MonoBehaviour
    {
        [Header("Match Network Timing")]
        [SerializeField, Min(.04f), Tooltip("Seconds between host state updates sent to guests.")]
        private float snapshotInterval = .05f;
        [SerializeField, Range(30, 240), Tooltip("Input and heartbeat messages allowed per guest each second. Includes immediate direction changes.")]
        private int maximumRequestsPerSecond = 120;
        [SerializeField, Min(2f), Tooltip("Seconds without a reply before the connection closes.")]
        private float peerTimeout = 15f;
        [SerializeField, Min(1f), Tooltip("Seconds allowed for a connection to finish its first handshake.")]
        private float handshakeTimeout = 5f;

        // Keep the LAN port the same on every build so local network discovery works.
        public const int Port = 47777;
        public static LanMultiplayerSession Instance { get; private set; }
        public static bool Active => Instance != null && Instance.active;
        public static bool IsClient => Active && !Instance.host;
        public bool Connecting => searching || connectTask != null;
        public bool IsMyTurn => onlineMode != null ? active && !failed && onlineMode.CanUseTurn(localPlayer) : active && !failed && !MatchPaused && latest != null && latest.turn == localPlayer
            && !latest.players[localPlayer].won;
        public bool MatchPaused { get; private set; }
        public string Status => status;
        public string RoomCode => roomCode;
        public int ConnectedPlayers => host ? 1 + peers.FindAll(peer => peer.slot > 0 && !peer.wire.Closed).Count : lobbyCount;
        public int LocalPlayerIndex => localPlayer;
        public LanSnapshot LatestState => latest;
        public bool HasOnlineRaceInventory => relayed && onlineMode == null;
        public bool UsesRaceMatchUI => HasOnlineRaceInventory && active;
        public bool ShowingRaceStorage => UsesRaceMatchUI && IsMyTurn && latest?.storageOpen == true;
        public int SelectedDiceNumber => RaceActions.SelectedDiceNumber;
        public IReadOnlyList<int> RaceBattleTargets => RaceActions.BattleTargets;
        private RaceNetworkActions raceActions;
        private RaceNetworkActions RaceActions => raceActions ??= new RaceNetworkActions(this);
        public static bool RunsOnlineBots => Active && Instance.relayed && Instance.host;
        private bool MatchFinished
        {
            get
            {
                if (onlineMode != null) return onlineMode.Finished;
                if (latest == null) return false;
                foreach (LanPlayerState player in latest.players) if (player.won) return true;
                return false;
            }
        }
        //True once the match is over: someone reached the exit, or the online mode reports its own finish.
        public bool HasMatchFinished => MatchFinished;
        private LocalMultiplayerSession local;
        private RelayTesting.OnlineModeMatch onlineMode;
        private bool MatchStarted => onlineMode != null ? onlineMode.Started : local != null && local.MatchStarted;

        public void InitializeOnlineMode(RelayTesting.OnlineModeMatch mode, LocalMultiplayerSession owner)
        {
            onlineMode = mode;
            local = owner;
            if (mode.UsesTurnSystem) Instance = this;
            previousBackground = Application.runInBackground;
        }

        public void SubmitModeInput(string input)
        {
            if (!active || failed || !MatchStarted) return;
            if (host) onlineMode.ReceiveInput(0, input);
            else Send(server, new LanMessage { kind = "command", sequence = ++sequence, modeState = input });
        }
        private LanWorldView world;
        private TcpListener listener;
        private LanDiscovery discovery;
        private bool searching;
        private TcpClient connectingClient;
        private Task connectTask;
        private IMatchMessageConnection server;
        private readonly List<Peer> peers = new List<Peer>();
        private bool active, host, failed, relayed;
        private int capacity = 2, localPlayer, lobbyCount, sequence;
        private string roomCode = "", status = "";
        private float connectionStarted, lastServerMessage, nextPublish, nextPing;
        private LanSnapshot latest;
        private bool previousBackground;

        private sealed class Peer
        {
            public IMatchMessageConnection wire;
            public int slot = -1, sequence, requests;
            public float lastSeen, rateWindow, joined;
        }

        //Assigned on the Race session in Unity and initialized before its lobby is used.
        public void Initialize(LocalMultiplayerSession owner)
        {
            Instance = this;
            previousBackground = Application.runInBackground;
            local = owner;
            world = new LanWorldView(owner);
        }

        // Online play reuses the same authoritative Race commands and snapshots through a Relay channel.
        // These entry points never open a LAN socket or run broadcast discovery.
        public bool StartRelayedHost(int count, string code)
        {
            if (active || Connecting || MatchStarted || count < 2 || count > 4) return false;
            capacity = count;
            roomCode = code;
            active = host = true;
            relayed = true;
            failed = false;
            localPlayer = 0;
            Application.runInBackground = true;
            status = "Online room ready. Waiting for players.";
            return true;
        }

        public bool JoinRelayedHost(IMatchMessageConnection connection, string code)
        {
            if (active || Connecting || MatchStarted || connection == null || connection.Closed) return false;
            server = connection;
            roomCode = code;
            active = true;
            relayed = true;
            host = failed = false;
            localPlayer = -1;
            lastServerMessage = Time.realtimeSinceStartup;
            Application.runInBackground = true;
            Send(server, new LanMessage { kind = "hello", code = roomCode });
            status = "Online: waiting for host.";
            return true;
        }

        public bool AcceptRelayedGuest(IMatchMessageConnection connection)
        {
            if (!active || !host || MatchStarted) return false;
            // A player who just left stays in the list until the next update. In the lobby that seat is
            // already free, so drop closed connections now and let an immediate replacement in.
            peers.RemoveAll(peer => peer.wire.Closed);
            if (peers.Count >= capacity - 1) return false;
            peers.Add(new Peer { wire = connection, joined = Time.realtimeSinceStartup, lastSeen = Time.realtimeSinceStartup });
            return true;
        }

        // Open connections only, including guests still finishing their hello.
        private int OpenPeers => peers.FindAll(peer => !peer.wire.Closed).Count;

        public void ReportRelayFailure(string reason) { if (active && !failed) Fail(reason); }

        public int GetRelayedSlot(IMatchMessageConnection connection)
        {
            Peer peer = peers.Find(value => ReferenceEquals(value.wire, connection));
            return peer != null ? peer.slot : -1;
        }

        //Starts a small lobby on this computer's local network.
        public bool StartHost(int count)
        {
            if (active || Connecting || MatchStarted || count < 2 || count > 4) return false;
            try
            {
                listener = new TcpListener(IPAddress.Any, Port);
                listener.Start(4);
                capacity = count;
                byte[] random = new byte[4];
                using (RandomNumberGenerator generator = RandomNumberGenerator.Create()) generator.GetBytes(random);
                roomCode = (BitConverter.ToUInt32(random, 0) % 1000000).ToString("D6");
                discovery = new LanDiscovery(true, roomCode, LanMessage.Version);
                active = host = true;
                failed = false;
                localPlayer = 0;
                Application.runInBackground = true;
                status = "Waiting for players.";
                return true;
            }
            catch (SocketException) { Disconnect(); status = "Could not host. Port 47777 may already be in use."; return false; }
        }

        //Uses an explicit local IPv4 address so joining does not contact an outside service.
        public bool Join(string hostAddress, string code)
        {
            if (active || Connecting || MatchStarted) return false;
            if (hostAddress == null || !IPAddress.TryParse(hostAddress.Trim(), out IPAddress ip) || !IsLocalAddress(ip)
                || !LanDiscovery.ValidCode(code))
            { status = "Enter the host's local IPv4 address and six-digit room code."; return false; }
            connectingClient = new TcpClient(AddressFamily.InterNetwork);
            roomCode = code;
            connectionStarted = Time.realtimeSinceStartup;
            connectTask = connectingClient.ConnectAsync(ip, Port);
            status = "Connecting...";
            return true;
        }

        //Finds the host from its code without asking the player for an IP address.
        public bool Join(string code)
        {
            if (active || Connecting || MatchStarted) return false;
            code = code?.Trim();
            if (!LanDiscovery.ValidCode(code)) { status = "Enter a six-digit room code."; return false; }
            try
            {
                discovery = new LanDiscovery(false, code, LanMessage.Version);
                roomCode = code;
                searching = true;
                connectionStarted = Time.realtimeSinceStartup;
                Application.runInBackground = true;
                status = "Looking for room...";
                return true;
            }
            catch (SocketException) { Disconnect(); status = "Could not search. Check Wi-Fi and firewall settings."; return false; }
        }

        //Stops after a short search, so a missing room never leaves the menu stuck.
        private void FindRoom()
        {
            IPAddress found = discovery.Poll();
            if (found != null && IsLocalAddress(found))
            {
                discovery.Dispose();
                discovery = null;
                searching = false;
                Join(found.ToString(), roomCode);
            }
            else if (Time.realtimeSinceStartup - connectionStarted >= 6f)
            {
                Disconnect();
                status = "Room not found. Check the code, same Wi-Fi, and host firewall.";
            }
        }

        //Private IPv4 ranges and loopback are enough for same-router play and local testing.
        public static bool IsLocalAddress(IPAddress ip)
        {
            return LanDiscovery.IsLocalAddress(ip);
        }

        //Starts only when all selected slots are connected.
        public bool StartHostedMatch(bool fillBots = false)
        {
            if (!active || !host || failed || (!fillBots && ConnectedPlayers != capacity) || MatchStarted) return false;
            bool botsAllowed = onlineMode != null && onlineMode.SupportsBots || local != null && local.Mode != null && local.Mode.SupportsBots;
            if (fillBots && (!relayed || !botsAllowed)) return false;
            if (relayed && local != null)
                for (int slot = 0; slot < capacity; slot++)
                    local.SetBotPlayer(slot, fillBots && !IsSlotConnected(slot));
            // Arena modes: empty slots become bots that the host drives.
            if (onlineMode != null && onlineMode.SupportsBots)
            {
                var bots = new bool[capacity];
                for (int slot = 0; slot < capacity; slot++)
                    bots[slot] = fillBots && relayed && !IsSlotConnected(slot);
                onlineMode.SetBotSlots(bots);
            }
            bool started = onlineMode != null ? onlineMode.StartHost(capacity) : local.StartNetworkMatch(capacity);
            if (!started) { status = onlineMode != null ? onlineMode.Error : local.Status; return false; }
            status = "Starting match...";
            MatchPaused = false;
            return true;
        }

        public bool IsSlotConnected(int slot) => slot == 0 || peers.Exists(peer => peer.slot == slot && !peer.wire.Closed);
        // Host: the open connection holding a guest seat, or null. Lets the lobby tell a replacement from the previous player.
        public IMatchMessageConnection GetSlotConnection(int slot) => peers.Find(peer => peer.slot == slot && slot > 0 && !peer.wire.Closed)?.wire;

        //Polls sockets using real time, including while the game is paused after a win.
        private void Update()
        {
            if (searching)
            {
                try { FindRoom(); }
                catch (SocketException) { Disconnect(); status = "Room search stopped. Check Wi-Fi and try again."; }
            }
            if (connectTask != null) CompleteConnection();
            if (!active || failed) return;
            try
            {
                if (host) PumpHost();
                else PumpClient();
                if (!failed && MatchStarted && onlineMode == null) RaceActions.ReadInput();
            }
            catch (Exception error) when (error is SocketException || error is ArgumentException || error is InvalidOperationException)
            {
                Fail("The network match stopped because its data was invalid or its connection was lost.");
            }
        }

        //Finishes or cancels an asynchronous connection without freezing the menu.
        private void CompleteConnection()
        {
            if (!connectTask.IsCompleted && Time.realtimeSinceStartup - connectionStarted < handshakeTimeout) return;
            if (!connectTask.IsCompleted || connectTask.IsFaulted || connectTask.IsCanceled)
            {
                if (connectTask.IsFaulted) _ = connectTask.Exception;
                Disconnect();
                status = "Could not connect. Check the IP, Wi-Fi, and host firewall.";
                return;
            }
            server = new LanConnection(connectingClient.Client);
            connectTask = null;
            active = true;
            host = failed = false;
            localPlayer = -1;
            lastServerMessage = Time.realtimeSinceStartup;
            Application.runInBackground = true;
            Send(server, new LanMessage { kind = "hello", code = roomCode });
            status = "Waiting for the host.";
        }

        //Accepts at most three guests and rejects late joins or stalled handshakes.
        // Read guest requests first, then send the current match state to all guests.
        private void PumpHost()
        {
            discovery?.Poll(!MatchStarted && ConnectedPlayers < capacity);
            if (listener != null && listener.Pending())
            {
                Socket socket = listener.AcceptSocket();
                if (MatchStarted || OpenPeers >= 3 || !IsLocalAddress(((IPEndPoint)socket.RemoteEndPoint).Address)) socket.Dispose();
                else peers.Add(new Peer { wire = new LanConnection(socket), joined = Time.realtimeSinceStartup,
                    lastSeen = Time.realtimeSinceStartup });
            }
            foreach (Peer peer in peers.ToArray())
            {
                peer.wire.Pump(json => ReceiveGuest(peer, json));
                if (Time.realtimeSinceStartup - peer.lastSeen > peerTimeout
                    || (peer.slot < 0 && Time.realtimeSinceStartup - peer.joined > handshakeTimeout)) peer.wire.Dispose();
                if (!peer.wire.Closed) continue;
                peers.Remove(peer);
                if (MatchStarted && peer.slot > 0 && !MatchFinished)
                { Fail("Player " + (peer.slot + 1) + " disconnected. Start a new match."); return; }
            }
            if (!MatchStarted) PublishState();
        }

        // Capture after gameplay Update, so guests receive the movement from this frame.
        private void LateUpdate()
        {
            if (!active || !host || failed || !MatchStarted) return;
            try { PublishState(); }
            catch (Exception error) when (error is SocketException || error is ArgumentException || error is InvalidOperationException)
            {
                Fail("The network match stopped because its data was invalid or its connection was lost.");
            }
        }

        private void PublishState()
        {
            if (Time.realtimeSinceStartup < nextPublish) return;
            nextPublish = Time.realtimeSinceStartup + snapshotInterval;
            if (onlineMode != null && MatchStarted)
            {
                var state = new LanMessage { kind = "mode-state", modeState = onlineMode.Capture() };
                foreach (Peer peer in peers)
                    if (peer.slot > 0 && !peer.wire.Closed) Send(peer.wire, state);
                return;
            }
            if (MatchStarted && FindFirstObjectByType<IsometricMapTemplate>().ExitReady) latest = world.Capture();
            foreach (Peer peer in peers)
                if (peer.slot > 0) Send(peer.wire, latest == null
                    ? new LanMessage { kind = "lobby", player = peer.slot, count = ConnectedPlayers, value = capacity }
                    : new LanMessage { kind = "state", state = latest });
        }

        //The connection determines ownership; a message cannot choose another player slot.
        private void ReceiveGuest(Peer peer, string json)
        {
            if (json.Length > 1024) { peer.wire.Dispose(); return; }
            LanMessage message;
            try { message = JsonUtility.FromJson<LanMessage>(json); }
            catch (ArgumentException) { peer.wire.Dispose(); return; }
            if (message == null || message.version != LanMessage.Version) { peer.wire.Dispose(); return; }
            if (Time.realtimeSinceStartup - peer.rateWindow >= 1f) { peer.rateWindow = Time.realtimeSinceStartup; peer.requests = 0; }
            if (++peer.requests > maximumRequestsPerSecond) { peer.wire.Dispose(); return; }
            peer.lastSeen = Time.realtimeSinceStartup;
            if (peer.slot < 0)
            {
                if (message.kind != "hello" || message.code != roomCode || MatchStarted || ConnectedPlayers >= capacity)
                { peer.wire.Dispose(); return; }
                // A closed connection no longer holds its seat, so a replacement can take it straight away.
                for (int slot = 1; slot < capacity; slot++)
                    if (!peers.Exists(other => other != peer && other.slot == slot && !other.wire.Closed)) { peer.slot = slot; break; }
                // Never send an invalid seat; a guest without one is turned away instead.
                if (peer.slot < 1) { peer.wire.Dispose(); return; }
                Send(peer.wire, new LanMessage { kind = "lobby", player = peer.slot, count = ConnectedPlayers, value = capacity });
                return;
            }
            if (message.kind == "ping") return;
            if (message.kind != "command" || message.sequence <= peer.sequence) return;
            peer.sequence = message.sequence;
            if (onlineMode != null)
            {
                if (MatchStarted) onlineMode.ReceiveInput(peer.slot, message.modeState);
            }
            else Execute(peer.slot, message);
        }

        //Clients only apply host results. Lost connections stop the match instead of changing authority.
        private void PumpClient()
        {
            server.Pump(json =>
            {
                LanMessage message = JsonUtility.FromJson<LanMessage>(json);
                if (message == null || message.version != LanMessage.Version) { Fail("This build does not match the host."); return; }
                lastServerMessage = Time.realtimeSinceStartup;
                if (message.kind == "mode-state" && onlineMode != null)
                {
                    if (localPlayer < 1 || !onlineMode.Apply(capacity, message.modeState))
                        Fail("The host sent invalid match data.");
                }
                else if (message.kind == "lobby")
                {
                    if (message.player < 1 || message.player > 3 || message.value < 2 || message.value > 4
                        || message.player >= message.value) { Fail("Invalid lobby data."); return; }
                    localPlayer = message.player;
                    capacity = message.value;
                    lobbyCount = message.count;
                    status = "Player " + (localPlayer + 1) + ": waiting for the host to start.";
                }
                else if (message.kind == "state")
                {
                    if (localPlayer < 1 || message.state == null || message.state.players == null
                        || localPlayer >= message.state.players.Length || !world.Apply(message.state))
                    { Fail("The host sent invalid match data."); return; }
                    latest = message.state;
                }
            });
            if (failed) return;
            if (server.Closed || Time.realtimeSinceStartup - lastServerMessage > peerTimeout)
            {
                if (!MatchFinished) Fail("Disconnected from the host. Start a new match.");
                return;
            }
            if (Time.realtimeSinceStartup >= nextPing)
            {
                nextPing = Time.realtimeSinceStartup + 1f;
                Send(server, new LanMessage { kind = "ping" });
            }
        }

        //Binds every request to the turn visible when the button was pressed.
        public void Submit(string action, int x = 0, int y = 0, int value = 0)
        {
            if (onlineMode != null)
            {
                onlineMode.SubmitTurnAction(this, action);
                return;
            }
            bool pauseAction = action == "pause" || action == "resume";
            if (!pauseAction && !IsMyTurn) return;
            var message = new LanMessage { kind = "command", action = action, x = x, y = y, value = value,
                sequence = ++sequence, turn = latest != null ? latest.turn : 0, round = latest != null ? latest.round : 1 };
            if (host) Execute(0, message);
            else Send(server, message);
        }

        // Shared pause commands are handled here. Race commands go to the Race input and rules bridge.
        private void Execute(int slot, LanMessage message)
        {
            if (!MatchStarted || failed || slot < 0 || slot >= local.Participants.Count) return;
            if (message.action == "pause")
            {
                MatchPaused = true;
                local.ApplyNetworkPause(true, false);
                nextPublish = 0f;
                return;
            }
            if (message.action == "resume")
            {
                MatchPaused = false;
                GameTimer timer = FindFirstObjectByType<GameTimer>();
                bool matchFinished = MatchFinished || (timer != null && timer.TimeRemaining <= 0f);
                local.ApplyNetworkPause(false, matchFinished);
                nextPublish = 0f;
                return;
            }
            if (MatchPaused || Time.timeScale <= 0f || latest == null) return;
            if (RaceActions.Execute(local, slot, message)) nextPublish = 0f;
        }

        // These methods are used by the existing Race HUD and saved Unity button events.
        public void UseItem() => RaceActions.UseItem();
        public void TakeNearbyItem() => RaceActions.TakeNearbyItem();
        public void CancelDiceNumber() => RaceActions.CancelDiceNumber();
        public void StepDiceNumber(int direction) => RaceActions.StepDiceNumber(direction);
        public void ChooseRaceOpponent(int index) => RaceActions.ChooseRaceOpponent(index);
        public void ChooseNumber(int value) => RaceActions.ChooseNumber(value);
        public bool Failed => failed;
        public bool IsActive => active;
        public bool IsHost => host;
        public int Capacity => capacity;
        public bool ChoosingNumber => RaceActions.ChoosingNumber;

        private static void Send(IMatchMessageConnection wire, LanMessage message) => wire.Send(LanMessageCodec.Serialize(message));

        //Keeps all controls stopped after a disconnect until the player leaves the match.
        private void Fail(string message)
        {
            discovery?.Dispose();
            discovery = null;
            status = message;
            failed = true;
            listener?.Stop();
            listener = null;
            server?.Dispose();
            foreach (Peer peer in peers) peer.wire.Dispose();
            if (onlineMode != null) onlineMode.ConnectionLost(message);
            else if (MatchStarted) Time.timeScale = 0f;
        }

        //Releases connections when cancelling, leaving, or closing the game.
        public void Disconnect()
        {
            discovery?.Dispose();
            discovery = null;
            searching = false;
            listener?.Stop();
            listener = null;
            server?.Dispose();
            server = null;
            connectingClient?.Dispose();
            connectingClient = null;
            connectTask = null;
            foreach (Peer peer in peers) peer.wire.Dispose();
            peers.Clear();
            active = host = failed = false;
            raceActions?.ResetSelection();
            localPlayer = lobbyCount = sequence = 0;
            latest = null;
            status = "";
            Application.runInBackground = previousBackground;
        }

        private void OnDestroy()
        {
            Disconnect();
            if (Instance == this) Instance = null;
        }
    }
}
