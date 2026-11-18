// Gives each connected controller its own player slot (1-4) for local multiplayer,
// keeps turn-based modes listening only to the current player's controller,
// and vibrates a slot's controller so players can check which one is theirs (the buttons live on the game mode lobbies).

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape4Now.Multiplayer
{
    [DefaultExecutionOrder(-12000)]
    [DisallowMultipleComponent]
    public sealed class ControllerHandling : MonoBehaviour
    {
        public const int MaxSlots = 4;

        [Header("Vibrate Test")]
        [SerializeField, Range(0f, 1f)] private float vibrateLowFrequency = .6f;
        [SerializeField, Range(0f, 1f)] private float vibrateHighFrequency = .6f;
        [SerializeField, Min(.05f)] private float vibrateSeconds = .5f;

        private static ControllerHandling instance;
        // Slot index -> gamepad. A disconnected pad keeps its slot so reconnecting returns it to the same player.
        private static readonly Gamepad[] slots = new Gamepad[MaxSlots];
        private static readonly List<InputActionMap> restrictedMaps = new List<InputActionMap>();
        private static int restrictedSlot = -2;
        private static Gamepad restrictedPad;
        private readonly Coroutine[] vibrations = new Coroutine[MaxSlots];

        // Raised whenever a controller joins, leaves or moves to another slot.
        public static event System.Action AssignmentsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            for (int i = 0; i < MaxSlots; i++) slots[i] = null;
            restrictedMaps.Clear();
            restrictedSlot = -2;
            restrictedPad = null;
            AssignmentsChanged = null;
            ListeningSlot = -1;
        }

        // Exists in every scene without needing to be placed by hand. A scene copy can still override the Inspector values.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("Controller Handling");
            go.AddComponent<ControllerHandling>();
        }

        public static ControllerHandling Instance
        {
            get
            {
                if (instance == null && Application.isPlaying) Bootstrap();
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                // Only one instance lives across scenes; a copy placed in a scene hands its Inspector settings to it.
                instance.vibrateLowFrequency = vibrateLowFrequency;
                instance.vibrateHighFrequency = vibrateHighFrequency;
                instance.vibrateSeconds = vibrateSeconds;
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            RefreshSlots();
        }

        private void OnEnable() => InputSystem.onDeviceChange += OnDeviceChange;

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            ClearRestriction();
            InputSystem.ResetHaptics();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ---------- Slot assignment ----------

        // Number of controllers currently connected and holding a slot.
        public static int ConnectedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MaxSlots; i++) if (IsConnected(slots[i])) n++;
                return n;
            }
        }

        // The controller for player slot 0-3, or null when that slot uses the keyboard.
        public static Gamepad GetGamepad(int slot)
        {
            if (Instance == null || slot < 0 || slot >= MaxSlots) return null;
            return IsConnected(slots[slot]) ? slots[slot] : null;
        }

        // The player slot 0-3 a controller belongs to, or -1.
        public static int GetSlot(Gamepad pad)
        {
            if (pad == null) return -1;
            for (int i = 0; i < MaxSlots; i++) if (slots[i] == pad) return i;
            return -1;
        }

        // True when every player 1..playerCount has their own controller.
        public static bool HasEnoughControllers(int playerCount)
        {
            playerCount = Mathf.Clamp(playerCount, 0, MaxSlots);
            for (int i = 0; i < playerCount; i++) if (GetGamepad(i) == null) return false;
            return playerCount > 0;
        }

        // For each player slot, which keyboard layout (0-3) they use, or -1 when they have a controller.
        // A keyboard player keeps their own slot's layout: 0 = Player 1 (the Input System's keyboard bindings),
        // 1-3 = the backup layouts for Players 2-4.
        public static int[] GetKeyboardLayouts(int playerCount)
        {
            playerCount = Mathf.Clamp(playerCount, 0, MaxSlots);
            var pads = new Gamepad[playerCount];
            for (int i = 0; i < playerCount; i++) pads[i] = GetGamepad(i);
            return KeyboardLayoutsFor(pads, playerCount);
        }

        public static int[] KeyboardLayoutsFor(Gamepad[] pads, int playerCount)
        {
            var layouts = new int[Mathf.Max(0, playerCount)];
            for (int i = 0; i < layouts.Length; i++)
                layouts[i] = pads != null && i < pads.Length && pads[i] != null ? -1 : i;
            return layouts;
        }

        private static bool IsConnected(Gamepad pad) => pad != null && pad.added;

        private void RefreshSlots()
        {
            bool changed = false;
            // Drop pads that were fully removed from the Input System.
            for (int i = 0; i < MaxSlots; i++)
                if (slots[i] != null && !slots[i].added && !IsDisconnectedDevice(slots[i])) { slots[i] = null; changed = true; }
            foreach (Gamepad pad in Gamepad.all)
                if (GetSlot(pad) < 0) changed |= Place(pad);
            if (changed) NotifyChanged();
        }

        private static bool IsDisconnectedDevice(InputDevice device)
        {
            foreach (InputDevice d in InputSystem.disconnectedDevices) if (d == device) return true;
            return false;
        }

        // First empty slot; otherwise take over the slot of a controller that was unplugged.
        private static bool Place(Gamepad pad)
        {
            for (int i = 0; i < MaxSlots; i++)
                if (slots[i] == null) { slots[i] = pad; return true; }
            for (int i = 0; i < MaxSlots; i++)
                if (!IsConnected(slots[i])) { slots[i] = pad; return true; }
            return false; // More than four controllers: extras are ignored.
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad pad)) return;
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                    if (GetSlot(pad) < 0) Place(pad);
                    break;
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    // Keep the slot reserved: the same controller gets it back when plugged in again,
                    // and that player uses the keyboard until then. A new controller takes empty slots first.
                    break;
                default:
                    return;
            }
            restrictedSlot = -2; // Force the turn restriction to rebuild its device list.
            NotifyChanged();
        }

        private static void NotifyChanged() => AssignmentsChanged?.Invoke();

        // Swap two players' controllers (for a future lobby "swap" button or debugging).
        public static void SwapSlots(int a, int b)
        {
            if (a < 0 || b < 0 || a >= MaxSlots || b >= MaxSlots || a == b) return;
            (slots[a], slots[b]) = (slots[b], slots[a]);
            restrictedSlot = -2;
            NotifyChanged();
        }

        // ---------- Reassigning controllers ----------
        // A lobby calls BeginAssign(slot); the next controller that presses any button moves to that player slot
        // (trading places with whoever had it). ClearSlot puts a player on the keyboard, e.g. to drop a faulty controller.

        [SerializeField, Min(1f), Tooltip("Seconds a lobby waits for a button press before giving up on reassigning.")]
        private float assignTimeout = 10f;
        private static float assignStarted;

        // The player slot waiting for a controller button press, or -1.
        public static int ListeningSlot { get; private set; } = -1;

        public static void BeginAssign(int slot)
        {
            if (Instance == null || slot < 0 || slot >= MaxSlots) return;
            ListeningSlot = slot;
            assignStarted = Time.unscaledTime;
            NotifyChanged();
        }

        public static void CancelAssign()
        {
            if (ListeningSlot < 0) return;
            ListeningSlot = -1;
            NotifyChanged();
        }

        // Give this player slot a specific controller. Its old slot gets this slot's previous controller.
        public static void AssignToSlot(Gamepad pad, int slot)
        {
            if (pad == null || slot < 0 || slot >= MaxSlots) return;
            int previous = GetSlot(pad);
            if (previous == slot) return;
            Gamepad occupant = slots[slot];
            slots[slot] = pad;
            if (previous >= 0) slots[previous] = occupant;
            restrictedSlot = -2;
            NotifyChanged();
        }

        // Take the controller away from this player so they use the keyboard. The controller is left unassigned.
        public static void ClearSlot(int slot)
        {
            if (slot < 0 || slot >= MaxSlots) return;
            if (ListeningSlot == slot) ListeningSlot = -1;
            if (slots[slot] == null) { NotifyChanged(); return; }
            slots[slot] = null;
            restrictedSlot = -2;
            NotifyChanged();
        }

        private void ListenForAssignment()
        {
            if (ListeningSlot < 0) return;
            if (Time.unscaledTime - assignStarted > assignTimeout) { CancelAssign(); return; }
            foreach (Gamepad pad in Gamepad.all)
            {
                if (!AnyButtonPressed(pad)) continue;
                int slot = ListeningSlot;
                ListeningSlot = -1;
                if (GetSlot(pad) == slot) NotifyChanged();
                else AssignToSlot(pad, slot);
                Vibrate(slot); // Confirms which controller just joined this player.
                return;
            }
        }

        private static bool AnyButtonPressed(Gamepad pad)
        {
            if (pad == null || !pad.added || !pad.wasUpdatedThisFrame) return false;
            foreach (InputControl control in pad.allControls)
                if (control is UnityEngine.InputSystem.Controls.ButtonControl button && !button.synthetic && button.wasPressedThisFrame)
                    return true;
            return false;
        }

        // ---------- Vibrate ----------

        public static bool Vibrate(int slot)
        {
            var self = Instance;
            Gamepad pad = GetGamepad(slot);
            if (self == null || pad == null) return false;
            if (self.vibrations[slot] != null) self.StopCoroutine(self.vibrations[slot]);
            self.vibrations[slot] = self.StartCoroutine(self.VibrateRoutine(slot, pad));
            return true;
        }

        private IEnumerator VibrateRoutine(int slot, Gamepad pad)
        {
            pad.SetMotorSpeeds(vibrateLowFrequency, vibrateHighFrequency);
            // Unscaled so it still stops while the game is paused.
            yield return new WaitForSecondsRealtime(vibrateSeconds);
            if (pad.added) pad.SetMotorSpeeds(0f, 0f);
            vibrations[slot] = null;
        }

        // ---------- Turn-based modes (Race / Survival) ----------

        // The shared Player/Monster action maps only hear the current turn's controller (plus keyboard and mouse),
        // so another player's controller cannot move someone else's piece.
        private void Update()
        {
            ListenForAssignment();

            // Only apply restrictions when a match is running.
            if (!LocalMultiplayerSession.MatchIsRunning)
            {
                if (restrictedMaps.Count > 0)
                    ClearRestriction();

                return;
            }

            int slot = LocalMultiplayerSession.ActiveInputSlot;
            Gamepad pad = slot >= 0 ? GetGamepad(slot) : null;

            if (slot == restrictedSlot && pad == restrictedPad)
                return;

            ApplyRestriction(slot, pad);
        }

        private static void ApplyRestriction(int slot, Gamepad pad)
        {
            ClearRestriction();

            restrictedSlot = slot;
            restrictedPad = pad;

            // No restrictions when there is no active human player.
            if (slot < 0 || ConnectedCount == 0)
                return;

            var devices = new List<InputDevice>();

            if (pad != null)
            {
                // Controller player: only their controller can send gameplay input.
                devices.Add(pad);
            }
            else
            {
                // Keyboard player: allow keyboard and other non-gamepad devices.
                foreach (InputDevice device in InputSystem.devices)
                {
                    if (!(device is Gamepad))
                        devices.Add(device);
                }
            }

            foreach (InputActionMap map in LocalMultiplayerSession.TurnInputMaps())
            {
                if (map == null || restrictedMaps.Contains(map))
                    continue;

                map.devices = devices.ToArray();
                restrictedMaps.Add(map);
            }
        }

        private static void ClearRestriction()
        {
            foreach (InputActionMap map in restrictedMaps)
                if (map != null) map.devices = null;
            restrictedMaps.Clear();
            restrictedSlot = -1;
            restrictedPad = null;
        }

        // ---------- Lobby display helpers ----------
        // The game mode lobbies (Race/Survival arcade lobby, Hot Potato/King of the Hill setup) draw the Vibrate panel
        // in their own style using these.

        // What a slot shows in a lobby: the controller's name, "DISCONNECTED", or "KEYBOARD".
        public static string DescribeSlot(int slot)
        {
            if (slot < 0 || slot >= MaxSlots) return "";
            Gamepad pad = slots[slot];
            if (pad == null) return "KEYBOARD";
            if (!IsConnected(pad)) return "DISCONNECTED";
            return Shorten(pad.displayName).ToUpperInvariant();
        }

        // True for the short time a slot's controller is rumbling, so a lobby can highlight its button.
        public static bool IsVibrating(int slot) =>
            instance != null && slot >= 0 && slot < MaxSlots && instance.vibrations[slot] != null;

        private static string Shorten(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Controller";
            return value.Length > 16 ? value.Substring(0, 16) : value;
        }
    }
}
