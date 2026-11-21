using System;
using System.IO;
using System.Threading.Tasks;
using Escape4Now.Map;
using UnityEngine;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // Development-only two-process test of real Relay rooms and existing authoritative Race actions.
    public sealed class OnlineRaceSmokeCheck : MonoBehaviour
    {
        [SerializeField, Min(10)] private float timeoutSeconds = 100;
        [SerializeField, Min(1)] private int requiredObservedTurns = 3;

        private async void Start()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            string role = Argument("-onlineRaceRole");
            if (role != "host" && role != "client") return;
            var session = GetComponent<OnlineRaceSession>();
            var relay = session.Relay;
            try
            {
                await Escape4Now.Accounts.AccountService.ConnectAsync();
                if (!Escape4Now.Accounts.AccountService.IsSignedIn)
                    throw new InvalidOperationException("Sign in with a game account on this test installation before running the online Race check.");
                if (Enum.TryParse(Argument("-relayProtocol"), true, out RelayTestProtocol protocol)) relay.SelectProtocol((int)protocol);
                await (role == "host" ? session.HostAsync(2) : session.JoinAsync(Argument("-relayCode")));
                string roomFile = Argument("-relayRoomFile");
                if (role == "host" && relay.Active && !string.IsNullOrEmpty(roomFile)) File.WriteAllText(roomFile, relay.RoomCode);
                float deadline = Time.realtimeSinceStartup + timeoutSeconds;
                int observedTurns = 0, lastTurn = -1, lastRound = -1;
                bool sawRoll = false, sawMovement = false;
                Vector2Int? initialCell = null;
                float nextAction = 0;
                var match = FindFirstObjectByType<LanMultiplayerSession>();
                while (relay.Active && Time.realtimeSinceStartup < deadline)
                {
                    if (!session.Playing)
                    {
                        if (match.LocalPlayerIndex >= 0 && (session.IsHost || match.ConnectedPlayers >= 2) && !session.Ready) session.ToggleReady();
                        if (session.CanStart) session.StartMatch();
                    }
                    else if (match.LatestState != null)
                    {
                        var state = match.LatestState;
                        var owned = state.players[match.LocalPlayerIndex];
                        var observedCell = new Vector2Int(owned.x, owned.y);
                        if (!initialCell.HasValue) initialCell = observedCell;
                        sawRoll |= owned.rolled && owned.roll > 0;
                        sawMovement |= observedCell != initialCell.Value;
                        if (state.turn != lastTurn || state.round != lastRound)
                        { observedTurns++; lastTurn = state.turn; lastRound = state.round; }
                        if (observedTurns >= requiredObservedTurns && relay.Passed && sawRoll && sawMovement) break;
                        if (match.IsMyTurn && Time.realtimeSinceStartup >= nextAction)
                        {
                            nextAction = Time.realtimeSinceStartup + 1.5f;
                            var player = state.players[match.LocalPlayerIndex];
                            if (!player.rolled) match.Submit("roll");
                            else if (!player.moving)
                            {
                                var map = FindFirstObjectByType<IsometricMapTemplate>();
                                bool requested = false;
                                foreach (var direction in new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down })
                                {
                                    var cell = new Vector2Int(player.x, player.y) + direction;
                                    if (map.IsWalkable(cell) && !map.IsExit(cell))
                                    { match.Submit("move", cell.x, cell.y); requested = true; break; }
                                }
                                if (!requested || player.moves == 0) match.Submit("end");
                            }
                        }
                    }
                    await Task.Delay(100);
                    if (this == null) return;
                }
                bool success = session.Playing && observedTurns >= requiredObservedTurns && relay.Passed && !match.Failed && sawRoll && sawMovement;
                string result = "Online Race local-process test: " + (success ? "PASS" : "FAIL")
                    + "\nRole=" + role + "\nObservedTurns=" + observedTurns + "\nStateSeed=" + match.LatestState?.seed
                    + "\nOwnedRollObserved=" + sawRoll + "\nOwnedMovementObserved=" + sawMovement
                    + "\nStatus=" + match.Status + "\nRelayError=" + relay.Error
                    + "\nDiagnosticReport=" + relay.Export("Local processes on one computer - NOT a different-network test");
                string resultFile = Argument("-relayResultFile");
                if (!string.IsNullOrEmpty(resultFile)) File.WriteAllText(resultFile, result);
                Debug.Log(result);
                // Keep the host up briefly so both peers can finish the same observation window.
                await Task.Delay(5000);
                relay.Leave();
                Application.Quit(success ? 0 : 2);
            }
            catch (Exception error)
            {
                Debug.LogError("[Online Race check] " + RelayTestReport.Safe(error.ToString()));
                Application.Quit(3);
            }
#endif
        }

        private static string Argument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }
    }
}
