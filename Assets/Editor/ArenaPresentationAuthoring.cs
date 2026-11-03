using System;
using Escape4Now.Arena;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Saves editable Unity UI. Runtime components only update values and listen to native UI events.
public static class ArenaPresentationAuthoring
{
    private static ArenaInterfaceStyle style;
    private static Sprite rounded;
    // Update saved UI/art only; keep both modes' gameplay and optional event-debug logic intact.
    public static void ApplyGymSign()
    {
        try
        {
            var metal = new Color(.14f, .18f, .23f);
            ArenaVisualAuthoring.Model("Gym Scoreboard", new ArenaVisualAuthoring.Part(0, 0, 3.2f, .22f, .10f, 1.58f, metal), new ArenaVisualAuthoring.Part(0, -.13f, 3.0f, .03f, .18f, 1.4f, new Color(.015f, .025f, .035f)), new ArenaVisualAuthoring.Part(-.82f, -.15f, 1.15f, .02f, .40f, .50f, new Color(.004f, .008f, .013f)), new ArenaVisualAuthoring.Part(.82f, -.15f, 1.15f, .02f, .40f, .50f, new Color(.004f, .008f, .013f)));
            var room = AssetDatabase.LoadAssetAtPath<ArenaRoomVisualSettings>("Assets/Settings/Arena/Room Visuals.asset");
            room.showScoreboardText = true;
            room.scoreboardTitle = "GYM";
            room.scoreboardText = "GYM";
            room.scoreboardTextSize = .09f;
            room.scoreboardFontSize = 64;
            room.scoreboardFontStyle = FontStyle.Bold;
            room.scoreboardTextColor = new Color(1, .9f, .6f);
            room.scoreboardTextInset = new Vector2(-.14f, 0);
            room.scoreboardTextHeight = .64f;
            room.scoreboardHomeLabel = "HOME";
            room.scoreboardAwayLabel = "AWAY";
            room.scoreboardHomeScore = room.scoreboardAwayScore = 0;
            room.scoreboardPeriodLabel = "PERIOD";
            room.scoreboardPeriod = 1;
            room.scoreboardColumnOffset = .82f;
            room.scoreboardSortingOffset=35;
            room.scoreboardHeaderHeight = 1.35f;
            room.scoreboardTeamHeight = 1.05f;
            room.scoreboardDigitsHeight = .66f;
            room.scoreboardPeriodHeight = .29f;
            room.scoreboardHeaderSize = .032f;
            room.scoreboardTeamSize = .026f;
            room.scoreboardDigitsSize = .08f;
            room.scoreboardPeriodSize = .026f;
            EditorUtility.SetDirty(room);
            AssetDatabase.SaveAssets();
            foreach (string stem in new[]
            {
                "FreeForAll",
                "HotPotato"
            }

            )
            {
                EditorSceneManager.OpenScene("Assets/Scenes/" + stem + ".unity");
                var ui = Object.FindFirstObjectByType<ArenaUI>();
                var data = new SerializedObject(ui);
                data.FindProperty("showEventButton").boolValue = false;
                ((Button)data.FindProperty("showEvents").objectReferenceValue).gameObject.SetActive(false);
                data.ApplyModifiedPropertiesWithoutUndo();
                Object.FindFirstObjectByType<ArenaEventDirector>().SetEventLocationsVisible(false);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }

            AssetDatabase.SaveAssets();
            ArenaPresentationChecks.Run();
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("Tools/Escape 4 Now/Polish Arena Interface")]
    public static void Apply()
    {
        try
        {
            foreach (string stem in new[]
            {
                "FreeForAll",
                "HotPotato"
            }

            )
            {
                EditorSceneManager.OpenScene("Assets/Scenes/" + stem + ".unity");
                var mode = Object.FindFirstObjectByType<ArenaMatch>();
                var data = new SerializedObject(mode);
                data.FindProperty("instructions").stringValue = stem == "FreeForAll" ? "Move freely, use cover and survive the gym's events.\nLast player standing wins. Attacks are not enabled yet." : "Chase and tag to pass the potato before time runs out.\nEach explosion eliminates its holder. Last player standing wins.";
                data.ApplyModifiedPropertiesWithoutUndo();
                BuildSceneUI(mode, AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions"));
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Arena setup and HUD views saved with native Input System UI.");
            if (System.Array.IndexOf(Environment.GetCommandLineArgs(), "-checkArenaPresentation") >= 0)
            {
                ArenaPresentationChecks.Run();
                return;
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }

    public static void BuildSceneUI(ArenaMatch mode, InputActionAsset actions)
    {
        LoadStyle();
        var ui = Object.FindFirstObjectByType<ArenaUI>(FindObjectsInactive.Include);
        GameObject root;
        if (ui == null)
        {
            root = new GameObject("Arena Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            ui = root.AddComponent<ArenaUI>();
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }
        else
            root = ui.gameObject;
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            if (root.transform.GetChild(i).name != "Environmental lighting")
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        // RawImage uses a white UI texture without asking the shared blackout shader for a sprite texture.
        var lightingRoot = root.transform.Find("Environmental lighting");
        if (lightingRoot != null && lightingRoot.GetComponent<Image>() != null)
        {
            Object.DestroyImmediate(lightingRoot.GetComponent<Image>());
            var shade = lightingRoot.gameObject.AddComponent<RawImage>();
            shade.color = Color.clear;
            shade.raycastTarget = false;
            Set(lightingRoot.GetComponent<ArenaEventLightingUI>(), "shade", shade);
        }

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        if (root.GetComponent<Escape4Now.UI.MatchCanvasVisibility>() == null)
            root.AddComponent<Escape4Now.UI.MatchCanvasVisibility>();
        EnsureInputSystem(actions);
        var lobby = Panel(root.transform, "Match Setup", style.setupWidth, style.panel, true);
        Vertical(lobby, style.padding, style.gap);
        var responsive = lobby.gameObject.AddComponent<Escape4Now.UI.ResponsivePanel>();
        Set(responsive, "viewport", root.transform, "content", lobby);
        var setup = lobby.gameObject.AddComponent<ArenaLobbyView>();
        Set(setup, "style", style);
        var title = Text(lobby, "Setup Title", mode.Title, style.setupTitleSize, true, 48);
        title.alignment = TextAnchor.MiddleCenter;
        var instructions = Text(lobby, "Instructions", mode.Instructions, style.bodySize, false, 44);
        instructions.alignment = TextAnchor.MiddleCenter;
        instructions.color = style.secondary;
        var selection = Group(lobby, "Player Count");
        Vertical(selection, 0, 8);
        Text(selection, "Count Heading", "PLAYER COUNT", style.smallSize, true, 16).color = style.secondary;
        var countRow = Group(selection, "Count Buttons");
        Horizontal(countRow, 8);
        Height(countRow, style.countButtonHeight);
        var countButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            countButtons[i] = Button(countRow, (i + 2) + " PLAYERS", style.countButtonHeight, style.input);
            Flex(countButtons[i].transform);
        }

        float contentWidth = style.setupWidth - style.padding * 2;
        var header = Group(lobby, "Player Columns");
        Height(header, 18);
        Header(header, "PLAYER", contentWidth, -contentWidth * .5f + 12, 164);
        Header(header, "CONTROLS", contentWidth, -contentWidth * .5f + 184, 220);
        Header(header, "SKIN", contentWidth, -contentWidth * .5f + 412, contentWidth - 424);
        var rowsParent = Group(lobby, "Player Rows");
        Vertical(rowsParent, 0, 8);
        var rows = new GameObject[4];
        var names = new InputField[4];
        var controls = new Dropdown[4];
        var skins = new Dropdown[4];
        var portraits = new Image[4];
        var strips = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var row = Panel(rowsParent, "Player " + (i + 1), contentWidth, style.card);
            Height(row, style.playerRowHeight);
            rows[i] = row.gameObject;
            strips[i] = Picture(row, "Identity", new Vector2(3, style.playerRowHeight - 16), new Vector2(-contentWidth * .5f + 3, 0), style.accent);
            portraits[i] = Picture(row, "Skin Portrait", new Vector2(28, 44), new Vector2(-contentWidth * .5f + 26, 0), Color.white);
            portraits[i].preserveAspect = true;
            names[i] = NameField(row, "Player " + (i + 1), new Vector2(128, 38), new Vector2(-contentWidth * .5f + 112, 0));
            controls[i] = Dropdown(row, "Controls", new Vector2(220, 38), new Vector2(-contentWidth * .5f + 294, 0));
            skins[i] = Dropdown(row, "Skin", new Vector2(contentWidth - 424, 38), new Vector2(212, 0));
        }

        var start = Button(lobby, "START MATCH", style.startButtonHeight, style.start);
        start.GetComponentInChildren<Text>().fontSize = 18;
        var error = Text(lobby, "Setup Error", "", style.smallSize, false, 30);
        error.color = new Color(1, .55f, .4f);
        error.gameObject.SetActive(false);
        Set(setup, "title", title, "instructions", instructions, "error", error, "start", start);
        Array(setup, "playerCounts", countButtons);
        Array(setup, "rows", rows);
        Array(setup, "names", names);
        Array(setup, "controls", controls);
        Array(setup, "skins", skins);
        Array(setup, "portraits", portraits);
        Array(setup, "identityStrips", strips);
        var hud = Panel(root.transform, "Match HUD", style.hudWidth, style.panel, true);
        Vertical(hud, style.hudPadding, style.hudGap);
        hud.localScale = Vector3.one * style.hudScale;
        hud.anchorMin = hud.anchorMax = new Vector2(0, 1);
        hud.pivot = new Vector2(0, 1);
        hud.anchoredPosition = style.hudOffset;
        var view = hud.gameObject.AddComponent<ArenaHudView>();
        Set(view, "style", style);
        var camera = Object.FindFirstObjectByType<ArenaSharedCamera>();
        if (camera != null)
            Set(camera, "hud", hud);
        var modeTitle = Text(hud, "Mode Title", mode.Title, style.headingSize, true, 21);
        var remaining = Text(hud, "Players Remaining", "", style.smallSize, true, 15);
        remaining.color = style.secondary;
        var potato = Panel(hud, "Potato Details", style.hudWidth - style.hudPadding * 2, style.card);
        Vertical(potato, 5, 2);
        var clockRow = Group(potato, "Round and Clock");
        Height(clockRow, 24);
        var round = FixedText(clockRow, "Round", "", new Vector2(90, 24), new Vector2(-49, 0), style.smallSize, true);
        var timer = FixedText(clockRow, "Countdown", "", new Vector2(80, 24), new Vector2(56, 0), 21, true);
        timer.alignment = TextAnchor.MiddleRight;
        timer.color = style.holder;
        var holder = Text(potato, "Holder", "", style.smallSize, true, 16);
        holder.color = style.holder;
        var timerBar = Picture(potato, "Timer Bar", new Vector2(220, 3), Vector2.zero, style.holder);
        timerBar.sprite = rounded;
        timerBar.type = Image.Type.Filled;
        timerBar.fillMethod = Image.FillMethod.Horizontal;
        Height(timerBar.transform, 3);
        var objective = Text(hud, "Objective", "", style.smallSize, false, 28);
        var divider = Panel(hud, "Health Divider", style.hudWidth - style.hudPadding * 2, new Color(style.accent.r, style.accent.g, style.accent.b, .35f));
        Height(divider, 1);
        var health = Group(hud, "Player Health");
        var grid = health.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.cellSize = new Vector2((style.hudWidth - style.hudPadding * 2 - 6) / 2, style.healthRowHeight);
        grid.spacing = new Vector2(6, 5);
        health.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var healthRows = new ArenaHealthRow[4];
        for (int i = 0; i < 4; i++)
            healthRows[i] = HealthRow(health, grid.cellSize, i);
        var notice = Text(hud, "Event Notice", "", style.smallSize, false, 36);
        notice.color = style.secondary;
        notice.gameObject.SetActive(false);
        Set(view, "modeTitle", modeTitle, "remaining", remaining, "objective", objective, "round", round, "timer", timer, "holder", holder, "notice", notice, "potatoSection", potato.gameObject, "timerBar", timerBar);
        Array(view, "healthRows", healthRows);
        var pause = Button(root.transform, "PAUSE", 34, style.input);
        var pauseRect = (RectTransform)pause.transform;
        pauseRect.sizeDelta = new Vector2(100, 34);
        pauseRect.anchorMin = pauseRect.anchorMax = new Vector2(1, 1);
        pauseRect.anchoredPosition = new Vector2(-62, -29);
        var showEvents = Button(root.transform, "Show Events", 34, style.input);
        var eventsRect = (RectTransform)showEvents.transform;
        eventsRect.sizeDelta = new Vector2(118, 34);
        eventsRect.anchorMin = eventsRect.anchorMax = new Vector2(1, 1);
        eventsRect.anchoredPosition = new Vector2(-179, -29);
        Set(ui, "showEvents", showEvents, "showEventsLabel", showEvents.GetComponentInChildren<Text>());
        var paused = Panel(root.transform, "Pause Menu", 360, style.panel, true);
        Vertical(paused, 24, 14);
        Text(paused, "Title", "PAUSED", 24, true, 40).alignment = TextAnchor.MiddleCenter;
        var resume = Button(paused, "RESUME", 42, style.start);
        var leave = Button(paused, "BACK TO SETUP", 38, style.input);
        var finished = Panel(root.transform, "Match Result", 420, style.panel, true);
        Vertical(finished, 24, 14);
        var result = Text(finished, "Winner", "", 24, true, 65);
        result.alignment = TextAnchor.MiddleCenter;
        var replay = Button(finished, "PLAY AGAIN", 42, style.start);
        var menu = Button(finished, "MAIN MENU", 38, style.input);
        Set(ui, "match", mode, "setup", setup, "presentation", view, "lobby", lobby.gameObject, "hud", hud.gameObject, "pausePanel", paused.gameObject, "resultPanel", finished.gameObject, "result", result, "pause", pause, "resume", resume, "leave", leave, "replay", replay, "menu", menu);
        hud.gameObject.SetActive(false);
        paused.gameObject.SetActive(false);
        finished.gameObject.SetActive(false);
    }

    private static ArenaHealthRow HealthRow(Transform parent, Vector2 size, int index)
    {
        var root = Panel(parent, "Health " + (index + 1), size.x, style.card);
        root.sizeDelta = size;
        var row = root.gameObject.AddComponent<ArenaHealthRow>();
        var outline = root.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1, -1);
        outline.enabled = false;
        var identity = Picture(root, "Identity", new Vector2(2, size.y - 8), new Vector2(-size.x * .5f + 2, 0), style.accent);
        var portrait = Picture(root, "Portrait", new Vector2(18, 29), new Vector2(-size.x * .5f + 15, 0), Color.white);
        portrait.preserveAspect = true;
        var name = FixedText(root, "Name", "", new Vector2(size.x - 40, 18), new Vector2(12, 8), 11, true);
        name.resizeTextForBestFit = true;
        name.resizeTextMinSize = 9;
        name.resizeTextMaxSize = 11;
        var value = FixedText(root, "HP", "", new Vector2(36, 18), new Vector2(size.x * .5f - 21, -9), 10, false);
        value.alignment = TextAnchor.MiddleRight;
        value.gameObject.SetActive(false);
        var hearts = FixedText(root, "Hearts", "", new Vector2(size.x - 29, 19), new Vector2(11, -8), 15, false);
        hearts.supportRichText = true;
        hearts.horizontalOverflow = HorizontalWrapMode.Overflow;
        Set(row, "style", style, "portrait", portrait, "identity", identity, "playerName", name, "value", value, "holderOutline", outline, "hearts", hearts);
        return row;
    }

    private static void LoadStyle()
    {
        const string path = "Assets/Settings/Arena/Interface Style.asset";
        style = AssetDatabase.LoadAssetAtPath<ArenaInterfaceStyle>(path);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<ArenaInterfaceStyle>();
            style.name = "Interface Style";
            style.playerRowHeight = 60;
            AssetDatabase.CreateAsset(style, path);
        }

        style.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        EditorUtility.SetDirty(style);
    }

    private static RectTransform Group(Transform parent, string name)
    {
        var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        r.SetParent(parent, false);
        return r;
    }

    private static RectTransform Panel(Transform parent, string name, float width, Color color, bool border = false)
    {
        var r = Group(parent, name);
        r.sizeDelta = new Vector2(width, 0);
        var image = r.gameObject.AddComponent<Image>();
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
        image.color = color;
        if (border)
        {
            var outline = r.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(style.accent.r, style.accent.g, style.accent.b, .8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        return r;
    }

    private static void Vertical(RectTransform root, float padding, float gap)
    {
        var group = root.gameObject.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
        group.spacing = gap;
        group.childControlWidth = group.childControlHeight = true;
        group.childForceExpandHeight = false;
        root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static void Horizontal(RectTransform root, float gap)
    {
        var group = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = gap;
        group.childControlWidth = group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = true;
    }

    private static Text Text(Transform parent, string name, string content, int size, bool bold, float height)
    {
        var text = FixedText(parent, name, content, Vector2.zero, Vector2.zero, size, bold);
        Height(text.transform, height);
        return text;
    }

    private static Text FixedText(Transform parent, string name, string content, Vector2 size, Vector2 position, int fontSize, bool bold)
    {
        var r = Group(parent, name);
        r.sizeDelta = size;
        r.anchoredPosition = position;
        var text = r.gameObject.AddComponent<Text>();
        text.font = style.font;
        text.fontSize = fontSize;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.color = style.text;
        text.alignment = TextAnchor.MiddleLeft;
        text.text = content;
        text.raycastTarget = false;
        return text;
    }

    private static Image Picture(Transform parent, string name, Vector2 size, Vector2 position, Color color)
    {
        var r = Group(parent, name);
        r.sizeDelta = size;
        r.anchoredPosition = position;
        var image = r.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Button Button(Transform parent, string caption, float height, Color color)
    {
        var r = Panel(parent, caption, 0, color);
        Height(r, height);
        var button = r.gameObject.AddComponent<Button>();
        button.targetGraphic = r.GetComponent<Image>();
        var text = FixedText(r, "Label", caption, Vector2.zero, Vector2.zero, style.bodySize, true);
        text.alignment = TextAnchor.MiddleCenter;
        Stretch(text.rectTransform, 8);
        return button;
    }

    private static InputField NameField(Transform parent, string value, Vector2 size, Vector2 position)
    {
        var r = Panel(parent, "Player Name", size.x, style.input);
        r.sizeDelta = size;
        r.anchoredPosition = position;
        var input = r.gameObject.AddComponent<InputField>();
        var text = FixedText(r, "Text", value, size - new Vector2(12, 0), Vector2.zero, style.bodySize, false);
        input.textComponent = text;
        input.text = value;
        input.characterLimit = 16;
        return input;
    }

    private static Dropdown Dropdown(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var resources = new DefaultControls.Resources
        {
            standard = rounded,
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd")
        };
        var root = DefaultControls.CreateDropdown(resources);
        root.name = name;
        root.transform.SetParent(parent, false);
        var r = (RectTransform)root.transform;
        r.sizeDelta = size;
        r.anchoredPosition = position;
        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            text.font = style.font;
            text.fontSize = style.smallSize;
            text.color = style.text;
        }

        foreach (var image in root.GetComponentsInChildren<Image>(true))
            image.color = image.name == "Arrow" || image.name == "Item Checkmark" ? style.secondary : style.input;
        return root.GetComponent<Dropdown>();
    }

    private static void Header(Transform parent, string text, float rowWidth, float left, float width)
    {
        var label = FixedText(parent, text, text, new Vector2(width, 18), new Vector2(left + width * .5f, 0), style.smallSize, true);
        label.color = style.secondary;
    }

    private static void Height(Transform target, float height)
    {
        var layout = target.GetComponent<LayoutElement>();
        if (layout == null)
            layout = target.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = height;
    }

    private static void Flex(Transform target)
    {
        var layout = target.GetComponent<LayoutElement>();
        if (layout == null)
            layout = target.gameObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1;
    }

    private static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * padding;
        rect.offsetMax = -Vector2.one * padding;
    }

    private static void Set(Object target, params object[] pairs)
    {
        var data = new SerializedObject(target);
        for (int i = 0; i < pairs.Length; i += 2)
            data.FindProperty((string)pairs[i]).objectReferenceValue = (Object)pairs[i + 1];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Array<T>(Object target, string field, T[] values)
        where T : Object
    {
        var data = new SerializedObject(target);
        var property = data.FindProperty(field);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureInputSystem(InputActionAsset asset)
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;
        var module = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>();
        module.actionsAsset = asset;
        module.point = Input(asset, "Point");
        module.leftClick = Input(asset, "Click");
        module.move = Input(asset, "Navigate");
        module.submit = Input(asset, "Submit");
        module.cancel = Input(asset, "Cancel");
        module.scrollWheel = Input(asset, "ScrollWheel");
    }

    private static InputActionReference Input(InputActionAsset asset, string name)
    {
        string path = "Assets/Settings/Arena/UI " + name + " Input.asset";
        var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
        if (reference == null)
        {
            reference = InputActionReference.Create(asset.FindAction("UI/" + name, true));
            AssetDatabase.CreateAsset(reference, path);
        }

        return reference;
    }
}
