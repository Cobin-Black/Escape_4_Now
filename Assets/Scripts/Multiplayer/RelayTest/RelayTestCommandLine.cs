using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // Optional development-build automation. Normal players use the scene's buttons.
    public sealed class RelayTestCommandLine : MonoBehaviour
    {
        [Header("Automated Run")]
        [SerializeField, Min(1)] private float successfulConnectionHoldSeconds = 5;
        [SerializeField, Min(10)] private float totalRunTimeoutSeconds = 100;

        private async void Start()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            string role = Argument("-relayRole");
            if (role != "host" && role != "client") return;
            var connection = GetComponent<RelayRoomConnection>();
            try
            {
                if (Enum.TryParse(Argument("-relayProtocol"), true, out RelayTestProtocol protocol))
                    connection.SelectProtocol((int)protocol);
                Task operation = role == "host" ? connection.HostAsync() : connection.JoinAsync(Argument("-relayCode"));
                await operation;
                string roomFile = Argument("-relayRoomFile");
                if (role == "host" && !string.IsNullOrEmpty(connection.RoomCode) && !string.IsNullOrEmpty(roomFile))
                    File.WriteAllText(roomFile, connection.RoomCode);
                float began = Time.realtimeSinceStartup;
                float passedAt = -1;
                string completedReport = null;
                while (connection.Active && Time.realtimeSinceStartup - began < totalRunTimeoutSeconds)
                {
                    if (connection.Passed && passedAt < 0)
                    {
                        passedAt = Time.realtimeSinceStartup;
                        // Record the completed observation window before either process deliberately tears down.
                        completedReport = connection.Export(Argument("-relayNetworkLabel"));
                    }
                    if (passedAt >= 0 && Time.realtimeSinceStartup - passedAt >= successfulConnectionHoldSeconds) break;
                    await Task.Delay(100);
                    if (this == null) return;
                }
                string report = completedReport ?? connection.Export(Argument("-relayNetworkLabel"));
                string resultFile = Argument("-relayResultFile");
                if (!string.IsNullOrEmpty(resultFile)) File.WriteAllText(resultFile, report);
                bool passed = completedReport != null;
                connection.Leave();
                Application.Quit(passed ? 0 : 2);
            }
            catch (Exception error)
            {
                Debug.LogError("[Relay automation] " + RelayTestReport.Safe(error.ToString()));
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
