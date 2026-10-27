// Sends and receives LAN messages over a connection; match rules are handled by the session.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Escape4Now.Multiplayer
{
    //Sends complete, size-limited messages over TCP without waiting on the game thread.
    public sealed class LanConnection : IDisposable
    {
        public const int MaxMessageBytes = 65536;
        private readonly Socket socket;
        private readonly byte[] incoming = new byte[MaxMessageBytes + 4];
        private readonly Queue<byte[]> outgoing = new Queue<byte[]>();
        private static readonly Encoding TextEncoding = new UTF8Encoding(false, true);
        private int received, sent, queuedBytes;
        public bool Closed { get; private set; }

        //The caller supplies an already connected socket.
        public LanConnection(Socket connected)
        {
            socket = connected;
            socket.Blocking = false;
            socket.NoDelay = true;
        }

        //Rejects large messages and disconnects a peer whose send queue keeps growing.
        // Queue a complete encoded message; sending may need several socket writes.
        public void Send(string message)
        {
            if (Closed) return;
            byte[] body = TextEncoding.GetBytes(message);
            if (body.Length == 0 || body.Length > MaxMessageBytes) throw new ArgumentException("Invalid message size.");
            if (queuedBytes + body.Length + 4 > MaxMessageBytes * 4) { Dispose(); return; }
            byte[] frame = new byte[body.Length + 4];
            int size = IPAddress.HostToNetworkOrder(body.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(size), 0, frame, 0, 4);
            Buffer.BlockCopy(body, 0, frame, 4, body.Length);
            outgoing.Enqueue(frame);
            queuedBytes += frame.Length;
        }

        //Handles partial reads and writes, with a fixed amount of work per game frame.
        // Collect complete incoming messages before handing them to the session.
        public void Pump(Action<string> onMessage)
        {
            if (Closed) return;
            try
            {
                for (int i = 0; i < 16 && outgoing.Count > 0; i++)
                {
                    if (!socket.Poll(0, SelectMode.SelectWrite)) break;
                    byte[] frame = outgoing.Peek();
                    sent += socket.Send(frame, sent, frame.Length - sent, SocketFlags.None);
                    if (sent != frame.Length) break;
                    queuedBytes -= frame.Length;
                    outgoing.Dequeue();
                    sent = 0;
                }
                for (int i = 0; i < 32 && !Closed; i++)
                {
                    if (received >= 4)
                    {
                        int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(incoming, 0));
                        if (length < 1 || length > MaxMessageBytes) { Dispose(); return; }
                        if (received >= length + 4)
                        {
                            string message = TextEncoding.GetString(incoming, 4, length);
                            received -= length + 4;
                            Buffer.BlockCopy(incoming, length + 4, incoming, 0, received);
                            onMessage(message);
                            continue;
                        }
                    }
                    if (!socket.Poll(0, SelectMode.SelectRead)) break;
                    int count = socket.Receive(incoming, received, incoming.Length - received, SocketFlags.None);
                    if (count == 0) { Dispose(); return; }
                    received += count;
                }
            }
            catch (SocketException error)
            {
                if (error.SocketErrorCode != SocketError.WouldBlock) Dispose();
            }
            catch (DecoderFallbackException) { Dispose(); }
            catch (ObjectDisposedException) { Dispose(); }
        }

        //Releases the socket on leave, timeout, or a broken connection.
        // Close this connection and its socket when the match leaves or the peer disconnects.
        public void Dispose()
        {
            if (Closed) return;
            Closed = true;
            socket.Dispose();
            outgoing.Clear();
            queuedBytes = 0;
        }
    }
}
