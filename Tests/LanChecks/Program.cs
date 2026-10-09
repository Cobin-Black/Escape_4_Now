using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Escape4Now.Multiplayer;

internal static class Program
{
    private static void Main()
    {
        CheckFrames();
        CheckInvalidLength();
        CheckQueueLimit();
        CheckDiscovery();
        Console.WriteLine("LAN TRANSPORT CHECKS PASSED");
    }

    //A room code finds the host, but wrong codes and different builds get no reply.
    private static void CheckDiscovery()
    {
        Require(LanDiscovery.ValidCode("001234"), "Leading zeros are allowed");
        foreach (string code in new[] { null, "", "12345", "1234567", "+12345", "12 456", "abcdef" })
            Require(!LanDiscovery.ValidCode(code), "Invalid room code rejected");
        using (var host = new LanDiscovery(true, "001234", 2))
        using (var guest = new LanDiscovery(false, "001234", 2))
        using (var secondGuest = new LanDiscovery(false, "001234", 2))
        using (var thirdGuest = new LanDiscovery(false, "001234", 2))
        using (var wrongCode = new LanDiscovery(false, "999999", 2))
        using (var oldBuild = new LanDiscovery(false, "001234", 1))
        {
            IPAddress found = null;
            IPAddress second = null, third = null;
            bool rejectedCode = true, rejectedBuild = true;
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed.TotalSeconds < 2)
            {
                found ??= guest.Poll();
                second ??= secondGuest.Poll();
                third ??= thirdGuest.Poll();
                rejectedCode &= wrongCode.Poll() == null;
                rejectedBuild &= oldBuild.Poll() == null;
                host.Poll();
                System.Threading.Thread.Sleep(5);
            }
            Require(found != null && second != null && third != null, "Three guests find host by code");
            Require(rejectedCode, "Wrong code not discovered");
            Require(rejectedBuild, "Different build not discovered");
        }
        //Disposal frees the host port, and a closed lobby does not advertise itself.
        using (var host = new LanDiscovery(true, "001234", 2))
        using (var guest = new LanDiscovery(false, "001234", 2))
        {
            var timer = Stopwatch.StartNew();
            bool hidden = true;
            while (timer.Elapsed.TotalSeconds < 1.2)
            {
                hidden &= guest.Poll() == null;
                host.Poll(false);
                System.Threading.Thread.Sleep(5);
            }
            Require(hidden, "Closed lobby stays hidden");
        }
        Console.WriteLine("LAN DISCOVERY CHECKS PASSED");
    }

    private static (Socket, Socket) Pair()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        client.Connect(listener.LocalEndpoint);
        Socket server = listener.AcceptSocket();
        listener.Stop();
        return (client, server);
    }

    //TCP can split one message or combine several into one read.
    private static void CheckFrames()
    {
        var pair = Pair();
        using Socket sender = pair.Item1;
        using var receiver = new LanConnection(pair.Item2);
        var messages = new List<string>();
        byte[] frame = Frame("hello");
        for (int i = 0; i < frame.Length - 1; i++)
        {
            sender.Send(new[] { frame[i] });
            receiver.Pump(messages.Add);
            Require(messages.Count == 0, "Incomplete message waits");
        }
        sender.Send(new[] { frame[frame.Length - 1] });
        sender.Send(Frame("second"));
        var watch = Stopwatch.StartNew();
        while (messages.Count < 2 && watch.ElapsedMilliseconds < 2000) receiver.Pump(messages.Add);
        Require(messages.Count == 2 && messages[0] == "hello" && messages[1] == "second", "Frames remain separate and ordered");
        sender.Dispose();
        watch.Restart();
        while (!receiver.Closed && watch.ElapsedMilliseconds < 2000) receiver.Pump(messages.Add);
        Require(receiver.Closed, "Closed peer is detected");
    }

    private static void CheckInvalidLength()
    {
        foreach (int length in new[] { -1, 0, LanConnection.MaxMessageBytes + 1, int.MaxValue })
        {
            var pair = Pair();
            using Socket sender = pair.Item1;
            using var receiver = new LanConnection(pair.Item2);
            sender.Send(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(length)));
            var watch = Stopwatch.StartNew();
            while (!receiver.Closed && watch.ElapsedMilliseconds < 2000) receiver.Pump(_ => throw new Exception("Invalid frame delivered"));
            Require(receiver.Closed, "Invalid message size disconnects");
        }
    }

    private static void CheckQueueLimit()
    {
        var pair = Pair();
        using Socket other = pair.Item1;
        using var wire = new LanConnection(pair.Item2);
        for (int i = 0; i < 5; i++) wire.Send(new string('x', LanConnection.MaxMessageBytes));
        Require(wire.Closed, "Slow peer cannot grow the send queue without a limit");
    }

    private static byte[] Frame(string text)
    {
        byte[] body = Encoding.UTF8.GetBytes(text);
        byte[] bytes = new byte[body.Length + 4];
        Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(body.Length)), 0, bytes, 0, 4);
        Buffer.BlockCopy(body, 0, bytes, 4, body.Length);
        return bytes;
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Console.WriteLine("PASS: " + label);
    }
}
