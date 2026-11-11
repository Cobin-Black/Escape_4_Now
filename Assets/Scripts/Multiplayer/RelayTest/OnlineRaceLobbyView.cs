using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // Presentation only: connections, readiness, and game rules remain in the session.
    public sealed class OnlineRaceLobbyView : MonoBehaviour
    {
        [Serializable]
        private sealed class PlayerRow
        {
            public GameObject root;
            public Image swatch;
            public Image portrait;
            public Text name;
            public Text state;
            public Image badge;
        }

        [Header("Session and Screen Sections")]
        [SerializeField] private OnlineRaceSession session;
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject setup;
        [SerializeField] private GameObject room;
        [SerializeField] private GameObject actions;
        [SerializeField] private GameObject diagnosticsPanel;
        [SerializeField] private CanvasGroup mainControls;
        [SerializeField] private InputActionReference cancel;
        [Header("Setup Controls")]
        [SerializeField] private Dropdown players;
        [SerializeField] private InputField joinCode;
        [SerializeField] private Button host;
        [SerializeField] private Button join;
        [SerializeField] private Button back;
        [Header("Room Controls")]
        [SerializeField] private Button ready;
        [SerializeField] private Button start;
        [SerializeField] private Button leave;
        [SerializeField] private Button copy;
        [SerializeField] private Text readyLabel;
        [SerializeField] private Text code;
        [SerializeField] private Text status;
        [SerializeField] private Text summary;
        [SerializeField] private PlayerRow[] rows;
        [Header("Optional Connection Diagnostics")]
        [SerializeField] private Button openDiagnostics;
        [SerializeField] private Button closeDiagnostics;
        [SerializeField] private Dropdown protocol;
        [SerializeField] private InputField networkLabel;
        [SerializeField] private Button export;
        [SerializeField] private Text diagnosticStatus;
        [SerializeField] private Text reportPath;
        [Header("State Colors")]
        [SerializeField] private Color readyColor = new Color(.15f, .78f, .59f);
        [SerializeField] private Color waitingColor = new Color(.32f, .34f, .4f);
        [SerializeField] private Color errorColor = new Color(1f, .55f, .52f);
        [SerializeField] private Color normalColor = new Color(.84f, .84f, .88f);
        [SerializeField] private Color readyTextColor = new Color(.04f, .08f, .07f);
        [Header("Player Row Copy")]
        [SerializeField] private string readyText = "READY";
        [SerializeField] private string notReadyText = "NOT READY";
        [SerializeField] private string emptySlotText = "WAITING";
        [SerializeField] private string hostSuffix = "  (HOST)";
        [SerializeField] private string localSuffix = "  - YOU";
        [SerializeField] private string connectedFormat = "{0} / {1} CONNECTED";
        [Header("Lobby Messages")]
        [SerializeField] private string setupMessage = "Host a room or join a friend using their room code.";
        [SerializeField] private string connectingMessage = "Connecting to your room...";
        [SerializeField] private string failureMessage = "Connection failed. Open Connection Settings to see the error or save a report.";
        [SerializeField] private string hostMessage = "Share the room code. Everyone must select Ready before you can start.";
        [SerializeField] private string guestMessage = "You are in the room. Select Ready when you are ready to play.";
        [SerializeField] private string waitingMessage = "You are ready. Waiting for the other players and host.";
        [SerializeField] private string allReadyMessage = "Everyone is ready. Start the match!";
        private InputAction cancelAction;
        private bool previousRoom;

        private void OnEnable()
        {
            cancelAction = cancel != null && cancel.action != null ? cancel.action.Clone() : null;
            cancelAction?.Enable();
        }

        private void OnDisable() { cancelAction?.Dispose(); cancelAction = null; }

        private void Start()
        {
            var counts = new List<string>();
            for (int count = 2; count <= session.Relay.Settings.maximumPlayers; count++) counts.Add(count + " Players");
            players.ClearOptions();
            players.AddOptions(counts);
            protocol.SetValueWithoutNotify((int)session.Relay.Protocol);
            protocol.onValueChanged.AddListener(session.Relay.SelectProtocol);
            host.onClick.AddListener(Host);
            join.onClick.AddListener(Join);
            ready.onClick.AddListener(session.ToggleReady);
            start.onClick.AddListener(session.StartMatch);
            leave.onClick.AddListener(session.Leave);
            back.onClick.AddListener(session.MainMenu);
            copy.onClick.AddListener(() => GUIUtility.systemCopyBuffer = session.Relay.RoomCode);
            export.onClick.AddListener(Export);
            openDiagnostics.onClick.AddListener(() => ShowDiagnostics(true));
            closeDiagnostics.onClick.AddListener(() => ShowDiagnostics(false));
            diagnosticsPanel.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(host.gameObject);
        }

        private void ShowDiagnostics(bool visible)
        {
            diagnosticsPanel.SetActive(visible);
            // A modal must block keyboard/gamepad selection behind it as well as mouse clicks.
            mainControls.interactable = mainControls.blocksRaycasts = !visible;
            EventSystem.current?.SetSelectedGameObject(visible ? protocol.gameObject : openDiagnostics.gameObject);
        }

        private async void Host() { await session.HostAsync(players.value + 2); }
        private async void Join() { await session.JoinAsync(joinCode.text); }
        private void Export()
        {
            try { reportPath.text = "Saved: " + session.Relay.Export(networkLabel.text); }
            catch (Exception error) { reportPath.text = "Could not save: " + RelayTestReport.Safe(error.Message); }
        }

        private void LateUpdate()
        {
            panel.SetActive(!session.Playing);
            if (session.Playing) return;
            if (cancelAction?.WasPressedThisFrame() == true)
            {
                if (diagnosticsPanel.activeSelf) ShowDiagnostics(false);
                else session.MainMenu();
                return;
            }
            var connection = session.Relay;
            bool inRoom = connection.Active && !connection.Busy && (session.IsHost || connection.Network.IsConnectedClient);
            setup.SetActive(!inRoom);
            room.SetActive(inRoom);
            actions.SetActive(inRoom);
            if (previousRoom != inRoom && !diagnosticsPanel.activeSelf)
                EventSystem.current?.SetSelectedGameObject(inRoom ? ready.gameObject : host.gameObject);
            previousRoom = inRoom;
            bool canConnect = connection.CanStart;
            host.interactable = players.interactable = joinCode.interactable = protocol.interactable = canConnect;
            join.interactable = canConnect && !string.IsNullOrWhiteSpace(joinCode.text);
            ready.interactable = inRoom && session.LocalSlot >= 0;
            readyLabel.text = session.Ready ? notReadyText : readyText;
            start.gameObject.SetActive(inRoom && session.IsHost);
            start.interactable = session.CanStart;
            copy.interactable = !string.IsNullOrEmpty(connection.RoomCode);
            code.text = connection.RoomCode;
            summary.text = string.Format(connectedFormat, session.PlayerCount, session.Capacity);
            for (int slot = 0; slot < rows.Length; slot++)
            {
                var row = rows[slot];
                row.root.SetActive(slot < session.Capacity);
                if (!row.root.activeSelf) continue;
                bool connected = slot < session.PlayerCount;
                bool isReady = connected && session.IsSlotReady(slot);
                row.name.text = session.SlotName(slot) + (slot == 0 ? hostSuffix : "") + (slot == session.LocalSlot ? localSuffix : "");
                row.swatch.color = session.SlotColor(slot);
                row.portrait.sprite = session.SlotPortrait(slot);
                row.portrait.color = connected ? Color.white : new Color(1, 1, 1, .25f);
                row.state.text = !connected ? emptySlotText : isReady ? readyText : notReadyText;
                row.state.color = isReady ? readyTextColor : normalColor;
                row.badge.color = isReady ? readyColor : waitingColor;
            }
            bool failed = !string.IsNullOrEmpty(connection.Error);
            status.color = failed ? errorColor : normalColor;
            status.text = failed ? failureMessage : connection.Busy || connection.Active && !inRoom ? connectingMessage
                : !inRoom ? setupMessage : session.CanStart ? allReadyMessage : session.Ready ? waitingMessage
                : session.IsHost ? hostMessage : guestMessage;
            diagnosticStatus.text = "PROTOCOL: " + connection.Protocol + "\nSTAGE: " + connection.Stage
                + "\nRELAY: " + connection.Endpoint + "\nROUND TRIPS: " + connection.Replies
                + "   RTT: " + connection.LastRoundTripMilliseconds.ToString("F1") + " ms\n" + connection.Error;
        }
    }
}
