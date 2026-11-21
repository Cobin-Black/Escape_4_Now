using System;
using System.IO;
using System.Linq;
using Escape4Now.Multiplayer;
using Escape4Now.Multiplayer.RelayTesting;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Creates an editable diagnostic scene. It does not modify any existing menu or game scene.
public static class RelayTestAuthoring
{
    public const string ScenePath = "Assets/Scenes/RelayConnectionTest.unity";
    public const string SettingsPath = "Assets/Settings/Networking/Relay Test Settings.asset";
    private static Font font;
    private static readonly Color Panel = new Color(.025f, .07f, .1f);
    private static readonly Color Accent = new Color(.02f, .67f, .83f);

    // Create the diagnostic scene with its own network manager, transport and test controls.
    [MenuItem("Tools/Escape 4 Now/Relay Test/Create Test Scene")]
    public static void CreateScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Directory.CreateDirectory("Assets/Settings/Networking");
        AssetDatabase.Refresh();
        var settings = AssetDatabase.LoadAssetAtPath<RelayTestSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<RelayTestSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var camera = new GameObject("Test Camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.015f, .025f, .035f);
        camera.transform.position = new Vector3(0, 0, -10);

        var services = new GameObject("Relay Test - Settings, Identity, Connection");
        var identity = services.AddComponent<RelayTestIdentity>();
        var networkObject = new GameObject("Test NetworkManager - No Gameplay");
        var transport = networkObject.AddComponent<UnityTransport>();
        var manager = networkObject.AddComponent<NetworkManager>();
        manager.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = false,
            ConnectionApproval = true,
            PlayerPrefab = null
        };
        var connection = services.AddComponent<RelayRoomConnection>();
        Assign(connection, "settings", settings, "identity", identity, "network", manager, "transport", transport);
        services.AddComponent<RelayTestCommandLine>();

        var canvas = new GameObject("Relay Test UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 900);
        scaler.matchWidthOrHeight = .5f;
        var panel = Box(canvas.transform, "Connection Test Panel", new Vector2(1160, 840), Vector2.zero, Panel);
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = Accent;
        outline.effectDistance = new Vector2(2, 2);
        Label(panel, "Title", "RELAY CONNECTION TEST", 27, new Vector2(1080, 40), new Vector2(0, 380));
        Label(panel, "Scope", "Internet host + join code only. No game mode starts here.", 17, new Vector2(1080, 30), new Vector2(0, 341));
        Label(panel, "Instructions", "Use the SAME protocol on both computers. Test DTLS, WSS, then UDP in separate rooms.\nFor a different-network test, put one computer on a phone hotspot or another internet connection.", 17, new Vector2(1080, 60), new Vector2(0, 286));

        var protocol = Protocol(panel, new Vector2(-390, 213));
        var host = Button(panel, "Host Room", new Vector2(190, 44), new Vector2(-155, 213), new Color(.15f, .47f, .31f));
        var joinCode = Input(panel, "Join code", new Vector2(210, 44), new Vector2(85, 213));
        joinCode.characterLimit = 12;
        var join = Button(panel, "Join Room", new Vector2(190, 44), new Vector2(335, 213), new Color(.06f, .31f, .45f));
        var code = Label(panel, "Room Code", "ROOM CODE: -", 24, new Vector2(600, 40), new Vector2(-240, 151));
        var copy = Button(panel, "Copy Code", new Vector2(180, 38), new Vector2(155, 151), new Color(.1f, .2f, .27f));
        var leave = Button(panel, "Stop / Leave", new Vector2(180, 38), new Vector2(355, 151), new Color(.33f, .16f, .17f));
        var status = Label(panel, "Status", "Ready", 19, new Vector2(1080, 56), new Vector2(0, 90));
        status.color = new Color(.55f, .92f, .8f);
        var diagnostics = Label(panel, "Diagnostics", "", 16, new Vector2(1080, 160), new Vector2(0, -15));
        diagnostics.color = new Color(.9f, .85f, .65f);
        var historyBox = Box(panel, "Recent Activity", new Vector2(1080, 232), new Vector2(0, -215), new Color(.015f, .035f, .05f));
        var history = Label(historyBox, "Activity Text", "", 14, new Vector2(1040, 215), Vector2.zero);
        history.alignment = TextAnchor.UpperLeft;
        var networkLabel = Input(panel, "Network label: Home Wi-Fi / Hotspot / School", new Vector2(710, 38), new Vector2(-185, -363));
        var export = Button(panel, "Save Test Report", new Vector2(340, 38), new Vector2(370, -363), new Color(.06f, .31f, .45f));
        var exported = Label(panel, "Report Path", "Reports exclude authentication tokens and room codes. Also keep Player.log when diagnosing transport errors.", 12, new Vector2(1080, 40), new Vector2(0, -400));
        var view = canvas.AddComponent<RelayTestView>();
        Assign(view, "connection", connection, "protocol", protocol, "joinCode", joinCode, "networkLabel", networkLabel,
            "host", host, "join", join, "leave", leave, "copyCode", copy, "export", export,
            "code", code, "status", status, "diagnostics", diagnostics, "history", history, "exportedPath", exported);

        var events = new GameObject("Input System UI", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Relay test scene authored. No existing game scenes or build settings were changed.");
    }

    // Build the saved connection-test player used by standalone networking checks.
    [MenuItem("Tools/Escape 4 Now/Relay Test/Build Standalone Tester")]
    public static void Build()
    {
        if (!File.Exists(ScenePath)) CreateScene();
        string output = Path.GetFullPath("Logs/RelayTestPlayer/RelayTest.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (result.summary.result != BuildResult.Succeeded) throw new Exception("Relay test build failed: " + result.summary.result);
        Debug.Log("RELAY TEST BUILD PASSED: " + output);
    }

    [MenuItem("Tools/Escape 4 Now/Relay Test/Create Online Race Scene")]
    public static void CreateOnlineRace() => CreateOnlineScene("Race_Gamemode", "OnlineRace");

    public static void CreateOnlineModes()
    {
        CreateOnlineRace();
        CreateOnlineScene("SurvivalMode", "OnlineSurvival");
        CreateOnlineScene("FreeForAll", "OnlineFreeForAll");
        CreateOnlineScene("HotPotato", "OnlineHotPotato");
    }

    // Build the online scene around the existing local mode and its network adapter.
    private static void CreateOnlineScene(string source, string destination)
    {
        CreateScene();
        var testScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var connection = Object.FindFirstObjectByType<RelayRoomConnection>();
        var manager = Object.FindFirstObjectByType<NetworkManager>();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + source + ".unity", OpenSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(connection.gameObject, scene);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(manager.gameObject, scene);
        Object.DestroyImmediate(connection.GetComponent<RelayTestCommandLine>());
        EditorSceneManager.CloseScene(testScene, true);
        var settings = AssetDatabase.LoadAssetAtPath<RelayTestSettings>("Assets/Settings/Networking/Online Race Connection.asset");
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<RelayTestSettings>();
            settings.maximumPlayers = 4;
            settings.testVersion = LanMessage.Version;
            AssetDatabase.CreateAsset(settings, "Assets/Settings/Networking/Online Race Connection.asset");
        }
        Assign(connection, "settings", settings);
        connection.gameObject.name = "Online Connection - Relay, Anonymous Identity, Diagnostics";
        manager.gameObject.name = "Online NetworkManager - Relay Only";
        var local = Object.FindFirstObjectByType<LocalMultiplayerSession>();
        var match = Object.FindFirstObjectByType<LanMultiplayerSession>();
        foreach (var lobby in Object.FindObjectsByType<Escape4Now.UI.LobbyUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var data = new SerializedObject(lobby);
            ((GameObject)data.FindProperty("panel").objectReferenceValue).SetActive(false);
            lobby.enabled = false;
        }
        var session = connection.gameObject.AddComponent<OnlineRaceSession>();
        Assign(session, "local", local, "match", match, "relay", connection);
        if (source == "Race_Gamemode") connection.gameObject.AddComponent<OnlineRaceSmokeCheck>();
        else if (source == "SurvivalMode")
        {
            var adapter = connection.gameObject.AddComponent<OnlineSurvivalMatch>();
            match = connection.gameObject.AddComponent<LanMultiplayerSession>();
            Assign(adapter, "local", local, "mode", Object.FindFirstObjectByType<Escape4Now.GameModes.SurvivalModeController>(),
                "transport", match);
            Assign(local, "lan", match);
            Assign(session, "match", match, "modeMatch", adapter);
        }
        else
        {
            var arena = Object.FindFirstObjectByType<Escape4Now.Arena.ArenaMatch>();
            OnlineArenaMatch adapter = arena is Escape4Now.Arena.HotPotatoController
                ? connection.gameObject.AddComponent<OnlineHotPotatoMatch>()
                : connection.gameObject.AddComponent<OnlineKingOfTheHillMatch>();
            match = connection.gameObject.AddComponent<LanMultiplayerSession>();
            Assign(adapter, "arena", arena, "arenaUI", Object.FindFirstObjectByType<Escape4Now.Arena.ArenaUI>(),
                "room", session, "transport", match, "presentation", connection.gameObject.AddComponent<OnlineArenaPresentation>());
            Assign(session, "match", match, "modeMatch", adapter);
        }
        string path = "Assets/Scenes/" + destination + ".unity";
        EditorSceneManager.SaveScene(scene, path);
        if (!EditorBuildSettings.scenes.Any(entry => entry.path == path))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(path, true)).ToArray();
        OnlineLobbyAuthoring.ApplyToCurrentScene();
        AssetDatabase.SaveAssets();
        Debug.Log("ONLINE RACE SCENE AUTHORED: existing Race scene is unchanged.");
    }

    // Build the online Race player from its saved scene and build settings.
    [MenuItem("Tools/Escape 4 Now/Relay Test/Build Online Race Prototype")]
    public static void BuildOnlineRace()
    {
        string output = Path.GetFullPath("Logs/OnlineRacePlayer/Escape4NowOnline.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path)
            .Concat(new[] { "Assets/Scenes/OnlineRace.unity", ScenePath }).Distinct().ToArray();
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        if (result.summary.result != BuildResult.Succeeded) throw new Exception("Online Race build failed: " + result.summary.result);
        Debug.Log("ONLINE RACE BUILD PASSED: " + output);
    }

    public static void AuthorAndBuildModes()
    {
        CreateOnlineModes();
        RelayFoundationChecks.Run();
        BuildOnlineRace();
    }

    public static void BuildAndCheckAll()
    {
        CreateOnlineRace();
        RelayFoundationChecks.Run();
        Build();
        BuildOnlineRace();
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static RectTransform Box(Transform parent, string name, Vector2 size, Vector2 position, Color color)
    {
        var rect = Rect(parent, name, size, position);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return rect;
    }

    // Create a styled diagnostic label with a saved UI reference.
    private static Text Label(Transform parent, string name, string value, int size, Vector2 dimensions, Vector2 position)
    {
        var text = Rect(parent, name, dimensions, position).gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.text = value;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        text.supportRichText = false;
        return text;
    }

    private static Button Button(Transform parent, string name, Vector2 size, Vector2 position, Color color)
    {
        var rect = Box(parent, name, size, position, color);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        var text = Label(rect, "Label", name, 17, size - new Vector2(16, 4), Vector2.zero);
        text.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    // Create a diagnostic text field for room codes or report labels.
    private static InputField Input(Transform parent, string placeholder, Vector2 size, Vector2 position)
    {
        var rect = Box(parent, placeholder, size, position, new Color(.09f, .15f, .19f));
        var input = rect.gameObject.AddComponent<InputField>();
        input.targetGraphic = rect.GetComponent<Image>();
        input.textComponent = Label(rect, "Entered Text", "", 17, size - new Vector2(20, 6), Vector2.zero);
        var hint = Label(rect, "Placeholder", placeholder, 16, size - new Vector2(20, 6), Vector2.zero);
        hint.color = new Color(.65f, .72f, .77f);
        input.placeholder = hint;
        return input;
    }

    // Build the protocol selector and its dropdown template for the connection-test UI.
    private static Dropdown Protocol(Transform parent, Vector2 position)
    {
        var rect = Box(parent, "Connection Protocol", new Vector2(280, 44), position, new Color(.09f, .15f, .19f));
        var dropdown = rect.gameObject.AddComponent<Dropdown>();
        dropdown.targetGraphic = rect.GetComponent<Image>();
        dropdown.captionText = Label(rect, "Current Protocol", "", 17, new Vector2(250, 40), Vector2.zero);
        dropdown.options.Clear();
        foreach (string label in new[] { "DTLS - encrypted UDP", "WSS - secure WebSocket", "UDP - diagnostic only" })
            dropdown.options.Add(new Dropdown.OptionData(label));
        var template = Box(rect, "Template", new Vector2(280, 132), new Vector2(0, -90), new Color(.09f, .15f, .19f));
        var scroll = template.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        var viewport = Box(template, "Viewport", new Vector2(280, 132), Vector2.zero, Color.white);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var content = Rect(viewport, "Content", new Vector2(280, 44), Vector2.zero);
        content.anchorMin = new Vector2(.5f, 1);
        content.anchorMax = new Vector2(.5f, 1);
        content.pivot = new Vector2(.5f, 1);
        var item = Box(content, "Item", new Vector2(280, 44), Vector2.zero, new Color(.12f, .25f, .32f));
        var toggle = item.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = item.GetComponent<Image>();
        toggle.graphic = item.GetComponent<Image>();
        dropdown.itemText = Label(item, "Item Label", "", 17, new Vector2(250, 40), Vector2.zero);
        scroll.viewport = viewport;
        scroll.content = content;
        dropdown.template = template;
        template.gameObject.SetActive(false);
        dropdown.RefreshShownValue();
        return dropdown;
    }

    private static void Assign(Object target, params object[] fields)
    {
        var serialized = new SerializedObject(target);
        for (int i = 0; i < fields.Length; i += 2)
            serialized.FindProperty((string)fields[i]).objectReferenceValue = (Object)fields[i + 1];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
