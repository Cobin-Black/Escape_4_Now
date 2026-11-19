using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Escape4Now.Accounts;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // Owns Relay transport and connection health checks. Game rules live in their own components.
    public sealed class RelayRoomConnection : MonoBehaviour
    {
        [Header("Connection Configuration")]
        [SerializeField] private RelayTestSettings settings;
        [SerializeField] private RelayTestIdentity identity;
        [Header("Dedicated Test Network")]
        [SerializeField] private NetworkManager network;
        [SerializeField] private UnityTransport transport;

        public RelayTestSettings Settings => settings;
        public NetworkManager Network => network;
        public bool AcceptingConnections { get; set; } = true;
        public bool RequiresAccount { get; set; }
        public bool HasRequiredAccount => !RequiresAccount || (AccountService.IsSignedIn && !AccountService.IsBusy);
        public string ModeId { get; set; } = "Diagnostics";
        private string ConnectionSignature => "E4N:" + settings.testVersion + ":" + ModeId;
        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;
        public RelayTestProtocol Protocol { get; private set; }
        public string RoomCode { get; private set; } = "";
        public string Status { get; private set; } = "Choose a protocol, then host or join.";
        public string Error { get; private set; } = "";
        public string Stage { get; private set; } = "Idle";
        public string Endpoint { get; private set; } = "";
        public bool Busy { get; private set; }
        public bool Active { get; private set; }
        public bool Hosting { get; private set; }
        public bool Passed { get; private set; }
        public bool CanStart => !Busy && !Active && !shutdownQueued && !network.IsListening
            && (RequiresAccount || pendingService == null || pendingService.IsCompleted) && !network.ShutdownInProgress;
        public int RemotePlayers => peers.Count;
        public string Log => report?.Recent ?? "";
        public int Replies { get; private set; }
        public float LastRoundTripMilliseconds { get; private set; }

        private const string Ping = "e4n.relay-test.ping";
        private const string Pong = "e4n.relay-test.pong";
        private readonly Dictionary<ulong, Peer> peers = new Dictionary<ulong, Peer>();
        private RelayTestReport report;
        private Task pendingService;
        private int generation;
        private float startedAt;
        private float nextProbe;
        private bool registered;
        private bool shutdownQueued;
        private bool previousBackground;
        private bool alive;
        private int capacity;

        private sealed class Peer
        {
            public int sent, received, replies, pendingSequence;
            public double sentAt;
            public float connectedAt, lastReply;
            // Smoothed round trip in milliseconds, so one slow reply doesn't jump the value around.
            public float smoothedRoundTrip;
        }

        // Host only: how long a guest's input takes to reach this computer on average (half a round trip), in seconds.
        // Zero when no guest has answered a ping yet.
        public float AverageGuestInputDelay
        {
            get
            {
                if (!Active || !Hosting) return 0;
                float total = 0;
                int count = 0;
                foreach (Peer peer in peers.Values)
                    if (peer.smoothedRoundTrip > 0) { total += peer.smoothedRoundTrip; count++; }
                return count == 0 ? 0 : total / count * .5f / 1000f;
            }
        }

        private void Awake()
        {
            alive = true;
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            Protocol = settings.defaultProtocol;
            capacity = settings.maximumPlayers;
            report = new RelayTestReport(settings.maximumLogEntries);
            network.OnClientConnectedCallback += Connected;
            network.OnClientDisconnectCallback += Disconnected;
            network.OnTransportFailure += TransportFailed;
            network.ConnectionApprovalCallback += Approve;
            Application.logMessageReceived += CaptureError;
            report.Add("Ready. No connections or paid service configuration have been created automatically.");
        }

        public void SelectProtocol(int index)
        {
            if (CanStart && Enum.IsDefined(typeof(RelayTestProtocol), index)) Protocol = (RelayTestProtocol)index;
        }

        public Task HostAsync() => HostAsync(settings.maximumPlayers);
        public Task HostAsync(int players)
        {
            if (!CanStart) return Task.CompletedTask;
            if (players < 2 || players > settings.maximumPlayers) throw new ArgumentOutOfRangeException(nameof(players));
            capacity = players;
            AcceptingConnections = true;
            return ConnectAsync(true, "");
        }
        public Task JoinAsync(string code) => ConnectAsync(false, code);

        // Set up Relay first, then hand the connection details to Unity Transport.
        private async Task ConnectAsync(bool host, string code)
        {
            if (!CanStart) return;
            if (!HasRequiredAccount)
            {
                Error = "Sign in with your game account to host or join.";
                return;
            }
            code = (code ?? "").Trim().ToUpperInvariant();
            if (!host && (code.Length < 4 || code.Length > 12 || !Array.TrueForAll(code.ToCharArray(), char.IsLetterOrDigit)))
            {
                Error = "Enter the Relay code shown on the host. This is not the old LAN room code.";
                return;
            }
            int attempt = ++generation;
            Busy = true;
            Error = "";
            RoomCode = "";
            Endpoint = "";
            Passed = false;
            Replies = 0;
            LastRoundTripMilliseconds = 0;
            Hosting = host;
            report.Add("New attempt: " + (host ? "host" : "join") + " / " + Protocol + " / environment=" + settings.environment);
            try
            {
                if (string.IsNullOrWhiteSpace(Application.cloudProjectId))
                    throw new InvalidOperationException("No Unity cloud project is linked. Set Project Settings > Services first.");
#if UNITY_WEBGL && !UNITY_EDITOR
                if (Protocol != RelayTestProtocol.WSS)
                    throw new NotSupportedException("Browser builds require WSS. Use a standalone desktop build to compare DTLS and UDP.");
#endif
                if (!RequiresAccount)
                    await Service(identity.SignInAsync(settings.environment, stage =>
                    {
                        if (alive && attempt == generation) SetStage(stage);
                    }), attempt);
                Guard(attempt);
                if (!HasRequiredAccount) throw new InvalidOperationException("Sign in with your game account to host or join.");
                ConfigureNetwork();
                string protocol = Protocol.ToString().ToLowerInvariant();
                if (host)
                {
                    SetStage("Relay allocation");
                    var allocation = await Service(RelayService.Instance.CreateAllocationAsync(capacity - 1,
                        string.IsNullOrWhiteSpace(settings.region) ? null : settings.region), attempt);
                    Guard(attempt);
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, protocol));
                    DescribeEndpoint(allocation.ServerEndpoints, protocol);
                    SetStage("Relay join code");
                    string joinCode = await Service(RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId), attempt);
                    Guard(attempt);
                    StartTransport(true);
                    Guard(attempt);
                    RoomCode = joinCode;
                }
                else
                {
                    SetStage("Relay join allocation");
                    var allocation = await Service(RelayService.Instance.JoinAllocationAsync(code), attempt);
                    Guard(attempt);
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, protocol));
                    DescribeEndpoint(allocation.ServerEndpoints, protocol);
                    StartTransport(false);
                    Guard(attempt);
                    RoomCode = code;
                }
                Stage = "Transport / peer connection";
                Status = host ? "Room created. Waiting for a remote player; connection is not proven yet." : "Joining host through Relay...";
            }
            catch (OperationCanceledException) { /* A cancelled attempt cannot start networking later. */ }
            catch (Exception error)
            {
                if (alive && attempt == generation) Fail(error);
            }
            finally { if (alive && attempt == generation) Busy = false; }
        }

        private void ConfigureNetwork()
        {
            Application.runInBackground = true;
            // This manager belongs only to this scene. No player prefab or scene sync is needed for an echo test.
            network.SetSingleton();
            network.NetworkConfig.NetworkTransport = transport;
            network.NetworkConfig.EnableSceneManagement = false;
            network.NetworkConfig.ConnectionApproval = true;
            network.NetworkConfig.ProtocolVersion = checked((ushort)settings.testVersion);
            network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(ConnectionSignature);
            transport.UseWebSockets = Protocol == RelayTestProtocol.WSS;
            transport.ConnectTimeoutMS = settings.connectAttemptTimeoutMilliseconds;
            transport.MaxConnectAttempts = settings.maximumConnectAttempts;
            transport.DisconnectTimeoutMS = settings.disconnectTimeoutMilliseconds;
        }

        private void StartTransport(bool host)
        {
            SetStage("Transport startup");
            Active = true;
            startedAt = Time.realtimeSinceStartup;
            if (!(host ? network.StartHost() : network.StartClient()))
                throw new InvalidOperationException("Netcode refused to start. Check the Unity Transport configuration and Player.log.");
            network.CustomMessagingManager.RegisterNamedMessageHandler(Ping, ReceivePing);
            network.CustomMessagingManager.RegisterNamedMessageHandler(Pong, ReceivePong);
            registered = true;
        }

        private void DescribeEndpoint(List<RelayServerEndpoint> endpoints, string protocol)
        {
            var endpoint = endpoints.Find(value => value.ConnectionType == protocol);
            if (endpoint == null) throw new NotSupportedException("Allocation has no endpoint for " + protocol + ".");
            Endpoint = endpoint.Host + ":" + endpoint.Port;
            report.Add("Relay endpoint: " + protocol + "://" + Endpoint);
        }

        // SDK calls cannot all be cancelled. A generation check keeps late replies out of a newer attempt.
        // Diagnostic sign-in still waits for its old identity request; signed-in game rooms can retry safely.
        private async Task Service(Task task, int attempt)
        {
            pendingService = task;
            _ = task.ContinueWith(failed => { var ignored = failed.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            var timeout = Task.Delay(TimeSpan.FromSeconds(settings.serviceTimeout));
            while (!task.IsCompleted && !timeout.IsCompleted)
            {
                Guard(attempt);
                await Task.WhenAny(task, timeout, Task.Delay(100));
            }
            Guard(attempt);
            if (!task.IsCompleted)
            {
                throw new TimeoutException("Unity Services request exceeded " + settings.serviceTimeout + " seconds. A late request is still being cleaned up.");
            }
            await task;
        }

        private async Task<T> Service<T>(Task<T> task, int attempt)
        {
            await Service((Task)task, attempt);
            return await task;
        }

        // Ignore replies from an older connection attempt after leaving or switching rooms.
        private void Guard(int attempt)
        {
            if (!alive || attempt != generation) throw new OperationCanceledException();
        }

        private void SetStage(string stage)
        {
            Stage = stage;
            Status = stage + "...";
            report.Add(stage);
        }

        // Check the room type and protocol version before giving a guest a place in the room.
        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            string signature = request.Payload == null ? "" : Encoding.UTF8.GetString(request.Payload);
            string[] parts = signature.Split(':');
            bool versionMatches = parts.Length == 3 && parts[0] == "E4N" && parts[1] == settings.testVersion.ToString();
            bool modeMatches = versionMatches && parts[2] == ModeId;
            bool space = network.ConnectedClientsIds.Count < capacity;
            response.Approved = modeMatches && space && AcceptingConnections;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = !AcceptingConnections ? "The match has already started." : !versionMatches
                ? "Game builds do not match. Both players need the same updated build."
                : !modeMatches ? "This room is for " + ModeLabel(ModeId) + ". Select that game mode before joining."
                : !space ? "Room is full." : "";
        }

        private static string ModeLabel(string mode) => mode switch
        {
            "OnlineRace" => "Race", "OnlineSurvival" => "Survival",
            "OnlineFreeForAll" => "King of the Hill", "OnlineHotPotato" => "Hot Potato", _ => mode
        };

        private void Connected(ulong client)
        {
            if (!Active || client == network.LocalClientId) return;
            AddPeer(client);
        }

        private void AddPeer(ulong client)
        {
            if (peers.ContainsKey(client)) return;
            float now = Time.realtimeSinceStartup;
            peers[client] = new Peer { connectedAt = now, lastReply = now };
            PeerConnected?.Invoke(client);
            report.Add("Remote peer connected: " + client + ". Starting two-way message checks.");
        }

        private void Update()
        {
            if (!Active || Busy) return;
            float now = Time.realtimeSinceStartup;
            // On a client the connection callback reports its own ID; the actual remote peer is the server.
            if (!Hosting && network.IsConnectedClient) AddPeer(NetworkManager.ServerClientId);
            if (peers.Count == 0)
            {
                // Ordinary game lobbies can wait for friends. Diagnostics can still enforce a time limit.
                if (Hosting && !settings.limitHostWaiting) return;
                float timeout = Hosting ? settings.hostWaitingTimeout : settings.connectionTimeout;
                if (now - startedAt > timeout) Fail(new TimeoutException(Hosting
                    ? "No remote player joined before the host waiting limit. This alone does not prove a network restriction."
                    : "Relay peer connection timed out. Check host availability, protocol, endpoint access, and Player.log."));
                return;
            }
            foreach (var entry in peers)
            {
                if (now - entry.Value.lastReply > settings.responseTimeout)
                {
                    Fail(new TimeoutException("No round-trip reply from remote peer " + entry.Key + " for " + settings.responseTimeout + " seconds."));
                    return;
                }
            }
            if (now >= nextProbe)
            {
                nextProbe = now + settings.probeInterval;
                foreach (var entry in peers)
                {
                    Peer peer = entry.Value;
                    if (peer.pendingSequence != 0) continue;
                    peer.pendingSequence = ++peer.sent;
                    peer.sentAt = Time.realtimeSinceStartupAsDouble;
                    using var writer = new FastBufferWriter(sizeof(int) + sizeof(double), Allocator.Temp);
                    writer.WriteValueSafe(peer.pendingSequence);
                    writer.WriteValueSafe(peer.sentAt);
                    network.CustomMessagingManager.SendNamedMessage(Ping, entry.Key, writer, NetworkDelivery.ReliableSequenced);
                }
            }
            bool passed = true;
            foreach (Peer peer in peers.Values)
                passed &= peer.replies >= settings.requiredRoundTrips && peer.received >= settings.requiredRoundTrips
                    && now - peer.connectedAt >= settings.minimumObservationSeconds;
            if (passed && !Passed)
            {
                Passed = true;
                Stage = "Two-way Relay messages verified";
                Status = "PASS on this connection. Export BOTH reports and record which networks were used.";
                report.Add(Status);
            }
            else if (!Passed) Status = "Connected. Checking repeated messages in both directions...";
        }

        private void ReceivePing(ulong sender, FastBufferReader reader)
        {
            if (Active && !Hosting && network.IsConnectedClient && sender == NetworkManager.ServerClientId) AddPeer(sender);
            if (!Active || !peers.TryGetValue(sender, out Peer peer) || reader.Length - reader.Position != sizeof(int) + sizeof(double)) return;
            reader.ReadValueSafe(out int sequence);
            reader.ReadValueSafe(out double sent);
            peer.received++;
            using var writer = new FastBufferWriter(sizeof(int) + sizeof(double), Allocator.Temp);
            writer.WriteValueSafe(sequence);
            writer.WriteValueSafe(sent);
            network.CustomMessagingManager.SendNamedMessage(Pong, sender, writer, NetworkDelivery.ReliableSequenced);
        }

        private void ReceivePong(ulong sender, FastBufferReader reader)
        {
            if (!Active || !peers.TryGetValue(sender, out Peer peer) || reader.Length - reader.Position != sizeof(int) + sizeof(double)) return;
            reader.ReadValueSafe(out int sequence);
            reader.ReadValueSafe(out double sent);
            if (sequence != peer.pendingSequence || Math.Abs(sent - peer.sentAt) > .000001) return;
            peer.pendingSequence = 0;
            peer.replies++;
            Replies++;
            peer.lastReply = Time.realtimeSinceStartup;
            LastRoundTripMilliseconds = (float)((Time.realtimeSinceStartupAsDouble - sent) * 1000);
            peer.smoothedRoundTrip = peer.smoothedRoundTrip <= 0 ? LastRoundTripMilliseconds
                : Mathf.Lerp(peer.smoothedRoundTrip, LastRoundTripMilliseconds, .25f);
            if (peer.replies == 1 || peer.replies == settings.requiredRoundTrips)
                report.Add("Peer " + sender + ": " + peer.replies + " round trips; latest " + LastRoundTripMilliseconds.ToString("F1") + " ms.");
        }

        private void Disconnected(ulong client)
        {
            if (!Active) return;
            if (Hosting)
            {
                // Rejected connections never joined our peer list and must not close the host's room.
                if (!peers.Remove(client)) return;
                PeerDisconnected?.Invoke(client);
                if (AcceptingConnections)
                {
                    startedAt = Time.realtimeSinceStartup;
                    Passed = false;
                    Status = "A player left. Waiting for another player to join.";
                    return;
                }
            }
            else PeerDisconnected?.Invoke(client);
            string reason = network.DisconnectReason;
            Fail(new InvalidOperationException(string.IsNullOrWhiteSpace(reason)
                ? "The connection to the other player ended." : reason));
        }

        private void TransportFailed() => Fail(new InvalidOperationException("Unity Transport reported a transport failure. See Player.log for the underlying driver error."));

        private void CaptureError(string message, string stack, LogType type)
        {
            if ((!Active && !Busy) || message.StartsWith("[Relay test]") || (type != LogType.Error && type != LogType.Exception)) return;
            // Retain the actual Unity error while this diagnostic is running, without logging it recursively.
            report.Add("Unity " + type + ": " + message);
        }

        private void Fail(Exception error)
        {
            string detail = error.GetType().Name + ": " + error.Message;
            if (error is RequestFailedException request) detail += " [Unity service error " + request.ErrorCode + "]";
            Error = RelayTestReport.Safe(Stage + " - " + detail);
            report.Add("FAILED: " + Error);
            Passed = false;
            StopNetwork();
            Status = error.Message + " You can host or join again after cleanup.";
        }

        public void Leave()
        {
            ++generation;
            Busy = false;
            report.Add("Test stopped by user. Previous messages are retained in this report.");
            StopNetwork();
            Passed = false;
            Stage = "Idle";
            Status = "Stopped. Create a fresh room when changing protocol.";
        }

        private void StopNetwork()
        {
            ++generation;
            Busy = false;
            Active = false;
            RoomCode = "";
            peers.Clear();
            shutdownQueued = true;
        }

        // NGO may call us from inside its message loop. Finish teardown after those callbacks return.
        private void LateUpdate()
        {
            if (shutdownQueued) ShutdownTransport();
        }

        private void ShutdownTransport()
        {
            shutdownQueued = false;
            if (network == null) return;
            if (registered && network.CustomMessagingManager != null)
            {
                network.CustomMessagingManager.UnregisterNamedMessageHandler(Ping);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(Pong);
            }
            registered = false;
            if (!network.ShutdownInProgress && (network.IsListening || network.IsClient || network.IsServer))
                network.Shutdown(true);
        }

        public string Export(string networkLabel) => report.Export(
            "Protocol=" + Protocol + " | Role=" + (Hosting ? "Host" : "Client") + " | Environment=" + settings.environment
            + " | Endpoint=" + Endpoint + " | Stage=" + Stage + " | Pass=" + Passed + " | Replies=" + Replies
            + " | RTT(ms)=" + LastRoundTripMilliseconds.ToString("F1") + "\nStatus=" + Status + "\nError=" + Error, networkLabel);

        private void OnDestroy()
        {
            alive = false;
            ++generation;
            StopNetwork();
            ShutdownTransport();
            if (network != null)
            {
                network.OnClientConnectedCallback -= Connected;
                network.OnClientDisconnectCallback -= Disconnected;
                network.OnTransportFailure -= TransportFailed;
                network.ConnectionApprovalCallback -= Approve;
            }
            Application.logMessageReceived -= CaptureError;
            Application.runInBackground = previousBackground;
            // NetworkManager marks itself persistent. This scene owns it and must not leak it into another mode.
            if (network != null) Destroy(network.gameObject);
        }
    }
}
