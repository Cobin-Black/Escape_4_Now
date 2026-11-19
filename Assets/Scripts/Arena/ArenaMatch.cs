using System;
using System.Collections.Generic;
using Escape4Now.Accounts;
using Escape4Now.GameModes;
using Escape4Now.Multiplayer;
using Escape4Now.Player;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Escape4Now.Arena
{
    // Shared match lifecycle only. Each mode keeps its own win and round rules in a separate controller.
    public abstract class ArenaMatch : GameMode
    {
        [Header("Scene References")]
        [SerializeField]
        private ArenaMap map;
        [SerializeField]
        private ArenaParticipant playerPrefab;
        [SerializeField]
        private Skin_Handler skins;
        [SerializeField]
        private ArenaEventDirector events;
        [Header("Input System")]
        [SerializeField, Tooltip("Shared-keyboard layouts (Player1..Player4 maps) used by players without a controller.")]
        private InputActionAsset inputActions;
        [SerializeField, Tooltip("Actions used by players with a controller (the Player map: Move, Attack, Potato Pass). Empty uses the project-wide Input System actions.")]
        private InputActionAsset gamepadActions;
        [SerializeField]
        private InputActionReference pause;
        [Header("Player Identity")]
        [SerializeField]
        private PlayerColorPalette playerPalette;
        [SerializeField, HideInInspector] private Color[] playerColors =
        {
            new Color(.15f, .7f, 1),
            new Color(1, .3f, .28f),
            new Color(.3f, .85f, .4f),
            new Color(1, .8f, .2f)
        };
        [Header("Presentation")]
        [SerializeField]
        private string modeTitle = "Arena";
        [SerializeField, TextArea]
        private string instructions = "Last player standing wins.";
        [SerializeField]
        private string winnerSuffix = " wins!";
        [SerializeField]
        private string noWinnerMessage = "No players remain.";
        [SerializeField]
        private UnityEvent onMatchStarted, onMatchEnded;
        private bool ownsPause;
        public bool Online { get; private set; }
        public bool Replica { get; private set; }
        public event System.Action<bool> OnlinePauseRequested;

        public void ConfigureOnline(bool replica)
        {
            Online = true;
            Replica = replica;
        }
        public ArenaMap Map => map;
        public InputActionAsset InputActions => inputActions;
        public InputActionAsset GamepadActions => gamepadActions != null ? gamepadActions : InputSystem.actions;
        // True when players were given controllers automatically, so plugging one in mid-match updates the inputs.
        private bool autoAssignedInput;
        public ArenaEventDirector Events => events;
        public Skin_Handler Skins => skins;
        public string Title => modeTitle;
        public string Instructions => instructions;

        public Color GetPlayerColor(int slot) => playerPalette != null ? playerPalette.GetColor(slot) : playerColors.Length > 0 ? playerColors[Mathf.Clamp(slot, 0, playerColors.Length - 1)] : Color.white;
        public readonly List<ArenaParticipant> Players = new List<ArenaParticipant>();
        public bool Playing { get; protected set; }
        public bool Paused { get; private set; }
        public string Result { get; private set; } = "";
        //Lobby slot of the last match's winner, or -1 when nobody won. Online guests get it from the host.
        public int WinnerSlot { get; private set; } = -1;
        //The leaderboard this mode's results are saved to.
        public string StatMode => Type == GameModeType.HotPotato ? AccountStats.HotPotato : AccountStats.FreeForAll;
        public string Error { get; private set; } = "";
        public string Notice { get; private set; } = "";

        private float noticeRemaining;
        public int Remaining
        {
            get
            {
                int n = 0;
                foreach (var p in Players)
                    if (p.Alive)
                        n++;
                return n;
            }
        }

        public virtual string MatchInfo => "";
        public abstract string Objective { get; }

        protected virtual void OnEnable()
        {
            if (pause != null && !pause.action.enabled)
            {
                pause.action.Enable();
                ownsPause = true;
            }
            ControllerHandling.AssignmentsChanged += ControllersChanged;
        }

        protected virtual void OnDisable()
        {
            ControllerHandling.AssignmentsChanged -= ControllersChanged;
            if (ownsPause && pause != null)
                pause.action.Disable();
            ownsPause = false;
        }

        protected virtual void Update()
        {
            if (Playing && pause != null && pause.action.WasPressedThisFrame())
                SetPaused(!Paused);
            if (!Playing || Paused || Replica)
                return;
            if (noticeRemaining > 0)
            {
                noticeRemaining -= Time.deltaTime;
                if (noticeRemaining <= 0)
                    Notice = "";
            }

            Tick(Time.deltaTime);
        }

        // Controllers come from ControllerHandling: each player slot uses its own controller,
        // and anyone without one gets a shared-keyboard layout.
        // bots marks the slots the computer plays. Bots never take a keyboard layout or controller.
        public bool StartMatch(int count, string[] names, int[] skinIndices, bool[] bots = null)
        {
            var pads = new Gamepad[Mathf.Clamp(count, 0, ControllerHandling.MaxSlots)];
            for (int i = 0; i < pads.Length; i++)
                pads[i] = IsBotSlot(bots, i) ? null : ControllerHandling.GetGamepad(i);
            bool started = StartMatch(count, names, pads, skinIndices, null, bots);
            autoAssignedInput = started;
            return started;
        }

        // Explicit assignment (used by editor checks): gamepads index Gamepad.all, -1 means keyboard.
        public bool StartMatch(int count, string[] names, int[] gamepads, int[] skinIndices, int? hostSeed = null, bool[] bots = null)
        {
            var pads = new Gamepad[Mathf.Max(0, count)];
            for (int i = 0; i < pads.Length && gamepads != null && i < gamepads.Length; i++)
            {
                if (gamepads[i] >= Gamepad.all.Count)
                {
                    Error = "Connect the selected gamepad or choose Keyboard.";
                    return false;
                }
                pads[i] = gamepads[i] >= 0 ? Gamepad.all[gamepads[i]] : null;
            }
            autoAssignedInput = false;
            return StartMatch(count, names, pads, skinIndices, hostSeed, bots);
        }

        // Validate the saved references, create participants, then let the selected mode start its rules.
        private static bool IsBotSlot(bool[] bots, int slot) => bots != null && slot < bots.Length && bots[slot];

        private bool StartMatch(int count, string[] names, Gamepad[] pads, int[] skinIndices, int? hostSeed = null, bool[] bots = null)
        {
            Error = "";
            if (map == null || map.Settings == null || map.Settings.material == null || playerPrefab == null || inputActions == null || pause == null)
            {
                Error = "Assign the map settings, material, player prefab, input actions and Pause action.";
                return false;
            }

            if (count < 2 || count > 4 || playerColors.Length < count)
            {
                Error = "Choose 2–4 players and assign their colors.";
                return false;
            }

            var used = new HashSet<Gamepad>();
            int[] layouts = KeyboardLayouts(pads, count, bots);
            for (int i = 0; i < count; i++)
            {
                if (IsBotSlot(bots, i))
                    continue;
                if (pads[i] != null && !used.Add(pads[i]))
                {
                    Error = "Give each gamepad to one player only.";
                    return false;
                }

                bool primary = pads[i] != null || layouts[i] == 0;
                if (primary && (GamepadActions == null || GamepadActions.FindActionMap("Player") == null))
                {
                    Error = "Player 1 and controller players need the Input System's Player action map.";
                    return false;
                }

                if (!primary && inputActions.FindActionMap("Player" + (layouts[i] + 1)) == null)
                {
                    Error = "The arena input asset needs an action map for each keyboard player.";
                    return false;
                }
            }

            // Remove old event reservations before generating a replacement room.
            events?.Stop();
            ClearPlayers();
            if (!map.Generate(count, out var spawns, hostSeed))
            {
                Error = "No safe connected layout with separated spawns. Check Map Settings.";
                return false;
            }

            skins?.ResetToDefaults();
            skins?.ClearAll();
            for (int i = 0; i < count; i++)
            {
                var player = Instantiate(playerPrefab, transform);
                bool bot = IsBotSlot(bots, i);
                player.Initialize(this, i, names[i], GetPlayerColor(i), spawns[i], bot);
                // Only the game that runs the rules (local play or the online host) drives bots.
                if (bot && !Replica)
                    player.gameObject.AddComponent<ArenaBotController>();
                player.Eliminated += ParticipantEliminated;
                Players.Add(player);
                if (skins != null && skinIndices[i] >= 0)
                    skins.TryAssign(i, skinIndices[i]);
            }

            // Online participants receive checked input from their room slot.
            if (!Online) ApplyInputs(pads);
            skins?.FillMissing();
            for (int i = 0; i < Players.Count; i++)
                skins?.ApplyTo(Players[i].Character, i);
            Playing = true;
            Paused = false;
            Result = "";
            WinnerSlot = -1;
            Notice = "";
            events?.Begin(this);
            if (!Replica) BeginRules();
            onMatchStarted?.Invoke();
            return true;
        }

        // Keyboard players keep their own slot's layout: Player 1 uses the Input System's keyboard bindings,
        // Players 2-4 use the backup Player2..Player4 maps. -1 means that slot has a controller.
        private static int[] KeyboardLayouts(Gamepad[] pads, int count, bool[] bots)
        {
            int[] layouts = ControllerHandling.KeyboardLayoutsFor(pads, count);
            // Bots do not use keys, so their layouts must not take keys away from a human.
            for (int i = 0; i < layouts.Length; i++)
                if (IsBotSlot(bots, i)) layouts[i] = -1;
            return layouts;
        }

        private void ApplyInputs(Gamepad[] pads)
        {
            var bots = new bool[Players.Count];
            for (int i = 0; i < bots.Length; i++) bots[i] = Players[i] != null && Players[i].IsBot;
            int[] layouts = KeyboardLayouts(pads, Players.Count, bots);
            // Keys used by the other keyboard players are switched off in Player 1's copy so nobody shares a key.
            var reserved = new HashSet<string>();
            for (int i = 1; i < layouts.Length; i++)
                if (layouts[i] >= 0)
                    ArenaLocalInput.CollectLayoutKeys(inputActions, layouts[i], reserved);
            for (int i = 0; i < Players.Count; i++)
                if (Players[i] != null && !Players[i].IsBot)
                    Players[i].ConfigureInput(pads[i], layouts[i], reserved);
        }

        // A controller was plugged in or unplugged: move each player to their current controller or keyboard layout.
        private void ControllersChanged()
        {
            if (!autoAssignedInput || Players.Count == 0)
                return;
            var pads = new Gamepad[Players.Count];
            for (int i = 0; i < pads.Length; i++)
                pads[i] = Players[i].IsBot ? null : ControllerHandling.GetGamepad(Players[i].Slot);
            ApplyInputs(pads);
        }

        // Use the same map and player footprints for normal movement and event knockback.
        public bool CanOccupy(ArenaParticipant player, Vector2 point)
        {
            float radius = map.Settings.playerRadius;
            if (map.Layout == null || !map.Layout.Fits(point, radius))
                return false;
            foreach (var other in Players)
                if (other != player && other.Alive && (other.Position - point).sqrMagnitude < radius * radius * 4)
                    return false;
            return true;
        }

        // Finish only after one or no participants remain; individual eliminations do not end a larger match.
        protected virtual void ParticipantEliminated(ArenaParticipant player)
        {
            CheckLastPlayer();
        }

        protected bool CheckLastPlayer()
        {
            if (Remaining > 1)
                return false;

            ArenaParticipant winner = null;

            foreach (var p in Players)
                if (p.Alive)
                    winner = p;
            EndMatch(winner != null
                ? winner.name + winnerSuffix
                : noWinnerMessage, winner);

            return true;
        }

        // End the match and report the winner.
        protected void EndMatch(string result, ArenaParticipant winner = null)
        {
            if (!Playing)
                return;

            Result = result;
            WinnerSlot = winner != null ? winner.Slot : -1;
            //Online, each player's own game saves their result through the room instead.
            if (!Online) ReportStats(winner);
            Playing = false;
            Paused = false;
            events?.Stop();
            onMatchEnded?.Invoke();
        }

        private void ReportStats(ArenaParticipant winner)
        {
            var stats = new List<MatchStat>();
            //Bots count toward the player total but never save stats, the same as Race.
            foreach (var p in Players)
                if (!p.IsBot)
                    stats.Add(new MatchStat(p.Slot, StatMode, p == winner, Players.Count));
            AccountStats.Report(stats);
        }

        public void Notify(string message, float duration = 3)
        {
            Notice = message;
            noticeRemaining = duration;
        }

        public void SetPaused(bool value)
        {
            if (Playing)
            {
                if (Online) OnlinePauseRequested?.Invoke(value);
                else Paused = value;
            }
        }

        public void ApplyOnlineState(bool playing, bool paused, string result, string notice)
        {
            Playing = playing;
            Paused = paused;
            Result = result ?? "";
            Notice = notice ?? "";
        }

        //Guests don't run the rules, so the host tells them who won.
        public void ApplyOnlineWinner(int slot) => WinnerSlot = slot;

        // Clear event changes before removing players or starting another room.
        public void ReturnToSetup()
        {
            Playing = false;
            Paused = false;
            Result = "";
            WinnerSlot = -1;
            events?.Stop();
            ClearPlayers();
        }

        private void ClearPlayers()
        {
            foreach (var p in Players)
                if (p != null)
                {
                    p.Eliminated -= ParticipantEliminated;
                    Destroy(p.gameObject);
                }

            Players.Clear();
        }

        protected virtual void BeginRules()
        {
        }

        protected abstract void Tick(float dt);
    }
}
