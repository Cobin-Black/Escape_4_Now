using System;

namespace Escape4Now.Multiplayer
{
    // Match rules exchange messages through this small boundary, regardless of LAN or Relay.
    public interface IMatchMessageConnection : IDisposable
    {
        bool Closed { get; }
        void Send(string message);
        void Pump(Action<string> onMessage);
    }
}
