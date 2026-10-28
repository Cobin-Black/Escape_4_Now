// Finds available LAN hosts on the local network so players do not need to type an address every time.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Escape4Now.Multiplayer
{
    //Finds a room on the local network. The normal connection still checks its code.
    public sealed class LanDiscovery : IDisposable
    {
        public const int Port = 47777;
        private readonly Socket socket;
        private readonly bool host;
        private readonly string code, prefix, nonce;
        private readonly byte[] buffer = new byte[128];
        private readonly List<IPAddress> targets = new List<IPAddress>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private double nextSearch, responseWindow;
        private int responses;

        //Only six ordinary digits count as a room code.
        public static bool ValidCode(string value)
        {
            if (value == null || value.Length != 6) return false;
            foreach (char digit in value) if (digit < '0' || digit > '9') return false;
            return true;
        }

        //The host listens on a known port; guests use separate temporary ports.
        public LanDiscovery(bool isHost, string roomCode, int version)
        {
            if (!ValidCode(roomCode)) throw new ArgumentException("Invalid room code.");
            host = isHost;
            code = roomCode;
            prefix = "E4N|" + version + "|";
            nonce = Guid.NewGuid().ToString("N");
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                socket.ExclusiveAddressUse = true;
                socket.EnableBroadcast = true;
                socket.Bind(new IPEndPoint(IPAddress.Any, host ? Port : 0));
                socket.Blocking = false;
                if (!host)
                {
                    try { FindBroadcastAddresses(); }
                    catch (NetworkInformationException) { /*The basic local broadcasts still work.*/ }
                }
            }
            catch { socket.Dispose(); throw; }
        }

        //Includes each local adapter so a second adapter does not hide the Wi-Fi room.
        private void FindBroadcastAddresses()
        {
            targets.Add(IPAddress.Loopback);
            targets.Add(IPAddress.Broadcast);
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                foreach (UnicastIPAddressInformation entry in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (entry.Address.AddressFamily != AddressFamily.InterNetwork || entry.IPv4Mask == null) continue;
                    byte[] address = entry.Address.GetAddressBytes();
                    byte[] mask = entry.IPv4Mask.GetAddressBytes();
                    for (int i = 0; i < address.Length; i++) address[i] |= (byte)~mask[i];
                    var broadcast = new IPAddress(address);
                    if (!targets.Contains(broadcast) && targets.Count < 32) targets.Add(broadcast);
                }
            }
        }

        //Processes a limited number of small packets without stopping the game loop.
        public IPAddress Poll(bool acceptingGuests = true)
        {
            double now = clock.Elapsed.TotalSeconds;
            if (!host && now >= nextSearch)
            {
                nextSearch = now + 1;
                foreach (IPAddress target in targets)
                    Send(prefix + "find|" + code + "|" + nonce, new IPEndPoint(target, Port));
            }
            if (now >= responseWindow) { responseWindow = now + 1; responses = 0; }
            for (int i = 0; i < 16 && socket.Poll(0, SelectMode.SelectRead); i++)
            {
                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                int length;
                try { length = socket.ReceiveFrom(buffer, ref sender); }
                catch (SocketException) { continue; }
                string message = Encoding.ASCII.GetString(buffer, 0, length);
                var endpoint = (IPEndPoint)sender;
                if (!IsLocalAddress(endpoint.Address)) continue;
                if (host)
                {
                    string request = prefix + "find|" + code + "|";
                    if (!acceptingGuests || responses >= 8 || !message.StartsWith(request, StringComparison.Ordinal)
                        || message.Length != request.Length + 32) continue;
                    string token = message.Substring(request.Length);
                    if (!Guid.TryParseExact(token, "N", out _)) continue;
                    responses++;
                    Send(prefix + "room|" + code + "|" + token, endpoint);
                }
                else if (endpoint.Port == Port && message == prefix + "room|" + code + "|" + nonce)
                    return endpoint.Address;
            }
            return null;
        }

        //An unavailable adapter can be skipped; the next search retries the others.
        private void Send(string message, IPEndPoint target)
        {
            try { socket.SendTo(Encoding.ASCII.GetBytes(message), target); }
            catch (SocketException) { }
        }

        //Only accepts private IPv4 addresses and loopback used by local tests.
        public static bool IsLocalAddress(IPAddress ip)
        {
            if (ip == null || ip.AddressFamily != AddressFamily.InterNetwork) return false;
            byte[] bytes = ip.GetAddressBytes();
            return IPAddress.IsLoopback(ip) || bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254);
        }

        //Stops listening when a lobby closes or a guest finds the host.
        public void Dispose() => socket.Dispose();
    }
}
