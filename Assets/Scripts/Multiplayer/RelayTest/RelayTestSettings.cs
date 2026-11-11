using UnityEngine;

namespace Escape4Now.Multiplayer.RelayTesting
{
    public enum RelayTestProtocol { DTLS, WSS, UDP }

    // This asset controls the connection experiment, not any game mode.
    [CreateAssetMenu(menuName = "Escape 4 Now/Networking/Relay Test Settings")]
    public sealed class RelayTestSettings : ScriptableObject
    {
        [Header("Unity Services")]
        [Tooltip("Both computers must use the same Unity project and environment.")]
        public string environment = "production";
        [Tooltip("Leave empty to let Relay choose a region. No private credentials belong here.")]
        public string region = "";

        [Header("Room")]
        public RelayTestProtocol defaultProtocol = RelayTestProtocol.DTLS;
        [Range(2, 4)] public int maximumPlayers = 2;
        [Range(1, 65535)] public int testVersion = 1;

        [Header("Timeouts - Seconds")]
        [Min(5)] public float serviceTimeout = 30;
        [Min(5)] public float connectionTimeout = 30;
        [Min(3)] public float responseTimeout = 12;
        [Min(.25f)] public float probeInterval = 1;
        [Min(10)] public float hostWaitingTimeout = 180;

        [Header("Pass Criteria")]
        [Min(1)] public int requiredRoundTrips = 5;
        [Min(1)] public float minimumObservationSeconds = 10;

        [Header("Transport")]
        [Min(100)] public int connectAttemptTimeoutMilliseconds = 1000;
        [Min(1)] public int maximumConnectAttempts = 30;
        [Min(1000)] public int disconnectTimeoutMilliseconds = 15000;

        [Header("Diagnostics")]
        [Range(20, 500)] public int maximumLogEntries = 150;
    }
}
