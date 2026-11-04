using System;
using System.Collections.Generic;
using Escape4Now.GameModes;
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
        [SerializeField]
        private InputActionAsset inputActions;
        [SerializeField]
        private InputActionReference pause;
        [Header("Player Identity")]
        [SerializeField]
        private Color[] playerColors =
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
        public ArenaMap Map => map;
        public InputActionAsset InputActions => inputActions;
        public ArenaEventDirector Events => events;
        public Skin_Handler Skins => skins;
        public string Title => modeTitle;
        public string Instructions => instructions;

        public Color GetPlayerColor(int slot) => playerColors.Length > 0 ? playerColors[Mathf.Clamp(slot, 0, playerColors.Length - 1)] : Color.white;
        public readonly List<ArenaParticipant> Players = new List<ArenaParticipant>();
        public bool Playing { get; protected set; }
        public bool Paused { get; private set; }
        public string Result { get; private set; } = "";
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
        }

        protected virtual void OnDisable()
        {
            if (ownsPause && pause != null)
                pause.action.Disable();
            ownsPause = false;
        }

        protected virtual void Update()
        {
            if (Playing && pause != null && pause.action.WasPressedThisFrame())
                SetPaused(!Paused);
            if (!Playing || Paused)
                return;
            if (noticeRemaining > 0)
            {
                noticeRemaining -= Time.deltaTime;
                if (noticeRemaining <= 0)
                    Notice = "";
            }

            Tick(Time.deltaTime);
        }

        // Validate the saved references, create participants, then let the selected mode start its rules.
        public bool StartMatch(int count, string[] names, int[] gamepads, int[] skinIndices)
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

            var pads = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                if (gamepads[i] >= Gamepad.all.Count)
                {
                    Error = "Connect the selected gamepad or choose Keyboard.";
                    return false;
                }

                if (gamepads[i] >= 0 && !pads.Add(gamepads[i]))
                {
                    Error = "Give each gamepad to one player only.";
                    return false;
                }

                if (inputActions.FindActionMap("Player" + (i + 1)) == null)
                {
                    Error = "The arena input asset needs an action map for each player.";
                    return false;
                }
            }

            // Remove old event reservations before generating a replacement room.
            events?.Stop();
            ClearPlayers();
            if (!map.Generate(count, out var spawns))
            {
                Error = "No safe connected layout with separated spawns. Check Map Settings.";
                return false;
            }

            skins?.ResetToDefaults();
            skins?.ClearAll();
            for (int i = 0; i < count; i++)
            {
                var player = Instantiate(playerPrefab, transform);
                player.Initialize(this, i, names[i], playerColors[i], spawns[i], gamepads[i]);
                player.Eliminated += ParticipantEliminated;
                Players.Add(player);
                if (skins != null && skinIndices[i] >= 0)
                    skins.TryAssign(i, skinIndices[i]);
            }

            skins?.FillMissing();
            for (int i = 0; i < Players.Count; i++)
                skins?.ApplyTo(Players[i].Character, i);
            Playing = true;
            Paused = false;
            Result = "";
            Notice = "";
            events?.Begin(this);
            BeginRules();
            onMatchStarted?.Invoke();
            return true;
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
            Result = winner != null ? winner.name + winnerSuffix : noWinnerMessage;
            Playing = false;
            Paused = false;
            events?.Stop();
            onMatchEnded?.Invoke();
            return true;
        }

        public void Notify(string message, float duration = 3)
        {
            Notice = message;
            noticeRemaining = duration;
        }

        public void SetPaused(bool value)
        {
            if (Playing)
                Paused = value;
        }

        // Clear event changes before removing players or starting another room.
        public void ReturnToSetup()
        {
            Playing = false;
            Paused = false;
            Result = "";
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
