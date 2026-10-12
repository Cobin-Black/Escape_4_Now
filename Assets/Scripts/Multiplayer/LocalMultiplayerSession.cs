using System.Collections.Generic;
using Escape4Now.Map;
using Escape4Now.Player;
using Escape4Now.TurnSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Escape4Now.Multiplayer
{
    //Sets up two to four people sharing one computer and keyboard.
    [DefaultExecutionOrder(-10000)]
    public sealed class LocalMultiplayerSession : MonoBehaviour
    {
        [SerializeField] private bool showTemporaryUI = true;
        private static LocalMultiplayerSession instance;
        private readonly List<MonoBehaviour> heldScripts = new List<MonoBehaviour>();
        private IsometricMapTemplate map;
        private TurnSystemController turns;
        private PlayerCharacter sourcePlayer;
        private PlayerCharacter[] participants;
        private int playerCount = 2;
        private string status = "";
        private Vector2 setupScroll;
        private GUIStyle titleStyle, labelStyle, smallStyle, buttonStyle, selectedStyle, errorStyle;
        private Texture2D buttonTexture, hoverTexture, selectedTexture;
        public bool MatchStarted { get; private set; }
        public string Status => status;

        private static readonly Color[] PlayerColors =
        {
            new Color(0.15f, 0.7f, 1f), new Color(1f, 0.35f, 0.3f),
            new Color(0.3f, 0.85f, 0.4f), new Color(1f, 0.8f, 0.2f)
        };

        //Turn controls are available once the shared-device match has started.
        public static bool AllowTurnButtons => instance == null || instance.MatchStarted;

        //Clears the scene reference when another Play Mode run begins.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { instance = null; }

        //Waits for a player count without disabling a teammate's menu canvas.
        private void Awake()
        {
            instance = this;
            map = FindFirstObjectByType<IsometricMapTemplate>();
            turns = FindFirstObjectByType<TurnSystemController>();
            sourcePlayer = FindFirstObjectByType<PlayerCharacter>();
            foreach (MonoBehaviour script in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                bool gameplay = script is IsometricMapTemplate || script is PlayerCharacter
                    || script is TurnSystemController || script is Escape4Now.Items.PlayerInventory
                    || script is MapEventController || script is Escape4Now.Items.ItemSpawner
                    || script is Escape4Now.Obstacles.ObstacleSpawner || script is Escape4Now.UI.ItemPickupBar;
                if (!gameplay || !script.enabled) continue;
                heldScripts.Add(script);
                script.enabled = false;
            }
            if (sourcePlayer != null)
                foreach (Renderer renderer in sourcePlayer.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        }

        //A replacement UI can connect its player-count selector to this method.
        public void SetPlayerCount(int count) { playerCount = Mathf.Clamp(count, 2, 4); }

        //A replacement UI can turn off the placeholder panel without changing gameplay.
        public void SetTemporaryUIVisible(bool visible) { showTemporaryUI = visible; }

        //A replacement UI can connect its Start button to this method.
        public void StartLocalMatch() { TryStartLocalMatch(playerCount); }

        //Places separate players on valid room floors before any other spawners start.
        public bool TryStartLocalMatch(int count)
        {
            if (MatchStarted) return false;
            if (count < 2 || count > 4)
            { status = "Choose two to four players."; return false; }
            if (map == null || turns == null || sourcePlayer == null || InputSystem.actions == null)
            { status = "The scene needs a map, player, turn system, and input actions."; return false; }
            map.PrepareLayout();
            var cells = new List<Vector2Int>();
            var reserved = new HashSet<Vector2Int>();
            foreach (SchoolRoom room in map.Rooms)
            {
                var center = new Vector2Int(room.CenterX, room.CenterY);
                if (map.IsWalkable(center) && !map.IsOccupied(center) && reserved.Add(center)) cells.Add(center);
                if (cells.Count == count) break;
            }
            while (cells.Count < count && map.TryGetRandomFloor(out Vector2Int cell, reserved))
            { cells.Add(cell); reserved.Add(cell); }
            if (cells.Count < count)
            { status = "There are not enough open spaces for the players."; return false; }

            participants = new PlayerCharacter[count];
            participants[0] = sourcePlayer;
            sourcePlayer.gameObject.SetActive(false);
            for (int i = 1; i < count; i++) participants[i] = Instantiate(sourcePlayer);
            for (int i = 0; i < count; i++)
            {
                PlayerCharacter player = participants[i];
                player.ConfigureParticipant(map, turns, cells[i], i + 1, PlayerColors[i]);
                player.enabled = true;
                if (player.Inventory != null) player.Inventory.enabled = true;
                foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
            }
            turns.SetPlayers(participants);
            foreach (PlayerCharacter player in participants) player.gameObject.SetActive(true);
            foreach (MonoBehaviour script in heldScripts) if (script != null) script.enabled = true;
            MatchStarted = true;
            status = "";
            Time.timeScale = 1f;
            return true;
        }

        //Temporary controls can be replaced by the team's main menu later.
        private void OnGUI()
        {
            if (!showTemporaryUI) return;
            PrepareSetupStyles();
            if (MatchStarted)
            {
                if (GUI.Button(new Rect(Screen.width - 156, 16, 140, 38), "Leave Match", buttonStyle)) ReturnToSetup();
                return;
            }
            DrawSetupColor(new Rect(0, 0, Screen.width, Screen.height), new Color(0.075f, 0.085f, 0.08f));
            float width = Mathf.Max(180, Mathf.Min(480, Screen.width - 32));
            float height = Mathf.Max(120, Mathf.Min(530, Screen.height - 32));
            Rect area = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
            setupScroll = GUI.BeginScrollView(area, setupScroll, new Rect(0, 0, width - 20, 520));
            float contentWidth = width - 24;
            GUI.Label(new Rect(0, 0, contentWidth, 44), "Escape 4 Now", titleStyle);
            DrawSetupColor(new Rect(0, 90, contentWidth, 1), new Color(0.23f, 0.27f, 0.24f));
            GUI.Label(new Rect(0, 108, contentWidth, 24), "Players", labelStyle);
            float segmentWidth = (contentWidth - 16) / 3;
            for (int count = 2; count <= 4; count++)
            {
                Rect choice = new Rect((count - 2) * (segmentWidth + 8), 142, segmentWidth, 44);
                if (GUI.Button(choice, count.ToString(), playerCount == count ? selectedStyle : buttonStyle))
                    SetPlayerCount(count);
            }
            for (int i = 0; i < 4; i++)
            {
                float y = 212 + i * 42;
                bool playing = i < playerCount;
                Color color = playing ? PlayerColors[i] : new Color(0.24f, 0.27f, 0.25f);
                DrawSetupColor(new Rect(8, y + 4, 14, 26), color);
                GUI.Label(new Rect(40, y, contentWidth - 170, 32), "Player " + (i + 1), playing ? labelStyle : smallStyle);
                if (!playing) GUI.Label(new Rect(contentWidth - 110, y + 4, 110, 26), "Not playing", smallStyle);
                DrawSetupColor(new Rect(0, y + 38, contentWidth, 1), new Color(0.16f, 0.19f, 0.17f));
            }
            if (GUI.Button(new Rect(0, 402, contentWidth, 48), "Start Match", selectedStyle)) StartLocalMatch();
            GUI.Label(new Rect(0, 464, contentWidth, 56), status, errorStyle);
            GUI.EndScrollView();
        }

        //Creates the temporary menu styles once, instead of allocating them every frame.
        private void PrepareSetupStyles()
        {
            if (titleStyle != null) return;
            buttonTexture = MakeSetupTexture(new Color(0.15f, 0.18f, 0.16f));
            hoverTexture = MakeSetupTexture(new Color(0.27f, 0.36f, 0.3f));
            selectedTexture = MakeSetupTexture(new Color(0.36f, 0.7f, 0.48f));
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = new Color(0.95f, 0.97f, 0.95f);
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            labelStyle.normal.textColor = new Color(0.9f, 0.93f, 0.9f);
            smallStyle = new GUIStyle(labelStyle) { fontSize = 14 };
            smallStyle.normal.textColor = new Color(0.6f, 0.67f, 0.62f);
            errorStyle = new GUIStyle(smallStyle) { wordWrap = true };
            errorStyle.normal.textColor = new Color(1f, 0.62f, 0.53f);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 17, alignment = TextAnchor.MiddleCenter, border = new RectOffset() };
            buttonStyle.normal.background = buttonTexture;
            buttonStyle.hover.background = hoverTexture;
            buttonStyle.active.background = selectedTexture;
            buttonStyle.focused.background = hoverTexture;
            buttonStyle.normal.textColor = buttonStyle.hover.textColor = buttonStyle.focused.textColor = Color.white;
            buttonStyle.active.textColor = new Color(0.04f, 0.1f, 0.06f);
            selectedStyle = new GUIStyle(buttonStyle) { fontStyle = FontStyle.Bold };
            selectedStyle.normal.background = selectedTexture;
            selectedStyle.normal.textColor = new Color(0.04f, 0.1f, 0.06f);
            selectedStyle.hover.background = selectedTexture;
            selectedStyle.hover.textColor = selectedStyle.normal.textColor;
        }

        //Makes a small reusable button background.
        private Texture2D MakeSetupTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        //Draws a flat color while keeping other GUI colors unchanged.
        private void DrawSetupColor(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        //A new match reloads the scene and gets a new school floor plan.
        public void ReturnToSetup()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameObject.scene.name);
        }

        //Clears only this session's static reference when the scene closes.
        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (buttonTexture != null) Destroy(buttonTexture);
            if (hoverTexture != null) Destroy(hoverTexture);
            if (selectedTexture != null) Destroy(selectedTexture);
        }
    }
}
