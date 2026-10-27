using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Escape4Now.GameModes;
using Escape4Now.Items;
using Escape4Now.Map;
using Escape4Now.Obstacles;
using Escape4Now.Multiplayer;
using Escape4Now.TurnSystem;
using Escape4Now.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class NativeUiChecks
{
    private const string Running = "NativeUiChecks.Running";
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static double deadline;
    private static InputSettings.BackgroundBehavior oldBackground;
    private static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
    private static bool waitingForTutorial;
    private static TutorialSection expectedSection;
    private static int tutorialSceneHandle;

    static NativeUiChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 150;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            EditorApplication.update += Watchdog;
            Object.FindFirstObjectByType<MatchUIContext>().StartCoroutine(Execute());
        };
    }

    public static void Run()
    {
        string scene = Argument("-uiScene", "Race_Gamemode");
        EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
        var playerSettings = new SerializedObject(Object.FindFirstObjectByType<Escape4Now.Player.PlayerCharacter>());
        Require(playerSettings.FindProperty("health").intValue == 5, "Scene retains main's serialized starting health");
        playerSettings.FindProperty("health").intValue = 7;
        playerSettings.ApplyModifiedPropertiesWithoutUndo();
        CheckInventoryLayout(Object.FindFirstObjectByType<MatchUIContext>());
        var visibility = Object.FindFirstObjectByType<MatchCanvasVisibility>();
        Require(visibility != null && !visibility.GetComponent<Canvas>().enabled, "Match Canvas is hidden outside Play Mode");
        var serializedVisibility = new SerializedObject(visibility);
        var preview = serializedVisibility.FindProperty("previewInEditMode");
        preview.boolValue = true;
        serializedVisibility.ApplyModifiedPropertiesWithoutUndo();
        visibility.SendMessage("Refresh");
        Require(visibility.GetComponent<Canvas>().enabled, "Inspector preview can show the authored UI for editing");
        preview.boolValue = false;
        serializedVisibility.ApplyModifiedPropertiesWithoutUndo();
        visibility.SendMessage("Refresh");
        Require(!visibility.GetComponent<Canvas>().enabled, "Turning off preview hides UI again without disabling its hierarchy");
        foreach (var appearance in Object.FindObjectsByType<Escape4Now.Player.PlayerAppearance>(FindObjectsSortMode.None))
            foreach (Renderer renderer in appearance.GetComponentsInChildren<Renderer>(true))
                Require(!renderer.enabled, "Player visuals do not leave a floating arrow in Edit Mode");
        if (!Application.isBatchMode)
        {
            var type = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(type).Show();
            SetSize(1280, 720);
        }
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    public static void Build()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Race_Gamemode.unity", "Assets/Scenes/SurvivalMode.unity", "Assets/Scenes/Tutorial.unity" },
            locationPathName = "Logs/GameplayUiPlayer/Escape4Now.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        bool success = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        Debug.Log(success ? "GAMEPLAY PLAYER BUILD PASSED" : "GAMEPLAY PLAYER BUILD FAILED");
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static void CheckHudLayout(MatchUIContext context)
    {
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interface/MatchInterface.prefab")
            .transform.Find("HUD View/HUD");
        var panel = (RectTransform)context.transform.Find("HUD View/HUD");
        Require(panel.rect.width == ((RectTransform)reference).rect.width,
            "Every mode inherits the same HUD width");
        foreach (var original in reference.GetComponentsInChildren<RectTransform>(true))
        {
            if (original == reference) continue;
            if (original.IsChildOf(reference.Find("Event Notice"))) continue;
            var path = AnimationUtility.CalculateTransformPath(original, reference);
            var actual = panel.Find(path) as RectTransform;
            Require(actual != null && actual.sizeDelta == original.sizeDelta &&
                actual.anchoredPosition == original.anchoredPosition,
                "Shared HUD geometry: " + path);
            var originalText = original.GetComponent<Text>();
            if (originalText == null) continue;
            var actualText = actual.GetComponent<Text>();
            Require(actualText.font == originalText.font && actualText.fontSize == originalText.fontSize &&
                actualText.fontStyle == originalText.fontStyle && actualText.color == originalText.color,
                "Shared HUD typography: " + path);
        }
        string objective = context.ActivePlayer.Mode != null ? context.ActivePlayer.Mode.GetObjective(context.ActivePlayer) : "";
        bool hasObjective = !string.IsNullOrEmpty(objective);
        var hud = new SerializedObject(context.GetComponentInChildren<MatchHudUI>(true));
        Require(panel.Find("Objective").gameObject.activeSelf == hasObjective,
            "Objective appears only when the mode provides one");
        Require(panel.rect.height == hud.FindProperty(hasObjective ? "heightWithObjective" : "heightWithoutObjective").floatValue,
            "HUD uses its Inspector-configured height without leaving an empty objective section");
        Require(panel.Find("Timer").gameObject.activeSelf == (context.Timer != null) &&
            panel.Find("Details Divider").gameObject.activeSelf == (context.Timer != null),
            "Timer and its divider are hidden together when absent");
        Require(((Text)hud.FindProperty("objective").objectReferenceValue).text == objective,
            "HUD displays the current mode's own objective");
    }

    private static IEnumerator Execute()
    {
        var stack = new System.Collections.Generic.Stack<IEnumerator>();
        stack.Push(Check());
        while (true)
        {
            object current;
            try
            {
                if (stack.Count == 0) { Finish(0); yield break; }
                var checks = stack.Peek();
                if (!checks.MoveNext()) { stack.Pop(); continue; }
                current = checks.Current;
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); yield break; }
            yield return current;
        }
    }

    private static IEnumerator Check()
    {
        yield return null; yield return null;
        var context = Object.FindFirstObjectByType<MatchUIContext>();
        Require(context.GetComponent<Canvas>().enabled, "The authored Canvas becomes visible in Play Mode");
        var module = Object.FindFirstObjectByType<InputSystemUIInputModule>();
        Require(context != null && module != null && module.actionsAsset == InputSystem.actions, "Native UI uses the project Input Actions asset");
        Require(module.point.action.actionMap.name == "UI" && module.submit.action.actionMap.name == "UI", "Pointer and navigation actions are assigned in Unity");
        var profile = Escape4Now.Input.GameplayInputContext.Current;
        Require(profile != null && profile.Pause == InputSystem.actions.FindAction("Player/Pause"), "Pause reuses the teammate's shared action");
        foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
        {
            var type = script.GetClass();
            if (type == null || type.Assembly != typeof(MatchUIContext).Assembly) continue;
            Require(type.GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly) == null,
                type.Name + " has no immediate-mode gameplay UI");
        }
        var session = context.Session;
        string prefix = Argument("-uiScene", "Race_Gamemode");
        if (session == null)
        {
            Require(new SerializedObject(context.ActivePlayer).FindProperty("health").intValue == 7,
                "Tutorial respects Inspector-configured health");
            Require(context.Events.EventLocationsVisible, "Tutorial always shows its event tiles without a visibility toggle");
            CheckHudLayout(context);
            CheckActivePlayerName(context, true);
            yield return CheckEventNotice(context, prefix);
            var tutorial = Object.FindFirstObjectByType<Tutorial_Functionality>();
            Require(tutorial != null && Object.FindFirstObjectByType<TutorialOverlay>() != null, "Tutorial retains a native lesson view");
            yield return Capture(prefix + "-tutorial");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Require(context.ActivePlayer.HasRolled, "Tutorial receives Roll Dice through its configured action");
            tutorial.CompleteCurrentSection();
            yield return null; yield return null;
            var next = ((GameObject)new SerializedObject(Object.FindFirstObjectByType<TutorialOverlay>())
                .FindProperty("next").objectReferenceValue).GetComponent<Button>();
            expectedSection = tutorial.CurrentSection + 1;
            tutorialSceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            waitingForTutorial = true;
            yield return Click(next);
            Require(!tutorial.IsSectionComplete, "Native tutorial Continue advances the lesson");
            Debug.Log("NATIVE UI CHECKS PASSED: Tutorial, native lesson, configured input, pointer Continue.");
            yield break;
        }

        CheckActivePlayerName(context, false);
        var lobby = context.transform.Find("Lobby View/Lobby/Content");
        foreach (int count in new[] { 2, 3, 4 })
        {
            var toggle = lobby.Find(count.ToString()).GetComponent<Toggle>();
            yield return Click(toggle);
            Require(session.PlayerCount == count, "Real pointer selects " + count + " players");
        }
        for (int i = 0; i < 4; i++)
            session.SetPlayerName(i, new[] { "Alex", "Jordan", "Morgan", "Sam" }[i]);
        if (session.Mode.SupportsBots)
        {
            var bot = lobby.Find("Player 4 Slot/Bot").GetComponent<Toggle>();
            session.SetBotPlayer(3, true);
            yield return null; yield return null;
            Require(bot.isOn, "Lobby reflects Inspector/API bot configuration");
            yield return Click(bot);
            Require(!session.IsBotPlayer(3), "Native bot toggle returns the slot to human control");
        }
        yield return Capture(prefix + "-lobby");
        yield return Click(lobby.Find("Start Match").GetComponent<Button>());
        Require(session.MatchStarted && session.Participants.Count == 4, "Native Start button creates four participants");
        yield return null; yield return null;
        foreach (var participant in session.Participants)
            Require(new SerializedObject(participant).FindProperty("health").intValue == 7,
                "Every participant inherits Inspector-configured health without changing identity");
        var firstHealth = new SerializedObject(session.Participants[0]);
        firstHealth.FindProperty("health").intValue = 3;
        firstHealth.ApplyModifiedPropertiesWithoutUndo();
        Require(new SerializedObject(session.Participants[1]).FindProperty("health").intValue == 7,
            "Health belongs to each player independently");
        firstHealth.FindProperty("health").intValue = 7;
        firstHealth.ApplyModifiedPropertiesWithoutUndo();
        var turnControls = context.GetComponentInChildren<TurnControlsUI>();
        var endTurn = (Button)new SerializedObject(turnControls).FindProperty("endTurn").objectReferenceValue;
        Require(!endTurn.gameObject.activeSelf, "Optional End Turn button is hidden by default");
        var turnSettings = new SerializedObject(context.Turns);
        turnSettings.FindProperty("showEndTurnButton").boolValue = true;
        turnSettings.ApplyModifiedPropertiesWithoutUndo();
        yield return null; yield return null;
        Require(endTurn.gameObject.activeInHierarchy && !endTurn.interactable,
            "Inspector can enable End Turn, but it cannot skip the required roll");
        var hud = Object.FindFirstObjectByType<MatchHudUI>();
        CheckHudLayout(context);
        CheckPlayerBodies(session);
        yield return CheckPlayerPixels(session);
        yield return CheckEventNotice(context, prefix);
        Require(hud.PanelRect.width > 0f, "One native HUD is visible");
        Require(UiPointer.IsOverUI(new Vector2(hud.PanelRect.center.x, Screen.height - hud.PanelRect.center.y)),
            "Native HUD prevents clicks reaching the map");
        Require(context.transform.Find("HUD View/HUD/Current Player").GetComponent<Text>().text == context.ActivePlayer.name.ToUpperInvariant(), "HUD follows randomized identity");
        var player = context.ActivePlayer;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Require(player.HasRolled, "Configured Space action still rolls after native menu interaction");
        yield return new WaitForSecondsRealtime(1);
        CheckActivePlayerName(context, true);
        yield return Capture(prefix + "-gameplay");
        Require(endTurn.interactable, "End Turn becomes available after the roll finishes");
        yield return Click(endTurn);
        Require(context.ActivePlayer != player, "Native End Turn advances the current player");
        for (int turn = 1; turn < session.Participants.Count; turn++)
            context.Turns.EndTurnAfterBattleLoss(context.ActivePlayer);
        turnSettings.FindProperty("showEndTurnButton").boolValue = false;
        turnSettings.ApplyModifiedPropertiesWithoutUndo();
        yield return null; yield return null;
        Require(context.ActivePlayer == player && !endTurn.gameObject.activeSelf,
            "Fixed turn order repeats and the optional control can be hidden again");
        for (int turn = 0; turn < session.Participants.Count; turn++)
        {
            var previous = context.ActivePlayer;
            context.Turns.EndTurnAfterBattleLoss(previous);
            yield return null; yield return null;
            Require(context.ActivePlayer != previous, "Name check advances to a different player");
            CheckActivePlayerName(context, true);
        }
        Require(context.ActivePlayer == player, "Name follows every participant through the fixed turn cycle");
        yield return Click(context.transform.Find("Pause View/Pause").GetComponent<Button>());
        Require(LocalMultiplayerSession.GameplayPaused && Time.timeScale == 0f, "Native Pause button pauses");
        float frozen = context.Timer.TimeRemaining;
        yield return new WaitForSecondsRealtime(0.1f);
        CheckActivePlayerName(context, false);
        Require(context.Timer.TimeRemaining == frozen, "Countdown freezes while native menu is open");
        yield return Capture(prefix + "-pause");
        var resume = context.transform.Find("Pause View/Pause Backdrop/Pause Menu/Resume Game").GetComponent<Button>();
        yield return Click(resume);
        Require(!LocalMultiplayerSession.GameplayPaused && Time.timeScale == 1f, "Native Resume button restores gameplay");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
        yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Require(LocalMultiplayerSession.GameplayPaused, "Existing shared Escape action still pauses");
        yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
        yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Require(!LocalMultiplayerSession.GameplayPaused, "Existing shared Escape action resumes");
        CheckPlayerBodies(session);
        KeyItem temporaryKey = null;
        if (session.Mode is Race_Gamemode keyMode)
        {
            temporaryKey = Object.Instantiate(keyMode.KeyPrefab);
            temporaryKey.PickUp(player.Inventory);
            yield return new WaitForSecondsRealtime(.6f);
            Require(player.HasKey && context.ActivePlayer == player, "The active player holds the test key");
            Require(!new SerializedObject(keyMode).FindProperty("updateObjectiveAfterKey").boolValue &&
                keyMode.GetObjective(player) == new SerializedObject(keyMode).FindProperty("objectiveBeforeKey").stringValue,
                "Race objective remains consistent when a player collects the key");
            CheckActivePlayerName(context, true);
            yield return Capture(prefix + "-active-name-key");
        }
        string originalName = context.ActivePlayer.name;
        context.ActivePlayer.name = "WWWWWWWWWWWWWWWW";
        yield return null; yield return null;
        CheckActivePlayerName(context, true);
        if (!Application.isBatchMode)
        {
            foreach (Vector2Int size in new[] { new Vector2Int(960, 540), new Vector2Int(720, 960) })
            {
                SetSize(size.x, size.y);
                for (int frame = 0; frame < 4; frame++) yield return null;
                CheckActivePlayerName(context, true);
                yield return Capture(prefix + "-active-name-" + size.x + "x" + size.y);
            }
            SetSize(1280, 720);
        }
        context.ActivePlayer.name = originalName;
        yield return null; yield return null;
        if (temporaryKey != null)
        {
            context.Turns.EndTurnAfterBattleLoss(context.ActivePlayer);
            yield return null; yield return null;
            CheckActivePlayerName(context, true);
            var markers = Object.FindFirstObjectByType<WorldMarkersUI>();
            var badges = (IDictionary)typeof(WorldMarkersUI).GetField("badges", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(markers);
            Require(((WorldMarkerUI)badges[player]).gameObject.activeSelf,
                "Inactive key holder keeps its separate key badge");
            Vector2Int previousCell = player.GridPosition;
            Require(player.SetGridPosition(context.ActivePlayer.GridPosition), "Key holder can share the active player's tile");
            yield return null; yield return null;
            var nameView = context.GetComponentInChildren<ActivePlayerNameUI>();
            Require(!((WorldMarkerUI)badges[player]).ScreenBounds.Overlaps(nameView.ScreenBounds),
                "Nearby key badge does not cover the active name even on a shared tile");
            yield return Capture(prefix + "-active-name-shared-key");
            Require(player.SetGridPosition(previousCell), "Key holder returns to its original tile");
            for (int turn = 1; turn < session.Participants.Count; turn++)
                context.Turns.EndTurnAfterBattleLoss(context.ActivePlayer);
            yield return null; yield return null;
            CheckActivePlayerName(context, true);
            player.Inventory.RemoveKey();
            Object.Destroy(temporaryKey.gameObject);
            yield return null; yield return null;
            CheckActivePlayerName(context, true);
        }

        if (session.Mode is Race_Gamemode raceMode)
        {
            yield return CheckStorageHints(context);
            player.BeginEventTurn();
            var customPrefab = Object.FindFirstObjectByType<ItemSpawner>().GetNetworkPrefab(2) as CustomDiceItem;
            Require(customPrefab != null, "Race has its Inspector-assigned Custom Dice prefab");
            var custom = Object.Instantiate(customPrefab);
            custom.PickUp(player.Inventory);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Require(custom.IsSelecting, "Use Item action opens native selection");
            yield return Click(context.transform.Find("Dice Selection View/Custom Dice Selection/Content/>").GetComponent<Button>());
            Require(custom.SelectedNumber == 2, "Native selection changes the chosen die value");
            yield return Capture(prefix + "-custom-dice");
            yield return Click(context.transform.Find("Dice Selection View/Custom Dice Selection/Content/Confirm").GetComponent<Button>());
            Require(!custom.IsSelecting && player.HasRolled && player.MovesRemaining == 2 && !player.IsUsingItem(),
                "Native Confirm gives moves and restores player control");

            var containers = Object.FindObjectsByType<ObstacleStorage>(FindObjectsSortMode.None);
            ObstacleStorage storage = null;
            bool adjacent = false;
            foreach (var candidate in containers)
            {
                if (!candidate.CanInteract || !candidate.HasItem || candidate.StoredItemPrefab is KeyItem) continue;
                foreach (var direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var cell = candidate.GridPosition + direction;
                    if (!player.GridMap.IsWalkable(cell)) continue;
                    int neighbors = 0;
                    foreach (var other in containers)
                    {
                        var delta = other.GridPosition - cell;
                        if (other.CanInteract && Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1) neighbors++;
                    }
                    if (neighbors == 1 && player.SetGridPosition(cell))
                    { storage = candidate; adjacent = true; break; }
                }
                if (adjacent) break;
            }
            Require(adjacent, "Storage has a valid interaction tile");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Require(storage.IsMenuOpen, "Interact action opens native storage menu (open: "
                + (ObstacleStorage.OpenStorage != null ? ObstacleStorage.OpenStorage.name : "none") + ", moving: " + player.IsMoving
                + ", menu: " + player.IsInMenu() + ", current: " + player.IsCurrentTurn + ", paused: " + LocalMultiplayerSession.GameplayPaused + ")");
            yield return Capture(prefix + "-storage");
            yield return Click(context.transform.Find("Storage View/Storage Menu/Content/Take Item").GetComponent<Button>());
            Require(!storage.IsMenuOpen && !player.IsInMenu() && player.Inventory.Items.Count == 1,
                "Native Take transfers an item once and closes the menu");

            var victim = session.Participants[context.Turns.CurrentPlayerIndex == 0 ? 1 : 0];
            foreach (var container in Object.FindObjectsByType<ObstacleStorage>(FindObjectsSortMode.None))
                if (container.ContainsKey) container.SetStoredItem(null);
            Object.Instantiate(raceMode.KeyPrefab).PickUp(victim.Inventory);
            yield return null; yield return null;
            int keyBadges = 0;
            foreach (var marker in context.GetComponentsInChildren<WorldMarkerUI>(true))
            {
                if (!marker.name.StartsWith("KeyBadge") || !marker.gameObject.activeInHierarchy) continue;
                keyBadges++;
                var badge = new SerializedObject(marker);
                Require(badge.FindProperty("border").objectReferenceValue != marker.GetComponent<Image>(),
                    "Key carrier badge tints only its outline");
                Require(((RectTransform)marker.transform).rect.size == new Vector2(24, 24), "Key carrier uses a small badge");
            }
            Require(keyBadges == 1, "Only the actual key carrier displays a key badge");
            Require(victim.SetGridPosition(player.GridPosition), "Key carrier can share the challenger's tile");
            Require(raceMode.TryNetworkBattle(player, victim), "Race battle still resolves through its mode API");
            yield return null; yield return null;
            yield return Capture(prefix + "-battle");
            yield return Click(context.transform.Find("Race Battle View/Race Battle/Content/Continue").GetComponent<Button>());
            Require(!raceMode.IsBattleActive && !player.IsInMenu(), "Native battle Continue releases player control");
            context.Events.ApplyNetworkDisplay("Blackout preview", 2);
            yield return null; yield return null;
            Require(context.transform.Find("Event Feedback View/Blackout").gameObject.activeInHierarchy, "Blackout uses a native UI material");
            yield return Capture(prefix + "-blackout");
            context.Events.ApplyNetworkDisplay("", 0);
        }

        if (!Application.isBatchMode)
        {
            foreach (var size in new[] { new Vector2Int(960, 720), new Vector2Int(720, 960) })
            {
                SetSize(size.x, size.y);
                for (int i = 0; i < 15; i++) yield return null;
                Canvas.ForceUpdateCanvases();
                var rect = hud.PanelRect;
                Require(rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= Screen.width && rect.yMax <= Screen.height, "HUD fits " + Screen.width + "x" + Screen.height);
                var pause = MatchUIContext.ScreenRect((RectTransform)context.transform.Find("Pause View/Pause"));
                Require(!rect.Overlaps(pause), "HUD avoids Pause at " + Screen.width + "x" + Screen.height);
                yield return Capture(prefix + "-" + size.x + "x" + size.y);
            }
            SetSize(1280, 720);
            for (int i = 0; i < 15; i++) yield return null;
        }
        typeof(GameTimer).GetField("timeRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(context.Timer, 0f);
        yield return null; yield return null; yield return null;
        Canvas.ForceUpdateCanvases();
        Require(LocalMultiplayerSession.MatchHasEnded, "Timer expiration opens native result screen");
        var message = context.transform.Find("Result View/Match Result/Result Content/Result And Survivors").GetComponent<Text>();
        Require(message.preferredHeight <= message.rectTransform.rect.height + 1, "Result content grows without clipping");
        if (session.Mode is SurvivalModeController)
            foreach (var survivor in session.Participants)
                Require(message.text.Contains(survivor.name), "Result names survivor " + survivor.name);
        yield return Capture(prefix + "-result");
        CheckActivePlayerName(context, false);
        Debug.Log("NATIVE UI CHECKS PASSED: " + prefix + ", pointer lobby/start/pause/resume, shared actions, randomized HUD, native result sizing.");
    }

    private static IEnumerator CheckStorageHints(MatchUIContext context)
    {
        Require(!LocalMultiplayerSession.ShowItemObstacles && !LocalMultiplayerSession.ShowKeyLocation,
            "Item/key reveal markers are off by default");
        var pause = context.transform.Find("Pause View/Pause").GetComponent<Button>();
        var resume = context.transform.Find("Pause View/Pause Backdrop/Pause Menu/Resume Game").GetComponent<Button>();
        var view = new SerializedObject(context.transform.Find("Pause View").GetComponent<PauseMenuUI>());
        var items = (Toggle)view.FindProperty("items").objectReferenceValue;
        var key = (Toggle)view.FindProperty("key").objectReferenceValue;
        yield return Click(pause);
        yield return Click(items);
        yield return Click(key);
        yield return Click(resume);
        Require(LocalMultiplayerSession.ShowItemObstacles && LocalMultiplayerSession.ShowKeyLocation,
            "Existing native pause toggles enable the optional reveal hints");

        int expected = 0;
        foreach (var obstacle in Object.FindObjectsByType<ObstacleStorage>(FindObjectsSortMode.None))
            if (obstacle.HasItem) expected++;
        int shown = 0;
        foreach (var marker in context.GetComponentsInChildren<WorldMarkerUI>(true))
        {
            if (!marker.name.StartsWith("StorageAssist") || !marker.gameObject.activeInHierarchy) continue;
            shown++;
            var references = new SerializedObject(marker);
            var border = (Image)references.FindProperty("border").objectReferenceValue;
            var label = (Text)references.FindProperty("label").objectReferenceValue;
            var background = marker.GetComponent<Image>();
            Require(border != background, "Reveal accent colors the outline, not the filled background");
            Require(label.text == "ITEM" || label.text == "KEY", "Storage hints have short, readable labels");
            Require(label.color.grayscale > background.color.grayscale + .4f, "Hint text contrasts with its dark background");
            Require(label.preferredWidth <= label.rectTransform.rect.width && label.preferredHeight <= label.rectTransform.rect.height + 1,
                "Hint label fits without truncation");
            var rect = (RectTransform)marker.transform;
            Require(rect.rect.width <= 44 && rect.rect.height <= 18 && rect.pivot == new Vector2(.5f, 0),
                "Compact hint is centered immediately over its target");
            foreach (var graphic in marker.GetComponentsInChildren<Graphic>(true))
                Require(!graphic.raycastTarget, "Reveal hints do not intercept map input");
        }
        Require(shown == expected && shown > 0, "Each revealed filled container has exactly one small hint");
        CheckHintSpacing(context);
        yield return Capture("Race_Gamemode-reveal-hints");
        if (!Application.isBatchMode)
        {
            foreach (var size in new[] { new Vector2Int(960, 720), new Vector2Int(720, 960) })
            {
                SetSize(size.x, size.y);
                for (int frame = 0; frame < 15; frame++) yield return null;
                CheckHintSpacing(context);
                yield return Capture("Race_Gamemode-reveal-hints-" + size.x + "x" + size.y);
            }
            SetSize(1280, 720);
            for (int frame = 0; frame < 15; frame++) yield return null;
        }
        yield return Click(pause);
        yield return Click(items);
        yield return Click(key);
        yield return Click(resume);
        foreach (var marker in context.GetComponentsInChildren<WorldMarkerUI>(true))
            if (marker.name.StartsWith("StorageAssist")) Require(!marker.gameObject.activeSelf, "Switching reveal options off clears every storage hint");
    }

    private static void CheckHintSpacing(MatchUIContext context)
    {
        var bounds = new System.Collections.Generic.List<Rect>();
        foreach (var marker in context.GetComponentsInChildren<WorldMarkerUI>(true))
        {
            if (!marker.name.StartsWith("StorageAssist") || !marker.gameObject.activeInHierarchy) continue;
            Rect rect = marker.ScreenBounds;
            foreach (Rect other in bounds) Require(!rect.Overlaps(other), "Compact storage hints do not overlap");
            bounds.Add(rect);
        }
    }

    private static IEnumerator Click(Selectable selectable)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)selectable.transform;
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector2 position = (corners[0] + corners[2]) * 0.5f;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
        yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        yield return null; yield return null;
    }

    private static IEnumerator Capture(string name)
    {
        if (Application.isBatchMode) yield break;
        for (int i = 0; i < 4; i++) yield return null;
        Directory.CreateDirectory("Logs/NativeUiPreviews");
        ScreenCapture.CaptureScreenshot("Logs/NativeUiPreviews/" + name + ".png");
        for (int i = 0; i < 4; i++) yield return null;
    }

    internal static void CheckInventoryLayout(MatchUIContext context)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interface/MatchInterface.prefab")
            .GetComponentInChildren<ItemPickupBar>(true);
        var original = new SerializedObject(source);
        var inventory = context.GetComponentInChildren<ItemPickupBar>(true);
        var current = new SerializedObject(inventory);
        foreach (string field in new[] { "panel", "popup" })
        {
            var ours = current.FindProperty(field).objectReferenceValue as Component;
            var saved = original.FindProperty(field).objectReferenceValue as Component;
            var actualRect = (RectTransform)ours.transform;
            var savedRect = (RectTransform)saved.transform;
            Require(actualRect.anchorMin == savedRect.anchorMin && actualRect.anchorMax == savedRect.anchorMax &&
                actualRect.pivot == savedRect.pivot && actualRect.anchoredPosition == savedRect.anchoredPosition &&
                actualRect.sizeDelta == savedRect.sizeDelta,
                "Every mode inherits the shared bottom-right inventory " + field + " layout");
        }
        if (Application.isPlaying && inventory.PanelRect.width > 0f)
        {
            Rect bounds = inventory.PanelRect;
            Require(bounds.xMin > Screen.width * .5f && bounds.xMax <= Screen.width &&
                bounds.yMax > Screen.height * .8f && bounds.yMax <= Screen.height,
                "Visible inventory stays at the bottom-right of the viewport");
            if (inventory.NoticeRect.width > 0f)
            {
                Require(!inventory.NoticeRect.Overlaps(bounds) && inventory.NoticeRect.yMax < bounds.yMin,
                    "Inventory notice sits above its matching slot");
                var popup = (Text)current.FindProperty("popup").objectReferenceValue;
                Require(popup.cachedTextGenerator.characterCountVisible >= popup.text.Length,
                    "Compact pickup notice wraps without truncation");
            }
        }
    }

    internal static IEnumerator CheckEventNotice(MatchUIContext context, string prefix)
    {
        if (context.Events == null) yield break;
        var feedback = new SerializedObject(context.GetComponentInChildren<EventFeedbackUI>(true));
        var panel = ((GameObject)feedback.FindProperty("panel").objectReferenceValue).GetComponent<RectTransform>();
        var message = (Text)feedback.FindProperty("message").objectReferenceValue;
        var hud = context.GetComponentInChildren<MatchHudUI>();
        string original = context.Events.CurrentMessage;
        int originalBlackout = context.Events.BlackoutTurns;
        foreach (Vector2Int size in Application.isBatchMode ? new[] { Vector2Int.zero } :
            new[] { new Vector2Int(1280, 720), new Vector2Int(960, 540), new Vector2Int(720, 960) })
        {
            if (size.x > 0) SetSize(size.x, size.y);
            foreach (string text in new[] { "Player 1: +1 move.", "Player 1: Good Luck Day: +1. Movement: 3.",
                "WWWWWWWWWWWWWWWW: Second Chance saved you from a harmful event. That protection is now used up.",
                "WWWWWWWWWWWWWWWW: Blackout: everyone's visibility is reduced for the next 3 player turns." })
            {
                context.Events.ApplyNetworkDisplay(text, text.Contains("Blackout") ? 3 : 0);
                for (int frame = 0; frame < 6; frame++) yield return null;
                Canvas.ForceUpdateCanvases();
                Rect bounds = MatchUIContext.ScreenRect(panel), hudBounds = hud.PanelRect;
                Require(panel.gameObject.activeInHierarchy && bounds.xMin >= hudBounds.xMin && bounds.xMax <= hudBounds.xMax &&
                    bounds.yMin >= hudBounds.yMin && bounds.yMax <= hudBounds.yMax,
                    "Event feedback stays completely inside the existing HUD");
                Require(bounds.xMin >= 0 && bounds.xMax <= Screen.width && bounds.yMax <= Screen.height,
                    "Event notice stays inside the viewport");
                if (message.cachedTextGenerator.characterCountVisible < message.text.Length)
                    yield return Capture(prefix + "-hud-feedback-overflow");
                Require(message.cachedTextGenerator.characterCountVisible >= message.text.Length,
                    "Event text wraps fully without truncation: " + message.cachedTextGenerator.characterCountVisible + "/" +
                    message.text.Length + ", font " + message.cachedTextGenerator.fontSizeUsedForBestFit + ", " + message.text);
                Require(panel.GetComponentsInChildren<Text>().Length == 1 && !panel.Find("Border").gameObject.activeSelf,
                    "Event strip has no extra heading or outer border");
                Require(!panel.GetComponent<Image>().enabled && panel.GetComponent<ContentSizeFitter>() == null,
                    "Event feedback has no separate box and cannot expand outside the HUD");
                var hudSettings = new SerializedObject(hud);
                var objective = (Text)hudSettings.FindProperty("objective").objectReferenceValue;
                bool hasObjective = objective.gameObject.activeInHierarchy;
                Require(Mathf.Approximately(hudBounds.height / ((RectTransform)hud.transform.Find("HUD")).lossyScale.y,
                    hudSettings.FindProperty(hasObjective ? "heightWithObjective" : "heightWithoutObjectiveWithNotice").floatValue),
                    "Race and Survival keep their existing HUD height; tutorial uses its configured internal feedback area");
                if (hasObjective)
                {
                    Require(!MatchUIContext.ScreenRect(objective.rectTransform).Overlaps(bounds),
                        "Stable objective text and event feedback do not overlap");
                    Require(objective.cachedTextGenerator.characterCountVisible >= objective.text.Length,
                        "Objective text remains fully readable");
                }
                if (text == "Player 1: +1 move.")
                    Require(message.cachedTextGenerator.lineCount == 1, "Short feedback occupies a single line");
                foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true))
                    Require(!graphic.raycastTarget, "Event notice does not intercept map clicks");
                var overlay = context.GetComponentInChildren<TutorialOverlay>();
                if (overlay != null)
                {
                    var card = (GameObject)new SerializedObject(overlay).FindProperty("panel").objectReferenceValue;
                    Require(!MatchUIContext.ScreenRect((RectTransform)card.transform).Overlaps(bounds),
                        "Tutorial card avoids the HUD's attached event notice");
                }
                string variant = text.Contains("Good Luck") ? "example" : text.Contains("Second Chance") ? "long" : text.Contains("Blackout") ? "blackout" : "short";
                yield return Capture(prefix + "-hud-feedback-" + variant + "-" + size.x + "x" + size.y);
            }
        }
        context.Events.ApplyNetworkDisplay("", 0);
        yield return null; yield return null;
        Require(!panel.gameObject.activeSelf, "Clearing event feedback removes the notice without empty space");
        context.Events.ApplyNetworkDisplay(original, originalBlackout);
        if (!Application.isBatchMode) SetSize(1280, 720);
        yield return null; yield return null;
    }

    private static void CheckActivePlayerName(MatchUIContext context, bool visible)
    {
        var names = context.GetComponentsInChildren<ActivePlayerNameUI>(true);
        Require(names.Length == 1, "Only one shared active-player name view exists");
        var settings = new SerializedObject(names[0]);
        var panel = (RectTransform)settings.FindProperty("panel").objectReferenceValue;
        var label = (Text)settings.FindProperty("label").objectReferenceValue;
        Require(panel.gameObject.activeInHierarchy == visible, "Active-player label visibility follows match state");
        foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true))
            Require(!graphic.raycastTarget, "Name label never blocks map clicks");
        if (!visible) return;
        Require(label.text == context.ActivePlayer.name, "Name follows the current player's identity after a turn change");
        Canvas.ForceUpdateCanvases();
        Require(label.cachedTextGenerator.characterCountVisible >= label.text.Length, "Full player name fits the compact label");
        var accent = (Image)settings.FindProperty("identityAccent").objectReferenceValue;
        Require(accent.color == context.ActivePlayer.IdentityColor, "Name accent follows the player's own color");
        var appearance = new SerializedObject(context.ActivePlayer.GetComponent<Escape4Now.Player.PlayerAppearance>());
        var ring = (SpriteRenderer)appearance.FindProperty("turnRing").objectReferenceValue;
        var glow = (SpriteRenderer)appearance.FindProperty("turnGlow").objectReferenceValue;
        Color identity = context.ActivePlayer.IdentityColor;
        Color ringColor = ring.color;
        Require(appearance.FindProperty("matchPlayerColor").boolValue && ring.enabled &&
            ringColor.r == identity.r && ringColor.g == identity.g && ringColor.b == identity.b,
            "Active ground square matches the current player's identity color");
        Require(ringColor.a == appearance.FindProperty("ringColor").colorValue.a &&
            glow.color.r == identity.r && glow.color.g == identity.g && glow.color.b == identity.b &&
            Mathf.Approximately(glow.color.a, ringColor.a * appearance.FindProperty("glowStrength").floatValue),
            "Matching square and glow preserve Inspector opacity settings");
        var key = (Image)settings.FindProperty("keyIcon").objectReferenceValue;
        bool hasKey = context.ActivePlayer.HasKey && context.Session != null && context.Session.Mode is Race_Gamemode;
        Require(key.gameObject.activeSelf == hasKey, "Inline key icon follows the active player's inventory");
        if (hasKey)
        {
            Rect iconRect = MatchUIContext.ScreenRect(key.rectTransform);
            Rect textRect = MatchUIContext.ScreenRect(label.rectTransform);
            Require(iconRect.xMax < textRect.xMin && iconRect.yMin >= MatchUIContext.ScreenRect(panel).yMin,
                "Key icon occupies its own slot without overlapping the player name");
            var markers = Object.FindFirstObjectByType<WorldMarkersUI>();
            var badges = (IDictionary)typeof(WorldMarkersUI).GetField("badges", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(markers);
            if (badges.Contains(context.ActivePlayer))
                Require(!((WorldMarkerUI)badges[context.ActivePlayer]).gameObject.activeSelf,
                    "Active player never has a duplicate floating key badge over the name");
        }
        var bounds = context.ActivePlayer.GetComponent<SpriteRenderer>().bounds;
        var top = context.WorldCamera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
        Rect screen = MatchUIContext.ScreenRect(panel);
        Require(screen.yMax <= Screen.height - top.y + 1f && screen.xMin >= 0f && screen.xMax <= Screen.width && screen.yMin >= 0f,
            "Compact name stays above the body and within the viewport");
        Require(panel.rect.width <= settings.FindProperty("widthRange").vector2Value.y,
            "Name width respects its Inspector limit");
    }

    internal static void CheckPlayerBodies(LocalMultiplayerSession session)
    {
        foreach (var participant in session.Participants)
        {
            var body = participant.GetComponent<SpriteRenderer>();
            Require(body.enabled && body.sprite != null && body.color == participant.IdentityColor && body.color.a > 0f &&
                body.bounds.size.x > 0f && body.bounds.size.y > 0f,
                participant.name + " has its own visible body, not only a turn ring");
            var appearance = new SerializedObject(participant.GetComponent<Escape4Now.Player.PlayerAppearance>());
            var outline = (SpriteRenderer)appearance.FindProperty("outline").objectReferenceValue;
            Require(outline.enabled, participant.name + " retains its body outline between turns");
        }
    }

    private static IEnumerator CheckPlayerPixels(LocalMultiplayerSession session)
    {
        if (Application.isBatchMode) yield break;
        yield return new WaitForEndOfFrame();
        var frame = ScreenCapture.CaptureScreenshotAsTexture();
        try
        {
            var camera = Camera.main;
            foreach (var participant in session.Participants)
            {
                var body = participant.GetComponent<SpriteRenderer>();
                Vector3 min = camera.WorldToScreenPoint(body.bounds.min);
                Vector3 max = camera.WorldToScreenPoint(body.bounds.max);
                int matches = 0;
                //Only sample the body above its feet, excluding the ground marker.
                for (int y = Mathf.Clamp(Mathf.CeilToInt(Mathf.Lerp(min.y, max.y, .2f)), 0, frame.height - 1);
                    y < Mathf.Min(frame.height, Mathf.FloorToInt(max.y)); y++)
                    for (int x = Mathf.Clamp(Mathf.CeilToInt(min.x), 0, frame.width - 1);
                        x < Mathf.Min(frame.width, Mathf.FloorToInt(max.x)); x++)
                    {
                        Color pixel = frame.GetPixel(x, y);
                        Color identity = participant.IdentityColor;
                        if (Mathf.Abs(pixel.r - identity.r) < .1f && Mathf.Abs(pixel.g - identity.g) < .1f &&
                            Mathf.Abs(pixel.b - identity.b) < .1f) matches++;
                    }
                Require(matches >= 4, participant.name + " body is present in the rendered map (" + matches + " pixels)");
            }
        }
        finally { Object.DestroyImmediate(frame); }
    }

    internal static void SetSize(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance").GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
        var groupType = group.GetType();
        int builtin = (int)groupType.GetMethod("GetBuiltinCount").Invoke(group, null);
        int custom = (int)groupType.GetMethod("GetCustomCount").Invoke(group, null);
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var kind = assembly.GetType("UnityEditor.GameViewSizeType");
        var size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new[] { Enum.ToObject(kind, 1), (object)width, height, "UI Validation " + width + "x" + height }, null);
        groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
        var viewType = assembly.GetType("UnityEditor.GameView");
        var window = EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(window, builtin + custom);
        window.Repaint();
    }

    private static string Argument(string name, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        Debug.Log("PASS: " + message);
    }

    private static void Watchdog()
    {
        if (waitingForTutorial && UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle != tutorialSceneHandle)
        {
            var tutorial = Object.FindFirstObjectByType<Tutorial_Functionality>();
            if (tutorial != null && tutorial.CurrentSection == expectedSection && !tutorial.IsSectionComplete)
            {
                Debug.Log("NATIVE UI CHECKS PASSED: Tutorial, native lesson, configured roll, pointer Continue and scene reload.");
                Finish(0);
                return;
            }
        }
        if (EditorApplication.timeSinceStartup <= deadline) return;
        Debug.LogError("Native UI checks timed out."); Finish(1);
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false);
        waitingForTutorial = false;
        EditorApplication.update -= Watchdog;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        InputSystem.settings.backgroundBehavior = oldBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
        Time.timeScale = 1;
        EditorApplication.Exit(code);
    }
}

