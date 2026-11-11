using System;
using UnityEngine;
using UnityEngine.UI;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // All layout and input routing live in the saved scene, not in the networking code.
    public sealed class RelayTestView : MonoBehaviour
    {
        [Header("Connection Test")]
        [SerializeField] private RelayRoomConnection connection;
        [Header("Setup")]
        [SerializeField] private Dropdown protocol;
        [SerializeField] private InputField joinCode;
        [SerializeField] private InputField networkLabel;
        [SerializeField] private Button host;
        [SerializeField] private Button join;
        [SerializeField] private Button leave;
        [SerializeField] private Button copyCode;
        [SerializeField] private Button export;
        [Header("Readout")]
        [SerializeField] private Text code;
        [SerializeField] private Text status;
        [SerializeField] private Text diagnostics;
        [SerializeField] private Text history;
        [SerializeField] private Text exportedPath;

        private void Start()
        {
            protocol.SetValueWithoutNotify((int)connection.Protocol);
            protocol.onValueChanged.AddListener(connection.SelectProtocol);
            host.onClick.AddListener(Host);
            join.onClick.AddListener(Join);
            leave.onClick.AddListener(connection.Leave);
            copyCode.onClick.AddListener(CopyCode);
            export.onClick.AddListener(Export);
        }

        private async void Host() { await connection.HostAsync(); }
        private async void Join() { await connection.JoinAsync(joinCode.text); }
        private void CopyCode() { GUIUtility.systemCopyBuffer = connection.RoomCode; }

        private void Export()
        {
            try { exportedPath.text = "Report saved: " + connection.Export(networkLabel.text); }
            catch (Exception error) { exportedPath.text = "Could not save report: " + RelayTestReport.Safe(error.Message); }
        }

        private void Update()
        {
            bool setup = connection.CanStart;
            host.interactable = join.interactable = protocol.interactable = joinCode.interactable = setup;
            leave.interactable = connection.Active || connection.Busy;
            copyCode.interactable = !string.IsNullOrEmpty(connection.RoomCode);
            code.text = string.IsNullOrEmpty(connection.RoomCode) ? "ROOM CODE: -" : "ROOM CODE: " + connection.RoomCode;
            status.text = connection.Status;
            diagnostics.text = "PROTOCOL: " + connection.Protocol + "     REMOTE PEERS: " + connection.RemotePlayers
                + "     ROUND TRIPS: " + connection.Replies + "     RTT: " + connection.LastRoundTripMilliseconds.ToString("F1") + " ms"
                + "\nSTAGE: " + connection.Stage + "\nRELAY: " + connection.Endpoint
                + (string.IsNullOrEmpty(connection.Error) ? "" : "\nERROR: " + connection.Error);
            history.text = connection.Log;
        }
    }
}
