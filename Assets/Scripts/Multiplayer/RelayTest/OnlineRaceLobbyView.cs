using System;
using UnityEngine;
using Escape4Now.Accounts;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // Presentation only: connections, readiness, and game rules remain in the session.
    public sealed class OnlineRaceLobbyView : MonoBehaviour
    {
        [SerializeField] private Button monsterRole;
        [SerializeField] private Button fillBots;
        [SerializeField] private Button signIn;
        [SerializeField, Tooltip("Cobin's CONTROLLERS / VIBRATE panel. Online it lists only this computer's controller.")]
        private Escape4Now.UI.LobbyControllerPanel controllerPanel;
        [Serializable]
        private sealed class PlayerRow
        {
            public GameObject root;
            public Image portrait;
            public Text name;
            public Text role;
            public Text state;
            public Image badge;
            [NonSerialized] public OnlineLockerCard locker;
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
        [SerializeField] private Toggle[] roomSizes;
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
        [Header("Player Profile")]
        [SerializeField] private GameObject raceProfile;
        [SerializeField] private Text accountName;
        [SerializeField] private Button nextSkin;
        [SerializeField] private Text skinLabel;
        [Header("Optional Connection Diagnostics")]
        [SerializeField] private Button openDiagnostics;
        [SerializeField] private Button closeDiagnostics;
        [SerializeField] private Dropdown protocol;
        [SerializeField] private InputField networkLabel;
        [SerializeField] private Button export;
        [SerializeField] private Text diagnosticStatus;
        [SerializeField] private Text reportPath;
        [Header("State Colors")]
        [SerializeField] private Color waitingColor = new Color(.32f, .34f, .4f);
        [SerializeField] private Color errorColor = new Color(1f, .55f, .52f);
        [SerializeField] private Color normalColor = new Color(.84f, .84f, .88f);
        [SerializeField] private Color readyTextColor = new Color(.04f, .08f, .07f);
        [SerializeField] private Color waitingTextColor = Color.white;
        [Header("Player Row Copy")]
        [SerializeField] private string readyText = "READY";
        [SerializeField] private string notReadyText = "NOT READY";
        [SerializeField] private string emptySlotText = "WAITING";
        [SerializeField] private string closedSlotText = "CLOSED";
        [SerializeField] private string hostRole = "HOST";
        [SerializeField] private string localRole = "YOU";
        [SerializeField] private string localHostRole = "HOST / YOU";
        [SerializeField] private string guestRole = "PLAYER";
        [SerializeField] private string connectedFormat = "{0} / {1} CONNECTED";
        [Header("Lobby Messages")]
        [SerializeField] private string setupMessage = "Host a room or join a friend using their room code.";
        [SerializeField] private string connectingMessage = "Connecting to your room...";
        [SerializeField] private string failureMessage = "Connection failed. Open Connection Settings to see the error or save a report.";
        [SerializeField] private string hostMessage = "Share the room code. Everyone must select Ready before you can start.";
        [SerializeField] private string guestMessage = "You are in the room. Select Ready when you are ready to play.";
        [SerializeField] private string waitingMessage = "You are ready. Waiting for the match to start.";
        [SerializeField] private string allReadyMessage = "Everyone is ready. Start the match!";
        private InputAction cancelAction;
        private bool previousRoom;
        private int selectedCapacity = 2;

        private void OnEnable()
        {
            cancelAction = cancel != null && cancel.action != null ? cancel.action.Clone() : null;
            cancelAction?.Enable();
        }

        private void OnDisable() { cancelAction?.Dispose(); cancelAction = null; }

        // Wire the saved Canvas controls to the shared room. Mode rules live on the match controller.
        private void Start()
        {
            // Each player is on their own computer, so the panel shows one row: this computer's controller,
            // labelled with the player's slot once they are in a room.
            if (controllerPanel != null)
                controllerPanel.BindOnline(() => session.LocalSlot >= 0 ? "P" + (session.LocalSlot + 1) : "YOU",
                    () => session.LocalSlot >= 0 ? session.SlotColor(session.LocalSlot) : Escape4Now.Menus.ArcadeMenuKit.Teal);
            for (int index = 0; index < roomSizes.Length; index++)
            {
                int capacity = index + 2;
                roomSizes[index].onValueChanged.AddListener(selected =>
                {
                    if (selected) selectedCapacity = capacity;
                });
            }
            protocol.SetValueWithoutNotify((int)session.Relay.Protocol);
            protocol.onValueChanged.AddListener(session.Relay.SelectProtocol);
            host.onClick.AddListener(Host);
            join.onClick.AddListener(Join);
            ready.onClick.AddListener(session.ToggleReady);
            start.onClick.AddListener(session.StartMatch);
            leave.onClick.AddListener(session.Leave);
            back.onClick.AddListener(session.MainMenu);
            copy.onClick.AddListener(() => GUIUtility.systemCopyBuffer = session.Relay.RoomCode);
            nextSkin.onClick.AddListener(() => session.CycleProfileSkin(1));
            foreach (var row in rows)
            {
                row.locker = row.root.GetComponent<OnlineLockerCard>();
                row.locker?.Bind(session);
            }
            signIn.onClick.AddListener(session.OpenSignIn);
            if (monsterRole != null) monsterRole.onClick.AddListener(session.CycleMonsterRole);
            if (fillBots != null) fillBots.onClick.AddListener(session.ToggleFillWithBots);
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

        private async void Host() { await session.HostAsync(selectedCapacity); }
        private async void Join() { await session.JoinAsync(joinCode.text); }
        private void Export()
        {
            try { reportPath.text = "Saved: " + session.Relay.Export(networkLabel.text); }
            catch (Exception error) { reportPath.text = "Could not save: " + RelayTestReport.Safe(error.Message); }
        }

        // Refresh the controls and locker cards when the account or room state changes.
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
            bool editProfile = inRoom && session.CanEditProfile;
            raceProfile.SetActive(editProfile && rows[0].locker == null);
            if (monsterRole != null)
            {
                monsterRole.gameObject.SetActive(inRoom && session.HasMonsterRole && session.IsHost);
                monsterRole.GetComponentInChildren<Text>().text = session.MonsterSlot < 0
                    ? "AI MONSTER" : "MONSTER / P" + (session.MonsterSlot + 1);
            }
            if (editProfile)
            {
                accountName.text = AccountService.DisplayName;
                skinLabel.text = session.ProfileSkinName(session.ProfileSkinIndex(session.LocalSlot));
            }
            if (fillBots != null)
            {
                fillBots.gameObject.SetActive(inRoom && session.IsHost && session.CanFillWithBots);
                fillBots.GetComponentInChildren<Text>().text = session.FillWithBots ? "FILL BOTS / ON" : "FILL BOTS / OFF";
            }
            if (previousRoom != inRoom && !diagnosticsPanel.activeSelf)
                EventSystem.current?.SetSelectedGameObject(inRoom ? ready.gameObject : host.gameObject);
            previousRoom = inRoom;
            bool canConnect = connection.CanStart;
            bool canPlayOnline = connection.HasRequiredAccount;
            signIn.gameObject.SetActive(!inRoom && !canPlayOnline);
            host.interactable = joinCode.interactable = canConnect && canPlayOnline;
            protocol.interactable = canConnect;
            for (int index = 0; index < roomSizes.Length; index++)
            {
                roomSizes[index].interactable = canConnect && index + 2 <= connection.Settings.maximumPlayers;
                Escape4Now.UI.ArcadeChip.Paint(roomSizes[index].transform, roomSizes[index].isOn);
            }
            join.interactable = canConnect && canPlayOnline && !string.IsNullOrWhiteSpace(joinCode.text);
            ready.interactable = inRoom && session.CanEditProfile && !session.ProfileUpdating;
            readyLabel.text = session.Ready ? notReadyText : readyText;
            start.gameObject.SetActive(inRoom && session.IsHost);
            start.interactable = session.CanStart;
            copy.interactable = !string.IsNullOrEmpty(connection.RoomCode);
            code.text = connection.RoomCode;
            summary.text = string.Format(connectedFormat, session.PlayerCount, session.Capacity);
            for (int slot = 0; slot < rows.Length; slot++)
            {
                var row = rows[slot];
                row.root.SetActive(slot < session.Capacity || row.locker != null && slot < connection.Settings.maximumPlayers);
                if (!row.root.activeSelf) continue;
                bool available = slot < session.Capacity;
                bool connected = available && slot < session.PlayerCount;
                bool isReady = connected && session.IsSlotReady(slot);
                int skinIndex = session.ProfileSkinIndex(slot);
                row.locker?.Present(connected, available, session.FillWithBots, connected && slot == session.LocalSlot && editProfile,
                    skinIndex >= 0 ? "SKIN " + (skinIndex + 1) + " / " + session.ProfileSkinCount : "", session.SlotColor(slot));
                row.name.text = connected ? session.SlotName(slot).ToUpperInvariant() : session.FillWithBots ? "BOT SLOT" : "OPEN SLOT";
                row.role.text = !connected ? "" : slot == session.MonsterSlot ? "MONSTER" : slot == 0 ? (slot == session.LocalSlot ? localHostRole : hostRole)
                    : slot == session.LocalSlot ? localRole : guestRole;
                row.portrait.sprite = session.SlotPortrait(slot);
                row.portrait.enabled = connected && row.portrait.sprite != null;
                row.portrait.color = connected ? Color.white : new Color(1, 1, 1, .25f);
                row.state.text = !available ? closedSlotText : !connected ? session.FillWithBots ? "BOT SLOT" : emptySlotText : isReady ? readyText : notReadyText;
                row.state.color = isReady ? readyTextColor : waitingTextColor;
                row.badge.color = isReady ? session.SlotColor(slot) : waitingColor;
            }
            bool failed = !string.IsNullOrEmpty(connection.Error);
            status.color = failed ? errorColor : normalColor;
            status.text = !canPlayOnline ? "Sign in with your game account to play online."
                : failed ? string.IsNullOrEmpty(connection.Status) ? failureMessage : connection.Status
                : connection.Busy || connection.Active && !inRoom ? connectingMessage
                : !inRoom ? setupMessage : session.CanStart ? allReadyMessage : session.Ready ? waitingMessage
                : session.IsHost ? hostMessage : guestMessage;
            diagnosticStatus.text = "PROTOCOL: " + connection.Protocol + "\nSTAGE: " + connection.Stage
                + "\nRELAY: " + connection.Endpoint + "\nROUND TRIPS: " + connection.Replies
                + "   RTT: " + connection.LastRoundTripMilliseconds.ToString("F1") + " ms\n" + connection.Error;
        }
    }
}
