// Manages the LAN lobby and match messages, with the host deciding the shared gameplay state.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Escape4Now.Items;
using Escape4Now.Map;
using Escape4Now.Obstacles;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.Multiplayer
{
    //One host runs the match. Each connection owns one of the remaining player slots.
    public sealed class LanMultiplayerSession : MonoBehaviour
    {
        public const int Port = 47777;
        public static LanMultiplayerSession Instance { get; private set; }
        public static bool Active => Instance != null && Instance.active;
        public static bool IsClient => Active && !Instance.host;
        public bool Connecting => searching || connectTask != null;
        public bool IsMyTurn => active && !failed && !MatchPaused && latest != null && latest.turn == localPlayer
            && !latest.players[localPlayer].won;
        public bool MatchPaused { get; private set; }
        public string Status => status;
        public string RoomCode => roomCode;
        public int ConnectedPlayers => host ? 1 + peers.FindAll(peer => peer.slot > 0 && !peer.wire.Closed).Count : lobbyCount;
        public int LocalPlayerIndex => localPlayer;
        public LanSnapshot LatestState => latest;
        private bool MatchFinished
        {
            get
            {
                if (latest == null) return false;
                foreach (LanPlayerState player in latest.players) if (player.won) return true;
                return false;
            }
        }
        private LocalMultiplayerSession local;
        private LanWorldView world;
        private TcpListener listener;
        private LanDiscovery discovery;
        private bool searching;
        private TcpClient connectingClient;
        private Task connectTask;
        private LanConnection server;
        private readonly List<Peer> peers = new List<Peer>();
        private bool active, host, failed, choosingNumber;
        private int capacity = 2, localPlayer, lobbyCount, sequence;
        private string roomCode = "", status = "";
        private float connectionStarted, lastServerMessage, nextPublish, nextPing;
        private LanSnapshot latest;
        private bool previousBackground;

        private sealed class Peer
        {
            public LanConnection wire;
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

        //Starts a small lobby on this computer's local network.
        public bool StartHost(int count)
        {
            if (active || Connecting || local.MatchStarted || count < 2 || count > 4) return false;
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
            if (active || Connecting || local.MatchStarted) return false;
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
            if (active || Connecting || local.MatchStarted) return false;
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
        public bool StartHostedMatch()
        {
            if (!active || !host || failed || ConnectedPlayers != capacity || local.MatchStarted) return false;
            if (!local.StartNetworkMatch(capacity)) { status = local.Status; return false; }
            status = "Starting match...";
            MatchPaused = false;
            return true;
        }

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
                if (!failed && local.MatchStarted) ReadInput();
            }
            catch (Exception error) when (error is SocketException || error is ArgumentException || error is InvalidOperationException)
            {
                Fail("The network match stopped because its data was invalid or its connection was lost.");
            }
        }

        //Finishes or cancels an asynchronous connection without freezing the menu.
        private void CompleteConnection()
        {
            if (!connectTask.IsCompleted && Time.realtimeSinceStartup - connectionStarted < 5f) return;
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
        private void PumpHost()
        {
            discovery?.Poll(!local.MatchStarted && ConnectedPlayers < capacity);
            if (listener != null && listener.Pending())
            {
                Socket socket = listener.AcceptSocket();
                if (local.MatchStarted || peers.Count >= 3 || !IsLocalAddress(((IPEndPoint)socket.RemoteEndPoint).Address)) socket.Dispose();
                else peers.Add(new Peer { wire = new LanConnection(socket), joined = Time.realtimeSinceStartup,
                    lastSeen = Time.realtimeSinceStartup });
            }
            foreach (Peer peer in peers.ToArray())
            {
                peer.wire.Pump(json => ReceiveGuest(peer, json));
                if (Time.realtimeSinceStartup - peer.lastSeen > 15f
                    || (peer.slot < 0 && Time.realtimeSinceStartup - peer.joined > 5f)) peer.wire.Dispose();
                if (!peer.wire.Closed) continue;
                peers.Remove(peer);
                if (local.MatchStarted && peer.slot > 0 && !MatchFinished)
                { Fail("Player " + (peer.slot + 1) + " disconnected. Start a new match."); return; }
            }
            if (Time.realtimeSinceStartup < nextPublish) return;
            nextPublish = Time.realtimeSinceStartup + 0.1f;
            IsometricMapTemplate map = FindFirstObjectByType<IsometricMapTemplate>();
            if (local.MatchStarted && map.ExitReady) latest = world.Capture();
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
            if (++peer.requests > 30) { peer.wire.Dispose(); return; }
            peer.lastSeen = Time.realtimeSinceStartup;
            if (peer.slot < 0)
            {
                if (message.kind != "hello" || message.code != roomCode || local.MatchStarted || ConnectedPlayers >= capacity)
                { peer.wire.Dispose(); return; }
                for (int slot = 1; slot < capacity; slot++)
                    if (!peers.Exists(other => other.slot == slot)) { peer.slot = slot; break; }
                Send(peer.wire, new LanMessage { kind = "lobby", player = peer.slot, count = ConnectedPlayers, value = capacity });
                return;
            }
            if (message.kind == "ping") return;
            if (message.kind != "command" || message.sequence <= peer.sequence) return;
            peer.sequence = message.sequence;
            Execute(peer.slot, message);
        }

        //Clients only apply host results. Lost connections stop the match instead of changing authority.
        private void PumpClient()
        {
            server.Pump(json =>
            {
                LanMessage message = JsonUtility.FromJson<LanMessage>(json);
                if (message == null || message.version != LanMessage.Version) { Fail("This build does not match the host."); return; }
                lastServerMessage = Time.realtimeSinceStartup;
                if (message.kind == "lobby")
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
            if (server.Closed || Time.realtimeSinceStartup - lastServerMessage > 15f)
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
            bool pauseAction = action == "pause" || action == "resume";
            if (!pauseAction && !IsMyTurn) return;
            var message = new LanMessage { kind = "command", action = action, x = x, y = y, value = value,
                sequence = ++sequence, turn = latest != null ? latest.turn : 0, round = latest != null ? latest.round : 1 };
            if (host) Execute(0, message);
            else Send(server, message);
        }

        //Checks ownership, turn, bounds, and busy state before calling the existing game rules.
        private void Execute(int slot, LanMessage message)
        {
            if (!local.MatchStarted || failed || slot < 0 || slot >= local.Participants.Count) return;
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
            TurnSystemController turns = FindFirstObjectByType<TurnSystemController>();
            PlayerCharacter player = local.Participants[slot];
            if (turns.CurrentPlayerIndex != slot || message.turn != slot || message.round != turns.TurnNumber
                || player.HasReachedExit || player.IsMoving) return;
            var battle = FindFirstObjectByType<Escape4Now.GameModes.Race_Gamemode>();
            if (message.action == "battleClose")
            {
                battle?.FinishNetworkBattle(player);
                nextPublish = 0f;
                return;
            }
            if (player.IsInMenu() || player.IsUsingItem()) return;
            switch (message.action)
            {
                case "roll": player.RollDice(); break;
                case "move":
                    if (message.x >= 0 && message.y >= 0 && message.x < SchoolMapLayout.Size && message.y < SchoolMapLayout.Size)
                        player.MoveToGridPosition(new Vector2Int(message.x, message.y));
                    break;
                case "end": turns.AdvanceTurn(); break;
                case "battle":
                    if (message.value >= 0 && message.value < local.Participants.Count)
                        battle?.TryNetworkBattle(player, local.Participants[message.value]);
                    break;
                case "item":
                    if (!player.CanUseItem() || player.Inventory.Items.Count == 0) break;
                    Item item = player.Inventory.Items[0];
                    if (item is CustomDiceItem)
                    {
                        if (message.value < 1 || message.value > 6) break;
                        player.SetMovesFromItem(message.value);
                        player.Inventory.RemoveItem(item);
                    }
                    else if (item is DoubleDiceItem || item is KeyStealerItem) item.Use(player.Inventory);
                    break;
                case "take":
                    foreach (ObstacleStorage storage in FindObjectsByType<ObstacleStorage>(FindObjectsSortMode.None))
                    {
                        if (storage.GridPosition == new Vector2Int(message.x, message.y))
                        { storage.TakeNetworkItem(player); break; }
                    }
                    break;
            }
            nextPublish = 0f;
        }

        //Uses the same action bindings, plus mouse clicks on map tiles.
        private void ReadInput()
        {
            if (!IsMyTurn) { choosingNumber = false; return; }
            if (MatchPaused || Time.timeScale <= 0f || latest.players[localPlayer].moving) return;
            var bindings = LocalMultiplayerSession.InputBindings;
            if (bindings == null) return;
            bool confirm = bindings.KeyBattle?.WasPressedThisFrame() == true;
            if (!string.IsNullOrEmpty(latest.battleMessage))
            {
                if (confirm) Submit("battleClose");
                return;
            }
            if (confirm)
            {
                LanPlayerState challenger = latest.players[localPlayer];
                for (int i = 0; i < latest.players.Length; i++)
                {
                    LanPlayerState target = latest.players[i];
                    if (i != localPlayer && !challenger.hasKey && target.hasKey && !target.protectedFromPlayers
                        && target.x == challenger.x && target.y == challenger.y)
                    { Submit("battle", value: i); return; }
                }
            }
            if (bindings.RollDice?.WasPressedThisFrame() == true) Submit("roll");
            if (bindings.UseItem?.WasPressedThisFrame() == true) UseItem();
            if (bindings.Interact?.WasPressedThisFrame() == true) TakeNearbyItem();
            InputAction move = bindings.Move;
            if (!choosingNumber && move != null && move.WasPressedThisFrame())
            {
                Vector2 input = move.ReadValue<Vector2>();
                Vector2Int direction = Mathf.Abs(input.y) >= Mathf.Abs(input.x)
                    ? new Vector2Int(0, Math.Sign(input.y)) : new Vector2Int(Math.Sign(input.x), 0);
                LanPlayerState player = latest.players[localPlayer];
                Submit("move", player.x + direction.x, player.y + direction.y);
            }
            if (bindings.Click?.WasPressedThisFrame() != true || bindings.Point == null || Camera.main == null) return;
            Vector2 position = bindings.Point.ReadValue<Vector2>();
            if (Escape4Now.UI.UiPointer.IsOverUI(position) || choosingNumber) return;
            IsometricMapTemplate map = FindFirstObjectByType<IsometricMapTemplate>();
            if (map.TryGetGridPosition(Camera.main.ScreenToWorldPoint(position), out Vector2Int cell))
            {
                foreach (LanObjectState item in latest.objects)
                    if ((item.kind == 1 || item.kind == 3) && item.x == cell.x && item.y == cell.y) { Submit("take", cell.x, cell.y); return; }
                Submit("move", cell.x, cell.y);
            }
        }

        //Custom Dice keeps the player's choice, while the host checks its valid range.
        public void UseItem()
        {
            if (!IsMyTurn || MatchPaused || latest.players[localPlayer].rolled) return;
            if (latest.players[localPlayer].item == 2) choosingNumber = true;
            else Submit("item");
        }

        //The host also checks adjacency, so remote coordinates cannot take distant items.
        public void TakeNearbyItem()
        {
            if (!IsMyTurn || MatchPaused) return;
            LanPlayerState player = latest.players[localPlayer];
            foreach (LanObjectState item in latest.objects)
                if ((item.kind == 1 || item.kind == 3) && item.interactable && Math.Abs(item.x - player.x) + Math.Abs(item.y - player.y) == 1)
                { Submit("take", item.x, item.y); return; }
        }

        public bool Failed => failed;
        public bool IsHost => host;
        public int Capacity => capacity;
        public bool ChoosingNumber => choosingNumber;

        public void ChooseNumber(int value)
        {
            if (!choosingNumber || !IsMyTurn || MatchPaused || value < 1 || value > 6) return;
            Submit("item", value: value);
            choosingNumber = false;
        }

        private static void Send(LanConnection wire, LanMessage message) => wire.Send(LanMessageCodec.Serialize(message));

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
            if (local.MatchStarted) Time.timeScale = 0f;
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
            active = host = failed = choosingNumber = false;
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
