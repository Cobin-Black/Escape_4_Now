using System.Collections.Generic;
using System.IO;
using Escape4Now.Multiplayer.RelayTesting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;
using RaceStyle = Escape4Now.Menus.ArcadeLobbyScreen;

// Builds saved, editable Canvas objects using the local Race lobby palette and fonts.
public static class OnlineLobbyAuthoring
{
    private static readonly Color Ink = new Color(.067f, .067f, .067f);
    private static readonly Color Paper = new Color(.663f, .863f, 1f);
    private static readonly Color Navy = new Color(.114f, .227f, .4f);
    private static readonly Color Teal = new Color(.212f, .945f, .804f);
    private static readonly Color Red = new Color(.9f, .224f, .275f);
    private static readonly Color Panel = RaceStyle.BoxNavy;
    private static readonly Color Line = RaceStyle.EdgeBlue;
    private static readonly Color Muted = RaceStyle.SoftBlue;
    private static Font heading, mono, bold;

    [MenuItem("Tools/Escape 4 Now/Relay Test/Style Online Lobby")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/OnlineRace.unity");
        ApplyToCurrentScene();
    }

    public static void ApplyAndCheck()
    {
        Apply();
        RelayFoundationChecks.Run();
    }

    public static void ApplyToCurrentScene()
    {
        heading = Resources.Load<Font>("Fonts/Bungee-Regular");
        mono = Resources.Load<Font>("Fonts/SpaceMono-Regular");
        bold = Resources.Load<Font>("Fonts/SpaceMono-Bold");
        var session = Object.FindFirstObjectByType<OnlineRaceSession>();
        var previous = Object.FindFirstObjectByType<OnlineRaceLobbyView>(FindObjectsInactive.Include);
        if (previous != null) Object.DestroyImmediate(previous.gameObject);

        var canvas = new GameObject("Online Race Lobby", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder = 100;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var lobby = Rect(canvas.transform, "Lobby", 0, 0, 1280, 720);
        Stretch(lobby, Vector2.zero, Vector2.zero);
        var backdrop = Box(lobby, "Screen Backdrop", 0, 0, 1280, 720, Paper);
        Stretch(backdrop, Vector2.zero, Vector2.zero);
        var screen = Rect(lobby, "Lobby Screen", 0, 0, 1280, 720);
        var controls = screen.gameObject.AddComponent<CanvasGroup>();
        screen.anchorMin = screen.anchorMax = screen.pivot = new Vector2(.5f, .5f);
        screen.anchoredPosition = Vector2.zero;
        Box(screen, "Blue Paper", 0, 0, 1280, 720, Paper);
        for (int x = 0; x <= 1280; x += 40) Box(screen, "Grid Vertical", x, 0, 1, 720, new Color(Navy.r, Navy.g, Navy.b, .12f));
        for (int y = 0; y <= 720; y += 40) Box(screen, "Grid Horizontal", 0, y, 1280, 1, new Color(Navy.r, Navy.g, Navy.b, .12f));
        var tape = Rect(screen, "Striped Tape", 0, 0, 1280, 18);
        tape.gameObject.AddComponent<RectMask2D>();
        Box(tape, "Tape Backing", 0, 0, 1280, 18, RaceStyle.Tape);
        for (int x = -60; x < 1310; x += 44)
        {
            var stripe = Box(tape, "Stripe", x, -35, 16, 100, Navy);
            stripe.localRotation = Quaternion.Euler(0, 0, 45);
        }
        var logo = Resources.Load<Font>("Fonts/BungeeShade-Regular");
        var wordmark = Text(screen, "Escape Wordmark", "ESCAPE", 40, logo, 56, 36, 197, 56, Color.white);
        var shadow = wordmark.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Escape4Now.Menus.ArcadeMenuKit.ShadowBlue;
        shadow.effectDistance = new Vector2(4, -4);
        var outline = wordmark.gameObject.AddComponent<Outline>();
        outline.effectColor = Navy;
        outline.effectDistance = new Vector2(2, -2);
        var die = Box(screen, "Logo Die", 254, 45, 38, 38, Color.white);
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
                Box(die, "Pip", 8 + x * 16, 8 + y * 16, 5, 5, Ink);
        Text(screen, "Now Wordmark", "NOW", 32, heading, 306, 36, 120, 56, Navy);
        var back = Button(screen, "MAIN MENU", 1034, 40, 190, 48, false, 15);

        Frame(screen, "Race Mode Panel", 56, 118, 290, 470, RaceStyle.PanelNavy, Color.white, true);
        var flag = Box(screen, "Race Flag", 78, 140, 52, 52, Teal);
        Box(flag, "Flag Pole", 12, 11, 4, 30, Ink);
        Box(flag, "Flag Cloth", 16, 11, 23, 14, Ink);
        Text(screen, "Mode Type", "ONLINE MODE", 11, mono, 142, 143, 172, 17, Muted);
        Text(screen, "Mode Title", "RACE", 30, heading, 142, 157, 172, 40, Color.white);
        Text(screen, "Race Objective", "One key is hidden in a locker. Find it, battle for it, and carry it through the EXIT first.",
            13, mono, 78, 205, 246, 91, RaceStyle.Tape);
        Box(screen, "Mode Divider", 78, 308, 246, 2, Line);
        Frame(screen, "Players Panel", 378, 118, 846, 470, Panel, Color.white, true);

        var setup = Rect(screen, "Host or Join", 0, 0, 1280, 720);
        Text(setup, "Setup Heading", "PRIVATE ROOM", 32, heading, 400, 128, 500, 60, RaceStyle.PaleBlue);
        Text(setup, "Room Size Label", "HOW MANY PLAYERS?", 12, mono, 78, 329, 246, 20, Muted);
        var sizes = Rect(setup, "Room Size", 78, 363, 246, 72);
        var sizeGroup = sizes.gameObject.AddComponent<ToggleGroup>();
        var roomSizes = new Toggle[3];
        for (int i = 0; i < roomSizes.Length; i++)
        {
            var tile = Box(sizes, "Room Size " + (i + 2), i * 84, 0, 78, 72, Line);
            var face = Box(tile, "Face", 3, 3, 72, 66, RaceStyle.CardNavy).GetComponent<Image>();
            var selected = Box(tile, "Selection Accent", 3, 65, 72, 4, Teal).GetComponent<Image>();
            var toggle = tile.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = face;
            toggle.graphic = selected;
            toggle.isOn = i == 0;
            toggle.group = sizeGroup;
            Text(tile, "Capacity", (i + 2).ToString(), 26, heading, 4, 1, 70, 44, Color.white).alignment = TextAnchor.MiddleCenter;
            Text(tile, "Unit", "PLAYERS", 10, mono, 4, 43, 70, 20, RaceStyle.Tape).alignment = TextAnchor.MiddleCenter;
            roomSizes[i] = toggle;
        }

        Frame(setup, "Create Room Card", 400, 202, 393, 334, RaceStyle.CardNavy, Line);
        Text(setup, "Create Title", "CREATE ROOM", 22, heading, 421, 228, 351, 38, Color.white);
        Text(setup, "Create Help", "Choose a room size and share your code with friends.", 13, mono, 421, 280, 351, 70, Muted);
        var host = Button(setup, "HOST ROOM", 421, 446, 351, 54, true);

        Frame(setup, "Join Room Card", 809, 202, 393, 334, RaceStyle.CardNavy, Line);
        Text(setup, "Join Title", "JOIN ROOM", 22, heading, 830, 228, 351, 38, Color.white);
        Text(setup, "Join Help", "Enter the code from your host.", 13, mono, 830, 280, 351, 70, Muted);
        var joinCode = Input(setup, "ROOM CODE", 830, 367, 351);
        joinCode.characterLimit = 12;
        joinCode.characterValidation = InputField.CharacterValidation.Alphanumeric;
        var join = Button(setup, "JOIN ROOM", 830, 446, 351, 54, false);

        var room = Rect(screen, "Connected Room", 0, 0, 1280, 720);
        Text(room, "Code Heading", "ROOM CODE", 12, bold, 78, 327, 246, 26, Muted);
        var code = Text(room, "Room Code", "", 28, heading, 78, 355, 246, 45, Teal);
        var copy = Button(room, "COPY CODE", 78, 412, 246, 42, false, 15);
        Text(room, "Players Heading", "WHO'S PLAYING?", 32, heading, 400, 128, 475, 60, RaceStyle.PaleBlue);
        var summary = Text(room, "Connected Count", "", 13, mono, 928, 142, 274, 30, Muted);
        summary.alignment = TextAnchor.MiddleRight;
        var rowObjects = new List<Object[]>();
        for (int slot = 0; slot < 4; slot++)
        {
            Color color = RaceStyle.LockerColor(slot);
            var card = Rect(room, "Player " + (slot + 1) + " Locker", 400 + slot * 204, 192, 190, 374);
            Frame(card, "Locker", 0, 0, 190, 374, RaceStyle.CardNavy, color);
            Box(card, "Locker Band", 3, 3, 184, 36, color);
            Text(card, "Slot", "P" + (slot + 1), 20, heading, 12, 4, 40, 34, Ink);
            for (int vent = 0; vent < 3; vent++)
                Box(card, "Locker Vent", 53, 12 + vent * 6, 17, 3, Escape4Now.Menus.ArcadeMenuKit.WithAlpha(Ink, .35f));
            Box(card, "Role Tag", 78, 11, 101, 20, Ink);
            var role = Text(card, "Role", "", 11, bold, 80, 11, 97, 20, color);
            role.alignment = TextAnchor.MiddleCenter;

            Frame(card, "Portrait Frame", 14, 52, 162, 172, RaceStyle.DeepNavy, Line);
            var portrait = Box(card, "Skin Portrait", 46, 64, 98, 148, Color.white).GetComponent<Image>();
            portrait.preserveAspect = true;
            Frame(card, "Name Plate", 14, 240, 162, 43, RaceStyle.DeepNavy, Line);
            var name = Text(card, "Player Name", "", 14, bold, 20, 241, 150, 41, Color.white);
            name.alignment = TextAnchor.MiddleCenter;
            var badge = Box(card, "Ready Badge", 14, 300, 162, 40, Line).GetComponent<Image>();
            var state = Text(badge.transform, "Ready Text", "", 12, bold, 0, 0, 162, 40, Color.white);
            state.alignment = TextAnchor.MiddleCenter;
            rowObjects.Add(new Object[] { card.gameObject, portrait, name, role, state, badge });
        }
        var actions = Rect(screen, "Room Actions", 614, 612, 610, 62);
        var layout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        var ready = Button(actions, "READY", 0, 0, 192, 62, false, 17);
        var start = Button(actions, "START RACE", 0, 0, 192, 62, true, 17);
        var leave = Button(actions, "LEAVE ROOM", 0, 0, 192, 62, false, 17);
        var status = Text(screen, "Player Status", "", 13, bold, 56, 612, 530, 62, Navy);
        var openDiagnostics = Button(screen, "CONNECTION SETTINGS", 78, 514, 246, 36, false, 12);

        // Connection details are available on demand, rather than filling the normal lobby.
        var diagnostics = Box(screen, "Connection Settings Overlay", 0, 0, 1280, 720, new Color(0, 0, 0, .65f));
        diagnostics.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
        var modal = Rect(diagnostics, "Settings Card", 200, 145, 880, 440);
        Frame(modal, "Settings Frame", 0, 0, 880, 440, Panel, Color.white, true);
        Text(modal, "Settings Heading", "CONNECTION SETTINGS", 23, heading, 28, 18, 680, 40, Paper);
        var close = Button(modal, "CLOSE", 760, 18, 92, 34, false, 13);
        Text(modal, "Protocol Heading", "CONNECTION PROTOCOL", 12, bold, 28, 78, 824, 20, Muted);
        var protocol = Dropdown(modal, "Protocol", 28, 106, 824, new[] { "DTLS - encrypted UDP", "WSS - secure WebSocket", "UDP - diagnostic only" });
        var diagnosticStatus = Text(modal, "Actual Connection Details", "", 12, mono, 28, 162, 824, 132, Color.white);
        var networkLabel = Input(modal, "Network label for report (Home / Hotspot / School)", 28, 306, 600);
        var export = Button(modal, "SAVE REPORT", 650, 306, 202, 42, true, 15);
        var report = Text(modal, "Saved Report Path", "", 11, mono, 28, 360, 824, 62, Muted);
        diagnostics.gameObject.SetActive(false);

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
            "readyLabel", ready.GetComponentInChildren<Text>(), "code", code, "status", status, "summary", summary,
            "openDiagnostics", openDiagnostics, "closeDiagnostics", close, "protocol", protocol,
            "networkLabel", networkLabel, "export", export, "diagnosticStatus", diagnosticStatus, "reportPath", report);
        var serialized = new SerializedObject(view);
        var sizeToggles = serialized.FindProperty("roomSizes");
        sizeToggles.arraySize = roomSizes.Length;
        for (int i = 0; i < roomSizes.Length; i++) sizeToggles.GetArrayElementAtIndex(i).objectReferenceValue = roomSizes[i];
        serialized.FindProperty("normalColor").colorValue = Navy;
        serialized.FindProperty("errorColor").colorValue = new Color(.63f, .1f, .13f);
        var rows = serialized.FindProperty("rows");
        rows.arraySize = rowObjects.Count;
        string[] fields = { "root", "portrait", "name", "role", "state", "badge" };
        for (int i = 0; i < rowObjects.Count; i++)
            for (int j = 0; j < fields.Length; j++) rows.GetArrayElementAtIndex(i).FindPropertyRelative(fields[j]).objectReferenceValue = rowObjects[i][j];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(session.gameObject.scene);
        Debug.Log("ONLINE LOBBY STYLED: editable arcade Canvas, separate setup/room views, optional diagnostics.");
    }

    private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    private static RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var rect = Rect(parent, name, x, y, w, h);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    private static void Frame(Transform parent, string name, float x, float y, float w, float h, Color fill, Color border, bool shadow = false)
    {
        if (shadow) Box(parent, name + " Shadow", x + 9, y + 9, w, h, Teal);
        Box(parent, name + " Border", x, y, w, h, border);
        Box(parent, name + " Fill", x + 3, y + 3, w - 6, h - 6, fill);
    }

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
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static Button Button(Transform parent, string name, float x, float y, float w, float h, bool primary, int size = 19)
    {
        var rect = Rect(parent, name, x, y, w, h);
        var shadow = Box(rect, "Red Offset Shadow", 5, 5, w, h, Red);
        Stretch(shadow, new Vector2(5, -5), new Vector2(5, -5));
        var border = Box(rect, "Border", 0, 0, w, h, primary ? Ink : Color.white);
        Stretch(border, Vector2.zero, Vector2.zero);
        var face = Box(rect, "Face", 3, 3, w - 6, h - 6, primary ? Teal : RaceStyle.PanelNavy).GetComponent<Image>();
        Stretch(face.rectTransform, new Vector2(3, 3), new Vector2(-3, -3));
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        var text = Text(rect, "Label", name, size, heading, 8, 3, w - 16, h - 6, primary ? Ink : Color.white);
        Stretch(text.rectTransform, new Vector2(8, 3), new Vector2(-8, -3));
        text.alignment = TextAnchor.MiddleCenter;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.disabledColor = new Color(.55f, .55f, .55f);
        button.colors = colors;
        return button;
    }

    private static InputField Input(Transform parent, string name, float x, float y, float w)
    {
        var rect = Box(parent, name, x, y, w, 44, Color.white);
        Box(rect, "Dark Input", 2, 2, w - 4, 40, RaceStyle.DeepNavy);
        var input = rect.gameObject.AddComponent<InputField>();
        input.targetGraphic = rect.GetComponent<Image>();
        input.textComponent = Text(rect, "Entered Text", "", 15, mono, 14, 0, w - 28, 44, Color.white);
        input.placeholder = Text(rect, "Hint", name, 13, mono, 14, 0, w - 28, 44, Muted);
        return input;
    }

    private static Dropdown Dropdown(Transform parent, string name, float x, float y, float w, string[] options)
    {
        var rect = Box(parent, name, x, y, w, 44, Line);
        Box(rect, "Dark Field", 2, 2, w - 4, 40, RaceStyle.DeepNavy);
        var dropdown = rect.gameObject.AddComponent<Dropdown>();
        dropdown.targetGraphic = rect.GetComponent<Image>();
        dropdown.captionText = Text(rect, "Selected", "", 15, mono, 14, 0, w - 52, 44, Color.white);
        Text(rect, "Arrow", "v", 16, bold, w - 34, 0, 26, 44, Teal);
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        var template = Box(rect, "Template", 0, 46, w, 132, Line);
        var viewport = Rect(template, "Viewport", 2, 2, w - 4, 128);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect(viewport, "Content", 0, 0, w - 4, 42);
        var item = Box(content, "Item", 0, 0, w - 4, 42, RaceStyle.CardNavy);
        var toggle = item.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = item.GetComponent<Image>();
        toggle.graphic = Box(item, "Selected Marker", 10, 15, 10, 10, Teal).GetComponent<Image>();
        dropdown.itemText = Text(item, "Item Label", "", 14, mono, 34, 0, w - 42, 42, Color.white);
        var scroll = template.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.viewport = viewport;
        scroll.content = content;
        dropdown.template = template;
        template.gameObject.SetActive(false);
        return dropdown;
    }

    private static void Assign(Object target, params object[] fields)
    {
        var serialized = new SerializedObject(target);
        for (int i = 0; i < fields.Length; i += 2) serialized.FindProperty((string)fields[i]).objectReferenceValue = (Object)fields[i + 1];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = minimum;
        rect.offsetMax = maximum;
    }
}
