using System.Collections.Generic;
using System.IO;
using Escape4Now.Arena;
using Escape4Now.Multiplayer.RelayTesting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;
using Kit = Escape4Now.Menus.ArcadeMenuKit;
using RaceStyle = Escape4Now.Menus.ArcadeLobbyScreen;

// Builds editable lobby screens that copy the Race and Survival lobby (ArcadeLobbyScreen):
// the same 1280x720 layout, panel sizes, borders, block shadows, fonts and colors.
public static class OnlineLobbyAuthoring
{
    private static Color Ink => Kit.Ink;
    private static Color Paper => new Color(.663f, .863f, 1f);
    private static Color Navy => Kit.Navy;
    private static Color Teal => Kit.Teal;
    private static Color Red => Kit.Red;
    private static Color Muted => RaceStyle.SoftBlue;
    private static Font heading, mono, bold;

    // Layout taken from ArcadeLobbyScreen so every lobby lines up with Race and Survival.
    private const float ModeX = 56, PanelY = 118, PanelH = 470, ModeW = 290;
    private const float PlayersX = 378, PlayersW = 846;
    private const float Inner = 78, InnerW = 246;
    private const float CardTop = 192, CardW = 190, CardH = 374, CardStep = 204, CardLeft = 400;
    private const float BarY = 612, BarH = 72, BarRight = 1224;
    // Controller panel to the right of WHO'S PLAYING, the same size and gap as Cobin's panel in ArcadeLobbyScreen.
    // The layout grows by its width plus the gap, and ResponsivePanel shrinks it to fit narrower screens.
    private const float ControllerGap = 24, ControllerPanelW = 176;
    private const float LayoutW = 1280 + ControllerGap + ControllerPanelW;
    private const float RightEdge = LayoutW - 56;

    [MenuItem("Tools/Escape 4 Now/Relay Test/Style Online Lobby")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (string scene in new[] { "OnlineRace", "OnlineSurvival", "OnlineFreeForAll", "OnlineHotPotato" })
        {
            EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
            ApplyToCurrentScene();
        }
    }

    public static void ApplyAndCheck()
    {
        Apply();
        RelayFoundationChecks.Run();
    }

    public static void ApplyToCurrentScene()
    {
        var session = Object.FindFirstObjectByType<OnlineRaceSession>();
        var mode = session.GetComponent<OnlineModeMatch>();
        var arena = Object.FindFirstObjectByType<ArenaMatch>();

        LoadFonts();
        string modeTitle = mode != null ? mode.Title.ToUpperInvariant() : "RACE";
        string objective = arena != null ? arena.Instructions : mode != null ? mode.Objective
            : "One key is hidden in a locker. Find it, battle for it, and carry it through the EXIT first.";
        var previous = Object.FindFirstObjectByType<OnlineRaceLobbyView>(FindObjectsInactive.Include);
        if (previous != null) Object.DestroyImmediate(previous.gameObject);

        var canvas = new GameObject("Online Lobby", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder = 100;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var lobby = Rect(canvas.transform, "Lobby", 0, 0, 1280, 720);
        Stretch(lobby, Vector2.zero, Vector2.zero);
        var screen = BuildBackdrop(lobby);
        FitToScreen(lobby, screen);
        var controls = screen.gameObject.AddComponent<CanvasGroup>();
        var back = MenuButton(screen, "MAIN MENU", "ESC · MAIN MENU", RightEdge - 190, 40, 190, 48);
        var openDiagnostics = MenuButton(screen, "CONNECTION SETTINGS", "CONNECTION SETTINGS", RightEdge - 190 - 16 - 254, 40, 254, 48);

        BuildModePanel(screen, modeTitle, "ONLINE MODE", mode == null || arena is KingOfTheHillController,
            objective, "Mode Objective", out _, out _);
        PlayersPanel(screen);

        // Host or join: room size chips on the left, the two choices on the right.
        var setup = Rect(screen, "Host or Join", 0, 0, 1280, 720);
        Text(setup, "Room Size Label", "HOW MANY PLAYERS?", 12, mono, Inner, 310, InnerW, 16, Muted);
        var sizes = Rect(setup, "Room Size", Inner, 334, InnerW, 138);
        var sizeGroup = sizes.gameObject.AddComponent<ToggleGroup>();
        Chip(sizes, "Solo", "SOLO", "not online", 0, 0, false);
        var roomSizes = new Toggle[3];
        for (int i = 0; i < roomSizes.Length; i++)
        {
            int count = i + 2;
            var tile = Chip(sizes, "Room Size " + count, count.ToString(), "players", ((count - 1) % 2) * 128, ((count - 1) / 2) * 74, true);
            var toggle = tile.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = tile.Find("Face").GetComponent<Image>();
            toggle.isOn = i == 0;
            toggle.group = sizeGroup;
            roomSizes[i] = toggle;
            Escape4Now.UI.ArcadeChip.Paint(tile, toggle.isOn);
        }
        Text(setup, "Setup Tip", "Everyone plays from their own computer. Share the room code so friends can join you.",
            12, mono, Inner, 500, InnerW, 66, RaceStyle.Tape).alignment = TextAnchor.UpperLeft;

        Text(setup, "Setup Heading", "PRIVATE ROOM", 32, heading, 400, 136, 500, 40, RaceStyle.PaleBlue);
        float roomCardW = (PlayersW - 44 - 16) / 2;
        float createX = PlayersX + 22, joinX = createX + roomCardW + 16;
        Card(setup, "Create Room Card", createX, CardTop, roomCardW, CardH - 2);
        Text(setup, "Create Title", "CREATE ROOM", 22, heading, createX + 22, CardTop + 22, roomCardW - 44, 34, Color.white);
        Text(setup, "Create Help", "Choose a room size on the left, then share your code with friends.", 13, mono,
            createX + 22, CardTop + 66, roomCardW - 44, 70, Kit.Soft).alignment = TextAnchor.UpperLeft;
        var host = Button(setup, "HOST ROOM", createX + 22, CardTop + CardH - 2 - 30 - 62, roomCardW - 44 - 6, 62, true, 20);

        Card(setup, "Join Room Card", joinX, CardTop, roomCardW, CardH - 2);
        Text(setup, "Join Title", "JOIN ROOM", 22, heading, joinX + 22, CardTop + 22, roomCardW - 44, 34, Color.white);
        Text(setup, "Join Help", "Enter the code from your host.", 13, mono, joinX + 22, CardTop + 66, roomCardW - 44, 40, Kit.Soft)
            .alignment = TextAnchor.UpperLeft;
        Text(setup, "Join Code Label", "ROOM CODE", 12, mono, joinX + 22, CardTop + 150, roomCardW - 44, 16, Muted);
        var joinCode = Input(setup, "ROOM CODE", joinX + 22, CardTop + 170, roomCardW - 44, 44, mono, 16);
        joinCode.characterLimit = 12;
        joinCode.characterValidation = InputField.CharacterValidation.Alphanumeric;
        var join = Button(setup, "JOIN ROOM", joinX + 22, CardTop + CardH - 2 - 30 - 62, roomCardW - 44 - 6, 62, false, 20);
        var signIn = Button(setup, "LOG IN", RightEdge - 220, BarY, 220, BarH, true, 26);
        signIn.gameObject.SetActive(false);

        // Connected room: code and host options on the left, lockers on the right.
        var room = Rect(screen, "Connected Room", 0, 0, 1280, 720);
        Text(room, "Code Heading", "ROOM CODE", 12, mono, Inner, 310, InnerW, 16, Muted);
        var code = Text(room, "Room Code", "", 26, heading, Inner, 332, 140, 40, Teal);
        var copy = Button(room, "COPY", Inner + 148, 336, InnerW - 148 - 6, 34, false, 13);
        var profile = Rect(room, "Your Profile", Inner, 390, InnerW, 88);
        Text(profile, "Account Heading", "SIGNED IN AS", 11, mono, 0, 0, InnerW, 16, Muted);
        var accountName = Text(profile, "Account Name", "", 17, heading, 0, 18, InnerW, 26, Color.white);
        var skinLabel = Text(profile, "Selected Skin", "", 11, mono, 0, 52, 112, 32, Color.white);
        var nextSkin = Button(profile, "CHANGE SKIN", 118, 52, 122, 32, false, 11);
        // Host options sit as full-width rows at the bottom of the panel, in the same place in every mode.
        // FILL BOTS is always the bottom row; the Survival-only monster option sits above it.
        float hostButtonW = InnerW - 6;
        var monsterRole = Button(room, "AI MONSTER", Inner, 476, hostButtonW, 40, false, 14);
        monsterRole.gameObject.SetActive(false);
        var fillBots = Button(room, "FILL BOTS / OFF", Inner, 526, hostButtonW, 40, false, 14);
        fillBots.gameObject.SetActive(false);
        Text(room, "Players Heading", "WHO'S PLAYING?", 32, heading, 400, 136, 475, 40, RaceStyle.PaleBlue);
        var summary = Text(room, "Connected Count", "", 13, mono, 822, 144, 380, 20, Muted);
        summary.alignment = TextAnchor.MiddleRight;
        var rowObjects = new List<Object[]>();
        for (int slot = 0; slot < 4; slot++)
        {
            Color color = session.SlotColor(slot);
            rowObjects.Add(LockerCard(room, color, slot));
        }

        // Bottom bar: status on the left like the Race hint, room buttons on the right.
        var status = Text(screen, "Player Status", "", 14, bold, 56, BarY, 520, BarH, Navy);
        const float actionsWidth = 618;
        var actions = Rect(screen, "Room Actions", RightEdge - actionsWidth - 6, BarY, actionsWidth, BarH);
        var layout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 18;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        // START gets the widest button, like the big start button in the local lobbies.
        float sideWidth = 166, startWidth = actionsWidth - sideWidth * 2 - layout.spacing * 2;
        var ready = Button(actions, "READY", 0, 0, sideWidth, BarH, false, 20);
        var start = Button(actions, mode == null ? "START RACE" : "START MATCH", 0, 0, startWidth, BarH, true, 24,
            "START " + modeTitle);
        var leave = Button(actions, "LEAVE ROOM", 0, 0, sideWidth, BarH, false, 20);
        foreach (var (button, width) in new[] { (ready, sideWidth), (start, startWidth), (leave, sideWidth) })
            button.gameObject.AddComponent<LayoutElement>().preferredWidth = width;

        // Connection details are available on demand, rather than filling the normal lobby.
        var diagnostics = Box(screen, "Connection Settings Overlay", 0, 0, LayoutW, 720, new Color(Ink.r, Ink.g, Ink.b, .7f));
        diagnostics.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
        var modal = Rect(diagnostics, "Settings Card", (LayoutW - 880) / 2, 145, 880, 440);
        Frame(modal, "Settings Frame", 0, 0, 880, 440, RaceStyle.PanelNavy, Color.white, 4, Teal, 8);
        Text(modal, "Settings Heading", "CONNECTION SETTINGS", 26, heading, 28, 22, 680, 36, RaceStyle.PaleBlue);
        var close = Button(modal, "CLOSE", 756, 22, 96, 36, false, 14);
        Text(modal, "Protocol Heading", "CONNECTION PROTOCOL", 12, mono, 28, 80, 824, 16, Muted);
        var protocol = Dropdown(modal, "Protocol", 28, 102, 824, 44, new[] { "DTLS - encrypted UDP", "WSS - secure WebSocket", "UDP - diagnostic only" });
        var diagnosticStatus = Text(modal, "Actual Connection Details", "", 12, mono, 28, 160, 824, 132, Color.white);
        diagnosticStatus.alignment = TextAnchor.UpperLeft;
        var networkLabel = Input(modal, "Network label for report (Home / Hotspot / School)", 28, 306, 600, 44, mono, 15);
        var export = Button(modal, "SAVE REPORT", 650, 306, 196, 44, true, 15);
        var report = Text(modal, "Saved Report Path", "", 11, mono, 28, 364, 824, 56, Muted);
        report.alignment = TextAnchor.UpperLeft;
        diagnostics.gameObject.SetActive(false);

        var controllerPanel = ControllerPanel(screen, 1);
        var view = canvas.AddComponent<OnlineRaceLobbyView>();
        const string cancelPath = "Assets/Settings/Menus/Menu Cancel.asset";
        var cancel = AssetDatabase.LoadAssetAtPath<InputActionReference>(cancelPath);
        if (cancel == null)
        {
            Directory.CreateDirectory("Assets/Settings/Menus");
            AssetDatabase.Refresh();
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
            cancel = InputActionReference.Create(input.FindAction("UI/Cancel", true));
            AssetDatabase.CreateAsset(cancel, cancelPath);
        }
        Assign(view, "session", session, "panel", lobby.gameObject, "setup", setup.gameObject,
            "room", room.gameObject, "actions", actions.gameObject, "diagnosticsPanel", diagnostics.gameObject,
            "mainControls", controls, "cancel", cancel,
            "joinCode", joinCode, "host", host, "join", join, "back", back,
            "ready", ready, "start", start, "leave", leave, "copy", copy,
            "readyLabel", ready.transform.Find("Label").GetComponent<Text>(), "code", code, "status", status, "summary", summary,
            "raceProfile", profile.gameObject, "accountName", accountName, "signIn", signIn, "nextSkin", nextSkin, "skinLabel", skinLabel,
            "monsterRole", monsterRole, "fillBots", fillBots,
            "openDiagnostics", openDiagnostics, "closeDiagnostics", close, "protocol", protocol,
            "networkLabel", networkLabel, "export", export, "diagnosticStatus", diagnosticStatus, "reportPath", report,
            "controllerPanel", controllerPanel);
        var serialized = new SerializedObject(view);
        var sizeToggles = serialized.FindProperty("roomSizes");
        sizeToggles.arraySize = roomSizes.Length;
        for (int i = 0; i < roomSizes.Length; i++) sizeToggles.GetArrayElementAtIndex(i).objectReferenceValue = roomSizes[i];
        serialized.FindProperty("normalColor").colorValue = Navy;
        serialized.FindProperty("errorColor").colorValue = Red;
        // Ready badges use the lobby's teal and the dark edge blue, like the PLAYER/BOT switch.
        serialized.FindProperty("waitingColor").colorValue = RaceStyle.EdgeBlue;
        serialized.FindProperty("readyTextColor").colorValue = Ink;
        serialized.FindProperty("waitingTextColor").colorValue = Color.white;
        var rows = serialized.FindProperty("rows");
        rows.arraySize = rowObjects.Count;
        string[] fields = { "root", "portrait", "name", "role", "state", "badge" };
        for (int i = 0; i < rowObjects.Count; i++)
            for (int j = 0; j < fields.Length; j++) rows.GetArrayElementAtIndex(i).FindPropertyRelative(fields[j]).objectReferenceValue = rowObjects[i][j];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        ShowRoomPreview(screen, setup, room, actions, profile, rowObjects, session);
        EditorSceneManager.SaveScene(session.gameObject.scene);
        Debug.Log("ONLINE LOBBY STYLED: matches the Race and Survival lobby layout.");
    }

    internal static ArenaLobbyView BuildSameDeviceLobby(RectTransform parent, ArenaMatch match, ArenaInterfaceStyle style)
    {
        LoadFonts();
        var lobby = Rect(parent, "Match Setup", 0, 0, 1280, 720);
        Stretch(lobby, Vector2.zero, Vector2.zero);
        var screen = BuildBackdrop(lobby);
        var responsive = lobby.gameObject.AddComponent<Escape4Now.UI.ResponsivePanel>();
        Assign(responsive, "viewport", lobby, "content", screen);
        var responsiveFields = new SerializedObject(responsive);
        responsiveFields.FindProperty("edgePadding").vector2Value = Vector2.zero;
        responsiveFields.ApplyModifiedPropertiesWithoutUndo();
        var view = lobby.gameObject.AddComponent<ArenaLobbyView>();
        var back = MenuButton(screen, "MAIN MENU", "ESC · MAIN MENU", RightEdge - 190, 40, 190, 48);

        string modeTitle = match.Title.ToUpperInvariant();
        BuildModePanel(screen, modeTitle, "GAME MODE", match is KingOfTheHillController, match.Instructions, "Instructions",
            out var title, out var instructions);
        var countPanel = Rect(screen, "Player Count", Inner, 310, InnerW, 162);
        Text(countPanel, "Count Heading", "HOW MANY PLAYERS?", 12, mono, 0, 0, InnerW, 16, Muted);
        var counts = Rect(countPanel, "Count Buttons", 0, 24, InnerW, 138);
        // Same 2x2 grid as Race. This mode needs at least two players, so SOLO is shown greyed out.
        Chip(counts, "Solo", "SOLO", "just you", 0, 0, false);
        var playerCounts = new Button[3];
        for (int n = 2; n <= 4; n++)
        {
            var chip = Chip(counts, n + " PLAYERS", n.ToString(), "players", ((n - 1) % 2) * 128, ((n - 1) / 2) * 74, true);
            var button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = chip.Find("Face").GetComponent<Image>();
            playerCounts[n - 2] = button;
            Escape4Now.UI.ArcadeChip.Paint(chip, n == 2);
        }
        Text(screen, "Mode Tip", "Controllers are assigned automatically. Use the panel on the right to change them or test vibration.",
            12, mono, Inner, 500, InnerW, 66, RaceStyle.Tape).alignment = TextAnchor.UpperLeft;

        PlayersPanel(screen);
        Text(screen, "Players Heading", "WHO'S PLAYING?", 32, heading, 400, 136, 475, 40, RaceStyle.PaleBlue);
        var summary = Text(screen, "Player Summary", "", 13, mono, 822, 144, 380, 20, Muted);
        summary.alignment = TextAnchor.MiddleRight;
        var rowsParent = Rect(screen, "Player Rows", 0, 0, 1280, 588);
        var rows = new GameObject[4];
        var names = new InputField[4];
        var controls = new Dropdown[4];
        var skins = new Dropdown[4];
        var portraits = new Image[4];
        var strips = new Image[4];
        var previous = new Button[4];
        var next = new Button[4];
        var empty = new Button[4];
        var skinLabels = new Text[4];
        var humanRoles = new Button[4];
        var botRoles = new Button[4];
        var roleBadges = new Text[4];
        for (int i = 0; i < rows.Length; i++)
        {
            var root = Rect(rowsParent, "Player " + (i + 1), CardLeft + i * CardStep, CardTop, CardW, CardH);
            rows[i] = root.gameObject;
            Color color = match.GetPlayerColor(i);
            portraits[i] = BuildLockerFront(root, color, i, out strips[i], out var role);
            role.text = "PLAYER";
            roleBadges[i] = role;
            previous[i] = ArrowButton(root, "<", 18, 107);
            next[i] = ArrowButton(root, ">", CardW - 18 - 30, 107);
            skinLabels[i] = Text(root, "Skin Number", "", 11, mono, 14, 178, 162, 16, Muted);
            skinLabels[i].alignment = TextAnchor.MiddleCenter;
            skins[i] = Dropdown(root, "Skin Selection", 14, 176, 162, 38, System.Array.Empty<string>());
            skins[i].gameObject.SetActive(false);
            names[i] = Input(root, "PLAYER NAME", 14, 212, 162, 42, heading, 15);
            names[i].text = "Player " + (i + 1);
            names[i].characterLimit = 16;
            // Leaves room on the right for the account chain button.
            names[i].textComponent.rectTransform.sizeDelta = new Vector2(112, 42);
            Text(root, "Controls Label", "CONTROLS", 10, mono, 14, 264, 162, 14, Muted);
            controls[i] = Dropdown(root, "Controls", 14, 282, 162, 38, System.Array.Empty<string>());
            controls[i].captionText.fontSize = 11;
            // PLAYER / BOT switch, the same two-part control the Race lobby cards use.
            var roleSwitch = Box(root, "Role Switch", 14, 328, 162, 34, RaceStyle.DashBlue);
            humanRoles[i] = RoleHalf(roleSwitch, "PLAYER", 2, color, true);
            botRoles[i] = RoleHalf(roleSwitch, "BOT", 81, color, false);
            PlayerPresentationAuthoring.ConfigureLocker(root, color);
            Tilt(root, i);
        }

        var placeholders = Rect(screen, "Available Lockers", 0, 0, 1280, 588);
        for (int i = 2; i < rows.Length; i++)
        {
            var open = Rect(placeholders, "Add Player " + (i + 1), CardLeft + i * CardStep, CardTop, CardW, CardH);
            OpenSlot(open, "ADD P" + (i + 1));
            open.gameObject.AddComponent<Image>().color = Color.clear;
            empty[i] = open.gameObject.AddComponent<Button>();
            empty[i].targetGraphic = open.GetComponent<Image>();
            Tilt(open, i);
        }

        string startLabel = "START " + modeTitle;
        float startWidth = Mathf.Min(560, Measure(heading, startLabel, 30) + 80);
        var start = Button(screen, "START MATCH", RightEdge - startWidth, BarY, startWidth, BarH, true, 30, startLabel);
        var hint = Text(screen, "Setup Hint", "Use < and > on each card to pick a skin. Press START when everyone is ready.",
            14, bold, 56, BarY, RightEdge - startWidth - 24 - 56, BarH, Navy);
        var error = Text(screen, "Setup Error", "", 14, bold, 56, BarY, RightEdge - startWidth - 24 - 56, BarH, Red);
        error.gameObject.SetActive(false);
        var controllerPanel = ControllerPanel(screen, Escape4Now.Multiplayer.ControllerHandling.MaxSlots);
        Assign(view, "style", style, "title", title, "instructions", instructions, "error", error, "hint", hint, "start", start,
            "playerSummary", summary, "back", back, "controllerPanel", controllerPanel);
        var viewStyle = new SerializedObject(view);
        viewStyle.FindProperty("selectedCountColor").colorValue = Teal;
        viewStyle.FindProperty("keyboardFormat").stringValue = "{0}";
        viewStyle.FindProperty("tagHints").GetArrayElementAtIndex(1).stringValue = "Arrows + RCtrl";
        // The saved panel below replaces the runtime-built controller panel and its CONTROLLERS button.
        viewStyle.FindProperty("showControllerPanel").boolValue = false;
        viewStyle.ApplyModifiedPropertiesWithoutUndo();
        SetArray(view, "playerCounts", playerCounts);
        SetArray(view, "rows", rows);
        SetArray(view, "names", names);
        SetArray(view, "controls", controls);
        SetArray(view, "skins", skins);
        SetArray(view, "portraits", portraits);
        SetArray(view, "identityStrips", strips);
        SetArray(view, "previousSkins", previous);
        SetArray(view, "nextSkins", next);
        SetArray(view, "skinLabels", skinLabels);
        SetArray(view, "openSlots", empty);
        SetArray(view, "humanRoles", humanRoles);
        SetArray(view, "botRoles", botRoles);
        SetArray(view, "roleBadges", roleBadges);

        // How the lobby looks in the editor before pressing Play: two players with their skins, the other two
        // lockers open, and the wide layout already shrunk to fit. ArenaLobbyView takes over once the game runs.
        screen.localScale = Vector3.one * (1280f / LayoutW);
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i].SetActive(i < 2);
            var skin = match.Skins != null && i < match.Skins.Count ? match.Skins.GetSkin(i) : null;
            portraits[i].sprite = skin != null ? skin.front : null;
            portraits[i].enabled = portraits[i].sprite != null;
        }
        return view;
    }

    // One half of the PLAYER / BOT switch. ArenaLobbyView repaints both halves when the choice changes.
    private static Button RoleHalf(RectTransform parent, string label, float x, Color color, bool selected)
    {
        var face = Box(parent, label + " Role", x, 2, 79, 30, selected ? color : RaceStyle.DeepNavy);
        var text = Text(face, "Label", label, 12, heading, 0, 0, 79, 30, selected ? Ink : RaceStyle.OffText);
        text.alignment = TextAnchor.MiddleCenter;
        var button = face.gameObject.AddComponent<Button>();
        button.targetGraphic = face.GetComponent<Image>();
        button.transition = Selectable.Transition.None;
        return button;
    }

    private static void LoadFonts()
    {
        heading = Resources.Load<Font>("Fonts/Bungee-Regular");
        mono = Resources.Load<Font>("Fonts/SpaceMono-Regular");
        bold = Resources.Load<Font>("Fonts/SpaceMono-Bold");
    }

    // Light blue grid paper, scrolling hazard tape and the ESCAPE · NOW logo.
    private static RectTransform BuildBackdrop(RectTransform lobby)
    {
        // Like the Race lobby, the paper, grid and tape fill the whole window, while the layout shrinks to fit inside it.
        var backdrop = Box(lobby, "Screen Backdrop", 0, 0, 1280, 720, Paper);
        Stretch(backdrop, Vector2.zero, Vector2.zero);
        backdrop.gameObject.AddComponent<RectMask2D>();
        var gridColor = new Color(Navy.r, Navy.g, Navy.b, .12f);
        for (int x = 0; x <= 2560; x += 40) Box(backdrop, "Grid Vertical", x, 0, 1, 1440, gridColor).GetComponent<Image>().raycastTarget = false;
        for (int y = 0; y <= 1440; y += 40) Box(backdrop, "Grid Horizontal", 0, y, 2560, 1, gridColor).GetComponent<Image>().raycastTarget = false;
        var tape = Rect(backdrop, "Striped Tape", 0, 0, 2560, 18);
        tape.anchorMax = new Vector2(1, 1);
        tape.sizeDelta = new Vector2(0, 18);
        tape.gameObject.AddComponent<RectMask2D>();
        var backing = Box(tape, "Tape Backing", 0, 0, 2560, 18, RaceStyle.Tape);
        Stretch(backing, Vector2.zero, Vector2.zero);
        for (int x = -60; x < 2600; x += 44)
        {
            var stripe = Box(tape, "Stripe", x, -35, 16, 100, Navy);
            stripe.localRotation = Quaternion.Euler(0, 0, 45);
        }
        var screen = Rect(lobby, "Lobby Screen", 0, 0, LayoutW, 720);
        screen.anchorMin = screen.anchorMax = screen.pivot = new Vector2(.5f, .5f);
        screen.anchoredPosition = Vector2.zero;
        var logo = Resources.Load<Font>("Fonts/BungeeShade-Regular");
        float logoWidth = Measure(logo, "ESCAPE", 40);
        var wordmark = Text(screen, "Escape Wordmark", "ESCAPE", 40, logo, ModeX, 36, logoWidth + 8, 56, Color.white);
        var shadow = wordmark.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Kit.ShadowBlue;
        shadow.effectDistance = new Vector2(4, -4);
        var outline = wordmark.gameObject.AddComponent<Outline>();
        outline.effectColor = Navy;
        outline.effectDistance = new Vector2(2, -2);
        // White die showing four, with a red block shadow, like the Race lobby logo.
        float dieX = ModeX + logoWidth + 14;
        Box(screen, "Logo Die Shadow", dieX + 3, 48, 38, 38, Red);
        var die = Box(screen, "Logo Die", dieX, 45, 38, 38, Color.white);
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
                Box(die, "Pip", 8 + x * 17, 8 + y * 17, 5, 5, Ink);
        Text(screen, "Now Wordmark", "NOW", 32, heading, ModeX + logoWidth + 64, 36, 120, 56, Navy);
        return screen;
    }

    // How the online room looks in the editor before pressing Play: the WHO'S PLAYING view with Player 1 in the
    // first locker and the other lockers open, like the local lobbies. OnlineRaceLobbyView and OnlineLockerCard
    // switch these parts on and off as soon as the game runs, so this only affects the saved scene.
    private static void ShowRoomPreview(RectTransform screen, RectTransform setup, RectTransform room, RectTransform actions,
        RectTransform profile, List<Object[]> lockers, OnlineRaceSession session)
    {
        screen.localScale = Vector3.one * (1280f / LayoutW);
        setup.gameObject.SetActive(false);
        room.gameObject.SetActive(true);
        actions.gameObject.SetActive(true);
        profile.gameObject.SetActive(false);
        for (int slot = 0; slot < lockers.Count; slot++)
        {
            var root = ((GameObject)lockers[slot][0]).transform;
            bool filled = slot == 0;
            root.Find("Occupied").gameObject.SetActive(filled);
            var empty = root.Find("Empty Locker");
            empty.gameObject.SetActive(!filled);
            var label = empty.Find("Open Slot Label")?.GetComponent<Text>();
            if (label != null) label.text = "AWAITING PLAYER";
            var portrait = (Image)lockers[slot][1];
            if (filled && portrait.sprite == null)
            {
                // Player 1's skin, when the scene's skin list can give one outside Play mode.
                try { portrait.sprite = session.SlotPortrait(0); }
                catch (System.Exception) { portrait.sprite = null; }
            }
            portrait.enabled = portrait.sprite != null;
            if (!filled) continue;
            ((Text)lockers[slot][2]).text = "PLAYER 1";
            ((Text)lockers[slot][3]).text = "HOST / YOU";
            ((Text)lockers[slot][4]).text = "NOT READY";
        }
    }

    // Shrinks the wide layout to fit the window, the same way the Race lobby fits its controller panel.
    private static void FitToScreen(RectTransform lobby, RectTransform screen)
    {
        var responsive = lobby.gameObject.AddComponent<Escape4Now.UI.ResponsivePanel>();
        Assign(responsive, "viewport", lobby, "content", screen);
        var fields = new SerializedObject(responsive);
        fields.FindProperty("edgePadding").vector2Value = Vector2.zero;
        fields.ApplyModifiedPropertiesWithoutUndo();
    }

    // Cobin's CONTROLLERS / VIBRATE panel from ArcadeLobbyScreen.DrawControllerPanel, rebuilt as saved UI:
    // a navy panel with a gamepad icon, then one row per player with their controller, VIBRATE and CHANGE.
    // LobbyControllerPanel fills in the rows while the lobby is open.
    // previewRows is how many rows show in the editor before Play: one online, one per player slot locally.
    private static Escape4Now.UI.LobbyControllerPanel ControllerPanel(RectTransform screen, int previewRows)
    {
        float left = BarRight + ControllerGap;
        Frame(screen, "Controller Panel", left, PanelY, ControllerPanelW, PanelH, RaceStyle.PanelNavy, Color.white, 4, RaceStyle.CardNavy, 8);
        var panel = Rect(screen, "Controller Panel Content", left, PanelY, ControllerPanelW, PanelH);
        var view = panel.gameObject.AddComponent<Escape4Now.UI.LobbyControllerPanel>();
        const float x = 18, w = ControllerPanelW - 36;

        var icon = Box(panel, "Gamepad Icon", x, 20, 40, 40, Teal);
        GamepadIcon(icon, 6, 10);
        Text(panel, "Panel Caption", "CONTROLLERS", 11, mono, x + 50, 22, w - 50, 14, Muted);
        Text(panel, "Panel Title", "VIBRATE", 18, heading, x + 50, 34, w - 50, 26, Color.white);
        Box(panel, "Panel Divider", x, 74, w, 2, RaceStyle.EdgeBlue);

        var rows = new SerializedObject(view);
        var rowList = rows.FindProperty("rows");
        rowList.arraySize = Escape4Now.Multiplayer.ControllerHandling.MaxSlots;
        for (int i = 0; i < rowList.arraySize; i++)
        {
            var row = Rect(panel, "Player " + (i + 1) + " Controller", x, 86 + i * 78, w, 70);
            var chip = Box(row, "Chip", 0, 0, 34, 22, Teal).GetComponent<Image>();
            var chipLabel = Text(chip.transform, "Chip Label", "P" + (i + 1), 12, heading, 0, 0, 34, 22, Ink);
            chipLabel.alignment = TextAnchor.MiddleCenter;
            var device = Text(row, "Device", "", 11, mono, 42, 2, w - 42, 18, RaceStyle.OffText);
            device.horizontalOverflow = HorizontalWrapMode.Overflow;
            var element = rowList.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("root").objectReferenceValue = row.gameObject;
            element.FindPropertyRelative("chip").objectReferenceValue = chip;
            element.FindPropertyRelative("chipLabel").objectReferenceValue = chipLabel;
            element.FindPropertyRelative("device").objectReferenceValue = device;
            // Editor preview: the same idle row the Race panel shows with no controller plugged in.
            device.text = "KEYBOARD";
            PanelRowButton(row, "Vibrate", 0, 30, 78, 34, element.FindPropertyRelative("left"), "NO PAD", false);
            PanelRowButton(row, "Change", 84, 30, w - 84, 34, element.FindPropertyRelative("right"), "CHANGE", true);
            row.gameObject.SetActive(i < previewRows);
        }
        var help = Text(panel, "Controller Help", "", 10, mono, x, PanelH - 46, w, 32, RaceStyle.Tape);
        help.alignment = TextAnchor.UpperLeft;
        rows.FindProperty("help").objectReferenceValue = help;
        rows.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // A small row button like the Race panel's RowButton: dark block shadow, colored 2px edge, label in Bungee.
    // The dashed outline shows instead when there is nothing to press, such as NO PAD.
    private static void PanelRowButton(Transform row, string name, float x, float y, float w, float h, SerializedProperty target,
        string previewLabel, bool active)
    {
        var shadow = Box(row, name + " Shadow", x + 4, y + 4, w, h, RaceStyle.DeepNavy).GetComponent<Image>();
        shadow.raycastTarget = false;
        var root = Rect(row, name, x, y, w, h);
        var edge = Box(root, "Edge", 0, 0, w, h, RaceStyle.DashBlue).GetComponent<Image>();
        var fill = Box(root, "Fill", 2, 2, w - 4, h - 4, RaceStyle.CardNavy).GetComponent<Image>();
        var dashes = Rect(root, "No Pad Dashes", 0, 0, w, h);
        DashedBorder(dashes, 0, 0, w, h, 2, Kit.WithAlpha(RaceStyle.DashBlue, .6f));
        var label = Text(root, "Label", previewLabel, 11, heading, 0, 0, w, h, active ? Color.white : Kit.WithAlpha(RaceStyle.OffText, .6f));
        edge.enabled = active;
        dashes.gameObject.SetActive(!active);
        if (!active) fill.color = Kit.WithAlpha(RaceStyle.DeepNavy, .6f);
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
        colors.disabledColor = Color.white;
        button.colors = colors;
        target.FindPropertyRelative("button").objectReferenceValue = button;
        target.FindPropertyRelative("edge").objectReferenceValue = edge;
        target.FindPropertyRelative("fill").objectReferenceValue = fill;
        target.FindPropertyRelative("label").objectReferenceValue = label;
        target.FindPropertyRelative("dashes").objectReferenceValue = dashes.gameObject;
    }

    // The gamepad shape from ArcadeLobbyScreen.DrawGamepadIcon: a body with two rounded grips, a cross and two buttons.
    private static void GamepadIcon(Transform icon, float x, float y)
    {
        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Box(icon, "Pad Body", x + 3, y + 2, 22, 12, Ink);
        foreach (float gripX in new[] { 0f, 16f })
        {
            var grip = Box(icon, "Pad Grip", x + gripX, y + 4, 12, 14, Ink).GetComponent<Image>();
            grip.sprite = knob;
        }
        Box(icon, "Pad Cross", x + 5, y + 7, 6, 2, Teal);
        Box(icon, "Pad Cross", x + 7, y + 5, 2, 6, Teal);
        Box(icon, "Pad Button", x + 18, y + 5, 2.5f, 2.5f, Teal);
        Box(icon, "Pad Button", x + 21, y + 8, 2.5f, 2.5f, Teal);
    }

    // Left panel: mode icon, title, rules and divider. Same spacing as ArcadeLobbyScreen.DrawModePanel.
    private static void BuildModePanel(Transform screen, string title, string caption, bool flag,
        string rules, string rulesName, out Text titleText, out Text rulesText)
    {
        Frame(screen, "Mode Panel", ModeX, PanelY, ModeW, PanelH, RaceStyle.PanelNavy, Color.white, 4, RaceStyle.CardNavy, 8);
        var icon = Box(screen, "Mode Icon", Inner, 140, 52, 52, Teal);
        if (flag)
        {
            Box(icon, "Pole", 16, 13, 3, 26, Ink);
            Box(icon, "Flag", 19, 14, 17, 12, Ink);
        }
        else
        {
            // Build the stopwatch from Canvas shapes so the icon doesn't depend on font glyph coverage.
            const int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                var edge = Box(icon, "Clock Edge", 26 + 11 * Mathf.Cos(angle), 30 + 11 * Mathf.Sin(angle), 5, 3, Ink);
                edge.localRotation = Quaternion.Euler(0, 0, -(angle * Mathf.Rad2Deg + 101.25f));
            }
            Box(icon, "Crown", 22, 12, 8, 3, Ink);
            Box(icon, "Hand", 25, 22, 3, 8, Ink);
        }
        Text(screen, "Mode Type", caption, 11, mono, 142, 144, 182, 16, Muted);
        float titleSize = Mathf.Clamp(Mathf.Floor(30 * 182 / Mathf.Max(1, Measure(heading, title, 30))), 14, 30);
        titleText = Text(screen, "Mode Title", title, (int)titleSize, heading, 142, 158, 182, 34, Color.white);
        rulesText = Text(screen, rulesName, rules, 13, mono, Inner, 206, InnerW, 84, Kit.Soft);
        rulesText.alignment = TextAnchor.UpperLeft;
        FitDown(rulesText, 10);
        Box(screen, "Mode Divider", Inner, 296, InnerW, 2, RaceStyle.EdgeBlue);
    }

    // Right panel that holds the player cards, with the teal block shadow.
    private static void PlayersPanel(Transform screen)
    {
        Frame(screen, "Players Panel", PlayersX, PanelY, PlayersW, PanelH, RaceStyle.BoxNavy, Color.white, 4, Teal, 8);
    }

    // A player-count chip: big number over a small caption. ArcadeChip colors it when picked.
    private static RectTransform Chip(Transform parent, string name, string big, string small, float x, float y, bool available)
    {
        var chip = Rect(parent, name, x, y, 118, 64);
        var shadow = Box(chip, "Selected Shadow", 4, 4, 118, 64, Red);
        shadow.GetComponent<Image>().raycastTarget = false;
        shadow.gameObject.SetActive(false);
        float alpha = available ? 1 : .4f;
        Box(chip, "Border", 0, 0, 118, 64, Kit.WithAlpha(RaceStyle.EdgeBlue, alpha));
        Box(chip, "Face", 4, 4, 110, 56, Kit.WithAlpha(RaceStyle.CardNavy, alpha));
        Color text = available ? Color.white : Kit.WithAlpha(Color.white, .3f);
        Text(chip, "Count", big, 24, heading, 0, 8, 118, 28, text).alignment = TextAnchor.MiddleCenter;
        Text(chip, "Unit", small, 11, mono, 0, 38, 118, 16, text).alignment = TextAnchor.MiddleCenter;
        return chip;
    }

    // One online player card: a filled locker when someone has joined, a dashed empty slot otherwise.
    // Returns the parts OnlineRaceLobbyView updates: root, portrait, name, role, state and ready badge.
    private static Object[] LockerCard(Transform room, Color color, int slot)
    {
        var root = Rect(room, "Player " + (slot + 1) + " Locker", CardLeft + CardStep * slot, CardTop, CardW, CardH);
        var locker = root.gameObject.AddComponent<OnlineLockerCard>();
        var occupied = Rect(root, "Occupied", 0, 0, CardW, CardH);
        var portrait = BuildLockerFront(occupied, color, slot, out _, out var role);
        var previous = ArrowButton(occupied, "<", 18, 107);
        var next = ArrowButton(occupied, ">", CardW - 18 - 30, 107);
        var skin = Text(occupied, "Skin Number", "", 11, mono, 14, 178, 162, 16, Muted);
        skin.alignment = TextAnchor.MiddleCenter;

        Frame(occupied, "Account Name", 14, 212, 162, 42, Kit.Hex("#12284d"), color, 2);
        var name = Text(occupied, "Player Name", "", 15, heading, 24, 212, 142, 42, Color.white);
        FitDown(name, 10);
        var badge = Box(occupied, "Ready Badge", 14, 264, 162, 38, RaceStyle.EdgeBlue).GetComponent<Image>();
        var state = Text(badge.transform, "Ready Text", "", 12, heading, 0, 0, 162, 38, Color.white);
        state.alignment = TextAnchor.MiddleCenter;
        var account = Box(occupied, "Signed In Badge", 14, 312, 162, 38, color);
        Text(account, "Account Label", "LOGGED IN", 12, heading, 0, 0, 162, 38, Ink).alignment = TextAnchor.MiddleCenter;

        var empty = Rect(root, "Empty Locker", 0, 0, CardW, CardH);
        var emptyLabel = OpenSlot(empty, "");
        Assign(locker, "occupied", occupied.gameObject, "empty", empty.gameObject, "emptyLabel", emptyLabel,
            "skin", skin, "previous", previous, "next", next);
        PlayerPresentationAuthoring.ConfigureLocker(root, color);
        Tilt(root, slot);
        return new Object[] { root.gameObject, portrait, name, role, state, badge };
    }

    // Dashed empty locker with a + box, like ArcadeLobbyScreen.DrawOpenSlot.
    private static Text OpenSlot(Transform card, string label)
    {
        Box(card, "Open Slot Fill", 0, 0, CardW, CardH, Kit.WithAlpha(RaceStyle.CardNavy, .55f)).GetComponent<Image>().raycastTarget = false;
        DashedBorder(card, 0, 0, CardW, CardH, 4, RaceStyle.DashBlue);
        float plusY = CardH * .5f - 44;
        var plus = Rect(card, "Open Slot Icon", CardW * .5f - 28, plusY, 56, 56);
        DashedBorder(plus, 0, 0, 56, 56, 3, Kit.Hex("#55556a"));
        Text(plus, "Open Slot Mark", "+", 28, heading, 0, 0, 56, 56, Muted).alignment = TextAnchor.MiddleCenter;
        var text = Text(card, string.IsNullOrEmpty(label) ? "Open Slot Label" : "Add Player", label, 16, heading, 8, plusY + 66, CardW - 16, 22, Muted);
        text.alignment = TextAnchor.MiddleCenter;
        FitDown(text, 10);
        return text;
    }

    // Card frame, colored top band with player number, vents and role tag, and the dashed skin window.
    private static Image BuildLockerFront(Transform card, Color color, int slot, out Image band, out Text role)
    {
        Box(card, "Locker Shadow", 5, 5, CardW, CardH, RaceStyle.DeepNavy).GetComponent<Image>().raycastTarget = false;
        Frame(card, "Locker Frame", 0, 0, CardW, CardH, RaceStyle.CardNavy, color, 4);
        band = Box(card, "Locker Band", 4, 4, CardW - 8, 36, color).GetComponent<Image>();
        Text(card, "Slot", "P" + (slot + 1), 20, heading, 14, 4, 40, 36, Ink);
        for (int vent = 0; vent < 3; vent++)
            Box(card, "Locker Vent", 54, 15 + vent * 6, 20, 3, Kit.WithAlpha(Ink, .35f));

        // The tag hugs its text and stays pinned to the right, so longer roles such as HOST / YOU still fit.
        var tag = Box(card, "Role Tag", CardW - 12, 12, 60, 20, Ink);
        tag.pivot = new Vector2(1, 1);
        tag.anchoredPosition = new Vector2(CardW - 12, -12);
        var fit = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
        fit.padding = new RectOffset(6, 6, 0, 0);
        fit.childControlWidth = fit.childControlHeight = true;
        fit.childForceExpandWidth = fit.childForceExpandHeight = false;
        tag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        role = Text(tag, "Role", "", 11, bold, 0, 0, 48, 20, color);
        role.alignment = TextAnchor.MiddleCenter;
        role.horizontalOverflow = HorizontalWrapMode.Overflow;

        Box(card, "Portrait Frame", 14, 50, 162, 150, RaceStyle.DeepNavy);
        DashedBorder(card, 14, 50, 162, 150, 2, RaceStyle.DashBlue);
        var portrait = Box(card, "Skin Portrait", 50, 58, 90, 116, Color.white).GetComponent<Image>();
        portrait.preserveAspect = true;
        return portrait;
    }

    // Leans the card a little, alternating left and right, like the Race lobby.
    private static void Tilt(RectTransform card, int slot)
    {
        Vector2 size = card.sizeDelta;
        card.pivot = new Vector2(.5f, .5f);
        card.anchoredPosition += new Vector2(size.x * .5f, -size.y * .5f);
        card.localRotation = Quaternion.Euler(0, 0, slot % 2 == 0 ? .8f : -.8f);
    }

    private static void DashedBorder(Transform parent, float x, float y, float width, float height, float stroke, Color color)
    {
        const float step = 12f, dash = 7f;
        for (float i = 0; i < width; i += step)
        {
            float length = Mathf.Min(dash, width - i);
            Box(parent, "Top Dash", x + i, y, length, stroke, color).GetComponent<Image>().raycastTarget = false;
            Box(parent, "Bottom Dash", x + i, y + height - stroke, length, stroke, color).GetComponent<Image>().raycastTarget = false;
        }
        for (float i = 0; i < height; i += step)
        {
            float length = Mathf.Min(dash, height - i);
            Box(parent, "Left Dash", x, y + i, stroke, length, color).GetComponent<Image>().raycastTarget = false;
            Box(parent, "Right Dash", x + width - stroke, y + i, stroke, length, color).GetComponent<Image>().raycastTarget = false;
        }
    }

    // An empty UI object placed from the top-left, using the same x/y layout numbers as the Race lobby.
    private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    // A solid colored rectangle.
    private static RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var rect = Rect(parent, name, x, y, w, h);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    // A bordered panel with an optional offset block shadow, like ArcadeMenuKit.Block.
    private static void Frame(Transform parent, string name, float x, float y, float w, float h, Color fill, Color border,
        float borderWidth = 4, Color shadow = default, float shadowOffset = 0)
    {
        if (shadowOffset > 0) Box(parent, name + " Shadow", x + shadowOffset, y + shadowOffset, w, h, shadow);
        Box(parent, name + " Border", x, y, w, h, border);
        Box(parent, name + " Fill", x + borderWidth, y + borderWidth, w - borderWidth * 2, h - borderWidth * 2, fill);
    }

    // Inner card for the host and join choices, drawn like an occupied locker.
    private static void Card(Transform parent, string name, float x, float y, float w, float h)
    {
        Box(parent, name + " Shadow", x + 5, y + 5, w, h, RaceStyle.DeepNavy);
        Frame(parent, name, x, y, w, h, RaceStyle.CardNavy, RaceStyle.EdgeBlue, 4);
    }

    // A label that wraps across lines and never blocks clicks.
    private static Text Text(Transform parent, string name, string value, int size, Font font, float x, float y, float w, float h, Color color)
    {
        var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.text = value;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    // Shrinks long text to fit its box instead of spilling over.
    private static void FitDown(Text text, int minimum)
    {
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMaxSize = text.fontSize;
        text.resizeTextMinSize = minimum;
    }

    // Width of a line of text in a font, used to size the start button and the logo.
    private static float Measure(Font font, string value, int size)
    {
        if (font == null) return value.Length * size * .7f;
        font.RequestCharactersInTexture(value, size, FontStyle.Normal);
        float width = 0;
        foreach (char c in value)
            width += font.GetCharacterInfo(c, out CharacterInfo info, size, FontStyle.Normal) ? info.advance : size * .7f;
        return width;
    }

    // Main buttons: teal face, ink border, red block shadow. Others: navy face, white border, dark block shadow.
    private static Button Button(Transform parent, string name, float x, float y, float w, float h, bool primary, int size = 19, string label = null)
    {
        var rect = Rect(parent, name, x, y, w, h);
        float offset = h >= 44 ? 6 : 4;
        var shadow = Box(rect, primary ? "Red Offset Shadow" : "Offset Shadow", offset, offset, w, h, primary ? Red : RaceStyle.DeepNavy);
        Stretch(shadow, new Vector2(offset, -offset), new Vector2(offset, -offset));
        shadow.GetComponent<Image>().raycastTarget = false;
        float edge = primary ? 4 : 3;
        var border = Box(rect, "Border", 0, 0, w, h, primary ? Ink : Color.white);
        Stretch(border, Vector2.zero, Vector2.zero);
        var face = Box(rect, "Face", edge, edge, w - edge * 2, h - edge * 2, primary ? Teal : RaceStyle.CardNavy).GetComponent<Image>();
        Stretch(face.rectTransform, new Vector2(edge, edge), new Vector2(-edge, -edge));
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        var text = Text(rect, "Label", label ?? name, size, heading, 8, 3, w - 16, h - 6, primary ? Ink : Color.white);
        Stretch(text.rectTransform, new Vector2(8, 3), new Vector2(-8, -3));
        text.alignment = TextAnchor.MiddleCenter;
        // Labels stay on one line and shrink to fit, so long mode names like START HOT POTATO never wrap.
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        float room = w - 28;
        float needed = Measure(heading, text.text, size);
        if (needed > room) text.fontSize = Mathf.Max(10, Mathf.FloorToInt(size * room / needed));
        var colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.disabledColor = new Color(.55f, .55f, .55f);
        button.colors = colors;
        return button;
    }

    // Flat header button, like the Race lobby's ESC · MAIN MENU.
    private static Button MenuButton(Transform parent, string name, string label, float x, float y, float w, float h)
    {
        var rect = Box(parent, name, x, y, w, h, Color.white);
        var face = Box(rect, "Face", 2, 2, w - 4, h - 4, RaceStyle.PanelNavy).GetComponent<Image>();
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        var text = Text(rect, "Label", label, 14, mono, 0, 0, w, h, RaceStyle.Tape);
        text.alignment = TextAnchor.MiddleCenter;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        button.colors = colors;
        return button;
    }

    // Small < and > skin arrows inside the portrait window.
    private static Button ArrowButton(Transform parent, string name, float x, float y)
    {
        var rect = Box(parent, name, x, y, 30, 36, RaceStyle.BoxNavy);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        Text(rect, "Label", name, 16, heading, 0, 0, 30, 36, Color.white).alignment = TextAnchor.MiddleCenter;
        return button;
    }

    // A text box with a blue edge and dark inside. The name doubles as the grey hint text.
    private static InputField Input(Transform parent, string name, float x, float y, float w, float h, Font font, int size)
    {
        var rect = Box(parent, name, x, y, w, h, RaceStyle.DashBlue);
        Box(rect, "Dark Input", 2, 2, w - 4, h - 4, RaceStyle.DeepNavy);
        var input = rect.gameObject.AddComponent<InputField>();
        input.targetGraphic = rect.GetComponent<Image>();
        input.textComponent = Text(rect, "Entered Text", "", size, font, 12, 0, w - 24, h, Color.white);
        input.placeholder = Text(rect, "Hint", name, 13, mono, 12, 0, w - 24, h, RaceStyle.OffText);
        var colors = input.colors;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        colors.selectedColor = new Color(1.6f, 1.6f, 1.6f);
        input.colors = colors;
        return input;
    }

    // A dropdown styled like the text boxes, with its option list built underneath.
    private static Dropdown Dropdown(Transform parent, string name, float x, float y, float w, float h, string[] options)
    {
        var rect = Box(parent, name, x, y, w, h, RaceStyle.DashBlue);
        Box(rect, "Dark Field", 2, 2, w - 4, h - 4, RaceStyle.DeepNavy);
        var dropdown = rect.gameObject.AddComponent<Dropdown>();
        dropdown.targetGraphic = rect.GetComponent<Image>();
        dropdown.captionText = Text(rect, "Selected", "", 15, mono, 12, 0, w - 46, h, Color.white);
        Text(rect, "Arrow", "v", 16, bold, w - 30, 0, 22, h, Teal);
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        var template = Box(rect, "Template", 0, h + 2, w, 132, RaceStyle.DashBlue);
        var viewport = Rect(template, "Viewport", 2, 2, w - 4, 128);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect(viewport, "Content", 0, 0, w - 4, 42);
        var item = Box(content, "Item", 0, 0, w - 4, 42, RaceStyle.CardNavy);
        var toggle = item.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = item.GetComponent<Image>();
        toggle.graphic = Box(item, "Selected Marker", 10, 15, 10, 10, Teal).GetComponent<Image>();
        dropdown.itemText = Text(item, "Item Label", "", 14, mono, 30, 0, w - 38, 42, Color.white);
        var scroll = template.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.viewport = viewport;
        scroll.content = content;
        dropdown.template = template;
        template.gameObject.SetActive(false);
        return dropdown;
    }

    // Sets saved object references on a component by field name, in name/value pairs.
    private static void Assign(Object target, params object[] fields)
    {
        var serialized = new SerializedObject(target);
        for (int i = 0; i < fields.Length; i += 2) serialized.FindProperty((string)fields[i]).objectReferenceValue = (Object)fields[i + 1];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray<T>(Object target, string field, T[] values) where T : Object
    {
        var serialized = new SerializedObject(target);
        var array = serialized.FindProperty(field);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // Fills the parent, inset by the given offsets.
    private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = minimum;
        rect.offsetMax = maximum;
    }
}
