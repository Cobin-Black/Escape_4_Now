using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // The room owns connections and slots. Each mode translates its own input and match state.
    public abstract class OnlineModeMatch : MonoBehaviour
    {
        public virtual bool UsesTurnSystem => false;
        public virtual bool CanUseTurn(int slot) => false;
        public virtual void SubmitTurnAction(LanMultiplayerSession session, string action) { }
        public abstract bool Started { get; }
        public abstract bool Finished { get; }
        public abstract string Error { get; }
        public abstract string Title { get; }
        public abstract string Objective { get; }
        public abstract Skin_Handler ProfileSkins { get; }
        public abstract void SetSlotName(int slot, string name);
        public abstract Sprite SlotPortrait(int slot);
        public abstract Color SlotColor(int slot);
        public virtual string SlotName(int slot) => "Player " + (slot + 1);
        public abstract bool StartHost(int count);
        // Modes that can fill empty room slots with computer players when the host turns FILL BOTS on.
        public virtual bool SupportsBots => false;
        // Called on the host just before StartHost. True marks a slot no one joined, which a bot plays.
        public virtual void SetBotSlots(bool[] bots) { }
        // Seconds the host's own input waits before it is used, so the host can't react faster than a guest
        // whose input has to travel over the internet. Set by the room every frame; always 0 on guests.
        public float HostInputDelay { get; set; }
        // Capture runs on the host. Apply runs on each guest to display what the host decided.
        public abstract string Capture();
        public abstract bool Apply(int count, string payload);
        // Read input on the local computer and process it on the host in the sender's assigned slot.
        public abstract void ReceiveInput(int slot, string payload);
        public abstract void SendLocalInput(LanMultiplayerSession session);
        public abstract void ConnectionLost(string reason);
        // The finished match's result for one room slot, saved by that player's own computer.
        // Returns false when there is nothing to save, such as when the connection dropped.
        public virtual bool TryGetSlotResult(int slot, out Escape4Now.Accounts.MatchStat stat)
        {
            stat = default;
            return false;
        }
    }
}
