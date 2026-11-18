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
                ApplyArcadeUI(mode);
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

    private static void ApplyArcadeUI(ArenaMatch match)
    {
        LoadStyle();
        var ui = Object.FindFirstObjectByType<ArenaUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            BuildSceneUI(match, AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions"));
            return;
        }

        var previous = ui.transform.Find("Match Setup");
        int order = previous != null ? previous.GetSiblingIndex() : -1;
        if (previous != null) Object.DestroyImmediate(previous.gameObject);
        var setup = OnlineLobbyAuthoring.BuildSameDeviceLobby((RectTransform)ui.transform, match, style);
        if (order >= 0) setup.transform.SetSiblingIndex(order);
        Set(ui, "setup", setup, "lobby", setup.gameObject);
        ApplyMatchStyle(ui);
    }

    // Rebuilds every lobby screen so they all match the Race and Survival lobby.
    [MenuItem("Tools/Escape 4 Now/Rebuild All Lobby Screens")]
    public static void RebuildAllLobbies()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try
        {
            foreach (string scene in new[] { "FreeForAll", "HotPotato", "OnlineRace", "OnlineSurvival", "OnlineFreeForAll", "OnlineHotPotato" })
            {
                EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
                var arena = Object.FindFirstObjectByType<ArenaMatch>();
                if (arena != null) ApplyArcadeUI(arena);
                if (scene.StartsWith("Online", StringComparison.Ordinal)) OnlineLobbyAuthoring.ApplyToCurrentScene();
                else EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ALL LOBBY SCREENS REBUILT: Free For All, Hot Potato and the four online rooms.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    public static void ApplySameDeviceAndOnline()
    {
        foreach (string scene in new[] { "FreeForAll", "HotPotato", "OnlineFreeForAll", "OnlineHotPotato" })
        {
            EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
            ApplyArcadeUI(Object.FindFirstObjectByType<ArenaMatch>());
            if (scene.StartsWith("Online", StringComparison.Ordinal)) OnlineLobbyAuthoring.ApplyToCurrentScene();
            else EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        RelayFoundationChecks.Run();
    }

    private static void ApplyMatchStyle(ArenaUI ui)
    {
        // Preserve the saved HUD widgets, including the hill inventory and Hot Potato timer.
        var hud = ui.transform.Find("Match HUD");
        if (hud == null) return;
        hud.GetComponent<Image>().color = new Color(.025f, .071f, .114f, .98f);
        var objective = hud.Find("Objective");
        if (objective != null && hud.Find("Objective Heading") == null)
        {
            var divider = Panel(hud, "Objective Divider", style.hudWidth - 2 * style.hudPadding,
                new Color(.61f, .75f, .84f, .4f));
            Height(divider, 1);
            divider.SetSiblingIndex(objective.GetSiblingIndex());
            var heading = Text(hud, "Objective Heading", "OBJECTIVE", style.smallSize, true, 14);
            heading.color = style.holder;
            heading.transform.SetSiblingIndex(objective.GetSiblingIndex());
        }
        var pause = ui.transform.Find("PAUSE");
        if (pause != null)
        {
            pause.GetComponent<Image>().color = new Color(.15f, .19f, .17f);
            pause.GetComponentInChildren<Text>(true).text = "Pause";
        }
        foreach (string path in new[] { "Pause Menu", "Match Result" })
        {
            var menu = ui.transform.Find(path);
            if (menu == null) continue;
            menu.GetComponent<Image>().color = Escape4Now.Menus.ArcadeLobbyScreen.PanelNavy;
            var label = menu.Find(path == "Pause Menu" ? "Title" : "Winner")?.GetComponent<Text>();
            if (label != null) label.font = Resources.Load<Font>("Fonts/Bungee-Regular");
            var button = menu.Find(path == "Pause Menu" ? "RESUME" : "PLAY AGAIN")?.GetComponent<Button>();
            if (button == null) continue;
            button.image.color = new Color(.212f, .945f, .804f);
            button.GetComponentInChildren<Text>(true).color = new Color(.067f, .067f, .067f);
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
        var setup = OnlineLobbyAuthoring.BuildSameDeviceLobby((RectTransform)root.transform, mode, style);
        var lobby = setup.transform;
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
        bool showHealth = !(mode is HotPotatoController);
        divider.gameObject.SetActive(showHealth);
        health.gameObject.SetActive(showHealth);
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
        ApplyMatchStyle(ui);
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

    private static void Height(Transform target, float height)
    {
        var layout = target.GetComponent<LayoutElement>();
        if (layout == null)
            layout = target.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = height;
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
