// The small set of event information and actions that the shared UI and player scripts need.

using Escape4Now.Player;
using UnityEngine;

namespace Escape4Now.Map
{
    public interface IMapEventSource
    {
        string CurrentMessage { get; }

        int BlackoutTurns { get; }

        bool EventLocationsVisible { get; }

        void SetEventLocationsVisible(bool visible);
        bool ResolveStep(PlayerCharacter player, Vector2Int direction);
        void ShowMessage(PlayerCharacter player, string message);
        void ApplyNetworkDisplay(string message, int blackout);
    }
}
