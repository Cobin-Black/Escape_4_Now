using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Escape4Now.Accounts;
using Escape4Now.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // Runs private rooms for every mode. The original class name is kept for the saved scenes.
    public sealed class OnlineRaceSession : MonoBehaviour
    {
        [Header("Match References")]
        [SerializeField] private LocalMultiplayerSession local;
        [SerializeField] private OnlineModeMatch modeMatch;
        [SerializeField] private LanMultiplayerSession match;
        [Header("Internet Connection")]
        [SerializeField] private RelayRoomConnection relay;
        [Header("Lobby")]
        [SerializeField, Min(.1f), Tooltip("Seconds between room updates from the host.")]
        private float lobbyPublishInterval = .5f;
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string startScreenScene = "StartScreen";

        public RelayRoomConnection Relay => relay;
        public bool Playing => modeMatch != null ? modeMatch.Started : local.MatchStarted;
        public bool IsHost => relay.Hosting;
        public bool Ready { get; private set; }
        public string Status { get; private set; } = "Host a private room or join with a room code.";
        public int PlayerCount => match.ConnectedPlayers;
        public int Capacity => match.Capacity;
        public int LocalSlot => match.LocalPlayerIndex;
        public bool HasMonsterRole => modeMatch is OnlineSurvivalMatch;
        public bool CanFillWithBots => modeMatch != null && modeMatch.SupportsBots || local != null && local.Mode != null && local.Mode.SupportsBots;
        public bool FillWithBots { get; private set; }
        public void ToggleFillWithBots()
        {
            if (!IsHost || Playing || !CanFillWithBots) return;
            FillWithBots = !FillWithBots;
            nextLobby = 0;
        }
        public int MonsterSlot => modeMatch is OnlineSurvivalMatch survival ? survival.MonsterSlot : -1;
        public void CycleMonsterRole()
        {
            if (!IsHost || Playing || !(modeMatch is OnlineSurvivalMatch survival)) return;
            survival.MonsterSlot = (survival.MonsterSlot + 2) % (Capacity + 1) - 1;
            nextLobby = 0;
        }
        private Skin_Handler Skins => modeMatch != null ? modeMatch.ProfileSkins : local.Skins;
        public bool CanEditProfile => LocalSlot >= 0 && Skins != null;
        public int ProfileSkinCount => CanEditProfile ? Skins.Count : 0;
        public string ProfileSkinName(int skin) => Skins.GetSkin(skin)?.skinName ?? "Skin " + (skin + 1);
        public void CycleProfileSkin(int direction)
        {
            if (!CanEditProfile) return;
            int current = ProfileSkinIndex(LocalSlot);
            for (int step = 1; step < ProfileSkinCount; step++)
            {
                int candidate = (current + direction * step + ProfileSkinCount) % ProfileSkinCount;
                if (Skins.GetSkin(candidate)?.IsValid != true || Skins.IsTaken(candidate)) continue;
                EditProfile(SlotName(LocalSlot), candidate);
                return;
            }
        }
        public int ProfileSkinIndex(int slot) => Skins != null ? Skins.GetSkinIndex(slot) : -1;
        public bool IsSlotReady(int slot) => ready.Contains(slot);
        public Color SlotColor(int slot) => modeMatch != null ? modeMatch.SlotColor(slot) : local.GetPlayerColor(slot);
        public string SlotName(int slot) => modeMatch != null ? modeMatch.SlotName(slot) : local.GetPlayerName(slot);
        public Sprite SlotPortrait(int slot) => modeMatch != null ? modeMatch.SlotPortrait(slot) : Skins != null ? Skins.GetPlayerSkin(slot)?.front : null;
        public bool CanStart => IsHost && relay.Active && !Playing && (FillWithBots || match.ConnectedPlayers == match.Capacity)
            && ready.Count == match.ConnectedPlayers && ready.All(slot => slot >= 0 && slot < match.Capacity && match.IsSlotConnected(slot));
        public string PlayerList => string.Join("\n", Enumerable.Range(0, match.Capacity).Select(slot =>
            "Player " + (slot + 1) + (slot == 0 ? " (Host)" : "") + ": " + (ready.Contains(slot) ? "Ready" : "Not ready / waiting")));

        private const string LobbyName = "e4n.race.lobby.v1";
        private readonly Dictionary<ulong, RelayMessageChannel> channels = new Dictionary<ulong, RelayMessageChannel>();
        private readonly HashSet<int> ready = new HashSet<int>();
        private bool listening;
        private bool profilePublished;
        private float nextLobby;

        [Serializable]
        private sealed class LobbyMessage
        {
            public bool request, ready, profile, fillWithBots;
            public string name;
            public int skin, monsterSlot = -1;
            public int[] readySlots, skins;
            public string[] names;
        }

        private void Awake()
        {
            relay.ModeId = gameObject.scene.name;
            relay.RequiresAccount = true;
            if (modeMatch != null) match.InitializeOnlineMode(modeMatch, local);
            relay.PeerConnected += AddPeer;
            relay.PeerDisconnected += RemovePeer;
        }

        public async Task HostAsync(int players)
        {
            if (!relay.CanStart) return;
            await relay.HostAsync(players);
            if (this == null || !relay.Active) return;
            if (!match.StartRelayedHost(players, relay.RoomCode)) { relay.Leave(); Status = "The online lobby could not start."; return; }
            Listen();
            Status = "Share the room code. All players must mark Ready before the host starts.";
        }

        public async Task JoinAsync(string code)
        {
            if (!relay.CanStart) return;
            await relay.JoinAsync(code);
            if (this != null && relay.Active) Listen();
        }

        public void OpenSignIn()
        {
            AccountService.LoginRequested = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(startScreenScene);
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

        // The host assigns a slot to each connection and shares the accepted profiles and readiness.
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
                if (message.profile) SaveProfile(slot, message.name, message.skin);
                else if (message.ready) ready.Add(slot);
                else ready.Remove(slot);
                nextLobby = 0;
            }
            else if (!IsHost && sender == NetworkManager.ServerClientId && !message.request && message.readySlots != null)
            {
                ready.Clear();
                foreach (int slot in message.readySlots) if (slot >= 0 && slot < match.Capacity) ready.Add(slot);
                Ready = ready.Contains(match.LocalPlayerIndex);
                FillWithBots = message.fillWithBots;
                if (modeMatch is OnlineSurvivalMatch survival) survival.MonsterSlot = message.monsterSlot;
                if (message.names?.Length == Capacity && message.skins?.Length == Capacity)
                {
                    Skins?.ClearAll();
                    for (int i = 0; i < Capacity; i++)
                    {
                        SetSlotName(i, message.names[i]);
                        Skins?.TryAssign(i, message.skins[i]);
                    }
                    Skins?.FillMissing();
                }
            }
        }

        public void EditProfile(string name, int skin)
        {
            if (!CanEditProfile || !relay.Active || Playing) return;
            if (IsHost) SaveProfile(LocalSlot, name, skin);
            else SendLobby(NetworkManager.ServerClientId, new LobbyMessage { request = true, profile = true, name = name, skin = skin });
        }

        private void SetSlotName(int slot, string name)
        {
            if (modeMatch != null) modeMatch.SetSlotName(slot, name);
            else local.SetPlayerName(slot, name);
        }

        private void SaveProfile(int slot, string name, int skin)
        {
            string previous = SlotName(slot);
            SetSlotName(slot, name);
            string requested = SlotName(slot);
            if (Enumerable.Range(0, Capacity).Any(i => i != slot &&
                string.Equals(SlotName(i), requested, StringComparison.OrdinalIgnoreCase)))
                SetSlotName(slot, previous);
            Skins?.TryAssign(slot, skin);
            ready.Remove(slot);
            if (slot == 0) Ready = false;
            nextLobby = 0;
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

        // Only the host starts the match once the required players are ready.
        public void StartMatch()
        {
            if (!CanStart) return;
            relay.AcceptingConnections = false;
            if (!match.StartHostedMatch(FillWithBots)) { relay.AcceptingConnections = true; Status = match.Status; }
        }

        // Each online player saves their own result once per match, to the same leaderboards and history as local play.
        // It is marked online so the match history can show how it was played.
        private bool resultSaved;

        private void SaveOnlineResult()
        {
            if (!Playing)
            {
                resultSaved = false;
                return;
            }
            if (resultSaved || !relay.Active || LocalSlot < 0 || !match.HasMatchFinished) return;
            MatchStat stat;
            bool hasResult = modeMatch != null ? modeMatch.TryGetSlotResult(LocalSlot, out stat) : TryGetRaceResult(out stat);
            if (!hasResult) return;
            resultSaved = true;
            AccountStats.Report(new[] { stat });
        }

        // Race is run by the shared network session, so the result comes from the host's latest update.
        private bool TryGetRaceResult(out MatchStat stat)
        {
            stat = default;
            var state = match.LatestState;
            if (state?.players == null || LocalSlot >= state.players.Length) return false;
            bool won = state.players[LocalSlot].won;
            float seconds = won && local != null && local.Mode != null && local.Mode.Timer != null ? local.Mode.Timer.Elapsed : 0f;
            stat = new MatchStat(0, AccountStats.Race, won, state.players.Length, seconds, true);
            return true;
        }

        private void Update()
        {
            SaveOnlineResult();
            // Match the host's input delay to the guests' measured ping.
            if (modeMatch != null) modeMatch.HostInputDelay = IsHost && relay.Active ? relay.AverageGuestInputDelay : 0;
            if (modeMatch != null && Playing && relay.Active) modeMatch.SendLocalInput(match);
            if (!listening) return;
            if (!profilePublished && relay.Active && LocalSlot >= 0)
            {
                profilePublished = true;
                EditProfile(AccountService.DisplayName, ProfileSkinIndex(LocalSlot));
            }
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
            var lobby = new LobbyMessage { readySlots = ready.ToArray(), monsterSlot = MonsterSlot, fillWithBots = FillWithBots };
            if (Skins != null)
            {
                lobby.names = Enumerable.Range(0, Capacity).Select(SlotName).ToArray();
                lobby.skins = Enumerable.Range(0, Capacity).Select(ProfileSkinIndex).ToArray();
            }
            foreach (var entry in channels)
                if (match.GetRelayedSlot(entry.Value) >= 1)
                    SendLobby(entry.Key, lobby);
        }

        public void Leave()
        {
            relay.Leave();
            match.Disconnect();
            if (local != null) local.ReturnToSetup();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
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
