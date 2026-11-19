using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // Adapts reliable Relay messages to the shared match messages used by all four modes.
    // It owns no player, dice, inventory, or map-generation rules.
    public sealed class RelayMessageChannel : IMatchMessageConnection
    {
        public const string MessageName = "e4n.race.state.v1";
        private static readonly Encoding TextEncoding = new UTF8Encoding(false, true);
        private readonly Queue<string> incoming = new Queue<string>();
        private readonly NetworkManager network;
        private readonly ulong remote;
        private int queuedBytes;
        public bool Closed { get; private set; }

        // Bind this message queue to one remote client on the scene's network manager.
        public RelayMessageChannel(NetworkManager network, ulong remote)
        {
            this.network = network;
            this.remote = remote;
        }

        // Encode a bounded match message and use reliable delivery that can split large updates.
        public void Send(string message)
        {
            if (Closed) return;
            byte[] bytes = TextEncoding.GetBytes(message);
            if (bytes.Length < 1 || bytes.Length > LanConnection.MaxMessageBytes) throw new ArgumentException("Match message is too large.");
            using var writer = new FastBufferWriter(bytes.Length + sizeof(int), Allocator.Temp);
            writer.WriteValueSafe(bytes.Length);
            writer.WriteBytesSafe(bytes);
            network.CustomMessagingManager.SendNamedMessage(MessageName, remote, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        // Check the packet size and queue limit before decoding and accepting a match message.
        public void Receive(FastBufferReader reader)
        {
            if (Closed) return;
            try
            {
                if (!reader.TryBeginRead(sizeof(int))) { Dispose(); return; }
                reader.ReadValueSafe(out int length);
                if (length < 1 || length > LanConnection.MaxMessageBytes || reader.Length - reader.Position != length
                    || queuedBytes + length > LanConnection.MaxMessageBytes * 4) { Dispose(); return; }
                var bytes = new byte[length];
                reader.ReadBytesSafe(ref bytes, length);
                incoming.Enqueue(TextEncoding.GetString(bytes));
                queuedBytes += length;
            }
            catch (Exception error) when (error is DecoderFallbackException || error is OverflowException)
            { Dispose(); }
        }

        public void Pump(Action<string> onMessage)
        {
            // Limit per-frame work just as the LAN connection does.
            for (int i = 0; i < 32 && !Closed && incoming.Count > 0; i++)
            {
                string message = incoming.Dequeue();
                queuedBytes -= TextEncoding.GetByteCount(message);
                onMessage(message);
            }
        }

        // Stops using this channel without touching the connection, so the room survives a rematch reload.
        public void Detach()
        {
            Closed = true;
            incoming.Clear();
            queuedBytes = 0;
        }

        // Close this channel once and release queued messages and its owned connection.
        public void Dispose()
        {
            if (Closed) return;
            Closed = true;
            incoming.Clear();
            queuedBytes = 0;
            if (network == null || !network.IsListening || network.ShutdownInProgress) return;
            if (network.IsServer)
            {
                if (network.ConnectedClients.ContainsKey(remote)) network.DisconnectClient(remote, "Match channel closed.");
            }
            else network.Shutdown();
        }
    }
}
