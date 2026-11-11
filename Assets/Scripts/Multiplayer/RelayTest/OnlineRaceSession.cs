using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // Online Race uses the existing host-authoritative Race rules through a different transport.
    // Survival, Free For All, and Hot Potato are deliberately not routed through Race's rules.
    public sealed class OnlineRaceSession : MonoBehaviour
    {
        [Header("Existing Race Systems")]
        [SerializeField] private LocalMultiplayerSession local;
        [SerializeField] private LanMultiplayerSession match;
        [Header("Internet Connection")]
        [SerializeField] private RelayRoomConnection relay;
        [Header("Lobby")]
        [SerializeField, Min(.1f)] private float lobbyPublishInterval = .5f;
        [SerializeField] private string mainMenuScene = "MainMenu";

        public RelayRoomConnection Relay => relay;
        public bool Playing => local.MatchStarted;
        public bool IsHost => relay.Hosting;
        public bool Ready { get; private set; }
        public string Status { get; private set; } = "Private online Race prototype. Cross-network testing is still required.";
        public int PlayerCount => match.ConnectedPlayers;
        public int Capacity => match.Capacity;
        public int LocalSlot => match.LocalPlayerIndex;
        public bool IsSlotReady(int slot) => ready.Contains(slot);
        public string SlotName(int slot) => local.GetPlayerName(slot);
        public Sprite SlotPortrait(int slot) => local.Skins != null ? local.Skins.GetPlayerSkin(slot)?.front : null;
        public bool CanStart => IsHost && relay.Active && !Playing && match.ConnectedPlayers == match.Capacity
            && ready.Count == match.Capacity && ready.All(slot => slot >= 0 && slot < match.Capacity);
        public string PlayerList => string.Join("\n", Enumerable.Range(0, match.Capacity).Select(slot =>
            "Player " + (slot + 1) + (slot == 0 ? " (Host)" : "") + ": " + (ready.Contains(slot) ? "Ready" : "Not ready / waiting")));

        private const string LobbyName = "e4n.race.lobby.v1";
        private readonly Dictionary<ulong, RelayMessageChannel> channels = new Dictionary<ulong, RelayMessageChannel>();
        private readonly HashSet<int> ready = new HashSet<int>();
        private bool listening;
        private float nextLobby;

        [Serializable]
        private sealed class LobbyMessage { public bool request, ready; public int[] readySlots; }

        private void Awake()
        {
            relay.PeerConnected += AddPeer;
            relay.PeerDisconnected += RemovePeer;
        }

        public async Task HostAsync(int players)
        {
            if (!relay.CanStart) return;
            await relay.HostAsync(players);
            if (this == null || !relay.Active) return;
            if (!match.StartRelayedHost(players, relay.RoomCode)) { relay.Leave(); Status = "Race session could not start its online lobby."; return; }
            Listen();
            Status = "Share the room code. All players must mark Ready before the host starts.";
        }

        public async Task JoinAsync(string code)
        {
            if (!relay.CanStart) return;
            await relay.JoinAsync(code);
            if (this != null && relay.Active) Listen();
        }

        private void Listen()
        {
            relay.Network.CustomMessagingManager.RegisterNamedMessageHandler(RelayMessageChannel.MessageName, ReceiveMatch);
            relay.Network.CustomMessagingManager.RegisterNamedMessageHandler(LobbyName, ReceiveLobby);
            listening = true;
        }

        private void AddPeer(ulong peer)
        {
            if (!listening || channels.ContainsKey(peer)) return;
            var channel = new RelayMessageChannel(relay.Network, peer);
            channels.Add(peer, channel);
            bool accepted = IsHost ? match.AcceptRelayedGuest(channel) : match.JoinRelayedHost(channel, relay.RoomCode);
            if (!accepted) { channel.Dispose(); channels.Remove(peer); }
        }

        private void RemovePeer(ulong peer)
        {
            if (channels.TryGetValue(peer, out var channel)) channel.Dispose();
            channels.Remove(peer);
            ready.Clear();
            Ready = false;
        }

        private void ReceiveMatch(ulong sender, FastBufferReader reader)
        {
            if (!channels.ContainsKey(sender)) AddPeer(sender);
            if (channels.TryGetValue(sender, out var channel)) channel.Receive(reader);
        }

        private void ReceiveLobby(ulong sender, FastBufferReader reader)
        {
            if (!relay.Active || Playing || reader.Length > 1024) return;
            LobbyMessage message;
            try
            {
                reader.ReadValueSafe(out string json);
                message = JsonUtility.FromJson<LobbyMessage>(json);
            }
            catch (Exception error) when (error is ArgumentException || error is OverflowException) { return; }
            if (message == null) return;
            if (IsHost && message.request && channels.TryGetValue(sender, out var channel))
            {
                // Ownership comes from the authenticated connection, never a supplied player index.
                int slot = match.GetRelayedSlot(channel);
                if (slot < 1) return;
                if (message.ready) ready.Add(slot); else ready.Remove(slot);
                nextLobby = 0;
            }
            else if (!IsHost && sender == NetworkManager.ServerClientId && !message.request && message.readySlots != null)
            {
                ready.Clear();
                foreach (int slot in message.readySlots) if (slot >= 0 && slot < match.Capacity) ready.Add(slot);
                Ready = ready.Contains(match.LocalPlayerIndex);
            }
        }

        public void ToggleReady()
        {
            if (!relay.Active || Playing || match.LocalPlayerIndex < 0) return;
            Ready = !Ready;
            if (IsHost) { if (Ready) ready.Add(0); else ready.Remove(0); nextLobby = 0; }
            else SendLobby(NetworkManager.ServerClientId, new LobbyMessage { request = true, ready = Ready });
        }

        private void SendLobby(ulong peer, LobbyMessage message)
        {
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            writer.WriteValueSafe(JsonUtility.ToJson(message));
            relay.Network.CustomMessagingManager.SendNamedMessage(LobbyName, peer, writer, NetworkDelivery.ReliableSequenced);
        }

        public void StartMatch()
        {
            if (!CanStart) return;
            relay.AcceptingConnections = false;
            if (!match.StartHostedMatch()) { relay.AcceptingConnections = true; Status = match.Status; }
        }

        private void Update()
        {
            if (!listening) return;
            if (!relay.Active)
            {
                match.ReportRelayFailure("Online connection ended. " + relay.Error);
                Status = relay.Error;
                return;
            }
            if (!IsHost && relay.Network.IsConnectedClient && !channels.ContainsKey(NetworkManager.ServerClientId))
                AddPeer(NetworkManager.ServerClientId);
            if (Playing || !IsHost || Time.realtimeSinceStartup < nextLobby) return;
            nextLobby = Time.realtimeSinceStartup + lobbyPublishInterval;
            foreach (var entry in channels)
                if (match.GetRelayedSlot(entry.Value) >= 1)
                    SendLobby(entry.Key, new LobbyMessage { readySlots = ready.ToArray() });
        }

        public void Leave()
        {
            relay.Leave();
            match.Disconnect();
            local.ReturnToSetup();
        }

        public void MainMenu()
        {
            relay.Leave();
            match.Disconnect();
            Time.timeScale = 1;
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuScene);
        }

        private void OnDestroy()
        {
            relay.PeerConnected -= AddPeer;
            relay.PeerDisconnected -= RemovePeer;
            if (listening && relay.Network != null && relay.Network.CustomMessagingManager != null)
            {
                relay.Network.CustomMessagingManager.UnregisterNamedMessageHandler(RelayMessageChannel.MessageName);
                relay.Network.CustomMessagingManager.UnregisterNamedMessageHandler(LobbyName);
            }
            foreach (var channel in channels.Values) channel.Dispose();
            channels.Clear();
        }
    }
}
