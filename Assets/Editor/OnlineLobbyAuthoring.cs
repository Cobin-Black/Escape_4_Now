using System.Collections.Generic;
using System.IO;
using Escape4Now.Multiplayer.RelayTesting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

// Builds saved, editable Canvas objects using the same palette and fonts as the main menu.
public static class OnlineLobbyAuthoring
{
    private static readonly Color Ink = new Color(.067f, .067f, .067f);
    private static readonly Color Paper = new Color(.663f, .863f, 1f);
    private static readonly Color Navy = new Color(.114f, .227f, .4f);
    private static readonly Color Teal = new Color(.212f, .945f, .804f);
    private static readonly Color Red = new Color(.9f, .224f, .275f);
    private static readonly Color Panel = new Color(.082f, .082f, .102f);
    private static readonly Color Line = new Color(.2f, .2f, .24f);
    private static readonly Color Muted = new Color(.65f, .65f, .7f);
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
        var backdrop = Box(canvas.transform, "Screen Backdrop", 0, 0, 1280, 720, Paper);
        Stretch(backdrop, Vector2.zero, Vector2.zero);
        var screen = Rect(canvas.transform, "Lobby Screen", 0, 0, 1280, 720);
        var controls = screen.gameObject.AddComponent<CanvasGroup>();
        screen.anchorMin = screen.anchorMax = screen.pivot = new Vector2(.5f, .5f);
        screen.anchoredPosition = Vector2.zero;
        Box(screen, "Blue Paper", 0, 0, 1280, 720, Paper);
        for (int x = 0; x <= 1280; x += 40) Box(screen, "Grid Vertical", x, 0, 1, 720, new Color(Navy.r, Navy.g, Navy.b, .12f));
        for (int y = 0; y <= 720; y += 40) Box(screen, "Grid Horizontal", 0, y, 1280, 1, new Color(Navy.r, Navy.g, Navy.b, .12f));
        var tape = Rect(screen, "Striped Tape", 0, 0, 1280, 18);
        tape.gameObject.AddComponent<RectMask2D>();
        for (int x = -30; x < 1310; x += 44)
        {
            var stripe = Box(tape, "Stripe", x, -20, 18, 60, Navy);
            stripe.localRotation = Quaternion.Euler(0, 0, 45);
        }
        Text(screen, "Online Race Heading", "ONLINE RACE", 32, heading, 80, 34, 850, 52, Navy);
        var back = Button(screen, "BACK TO MENU", 980, 40, 220, 42, false, 15);
        Frame(screen, "Main Panel", 80, 110, 1120, 550, Panel, Color.white, true);
        Text(screen, "Room Heading", "PRIVATE ROOM", 25, heading, 112, 130, 750, 42, Paper);
        Text(screen, "Room Guide", "One host. One room code. Everyone ready.", 13, mono, 112, 178, 1000, 26, Muted);
        Text(screen, "Mode Badge", "RACE  /  2-4 PLAYERS", 12, bold, 936, 138, 230, 24, Teal).alignment = TextAnchor.MiddleRight;
        Box(screen, "Header Divider", 112, 214, 1056, 2, Line);

        var setup = Rect(screen, "Host or Join", 112, 234, 1056, 272);
        Frame(setup, "Create Room Card", 0, 0, 510, 272, Ink, Line);
        Text(setup, "Create Title", "CREATE ROOM", 21, heading, 22, 14, 466, 34, Color.white);
        Text(setup, "Create Help", "Choose a room size and invite your teammates.", 12, mono, 22, 54, 466, 40, Muted);
        var players = Dropdown(setup, "Room Size", 22, 108, 466, new[] { "2 Players", "3 Players", "4 Players" });
        var host = Button(setup, "HOST ROOM", 22, 194, 466, 48, true);
        Frame(setup, "Join Room Card", 546, 0, 510, 272, Ink, Line);
        Text(setup, "Join Title", "JOIN ROOM", 21, heading, 568, 14, 466, 34, Color.white);
        Text(setup, "Join Help", "Have a code? Enter it below to join your host.", 12, mono, 568, 54, 466, 40, Muted);
        var joinCode = Input(setup, "ROOM CODE", 568, 108, 466);
        joinCode.characterLimit = 12;
        joinCode.characterValidation = InputField.CharacterValidation.Alphanumeric;
        var join = Button(setup, "JOIN ROOM", 568, 194, 466, 48, false);

        var room = Rect(screen, "Connected Room", 112, 234, 1056, 272);
        Frame(room, "Share Code Card", 0, 0, 310, 272, Ink, Line);
        Text(room, "Code Heading", "ROOM CODE", 13, bold, 20, 18, 270, 26, Muted);
        var code = Text(room, "Room Code", "", 28, heading, 20, 64, 270, 52, Teal);
        var copy = Button(room, "COPY CODE", 20, 130, 270, 42, false, 16);
        Text(room, "Code Help", "Share this code with your teammates.", 12, mono, 20, 196, 270, 52, Muted);
        Frame(room, "Players Card", 334, 0, 722, 272, Ink, Line);
        Text(room, "Players Heading", "PLAYERS", 20, heading, 350, 10, 300, 38, Color.white);
        var summary = Text(room, "Connected Count", "", 12, bold, 782, 14, 258, 30, Teal);
        summary.alignment = TextAnchor.MiddleRight;
        var rowObjects = new List<Object[]>();
        for (int slot = 0; slot < 4; slot++)
        {
            var row = Box(room, "Player " + (slot + 1) + " Row", 350, 60 + slot * 48, 690, 42, Panel);
            var swatch = Box(row, "Player Color", 0, 0, 4, 42, Teal).GetComponent<Image>();
            var portrait = Box(row, "Skin Portrait", 16, 3, 28, 36, Color.white).GetComponent<Image>();
            portrait.preserveAspect = true;
            var name = Text(row, "Player Name", "", 13, bold, 60, 0, 470, 42, Color.white);
            var badge = Box(row, "Ready Badge", 540, 9, 134, 24, Line).GetComponent<Image>();
            var state = Text(badge.transform, "Ready Text", "", 11, bold, 0, 0, 134, 24, Color.white);
            state.alignment = TextAnchor.MiddleCenter;
            rowObjects.Add(new Object[] { row.gameObject, swatch, portrait, name, state, badge });
        }
        var actions = Rect(screen, "Room Actions", 112, 528, 1056, 48);
        var layout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 18;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        var ready = Button(actions, "READY", 0, 0, 340, 48, false);
        var start = Button(actions, "START MATCH", 0, 0, 340, 48, true);
        var leave = Button(actions, "LEAVE ROOM", 0, 0, 340, 48, false);
        var status = Text(screen, "Player Status", "", 12, mono, 112, 598, 1056, 46, Color.white);
        var openDiagnostics = Button(screen, "CONNECTION SETTINGS", 902, 678, 298, 30, false, 12);
        Text(screen, "Footer", "PLAY TOGETHER. ESCAPE FIRST.", 12, bold, 80, 678, 700, 30, Navy);

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
        Assign(view, "session", session, "panel", screen.gameObject, "setup", setup.gameObject,
            "room", room.gameObject, "actions", actions.gameObject, "diagnosticsPanel", diagnostics.gameObject,
            "mainControls", controls, "cancel", cancel,
            "players", players, "joinCode", joinCode, "host", host, "join", join, "back", back,
            "ready", ready, "start", start, "leave", leave, "copy", copy,
            "readyLabel", ready.GetComponentInChildren<Text>(), "code", code, "status", status, "summary", summary,
            "openDiagnostics", openDiagnostics, "closeDiagnostics", close, "protocol", protocol,
            "networkLabel", networkLabel, "export", export, "diagnosticStatus", diagnosticStatus, "reportPath", report);
        var serialized = new SerializedObject(view);
        var rows = serialized.FindProperty("rows");
        rows.arraySize = rowObjects.Count;
        string[] fields = { "root", "swatch", "portrait", "name", "state", "badge" };
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
        var face = Box(rect, "Face", 3, 3, w - 6, h - 6, primary ? Teal : Ink).GetComponent<Image>();
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
        Box(rect, "Dark Input", 2, 2, w - 4, 40, Ink);
        var input = rect.gameObject.AddComponent<InputField>();
        input.targetGraphic = rect.GetComponent<Image>();
        input.textComponent = Text(rect, "Entered Text", "", 15, mono, 14, 0, w - 28, 44, Color.white);
        input.placeholder = Text(rect, "Hint", name, 13, mono, 14, 0, w - 28, 44, Muted);
        return input;
    }

    private static Dropdown Dropdown(Transform parent, string name, float x, float y, float w, string[] options)
    {
        var rect = Box(parent, name, x, y, w, 44, Line);
        Box(rect, "Dark Field", 2, 2, w - 4, 40, Ink);
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
        var item = Box(content, "Item", 0, 0, w - 4, 42, Ink);
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
